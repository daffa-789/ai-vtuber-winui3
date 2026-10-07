using System.Collections.ObjectModel;
using System.Text;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Dispatching;
using SilverWolf.App.Configuration;
using SilverWolf.App.Diagnostics;
using SilverWolf.App.Models;
using SilverWolf.Core.Domain;
using SilverWolf.Core.Domain.Kizuna;
using SilverWolf.Core.Text;
using SilverWolf.Services.Backend;
using SilverWolf.Services.Bootstrap;
using SilverWolf.Services.Inference;

namespace SilverWolf.App.ViewModels;

/// <summary>
/// Satu sumber keadaan antarmuka — port <c>useCompanionStore</c>
/// (<c>packages/stage-ui/src/store.js</c>).
///
/// Aplikasi lama memakai <b>satu</b> store Pinia untuk seluruh UI, dan
/// <c>docs/migrasi/01-peta-fitur.md</c> §3 mengunci keputusan itu: jangan
/// dipecah menjadi beberapa ViewModel, karena beberapa komponen membaca
/// keadaan yang sama (HUD ikatan dibaca Konsol, ekspresi dibaca Panggung).
///
/// Tiga perilaku yang sengaja dipertahankan dan mudah hilang saat refactor:
///   1. <c>/api/chat</c> tidak pernah mengirim snapshot ikatan, jadi
///      <c>ChatAsync</c> memberi <c>Kizuna = null</c> dan UI <b>wajib</b>
///      menyegarkan sendiri setelah aliran selesai.
///   2. <c>TouchAsync</c> / <c>ProactiveAsync</c> memberi snapshot
///      <b>pra</b>-interaksi; jangan "diperbaiki" menjadi pasca-interaksi.
///   3. Poin ikatan pecahan, jadi tampilan XP tidak dibulatkan di sini.
/// </summary>
public sealed class CompanionViewModel : ObservableObject, IAsyncDisposable
{
    /// <summary>Padanan <c>localStorage['silverwolf_mirror_track']</c>.</summary>
    private const string KunciMirror = "silverwolf_mirror_track";

    private static readonly string[] TurunanHealth =
    [
        nameof(IsReady), nameof(IsLoading), nameof(StatusTeks), nameof(StatusTombol),
        nameof(TeksGpu), nameof(TeksMemori), nameof(TeksTts),
    ];

    private static readonly string[] TurunanKizuna =
    [
        nameof(TeksIkatan), nameof(TeksXp), nameof(Kemajuan), nameof(TeksKehangatan),
        nameof(TeksAtmosfer), nameof(TeksSikap),
    ];

    private CompanionRuntime? _runtime;
    private CompanionBackend? _backend;
    private DispatcherQueue? _dispatcher;
    private DispatcherQueueTimer? _timerHealth;
    private DispatcherQueueTimer? _timerProaktif;
    private CancellationTokenSource _cts = new();

    /// <summary>
    /// Dinyalakan begitu pembongkaran dimulai.
    ///
    /// Kenapa perlu: <c>DispatcherQueueTimer.Stop()</c> **tidak** menunggu
    /// callback yang sedang berjalan. Callback yang belum sempat mulai akan
    /// tetap dipanggil setelah pembongkaran dan menyentuh <c>_cts.Token</c> —
    /// kalau <c>_cts</c> sudah dibuang, hasilnya <c>ObjectDisposedException</c>.
    /// Itu pernah benar-benar tercatat di <c>crash.log</c>:
    ///
    /// <code>
    /// sumber  : SegarkanHealthAsync
    /// tipe    : System.ObjectDisposedException
    /// pesan   : The CancellationTokenSource has been disposed.
    /// </code>
    /// </summary>
    private volatile bool _sedangDibuang;

    private HealthSnapshot? _health;
    private BondSnapshot? _kizuna;
    private string? _healthError;
    private bool _isSending;
    private bool _suaraSibuk;
    private string _expression = "netral";
    private bool _autonomousMode = true;
    private bool _suaraAktif = true;
    private bool _mirrorTrack;
    private DateTimeOffset _lastUserActivity = DateTimeOffset.Now;
    private string _draf = string.Empty;

