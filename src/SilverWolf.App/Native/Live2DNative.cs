using System.Runtime.InteropServices;
using SilverWolf.App.Diagnostics;
using WinRT;

namespace SilverWolf.App.Native;

/// <summary>
/// Antarmuka ke <c>SilverWolf.Live2D.dll</c> — renderer Live2D native (M8).
///
/// <b>Degradasi anggun adalah syarat, bukan tambahan.</b> DLL ini hanya ada
/// setelah proyek native dibangun di Visual Studio. Kalau belum ada, aplikasi
/// harus tetap hidup dan menampilkan placeholder — bukan mati saat startup.
/// Karena itu setiap pemanggilan dibungkus, dan kegagalan dicatat ke
/// <c>CrashLog</c> supaya langsung kelihatan di <c>crash.log</c>.
/// </summary>
internal static class Live2DNative
{
    private const string Berkas = "SilverWolf.Live2D.dll";

    /// <summary>Dipakai bila DLL belum tersedia.</summary>
    public const int TidakSiap = -100;

    private static bool _siap;
    private static bool _pernahDicoba;
    private static string _alasan = "belum dicoba";

    // Delegasi pencatat harus disimpan — kalau tidak, ia akan dikumpulkan
    // GC dan native akan memanggil penunjuk yang sudah mati.
    private static LogSink? _penampungLog;

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate void LogSink(string message);

    public static bool Siap => _siap;

    public static string Alasan => _alasan;

    /// <summary>Mulai CubismFramework. Mengembalikan kode galat, 0 bila siap.</summary>
    public static int Mulai()
    {
        if (_pernahDicoba)
        {
            return _siap ? 0 : TidakSiap;
        }

        _pernahDicoba = true;

        try
        {
            _penampungLog = pesan => CrashLog.Tahap($"[live2d] {pesan}");
            swl2d_set_log(_penampungLog);

            var hasil = swl2d_init();
            if (hasil == 0)
            {
                _siap = true;
                CrashLog.Tahap("live2d: framework siap");
            }
            else
            {
                _alasan = $"swl2d_init mengembalikan {hasil}";
                CrashLog.Tahap($"live2d: GAGAL — {_alasan}");
            }

            return hasil;
        }
        catch (DllNotFoundException galat)
        {
            _alasan = $"{Berkas} tidak ditemukan (belum dibangun di Visual Studio?)";
            CrashLog.Tulis("Live2DNative.Mulai", galat, _alasan);
            return TidakSiap;
        }
        catch (Exception galat)
        {
            _alasan = galat.Message;
            CrashLog.Tulis("Live2DNative.Mulai", galat);
            return TidakSiap;
        }
    }

    public static int BuatPanggung(IntPtr panelAsli, string direktoriModel, string namaBerkasModel) =>
        Coba(() => swl2d_stage_create(panelAsli, direktoriModel, namaBerkasModel), "swl2d_stage_create");

    public static int UbahUkuran(int panggung, int lebar, int tinggi) =>
        Coba(() => swl2d_stage_resize(panggung, lebar, tinggi), "swl2d_stage_resize");

    public static int Gambar(int panggung) =>
        Coba(() => swl2d_stage_render(panggung), "swl2d_stage_render");

    public static int AturEkspresi(int panggung, string nama) =>
        Coba(() => swl2d_stage_set_expression(panggung, nama), "swl2d_stage_set_expression");

    public static int MainkanGerakan(int panggung, string grup, int nomor) =>
        Coba(() => swl2d_stage_play_motion(panggung, grup, nomor), "swl2d_stage_play_motion");

    /// <summary>Putar gerakan idle berulang. Tanpa ini model tampak diam.</summary>
    public static int MainkanIdle(int panggung, string grup, int nomor) =>
        Coba(() => swl2d_stage_play_idle(panggung, grup, nomor), "swl2d_stage_play_idle");

    public static int AturPandang(int panggung, float x, float y) =>
        Coba(() => swl2d_stage_set_look(panggung, x, y), "swl2d_stage_set_look");

    public static int AturTampak(int panggung, float perbesaran, float geserX, float jangkarY) =>
        Coba(() => swl2d_stage_set_view(panggung, perbesaran, geserX, jangkarY), "swl2d_stage_set_view");

    /// <summary>
    /// LipSync: atur bukaan mulut 0..1. Dipanggil ~60 kali per detik selama
    /// audio diputar. Nilainya dihaluskan di sisi native, jadi mengirim nilai
    /// mentah RMS di sini memang yang diinginkan.
    /// </summary>
    public static int AturMulut(int panggung, float buka) =>
        Coba(() => swl2d_stage_set_mulut(panggung, buka), "swl2d_stage_set_mulut");

