// SilverWolf.Live2D — antarmuka C untuk renderer Live2D (M8).
//
// Sengaja berupa ekspor C polos, bukan komponen C++/WinRT: tidak perlu .idl,
// .winmd, maupun paket NuGet Microsoft.Windows.CppWinRT. C# memanggilnya lewat
// DllImport, dan meneruskan penunjuk ISwapChainPanelNative milik SwapChainPanel.
//
// Semua jalur berkas diterima sebagai UTF-8.
#pragma once

#ifdef SWL2D_EXPORTS
#define SWL2D_API __declspec(dllexport)
#else
#define SWL2D_API __declspec(dllimport)
#endif

// Galat yang dikembalikan fungsi-fungsi di bawah.
#define SWL2D_OK                    0
#define SWL2D_ERR_FRAMEWORK        -1   // CubismFramework gagal start
#define SWL2D_ERR_DEVICE           -2   // D3D11 tidak bisa dibuat
#define SWL2D_ERR_PANEL            -3   // penunjuk panel bukan ISwapChainPanelNative
#define SWL2D_ERR_SWAPCHAIN        -4   // swap chain gagal dibuat/dipasang
#define SWL2D_ERR_MODEL_JSON       -5   // model3.json tidak terbaca
#define SWL2D_ERR_MOC              -6   // .moc3 gagal dimuat
#define SWL2D_ERR_TEXTURE          -7   // tekstur gagal didekode
#define SWL2D_ERR_HANDLE           -8   // handle panggung tidak dikenal
#define SWL2D_ERR_NOT_READY        -9   // dipanggil sebelum swl2d_init

#ifdef __cplusplus
extern "C" {
#endif

/// <summary>
/// Pasang pencatat. Dipanggil sebelum <c>swl2d_init</c> bila ingin melihat
/// keluhan Cubism (misalnya shader .fx tidak ketemu).
/// </summary>
SWL2D_API void swl2d_set_log(void (*sink)(const char* message));

/// <summary>Mulai CubismFramework. Wajib dipanggil sekali sebelum yang lain.</summary>
SWL2D_API int  swl2d_init(void);

/// <summary>Hentikan CubismFramework dan bebaskan semua panggung.</summary>
SWL2D_API void swl2d_shutdown(void);

/// <summary>
/// Buat satu panggung: perangkat D3D11, swap chain yang dipasang ke panel,
/// lalu model dimuat dari <c>modelDir/modelJson</c>.
/// </summary>
/// <param name="panelNative">
/// Penunjuk native SwapChainPanel milik WinUI 3 (IInspectable). Akan
/// di-QueryInterface ke ISwapChainPanelNative WinUI 3
/// {63AAD0B8-7C24-40FF-85A8-640D944CC325}.
///
/// PERHATIAN: jangan tertukar dengan {F92F19D2-3ADE-45A6-A20C-F6F1EA90554B}
/// — itu IID milik Windows.UI.Xaml.Controls.SwapChainPanel (XAML sistem),
/// bukan Microsoft.UI.Xaml.Controls.SwapChainPanel (WinUI 3). Salah IID
/// hanya menghasilkan E_NOINTERFACE dan layar kosong.
/// </param>
SWL2D_API int  swl2d_stage_create(void* panelNative, const char* modelDir, const char* modelJson);

SWL2D_API void swl2d_stage_destroy(int stage);
SWL2D_API int  swl2d_stage_resize(int stage, int width, int height);

/// <summary>Gambar satu bingkai. Mengembalikan SWL2D_OK bila berhasil.</summary>
SWL2D_API int  swl2d_stage_render(int stage);

/// <summary>Ganti ekspresi. Nama mengikuti yang ada di model3.json.</summary>
SWL2D_API int  swl2d_stage_set_expression(int stage, const char* name);

/// <summary>Mainkan gerakan dari grup tertentu. no = -1 berarti acak.</summary>
SWL2D_API int  swl2d_stage_play_motion(int stage, const char* group, int no);

/// <summary>
/// Putar gerakan sebagai loop latar (idle) berprioritas rendah. Dipakai sekali
/// setelah panggung dibuat supaya model tidak diam — gerakan lain tetap bisa
/// menyela. no = -1 berarti gerakan pertama di grup.
/// </summary>
SWL2D_API int  swl2d_stage_play_idle(int stage, const char* group, int no);

/// <summary>Atur arah pandang model, rentang -1..1.</summary>
SWL2D_API int  swl2d_stage_set_look(int stage, float x, float y);

/// <summary>Atur pembingkaian: zoom, geser horizontal, dan jangkar vertikal.</summary>
SWL2D_API int  swl2d_stage_set_view(int stage, float zoom, float offsetX, float anchorY);

#ifdef __cplusplus
}
#endif