    public CompanionViewModel()
    {
        _mirrorTrack = BacaMirror();

        // canExecute harus berupa delegasi — BisaKirim/BisaPicu adalah properti,
        // jadi dilewatkan sebagai lambda agar nilainya dievaluasi setiap kali.
        KirimCommand = new AsyncRelayCommand(KirimAsync, canExecute: () => BisaKirim);
        ElusKepalaCommand = new AsyncRelayCommand(ElusKepalaAsync, canExecute: () => BisaPicu);
        PancingObrolanCommand = new AsyncRelayCommand(() => PancingObrolanAsync(null), canExecute: () => BisaPicu);
        SegarkanCommand = new AsyncRelayCommand(SegarkanHealthAsync);
    }

    // ── Keadaan ────────────────────────────────────────────────────────────

    public ObservableCollection<ChatBubble> Messages { get; } = new();

    public HealthSnapshot? Health => _health;

    public BondSnapshot? Kizuna
    {
        get => _kizuna;
        private set
        {
            if (!SetProperty(ref _kizuna, value))
            {
                return;
            }

            foreach (var nama in TurunanKizuna)
            {
                OnPropertyChanged(nama);
            }
        }
    }

    public string? HealthError
    {
        get => _healthError;
        private set => SetProperty(ref _healthError, value);
    }

    public bool IsSending
    {
        get => _isSending;
        private set
        {
            if (!SetProperty(ref _isSending, value))
            {
                return;
            }

            KirimCommand.NotifyCanExecuteChanged();
            ElusKepalaCommand.NotifyCanExecuteChanged();
            PancingObrolanCommand.NotifyCanExecuteChanged();
            OnPropertyChanged(nameof(TeksKirim));
        }
    }

    /// <summary>
    /// Dipakai pekerja proaktif sebagai salah satu syarat lewati, padanan
    /// <c>audioBusy</c>. M11 yang akan mengisinya; M9 hanya menyediakan
    /// tempatnya supaya logika proaktif tidak perlu diubah lagi nanti.
    /// </summary>
    public bool SuaraSibuk
    {
        get => _suaraSibuk;
        set => SetProperty(ref _suaraSibuk, value);
    }

    /// <summary>Tag emosi apa adanya, bahasa Indonesia (mis. "senyum", "goda").</summary>
    public string Expression
    {
        get => _expression;
        private set => SetProperty(ref _expression, value);
    }

    public bool AutonomousMode
    {
        get => _autonomousMode;
        set
        {
            if (SetProperty(ref _autonomousMode, value))
            {
                OnPropertyChanged(nameof(TeksNeuro));
            }
        }
    }

    public bool SuaraAktif
    {
        get => _suaraAktif;
        set => SetProperty(ref _suaraAktif, value);
    }

    public bool MirrorTrack
    {
        get => _mirrorTrack;
        set
        {
            if (!SetProperty(ref _mirrorTrack, value))
            {
                return;
            }

            SimpanMirror(value);
            OnPropertyChanged(nameof(TeksMirror));
        }
    }

    public DateTimeOffset LastUserActivity
    {
        get => _lastUserActivity;
        private set => SetProperty(ref _lastUserActivity, value);
    }

    public string Draf
    {
        get => _draf;
        set
        {
            if (SetProperty(ref _draf, value))
            {
                KirimCommand.NotifyCanExecuteChanged();
            }
        }
    }

    // ── Turunan untuk tampilan ─────────────────────────────────────────────

    public bool IsReady => _health?.Ok == true;

    public bool IsLoading => _health?.Loading == true;

    /// <summary>"siap" | "memuat" | "tidak-jalan" — nilai apa adanya dari backend.</summary>
    public string StatusTeks => _health?.StatusText ?? "memuat";

    /// <summary>Padanan tiga keadaan tombol status di Konsol aplikasi lama.</summary>
    public string StatusTombol => StatusTeks switch
    {
        "siap" => "VULKAN ONLINE",
        "memuat" => "MEMUAT VULKAN…",
        _ => "OFFLINE",
    };

    public string TeksNeuro => AutonomousMode ? "NEURO-SAMA ON" : "NEURO-SAMA OFF";

    public string TeksMirror => MirrorTrack ? "MIRROR ON" : "MIRROR OFF";

    public string TeksKirim => IsSending ? "MENGALIRKAN…" : "KIRIM";

    public string TeksGpu => _health?.Model ?? "-";

    public string TeksMemori => _health?.Memori ?? "memori tidak tersedia";

    public string TeksTts => _health?.Tts ?? "siap";

    public string TeksIkatan =>
        _kizuna is null ? "Lv.1 —" : $"Lv.{_kizuna.Level} {_kizuna.StageName} — {_kizuna.StageLabel}";