    public static void Hentikan()
    {
        if (!_siap)
        {
            return;
        }

        try
        {
            swl2d_shutdown();
        }
        catch (Exception galat)
        {
            CrashLog.Tulis("Live2DNative.Hentikan", galat);
        }
        finally
        {
            _siap = false;
        }
    }

    /// <summary>
    /// Ambil penunjuk IUnknown milik SwapChainPanel. Komponen native yang akan
    /// melakukan QueryInterface ke ISwapChainPanelNative, jadi di sini cukup
    /// penunjuk dasarnya saja.
    /// </summary>
    public static IntPtr AmbilPenunjukPanel(object panel)
    {
        try
        {
            // Penunjuk identitas (IUnknown kanonik) lebih aman daripada
            // NativeObject.ThisPtr, yang bisa jadi penunjuk ke antarmuka bawaan
            // kelas dan belum tentu identitas objeknya.
            var identitas = Marshal.GetIUnknownForObject(panel);

            if (panel is IWinRTObject objekWinrt)
            {
                // ThisPtr adalah penunjuk WinRT yang sebenarnya.
                // Marshal.GetIUnknownForObject mengembalikan CCW terkelola milik
                // RCW, BUKAN objek XAML-nya — QI ke IInspectable padanya gagal,
                // sehingga ISwapChainPanelNative mustahil ditemukan.
                CrashLog.Tahap($"live2d: ThisPtr={objekWinrt.NativeObject.ThisPtr} (dipakai), identitasCCW={identitas} (tidak dipakai)");
                return objekWinrt.NativeObject.ThisPtr;
            }

            return identitas;
        }
        catch (Exception galat)
        {
            CrashLog.Tulis("Live2DNative.AmbilPenunjukPanel", galat);
            return IntPtr.Zero;
        }
    }

    private static int Coba(Func<int> aksi, string nama)
    {
        if (!_siap)
        {
            return TidakSiap;
        }

        try
        {
            return aksi();
        }
        catch (DllNotFoundException galat)
        {
            _siap = false;
            _alasan = $"{Berkas} hilang saat {nama}";
            CrashLog.Tulis(nama, galat, _alasan);
            return TidakSiap;
        }
        catch (EntryPointNotFoundException galat)
        {
            _siap = false;
            _alasan = $"ekspor {nama} tidak ada di DLL";
            CrashLog.Tulis(nama, galat, _alasan);
            return TidakSiap;
        }
        catch (Exception galat)
        {
            CrashLog.Tulis(nama, galat);
            return TidakSiap;
        }
    }

    // ── Ekspor C ──────────────────────────────────────────────────────────

    [DllImport(Berkas, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    private static extern void swl2d_set_log(LogSink sink);

    [DllImport(Berkas, CallingConvention = CallingConvention.Cdecl)]
    private static extern int swl2d_init();

    [DllImport(Berkas, CallingConvention = CallingConvention.Cdecl)]
    private static extern void swl2d_shutdown();

    [DllImport(Berkas, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    private static extern int swl2d_stage_create(IntPtr panelNative, string direktoriModel, string namaBerkasModel);

    [DllImport(Berkas, CallingConvention = CallingConvention.Cdecl)]
    private static extern int swl2d_stage_resize(int panggung, int lebar, int tinggi);

    [DllImport(Berkas, CallingConvention = CallingConvention.Cdecl)]
    private static extern int swl2d_stage_render(int panggung);

    [DllImport(Berkas, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    private static extern int swl2d_stage_set_expression(int panggung, string nama);

    [DllImport(Berkas, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    private static extern int swl2d_stage_play_motion(int panggung, string grup, int nomor);

    [DllImport(Berkas, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    private static extern int swl2d_stage_play_idle(int panggung, string grup, int nomor);

    [DllImport(Berkas, CallingConvention = CallingConvention.Cdecl)]
    private static extern int swl2d_stage_set_look(int panggung, float x, float y);

    [DllImport(Berkas, CallingConvention = CallingConvention.Cdecl)]
    private static extern int swl2d_stage_set_view(int panggung, float perbesaran, float geserX, float jangkarY);

    [DllImport(Berkas, CallingConvention = CallingConvention.Cdecl)]
    private static extern int swl2d_stage_set_mulut(int panggung, float buka);
}
