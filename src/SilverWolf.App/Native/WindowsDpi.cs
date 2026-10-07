using System.Runtime.InteropServices;

namespace SilverWolf.App.Native;

/// <summary>
/// Skala tampilan (DPI) jendela, untuk menerjemahkan ukuran dalam DIP ke piksel
/// fisik.
///
/// Kenapa perlu: <c>AppWindow.Resize</c> memakai **piksel fisik**, sedangkan
/// aplikasi lama (Electron) memakai **DIP**. Menulis <c>Resize(1180, 760)</c>
/// apa adanya membuat jendela 20% lebih kecil daripada aslinya di layar 125% —
/// dan panggung Live2D-nya ikut mengecil sehingga detail model terasa lebih
/// kasar. Lihat <c>docs/PROYEK.md</c> §8.3.
/// </summary>
internal static class WindowsDpi
{
    private const uint DpiBawaan = 96;

    [DllImport("user32.dll")]
    private static extern uint GetDpiForWindow(IntPtr hwnd);

    /// <summary>
    /// Faktor skala jendela. 1.0 berarti 100%. Tidak pernah melempar — kalau
    /// panggilan gagal (mis. HWND belum sah), jatuh ke 1.0 supaya jendela tetap
    /// muncul dengan ukuran DIP apa adanya.
    /// </summary>
    public static double Skala(IntPtr hwnd)
    {
        try
        {
            var dpi = GetDpiForWindow(hwnd);
            return dpi >= 48 ? dpi / (double)DpiBawaan : 1.0;
        }
        catch (Exception)
        {
            return 1.0;
        }
    }

    /// <summary>Ubah ukuran DIP menjadi piksel fisik.</summary>
    public static int Piksel(int dip, double skala) =>
        Math.Max(1, (int)Math.Round(dip * skala));
}