    public string TeksXp =>
        _kizuna is null ? "0/100 XP (0%)" : $"{_kizuna.Points:0.##}/{_kizuna.NextPoints} XP ({_kizuna.Progress}%)";

    /// <summary>Nilai 0..1 untuk <c>ProgressBar</c>. Ikatan memakai persen bulat.</summary>
    public double Kemajuan =>
        _kizuna is null || _kizuna.NextPoints <= 0
            ? 0
            : Math.Clamp(_kizuna.Points / _kizuna.NextPoints, 0, 1);

    public string TeksKehangatan => _kizuna is null ? "0%" : $"{_kizuna.Warmth * 100:0}%";

    public string TeksAtmosfer => _kizuna?.Atmosphere ?? "netral";

    public string TeksSikap => _kizuna?.Tone ?? "-";

    // ── Perintah ───────────────────────────────────────────────────────────

    public IAsyncRelayCommand KirimCommand { get; }

    public IAsyncRelayCommand ElusKepalaCommand { get; }

    public IAsyncRelayCommand PancingObrolanCommand { get; }

    /// <summary>Tombol status di Konsol: segarkan health sekarang juga.</summary>
    public IAsyncRelayCommand SegarkanCommand { get; }

    private bool BisaKirim => !IsSending && Draf.Trim().Length > 0;

    private bool BisaPicu => !IsSending;

    // ── Inisialisasi ───────────────────────────────────────────────────────

    /// <summary>
    /// Mulai runtime. Dipanggil sekali dari <c>MainWindow</c> setelah jendela
    /// dimuat, supaya startup tidak memblokir <c>Activate()</c>.
    /// </summary>
    public async Task InisialisasiAsync(DispatcherQueue dispatcher)
    {
        _dispatcher = dispatcher;

        try
        {
            CrashLog.Tahap("runtime: StartAsync mulai");

            _runtime = await CompanionRuntime.StartAsync(
                log: pesan => CrashLog.Tahap($"runtime: {pesan}"),
                onError: galat => CrashLog.Tulis("runtime", galat));

            _backend = _runtime.Backend;
            CrashLog.Tahap("runtime: siap");

            await SegarkanHealthAsync();

            MulaiTimer();
            CrashLog.Tahap("runtime: timer health + proaktif jalan");
        }
        catch (Exception galat)
        {
            CrashLog.Tulis("InisialisasiAsync", galat);
            HealthError = galat.Message;
        }
    }

    private void MulaiTimer()
    {
        if (_dispatcher is null)
        {
            return;
        }

        // Aplikasi lama: 8000 ms bila siap, 2000 ms bila belum.
        _timerHealth = _dispatcher.CreateTimer();
        _timerHealth.Interval = TimeSpan.FromMilliseconds(2000);
        // Dibungkus try/catch, bukan sekadar await. Handler timer adalah
        // `async void`: satu exception yang lolos dari sini menjadi exception
        // tak tertangani dan mematikan proses dengan fail-fast senyap — persis
        // pola kegagalan yang paling sulit didiagnosis di aplikasi WinUI 3.
        _timerHealth.Tick += async (_, _) =>
        {
            try
            {
                await SegarkanHealthAsync();
            }
            catch (Exception galat)
            {
                CrashLog.Tulis("timer health", galat);
            }
        };
        _timerHealth.Start();

        // Aplikasi lama: interval 5000 ms.
        _timerProaktif = _dispatcher.CreateTimer();
        _timerProaktif.Interval = TimeSpan.FromMilliseconds(5000);
        _timerProaktif.Tick += async (_, _) =>
        {
            try
            {
                await CekProaktifAsync();
            }
            catch (Exception galat)
            {
                CrashLog.Tulis("timer proaktif", galat);
            }
        };
        _timerProaktif.Start();
    }

    // ── Aliran ─────────────────────────────────────────────────────────────

