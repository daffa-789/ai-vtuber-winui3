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
using SilverWolf.Services.Llama;

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

    /// <summary>
    /// Model GGUF yang dipilih pengguna. Disimpan sebagai jalur relatif agar
    /// tetap berlaku bila folder proyek dipindah.
    /// </summary>
    private const string KunciModel = "silverwolf_model_path";

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
    private SilverWolf.Services.Tts.TtsPipeline? _tts;
    private float _levelSuara;
    private int _jumlahLevel;
    private float _puncakLevel;
    private string? _alasanSuaraHening;
    private long _versiSuara;

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
    /// <summary>
    /// Mode otonom (Neuro-sama). **Bawaan = mati sejak 2026-10-09.**
    ///
    /// <para>
    /// Dulu <c>true</c>, dan tombol NEURO-SAMA sudah dibuang dari XAML atas
    /// permintaan Master — jadi tidak ada lagi cara mematikannya dari UI.
    /// Akibatnya setiap 65 dtk hening Silver Wolf berbicara sendiri, dan Master
    /// melihat satu percakapan "dibalas dua kali".
    /// </para>
    /// <para>
    /// Nilai ini TIDAK tersimpan di <c>UiSettings</c>, jadi mengubah bawaan di
    /// sini langsung berlaku. Tombol bisa dipasang kembali kapan saja lewat
    /// <c>PancingObrolanCommand</c> — kalaupun dinyalakan lagi, pemeriksaan di
    /// <see cref="CekProaktifAsync"/> mencegah dua balasan berturut-turut.
    /// </para>
    /// </summary>
    private bool _autonomousMode = false;
    private bool _suaraAktif = true;
    private bool _mirrorTrack;
    private DateTimeOffset _lastUserActivity = DateTimeOffset.Now;
    private string _draf = string.Empty;

    /// <summary>Jalur absolut setiap model, sejajar urutan <see cref="NamaModel"/>.</summary>
    private readonly List<string> _jalurModel = new();

    private int _indeksModel = -1;

    /// <summary>
    /// Index model yang <b>benar-benar sedang dimuat</b> llama-server. Terpisah
    /// dari <see cref="IndeksModel"/> (pilihan ComboBox) supaya kegagalan
    /// pemuatan bisa mengembalikan pilihan ke keadaan sebenarnya, dan supaya
    /// memilih model yang sudah aktif tidak memicu pemuatan ulang 5 GB.
    /// </summary>
    private int _indeksModelAktif = -1;

    /// <summary>
    /// Pengaman: ComboBox mengubah <see cref="IndeksModel"/> juga saat daftarnya
    /// sedang diisi, dan itu bukan permintaan pengguna untuk memuat ulang model.
    /// </summary>
    private bool _siapGantiModel;

    private bool _sedangGantiModel;

    private string _statusModel = string.Empty;

    public CompanionViewModel()
    {
        // _mirrorTrack TIDAK dibaca di sini. Konstruktor ini berjalan di jalur
        // MainWindow → Activate(); membaca JSON dari disk secara sinkron di UI
        // thread menahan aktivasi tanpa alasan. Pembacaan dipindah ke
        // InisialisasiAsync (lihat BacaMirror di sana). Nilai awal false sampai
        // pengaturan tersimpan dibaca; bawaan pengaturan sebenarnya true.

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
        private set
        {
            // AdaHealthError diturunkan dari HealthError. Tanpa pemberitahuan
            // manual ini, banner galat tidak pernah muncul walau nilainya
            // berubah — SetProperty hanya memberitakan "HealthError".
            if (SetProperty(ref _healthError, value))
            {
                OnPropertyChanged(nameof(AdaHealthError));
            }
        }
    }

    /// <summary>
    /// Harus ditampilkan atau tidak. Dipakai XAML lewat pengikatan langsung —
    /// WinUI 3 sudah punya konversi bool -> Visibility bawaan, jadi tidak perlu
    /// konverter khusus dan tidak boleh ada <c>Visibility</c> yang dipaksa.
    ///
    /// Kenapa ini ada: sebelum 2026-10-08, <c>HealthError</c> diisi tetapi
    /// **tidak ada satu pun elemen XAML yang menampilkannya** setelah panel HUD
    /// dibuang. Kegagalan runtime jadi tidak terlihat sama sekali — pengguna
    /// hanya melihat chat yang tidak pernah menjawab, tanpa tahu sebabnya.
    /// </summary>
    public bool AdaHealthError => !string.IsNullOrWhiteSpace(HealthError);

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
    /// <c>audioBusy</c>. Diisi oleh <see cref="Tts"/> selama suara diputar —
    /// sebelum ini properti ini ada tetapi <b>tidak pernah diisi</b>, sehingga
    /// obrolan proaktif bisa berbicara di tengah kalimat yang sedang diucapkan.
    /// </summary>
    public bool SuaraSibuk
    {
        get => _suaraSibuk;
        set => SetProperty(ref _suaraSibuk, value);
    }

    /// <summary>
    /// Rantai suara (Piper → RVC) + pemutarannya. <c>null</c> kalau TTS mati
    /// atau konfigurasinya belum siap — dalam hal itu <see cref="TeksTts"/>
    /// memberi tahu alasannya, bukan diam.
    /// </summary>
    public SilverWolf.Services.Tts.TtsPipeline? Tts => _tts;

    /// <summary>
    /// Level amplitudo suara 0..1 yang sedang diputar, dipakai LipSync supaya
    /// mulut bergerak mengikuti suara nyata.
    /// </summary>
    public float LevelSuara
    {
        get => _levelSuara;
        private set
        {
            if (!SetProperty(ref _levelSuara, value))
            {
                return;
            }

            // Diagnostik murah: berapa kali level benar-benar sampai dan
            // setinggi apa. Tanpa ini, "mulut tidak bergerak" tidak bisa
            // dipisahkan antara (a) tidak ada audio yang diputar, (b) audio
            // diputar tetapi level tidak pernah dikirim, atau (c) level
            // dikirim tetapi nilainya selalu nol. Ketiganya terlihat sama
            // dari luar — persis keluhan Master.
            _jumlahLevel++;
            if (value > _puncakLevel) _puncakLevel = value;

            // Dicatat tiap ~0,5 dtk supaya tidak membanjiri crash.log pada
            // 60 laporan per detik, tetapi tetap memberi jejak yang terbaca.
            if (_jumlahLevel == 1 || _jumlahLevel % 30 == 0)
            {
                CrashLog.Tahap($"lipsync: level#{_jumlahLevel} kini={value:F3} puncak={_puncakLevel:F3}");
            }
        }
    }

    /// <summary>
    /// Ringkasan jalur LipSync untuk ditampilkan di UI. Membuat "mulut diam"
    /// bisa dibedakan: "tidak ada suara", "suara jalan tapi level nol", atau
    /// "level ada tetapi panggung belum siap".
    /// </summary>
    public string TeksMulut =>
        _jumlahLevel == 0
            ? "mulut: belum ada level suara"
            : $"mulut: {_jumlahLevel} level, puncak {_puncakLevel:F2}";

    /// <summary>Alasan suara tidak keluar; <c>null</c> kalau tidak ada masalah.</summary>
    public string? AlasanSuaraHening
    {
        get => _alasanSuaraHening;
        private set
        {
            if (SetProperty(ref _alasanSuaraHening, value))
            {
                OnPropertyChanged(nameof(AdaMasalahSuara));
                OnPropertyChanged(nameof(TeksTts));
            }
        }
    }

    /// <summary>Padanan <see cref="AdaHealthError"/> untuk jalur suara.</summary>
    public bool AdaMasalahSuara => !string.IsNullOrWhiteSpace(AlasanSuaraHening);

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

    public string TeksKirim => IsSending ? "MENYIAPKAN…" : "KIRIM";

    public string TeksGpu => _health?.Model ?? "-";

    public string TeksMemori => _health?.Memori ?? "memori tidak tersedia";

    /// <summary>
    /// Keadaan rantai suara sebenarnya.
    ///
    /// Dulu properti ini hanya meneruskan <c>_health?.Tts</c> dan jatuh ke
    /// "siap" kalau kosong — jadi rantai suara yang MATI tampil sama persis
    /// dengan yang HIDUP, dan satu-satunya gejalanya adalah "dia tidak
    /// menjawab". Sekarang urutannya: masalah nyata dari pipeline suara dulu
    /// (paling penting), lalu laporan health, baru terakhir "siap".
    /// </summary>
    public string TeksTts
    {
        get
        {
            if (!string.IsNullOrWhiteSpace(_alasanSuaraHening))
            {
                return _alasanSuaraHening!;
            }

            return _health?.Tts ?? "siap";
        }
    }

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

    // ── Pemilih model ──────────────────────────────────────────────────────

    /// <summary>Nama berkas setiap GGUF yang ditemukan, untuk ComboBox.</summary>
    public ObservableCollection<string> NamaModel { get; } = new();

    /// <summary>
    /// Indek model aktif. Setter-nya sengaja memicu pemuatan ulang — itu cara
    /// ComboBox memberitahu "pengguna memilih model lain".
    /// </summary>
    public int IndeksModel
    {
        get => _indeksModel;
        set
        {
            if (value == _indeksModel)
            {
                return;
            }

            if (SetProperty(ref _indeksModel, value))
            {
                OnPropertyChanged(nameof(TeksModel));

                // Jangan memuat ulang saat daftar sedang diisi, dan jangan
                // menumpuk dua pemuatan sekaligus.
                if (_siapGantiModel && value >= 0 && !_sedangGantiModel)
                {
                    _ = GantiModelAsync(value);
                }
            }
        }
    }

    /// <summary>
    /// Sedang memuat GGUF lain. Mengunci ComboBox supaya pengguna tidak
    /// memilih berkali-kali selama model 5 GB masuk ke VRAM.
    /// </summary>
    public bool SedangGantiModel
    {
        get => _sedangGantiModel;
        private set
        {
            if (SetProperty(ref _sedangGantiModel, value))
            {
                OnPropertyChanged(nameof(TeksModel));
                OnPropertyChanged(nameof(BisaPilihModel));
            }
        }
    }

    /// <summary>Pesan hasil pemuatan terakhir; kosong bila belum pernah ganti.</summary>
    public string StatusModel
    {
        get => _statusModel;
        private set
        {
            if (SetProperty(ref _statusModel, value))
            {
                OnPropertyChanged(nameof(AdaStatusModel));
            }
        }
    }

    public bool AdaStatusModel => !string.IsNullOrWhiteSpace(StatusModel);

    /// <summary>Sembunyikan pemilih bila cuma ada satu model — tidak ada yang bisa dipilih.</summary>
    public bool BisaGantiModel => NamaModel.Count > 1;

    /// <summary>Index model yang sedang dimuat; <c>-1</c> bila tidak ada.</summary>
    public int IndeksModelAktif
    {
        get => _indeksModelAktif;
        private set => SetProperty(ref _indeksModelAktif, value);
    }

    /// <summary>
    /// Bolehkah ComboBox disentuh. Terbalik dari <see cref="SedangGantiModel"/>
    /// karena XAML di sini memakai pengikatan langsung tanpa konverter.
    /// </summary>
    public bool BisaPilihModel => !_sedangGantiModel;

    /// <summary>Label ComboBox: nama model aktif, atau keadaan pemuatan.</summary>
    public string TeksModel
    {
        get
        {
            if (_sedangGantiModel)
            {
                return "MEMUAT MODEL…";
            }

            if (_indeksModel >= 0 && _indeksModel < NamaModel.Count)
            {
                return NamaModel[_indeksModel];
            }

            return "-";
        }
    }

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
            // Baca di latar tanpa menulis ulang JSON lewat setter MirrorTrack.
            var mirror = await Task.Run(BacaMirror);
            if (_sedangDibuang) return;
            if (SetProperty(ref _mirrorTrack, mirror, nameof(MirrorTrack)))
                OnPropertyChanged(nameof(TeksMirror));
            CrashLog.Tahap("runtime: StartAsync mulai");

            var modelTersimpan = await Task.Run(BacaModelTersimpan);
            if (_sedangDibuang) return;

            var runtime = await CompanionRuntime.StartAsync(
                log: pesan => CrashLog.Tahap($"runtime: {pesan}"),
                onError: galat => CrashLog.Tulis("runtime", galat),
                modelPath: modelTersimpan,
                ct: _cts.Token);
            if (_sedangDibuang)
            {
                await runtime.DisposeAsync();
                return;
            }
            _runtime = runtime;
            _backend = _runtime.Backend;
            CrashLog.Tahap("runtime: siap");

            MuatDaftarModel(runtime);

            SiapkanTts(_runtime);

            await SegarkanHealthAsync();

            MulaiTimer();
            CrashLog.Tahap("runtime: timer health + proaktif jalan");
        }
        catch (OperationCanceledException) when (_sedangDibuang)
        {
            // Penutupan selama startup bukan galat runtime.
        }
        catch (Exception galat)
        {
            CrashLog.Tulis("InisialisasiAsync", galat);
            HealthError = galat.Message;
        }
    }

    /// <summary>
    /// Isi daftar model dari berkas GGUF yang benar-benar ada di
    /// <c>model/</c>, lalu tandai mana yang sedang dipakai.
    ///
    /// <para>
    /// Dipanggil sekali setelah runtime siap — bukan dari konstruktor — karena
    /// butuh <see cref="AppConfig"/> yang sudah jadi (dan pemindaian disk tidak
    /// boleh menahan aktivasi jendela).
    /// </para>
    /// </summary>
    private void MuatDaftarModel(CompanionRuntime runtime)
    {
        try
        {
            var jalur = ModelLocator.Pilihan(runtime.Konfig);
            var aktif = ModelLocator.Absolut(runtime.Konfig, runtime.Konfig.LocalModelPath);

            _siapGantiModel = false;
            NamaModel.Clear();
            _jalurModel.Clear();

            var indeksAktif = -1;
            for (var i = 0; i < jalur.Count; i++)
            {
                NamaModel.Add(Path.GetFileName(jalur[i]));
                _jalurModel.Add(jalur[i]);

                if (string.Equals(jalur[i], aktif, StringComparison.OrdinalIgnoreCase))
                {
                    indeksAktif = i;
                }
            }

            _indeksModel = indeksAktif;
            _indeksModelAktif = indeksAktif;
            _siapGantiModel = true;

            OnPropertyChanged(nameof(IndeksModel));
            OnPropertyChanged(nameof(IndeksModelAktif));
            OnPropertyChanged(nameof(TeksModel));
            OnPropertyChanged(nameof(BisaGantiModel));

            CrashLog.Tahap(
                $"model: {jalur.Count} ditemukan, aktif = " +
                (indeksAktif >= 0 ? NamaModel[indeksAktif] : "tidak ada")
                + (jalur.Count > 0 ? $" | daftar = {string.Join(", ", NamaModel)}" : string.Empty));
        }
        catch (Exception galat)
        {
            // Daftar model yang gagal dimuat tidak boleh menggagalkan startup:
            // chat tetap harus jalan. Tetap dicatat supaya kalau pemilihnya
            // hilang, penyebabnya bisa dibaca di crash.log.
            CrashLog.Tulis("MuatDaftarModel", galat, $"akar={runtime.Konfig.Akar}");
        }
    }

    /// <summary>
    /// Muat GGUF lain ke llama-server. Ini operasi berat: model 5 GB butuh
    /// puluhan detik, jadi ComboBox dikunci dan keadaannya diberitahu ke
    /// pengguna lewat <see cref="TeksModel"/>.
    /// </summary>
    private async Task GantiModelAsync(int indeks)
    {
        if (indeks < 0 || indeks >= _jalurModel.Count || _runtime is null)
        {
            return;
        }

        var jalur = _jalurModel[indeks];
        var nama = Path.GetFileName(jalur);

        // Sudah aktif? Jangan mematikan llama-server dan memuat ulang 5 GB hanya
        // karena index ComboBox berubah (mis. saat daftar diisi ulang).
        if (IndeksModelAktif == indeks)
        {
            return;
        }

        SedangGantiModel = true;
        StatusModel = $"memuat {nama}…";
        CrashLog.Tahap($"model: ganti ke {nama}");
        // Selalu kembali ke utas UI: SegarkanHealthAsync di bawah menyentuh
        // state terikat XAML (HealthError/StatusTeks), dan callback ini bisa
        // berjalan di utas threadpool karena dipanggil dari setter properti.
        var ui = _dispatcher;

        try
        {
            var hasil = await _runtime.GantiModelAsync(jalur, _cts.Token)
                .ConfigureAwait(false);
            if (_sedangDibuang)
            {
                return;
            }

            if (hasil.Ok)
            {
                IndeksModelAktif = indeks;
                // Simpan hanya setelah berhasil dimuat — kalau gagal, pilihan
                // lama harus tetap yang dipakai saat aplikasi dibuka lagi.
                SimpanModel(jalur);
                StatusModel = $"model aktif: {nama}";
                HealthError = null;
            }
            else
            {
                // Kembalikan pilihan ke model yang benar-benar berjalan supaya
                // ComboBox tidak berbohong tentang keadaan sebenarnya.
                StatusModel = $"gagal memuat {nama}: {hasil.Reason}";
                HealthError = $"Gagal memuat model {nama}: {hasil.Reason}";
                CrashLog.Tahap($"model: gagal — {hasil.Reason}");

                if (ui is not null)
                {
                    ui.TryEnqueue(() =>
                    {
                        if (_sedangDibuang || IndeksModelAktif < 0) return;
                        IndeksModel = IndeksModelAktif;
                    });
                }
            }

            await SegarkanHealthAsync().ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (_sedangDibuang)
        {
            // Penutupan selama pemuatan bukan galat.
        }
        catch (Exception galat)
        {
            CrashLog.Tulis("GantiModelAsync", galat);
            StatusModel = $"gagal memuat {nama}: {galat.Message}";
        }
        finally
        {
            SedangGantiModel = false;
        }
    }

    private static string? BacaModelTersimpan()
    {
        var teks = UiSettings.BacaTeks(KunciModel);
        return string.IsNullOrWhiteSpace(teks) ? null : teks;
    }

    private static void SimpanModel(string absolut)
    {
        // Butuh akar proyek untuk membuat jalur relatif; AppPaths tahu caranya.
        var akar = SilverWolf.Services.Configuration.AppPaths.TentukanAkar(null);
        var relatif = ModelLocator.Relatif(
            new SilverWolf.Core.Configuration.AppConfig { Akar = akar }, absolut);

        UiSettings.TulisTeks(KunciModel, relatif);
    }

    /// <summary>
    /// Bangun rantai suara dari konfigurasi runtime.
    ///
    /// <para>
    /// <b>Inilah bagian yang hilang sampai 2026-10-09.</b> Sebelum ini tidak ada
    /// satu pun kode yang memutar audio di seluruh proyek: NAudio dirujuk di
    /// csproj sehingga DLL-nya ikut tersalin, tetapi tidak pernah dipanggil.
    /// Akibatnya balasan masuk ke chat tanpa suara sama sekali — persis keluhan
    /// "pesannya sudah jalan, TTS-nya tidak balas".
    /// </para>
    ///
    /// <para>
    /// Kegagalan di sini <b>tidak boleh</b> menjatuhkan inisialisasi: chat tetap
    /// harus jalan walaupun suara tidak siap. Karena itu alasan kegagalannya
    /// disimpan di <see cref="AlasanSuaraHening"/> untuk ditampilkan, bukan
    /// dilempar sebagai exception.
    /// </para>
    /// </summary>
    private void SiapkanTts(CompanionRuntime runtime)
    {
        try
        {
            var konfig = runtime.Konfig;
            _tts = new SilverWolf.Services.Tts.TtsPipeline(
                konfig,
                pesan => CrashLog.Tahap($"tts: {pesan}"));

            _tts.LevelBerubah += level =>
            {
                // Kejadian ini datang dari utas audio, bukan utas UI. Menyentuh
                // state terikat XAML dari utas lain = crash senyap.
                _dispatcher?.TryEnqueue(() =>
                {
                    if (!_sedangDibuang && SuaraSibuk) LevelSuara = level;
                });
            };

            var alasan = _tts.AlasanTidakSiap();
            AlasanSuaraHening = alasan;

            CrashLog.Tahap(alasan is null
                ? $"tts: siap (rantai={konfig.TtsRantai}, rvc={konfig.Rvc})"
                : $"tts: TIDAK siap - {alasan}");
        }
        catch (Exception galat)
        {
            CrashLog.Tulis("SiapkanTts", galat);
            AlasanSuaraHening = galat.Message;
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

        var gelembung = new ChatBubble { Role = "assistant", Pending = true, SedangMemuat = true };
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

        var gelembung = new ChatBubble { Role = "assistant", Pending = true, SedangMemuat = true };
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

        var gelembung = new ChatBubble { Role = "assistant", Pending = true, SedangMemuat = true };
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

        // Satu percakapan = satu balasan. Kalau balasan terakhir yang punya isi
        // sudah dari asisten, jangan menambah balasan lagi.
        //
        // Inilah sumber keluhan Master: ia meninggalkan jendela, pekerja
        // proaktif lalu menyisipkan satu balasan baru di ATAS balasan yang
        // sudah ada, sehingga terlihat seperti satu pesan dibalas dua kali.
        for (var i = Messages.Count - 1; i >= 0; i--)
        {
            var b = Messages[i];
            if (string.IsNullOrWhiteSpace(b.Content))
            {
                continue;
            }

            if (b.Role == "assistant")
            {
                return;
            }

            break;
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
        var ct = _cts.Token;
        var mentah = new StringBuilder();
        gelembung.SedangMemuat = true;
        gelembung.TeksMemuat = "sedang berpikir…";
        // Balasan baru membatalkan ucapan lama sebelum mulai inferensi/produksi.
        var versiSuara = ++_versiSuara;
        _tts?.Hentikan();
        SuaraSibuk = false;
        LevelSuara = 0f;
        var pantauSuara = false;
        try
        {
            // Buffer saja: tidak mengubah Content atau wajah pada setiap token.
            await foreach (var potong in body.WithCancellation(ct))
            {
                ct.ThrowIfCancellationRequested();
                if (potong.Err is { } galat)
                {
                    gelembung.Error = galat.Message;
                    return;
                }
                mentah.Append(potong.Text ?? string.Empty);
            }

            ct.ThrowIfCancellationRequested();
            var teks = EmotionParser.CleanEmotionTags(mentah.ToString());
            if (string.IsNullOrWhiteSpace(teks))
            {
                gelembung.Error = "Model tidak menghasilkan balasan. Silakan coba lagi.";
                return;
            }

            var tts = _tts;
            if (SuaraAktif && tts is not null)
            {
                gelembung.TeksMemuat = "sedang menyiapkan suara…";
                SuaraSibuk = true;
                // Mulai hitung ulang per gelembung, supaya angka diagnostik
                // LipSync yang tercatat memang milik balasan ini.
                var levelSebelum = _jumlahLevel;
                try
                {
                    // Seluruh kalimat disintesis dulu sebelum ada suara, jadi
                    // gelembung harus menunjukkan kemajuan — tanpa ini Master
                    // melihat "sedang menyiapkan suara…" yang tampak menggantung.
                    // Callback datang dari utas pekerja, jadi wajib dipindah ke
                    // utas UI sebelum menyentuh properti terikat.
                    var siap = await tts.SiapkanDanPutarAsync(teks, ct, (selesai, total) =>
                    {
                        _dispatcher?.TryEnqueue(() =>
                        {
                            if (_sedangDibuang || versiSuara != _versiSuara) return;
                            gelembung.TeksMemuat = total > 1
                                ? $"sedang menyiapkan suara… ({selesai}/{total})"
                                : "sedang menyiapkan suara…";
                        });
                    });
                    ct.ThrowIfCancellationRequested();
                    if (siap)
                    {
                        AlasanSuaraHening = null;
                        pantauSuara = true;
                        // Snapshot tugas dan versi: penyelesaian lama tidak
                        // boleh mereset state ucapan baru. Tidak memakai timeout
                        // palsu lima menit untuk menandai ucapan sudah selesai.
                        _ = PantauSuaraAsync(tts.Penyelesaian, versiSuara);

                        // Diagnostik LipSync: audio SUDAH mulai diputar, jadi
                        // seharusnya level mengalir. Kalau tidak ada satu pun
                        // level yang sampai, mulut akan diam walau suaranya
                        // terdengar — dan itu persis keluhan Master. Dicatat
                        // supaya bisa dibedakan dari "memang tidak ada suara".
                        _ = LaporkanMulutAsync(levelSebelum, versiSuara);
                    }
                    else
                    {
                        gelembung.CatatanSuara = "Suara tidak dapat dimulai; balasan ditampilkan sebagai teks.";
                        AlasanSuaraHening = tts.AlasanTidakSiap() ?? "Tidak ada audio yang berhasil dimulai. Lihat crash.log.";
                    }
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception galat)
                {
                    CrashLog.Tulis("AlirkanAsync.SiapkanDanPutar", galat);
                    gelembung.CatatanSuara = "Suara gagal disiapkan; balasan ditampilkan sebagai teks.";
                    AlasanSuaraHening = galat.Message;
                }
            }
            else if (SuaraAktif)
            {
                gelembung.CatatanSuara = "Suara belum siap; balasan ditampilkan sebagai teks.";
            }

            ct.ThrowIfCancellationRequested();
            if (_sedangDibuang) return;
            // Ekspresi dan chat tampil pada titik yang sama, setelah audio mulai
            // atau setelah kegagalan TTS yang dijelaskan kepada pengguna.
            var emosi = EmotionParser.ExtractEmotion(mentah.ToString()).Emotion;
            if (!string.IsNullOrWhiteSpace(emosi)) Expression = emosi.ToLowerInvariant();
            gelembung.Content = teks;
        }
        catch (OperationCanceledException)
        {
            gelembung.Error = "Balasan dibatalkan.";
        }
        finally
        {
            gelembung.Pending = false;
            if (!pantauSuara && versiSuara == _versiSuara)
            {
                SuaraSibuk = false;
                LevelSuara = 0f;
            }
        }
    }

    private async Task PantauSuaraAsync(Task selesai, long versiSuara)
    {
        try { await selesai.ConfigureAwait(false); }
        catch (Exception galat) { CrashLog.Tulis("PantauSuaraAsync", galat); }
        finally
        {
            _dispatcher?.TryEnqueue(() =>
            {
                if (_sedangDibuang || versiSuara != _versiSuara) return;
                SuaraSibuk = false;
                LevelSuara = 0f;
            });
        }
    }

    /// <summary>
    /// Setelah pemutaran selesai, laporkan berapa level LipSync yang benar-benar
    /// sampai selama balasan ini.
    ///
    /// <para>
    /// <b>Kenapa ini perlu ada.</b> "Mulut tidak bergerak" punya tiga sebab yang
    /// dari luar terlihat identik:
    /// </para>
    /// <list type="number">
    /// <item><description>Tidak ada audio yang diputar sama sekali — mulut
    /// memang tidak punya apa pun untuk diikuti.</description></item>
    /// <item><description>Audio diputar, tetapi <c>LevelBerubah</c> tidak pernah
    /// menyala (pemutar tersangkut, berkas tidak terbaca).</description></item>
    /// <item><description>Level mengalir tetapi nilainya selalu nol (berkas
    /// hening, atau normalisasi gagal).</description></item>
    /// </list>
    /// <para>
    /// Angka ini memisahkan ketiganya tanpa perlu debugger, dan tanpa memaksa
    /// Master menebak-nebak.
    /// </para>
    /// </summary>
    private async Task LaporkanMulutAsync(int levelSebelum, long versiSuara)
    {
        // Beri waktu pemutaran benar-benar berjalan sebelum menyimpulkan.
        try { await Task.Delay(1500).ConfigureAwait(false); }
        catch (Exception) { return; }

        _dispatcher?.TryEnqueue(() =>
        {
            if (_sedangDibuang || versiSuara != _versiSuara) return;

            var baru = _jumlahLevel - levelSebelum;
            if (baru <= 0)
            {
                // Suara sudah mulai diputar tetapi tidak ada level yang sampai:
                // inilah yang membuat mulut diam walau speaker berbunyi.
                CrashLog.Tahap("lipsync: TIDAK ada level yang sampai walau audio sudah mulai — mulut akan diam");
                AlasanSuaraHening =
                    "Suara diputar tetapi data amplitudo tidak mengalir; mulut tidak bisa bergerak. Lihat crash.log.";
                OnPropertyChanged(nameof(TeksMulut));
                return;
            }

            CrashLog.Tahap($"lipsync: {baru} level sampai selama balasan ini, puncak={_puncakLevel:F3}");
            OnPropertyChanged(nameof(TeksMulut));
        });
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

        // TTS dibongkar SEBELUM runtime.
        //
        // Rantai TTS menjalankan proses Python (RVC memuat model ke memori dan
        // hidup lama). Kalau tidak ditunggu, menutup jendela meninggalkan proses
        // Python yatim yang menahan beberapa GB RAM — persis bahan bakar Mode B.
        // Batas 3 detik dipakai supaya penutupan jendela tidak terasa menggantung;
        // pembatalan diteruskan TtsWorker ke Kill(entireProcessTree: true).
        // Dispose pipeline menunggu tugas berakhir sebelum melepas pemutar.
        if (_tts is not null)
        {
            try
            {
                _tts.Hentikan();
                await _tts.TungguSelesaiAsync(TimeSpan.FromSeconds(3)).ConfigureAwait(false);
            }
            catch (Exception)
            {
                // Penutupan tidak boleh gagal hanya karena suara masih sibuk.
            }

            _tts.Dispose();
            _tts = null;
        }

        // Buang seluruh audio sementara TTS SETELAH _tts.Dispose().
        //
        // Urutannya wajib: Dispose() memicu _buangPemutar() di TtsPipeline yang
        // mematikan pekerja Python (TtsWorker.Matikan -> PekerjaTts.Matikan),
        // dan di atasnya TungguSelesaiAsync(3 dtk) sudah menunggu tugas suara
        // berakhir. Folder %TEMP%\sw-pekerja-* dipegang proses Python itu; kalau
        // dihapus selagi prosesnya hidup, penghapusan gagal senyap. Bersihkan
        // sendiri tidak pernah melempar, jadi aman di jalur penutupan.
        try
        {
            SilverWolf.Services.Tts.TempAudio.Bersihkan(pesan => CrashLog.Tahap(pesan));
        }
        catch (Exception)
        {
            // Penutupan tidak boleh gagal hanya karena pembersihan berkas.
        }

        if (_runtime is not null)
        {
            await _runtime.DisposeAsync().ConfigureAwait(false);
        }

        GC.SuppressFinalize(this);
    }
}