    private async Task KirimAsync()
    {
        var isi = Draf.Trim();
        if (isi.Length == 0 || _backend is null)
        {
            return;
        }

        Draf = string.Empty;
        LastUserActivity = DateTimeOffset.Now;

        Messages.Add(new ChatBubble { Role = "user", Content = isi });

        var gelembung = new ChatBubble { Role = "assistant", Pending = true };
        Messages.Add(gelembung);

        IsSending = true;
        try
        {
            var aliran = await _backend.ChatAsync(RiwayatUntukBackend(), _cts.Token);

            if (aliran.Status != 200 || aliran.Body is null)
            {
                gelembung.Error = aliran.Error ?? $"backend menjawab {aliran.Status}";
                gelembung.Pending = false;
                return;
            }

            // /api/chat tidak pernah mengirim header x-kizuna, jadi tidak ada
            // snapshot yang bisa dipakai di sini — penyegaran dilakukan di finally.
            await AlirkanAsync(aliran.Body, gelembung);
        }
        catch (Exception galat)
        {
            CrashLog.Tulis("KirimAsync", galat);
            gelembung.Error = galat.Message;
            gelembung.Pending = false;
        }
        finally
        {
            IsSending = false;
            await SegarkanHealthAsync();
        }
    }

    /// <summary>Padanan tombol 🌸 Elus Kepala.</summary>
    private async Task ElusKepalaAsync()
    {
        if (_backend is null || IsSending)
        {
            return;
        }

        LastUserActivity = DateTimeOffset.Now;

        var gelembung = new ChatBubble { Role = "assistant", Pending = true };
        Messages.Add(gelembung);

        IsSending = true;
        try
        {
            var aliran = await _backend.TouchAsync(_cts.Token);

            // Snapshot PRA-sentuhan: di aplikasi lama header x-kizuna disusun
            // sebelum generator async berjalan. Jangan diganti jadi pasca.
            if (aliran.Kizuna is { } pra)
            {
                Kizuna = pra;
            }

            if (aliran.Status != 200 || aliran.Body is null)
            {
                gelembung.Error = aliran.Error ?? $"backend menjawab {aliran.Status}";
                gelembung.Pending = false;
                return;
            }

            await AlirkanAsync(aliran.Body, gelembung);
        }
        catch (Exception galat)
        {
            CrashLog.Tulis("ElusKepalaAsync", galat);
            gelembung.Error = galat.Message;
            gelembung.Pending = false;
        }
        finally
        {
            IsSending = false;
            await SegarkanHealthAsync();
        }
    }

    /// <summary>Padanan tombol ⚡ Pancing Obrolan dan pekerja proaktif.</summary>
    private async Task PancingObrolanAsync(double? idleDetik)
    {
        if (_backend is null || IsSending)
        {
            return;
        }

        var gelembung = new ChatBubble { Role = "assistant", Pending = true };
        Messages.Add(gelembung);

        IsSending = true;
        try
        {
            var aliran = await _backend.ProactiveAsync(idleDetik, _cts.Token);

            if (aliran.Kizuna is { } pra)
            {
                Kizuna = pra;
            }

            if (aliran.Status != 200 || aliran.Body is null)
            {
                gelembung.Error = aliran.Error ?? $"backend menjawab {aliran.Status}";
                gelembung.Pending = false;
                return;
            }

            await AlirkanAsync(aliran.Body, gelembung);
        }
        catch (Exception galat)
        {
            CrashLog.Tulis("PancingObrolanAsync", galat);
            gelembung.Error = galat.Message;
            gelembung.Pending = false;
        }
        finally
        {
            IsSending = false;
            LastUserActivity = DateTimeOffset.Now;
            await SegarkanHealthAsync();
        }
    }

    /// <summary>
    /// Pekerja proaktif — port <c>proactive.js</c>. Syarat lewati persis seperti
    /// aplikasi lama: mode otonom mati, sedang mengirim, atau suara sibuk.
    /// </summary>
    private async Task CekProaktifAsync()
    {
        if (_sedangDibuang)
        {
            return;
        }

        if (!AutonomousMode || IsSending || SuaraSibuk)
        {
            return;
        }

        var hening = DateTimeOffset.Now - LastUserActivity;
        if (hening.TotalMilliseconds <= 65_000)
        {
            return;
        }

        var idle = Math.Max(10, (int)Math.Round(hening.TotalSeconds));
        await PancingObrolanAsync(idle);
    }

    private async Task AlirkanAsync(IAsyncEnumerable<StreamChunk> body, ChatBubble gelembung)
    {
        var mentah = new StringBuilder();

        await foreach (var potong in body)
        {
            if (potong.Err is { } galat)
            {
                gelembung.Error = galat.Message;
                gelembung.Pending = false;
                return;
            }

            mentah.Append(potong.Text ?? string.Empty);

            var teks = mentah.ToString();

            // Tag emosi dibuang dari yang ditampilkan, tetapi nilainya dipakai
            // untuk menggerakkan ekspresi — persis seperti Konsol aplikasi lama.
            gelembung.Content = EmotionParser.CleanEmotionTags(teks);

            var emosi = EmotionParser.ExtractEmotion(teks).Emotion;
            if (!string.IsNullOrWhiteSpace(emosi))
            {
                Expression = emosi!.ToLowerInvariant();
            }
        }

        gelembung.Pending = false;
    }

    private List<ChatMessage> RiwayatUntukBackend()
    {
        var daftar = new List<ChatMessage>();

        foreach (var b in Messages)
        {
            if (b.Pending || b.Error is not null || string.IsNullOrWhiteSpace(b.Content))
            {
                continue;
            }

            daftar.Add(new ChatMessage(b.Role == "assistant" ? "assistant" : "user", b.Content));
        }

        return daftar;
    }

    // ── Penyegaran ─────────────────────────────────────────────────────────

    /// <summary>
    /// Segarkan health sekaligus snapshot ikatan. Inilah pengganti
    /// <c>fetchKizuna()</c> yang dipanggil <c>store.send()</c> di akhir aliran
    /// — karena <c>/api/chat</c> tidak pernah mengirim header <c>x-kizuna</c>.
    /// </summary>
    private async Task SegarkanHealthAsync()
    {
        // Callback timer bisa dipanggil setelah Stop() saat pembongkaran.
        // Menyentuh _cts.Token sesudahnya melempar ObjectDisposedException —
        // pernah tercatat di crash.log. Lihat _sedangDibuang.
        if (_sedangDibuang || _backend is null)
        {
            return;
        }

        try
        {
            var snapshot = await _backend.GetHealthAsync(_cts.Token);
            TerapkanHealth(snapshot);
            HealthError = null;
        }
        catch (Exception galat)
        {
            CrashLog.Tulis("SegarkanHealthAsync", galat);
            HealthError = galat.Message;
        }

        if (_timerHealth is not null)
        {
            _timerHealth.Interval = TimeSpan.FromMilliseconds(IsReady ? 8000 : 2000);
        }
    }

    private void TerapkanHealth(HealthSnapshot? snapshot)
    {
        _health = snapshot;

        foreach (var nama in TurunanHealth)
        {
            OnPropertyChanged(nama);
        }

        // Snapshot ikatan ikut terisi di payload health — jadi HUD ikatan
        // tersegarkan oleh polling tanpa pemanggilan terpisah.
        if (snapshot?.Kizuna is { } ikatan)
        {
            Kizuna = ikatan;
        }

        OnPropertyChanged(nameof(Health));
    }

    // ── Mirror (padanan localStorage) ──────────────────────────────────────

    /// <summary>
    /// Padanan <c>localStorage['silverwolf_mirror_track']</c>.
    ///
    /// Tidak memakai <c>ApplicationData.Current.LocalSettings</c> karena
    /// aplikasi ini unpackaged dan API itu melempar untuk aplikasi tanpa
    /// identitas paket — lihat <see cref="UiSettings"/>.
    /// </summary>
    private static bool BacaMirror() => UiSettings.Baca(KunciMirror, bawaan: true);

    private static void SimpanMirror(bool nilai) => UiSettings.Tulis(KunciMirror, nilai);

    // ── Bongkar ────────────────────────────────────────────────────────────

    public async ValueTask DisposeAsync()
    {
        _sedangDibuang = true;

        _timerHealth?.Stop();
        _timerProaktif?.Stop();
        _timerHealth = null;
        _timerProaktif = null;

        // _cts hanya DIBATALKAN, tidak dibuang.
        //
        // Stop() tidak menunggu callback yang sedang berjalan, dan callback
        // yang belum mulai tetap akan dipanggil sekali lagi. Kalau _cts sudah
        // di-Dispose saat itu, callback langsung melempar
        // ObjectDisposedException. Membiarkan satu CancellationTokenSource
        // tidak dibuang tidak menimbulkan masalah: prosesnya memang sedang
        // berakhir, dan _sedangDibuang sudah menahan callback berikutnya.
        try
        {
            _cts.Cancel();
        }
        catch (ObjectDisposedException)
        {
            // sudah dibatalkan/dibuang sebelumnya
        }

        if (_runtime is not null)
        {
            await _runtime.DisposeAsync().ConfigureAwait(false);
        }

        GC.SuppressFinalize(this);
    }
}
