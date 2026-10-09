// SilverWolf.Live2D — implementasi renderer Live2D (M8).
//
// Mengikuti pola resmi Samples/D3D11/Demo (LAppModel): model dibungkus
// CubismUserModel, tekstur diikat ke renderer D3D11, dan parameter diperbarui
// tiap bingkai lewat CubismMotionManager.
//
// Efek hidup (napas, kedip, ikut kursor) memakai kelas Cubism 5
// CubismBreath / CubismEyeBlink / CubismLook. Ketiganya dipanggil LANGSUNG,
// bukan lewat CubismUpdateScheduler: scheduler itu hanya berguna kalau
// updater-nya perlu diurutkan otomatis, sedangkan di sini urutannya sudah
// tetap dan sedikit. Urutan resminya tetap diikuti — lihat CubismUpdateOrder
// di Framework/src/Motion/ICubismUpdater.hpp:
//     kedip(200) -> ekspresi(300) -> pandangan(400) -> napas(500) -> fisika(600)
// Peredaman pandangan memakai CubismTargetPoint, bukan perhitungan sendiri.

#include "Live2DStage.h"

#include <windows.h>
#include <wincodec.h>
#include <winstring.h>
#include <d3d11.h>
#include <d3dcompiler.h>
#include <dxgi1_2.h>
#include <wrl/client.h>

// ── Interop SwapChainPanel khusus WinUI 3 ────────────────────────────────
//
// JEBATAN BESAR — jangan pernah menyertakan <windows.ui.xaml.media.dxinterop.h>
// untuk objek WinUI 3:
//
// XAML sistem (Windows.UI.Xaml) dan WinUI 3 (Microsoft.UI.Xaml) sama-sama
// punya SwapChainPanel, dan keduanya mengekspos antarmuka native bernama
// ISwapChainPanelNative — tetapi IID-nya BERBEDA:
//
//   Windows.UI.Xaml.Controls.SwapChainPanel      F92F19D2-3ADE-45A6-A20C-F6F1EA90554B
//   Microsoft.UI.Xaml.Controls.SwapChainPanel    63AAD0B8-7C24-40FF-85A8-640D944CC325
//   (turunan ISwapChainPanelNative2)            88FD8248-10DA-4810-BB4C-010DD27FAEA9
//
// IID WinUI 3 itu dideklarasikan di microsoft.ui.xaml.media.dxinterop.h yang
// ikut dalam paket NuGet Microsoft.WindowsAppSDK.WinUI — BUKAN di header SDK
// Windows. Memakai IID XAML sistem menghasilkan E_NOINTERFACE (0x80004002)
// meski penunjuknya benar-benar objek SwapChainPanel yang sah (terbukti lewat
// GetRuntimeClassName), sehingga yang terjadi hanyalah layar kosong tanpa
// kesalahan apa pun.
//
// Karena itu antarmukanya dideklarasikan sendiri di sini supaya tidak mungkin
// tertukar. Susunan vtable: IUnknown (3 fungsi) lalu SetSwapChain.
struct ISwapChainPanelNativeWinUI : public IUnknown
{
    virtual HRESULT STDMETHODCALLTYPE SetSwapChain(IDXGISwapChain* swapChain) = 0;
};

static const GUID IID_ISwapChainPanelNativeWinUI =
{
    0x63aad0b8, 0x7c24, 0x40ff, { 0x85, 0xa8, 0x64, 0x0d, 0x94, 0x4c, 0xc3, 0x25 }
};

#include <cstdint>
#include <cstdio>
#include <cstdlib>
#include <cstring>
#include <string>
#include <vector>
#include <map>

#include <CubismFramework.hpp>
#include <CubismDefaultParameterId.hpp>
#include <Model/CubismUserModel.hpp>
#include <CubismModelSettingJson.hpp>
#include <Math/CubismModelMatrix.hpp>
#include <Motion/CubismMotion.hpp>
#include <Motion/CubismMotionManager.hpp>
#include <Motion/CubismExpressionMotion.hpp>
#include <Motion/CubismExpressionMotionManager.hpp>
#include <Utils/CubismString.hpp>
#include <Id/CubismIdManager.hpp>
#include <Rendering/D3D11/CubismRenderer_D3D11.hpp>
#include <Rendering/D3D11/CubismDeviceInfo_D3D11.hpp>
#include <Effect/CubismBreath.hpp>
#include <Effect/CubismEyeBlink.hpp>
#include <Effect/CubismLook.hpp>
#include <Math/CubismTargetPoint.hpp>

using namespace Live2D::Cubism::Framework;

namespace
{
    // ── Pencatat ──────────────────────────────────────────────────────────
    void (*g_log)(const char*) = nullptr;

    /// <summary>
    /// Direktori beradanya DLL ini. Penting karena renderer D3D11 meminta
    /// shader dengan jalur relatif murni — "FrameworkShaders/CubismEffect.fx" —
    /// sedangkan CWD aplikasi WinUI tidak bisa dijamin. Kalau berkas itu tidak
    /// ketemu, layar tetap kosong TANPA pengecualian apa pun.
    /// </summary>
    std::wstring g_direktoriDll;

    /// <summary>
    /// Tulis ke berkas native murni, tanpa menyentuh kode terkelola.
    /// Dipakai saat SWL2D_NOLOG aktif — lihat penjelasan di Catat().
    /// </summary>
    void CatatKeBerkasNative(const std::string& pesan)
    {
        static FILE* berkas = nullptr;
        static bool sudahDicoba = false;

        if (!sudahDicoba)
        {
            sudahDicoba = true;
            const std::wstring jalur = g_direktoriDll + L"\\swl2d-native.log";
            _wfopen_s(&berkas, jalur.c_str(), L"a");
        }

        if (berkas == nullptr) return;

        fwrite(pesan.data(), 1, pesan.size(), berkas);
        fputc('\n', berkas);
        fflush(berkas);
    }

    void Catat(const std::string& pesan)
    {
        // Jalur ke kode terkelola bisa DIMATIKAN lewat SWL2D_NOLOG=1.
        //
        // Alasannya bukan sekadar mengurangi kebisingan. Memanggil balik ke C#
        // dari dalam pemanggilan native Cubism — terutama saat Cubism
        // mengompilasi shader lewat D3DCompile — pernah membuat proses
        // menggantung tanpa galat: panggung tidak pernah selesai dibuat dan
        // tidak ada satu pun baris kesalahan yang tercatat. Untuk memisahkan
        // "panggilan balik terkelola" dari "kompilasi shader itu sendiri",
        // jalankan dengan SWL2D_NOLOG=1: jejaknya lalu ditulis native murni ke
        // swl2d-native.log di sebelah DLL.
        static const bool lewatiTerkelola = getenv("SWL2D_NOLOG") != nullptr;

        if (lewatiTerkelola)
        {
            CatatKeBerkasNative(pesan);
        }
        else if (g_log)
        {
            g_log(pesan.c_str());
        }

        OutputDebugStringA(pesan.c_str());
        OutputDebugStringA("\n");
    }

    /// <summary>
    /// Penangkap pengecualian fatal tingkat pertama.
    ///
    /// Kenapa ini ada: access violation di dalam DLL native membuat .NET
    /// melaporkan "Fatal error. System.AccessViolationException" TANPA nama
    /// fungsi, dan penanda tahap terakhir hanya menunjukkan langkah besarnya
    /// (mis. "sebelum CreateRenderer") — bukan barisnya. Penangkap ini mencatat
    /// kode pengecualian, alamat kesalahan, modul pemiliknya, dan RVA-nya.
    /// RVA itu lalu dicari di berkas .map hasil taut (lihat -MAP: di
    /// build-cli.sh) untuk mendapatkan nama fungsi yang tepat.
    ///
    /// Handler ini hanya MENCATAT lalu meneruskan (EXCEPTION_CONTINUE_SEARCH),
    /// jadi perilaku aplikasi tidak diubah sama sekali.
    /// </summary>
    LONG WINAPI CatatPengecualian(EXCEPTION_POINTERS* info)
    {
        if (info == nullptr || info->ExceptionRecord == nullptr)
        {
            return EXCEPTION_CONTINUE_SEARCH;
        }

        const DWORD kode = info->ExceptionRecord->ExceptionCode;

        // Dua kode ini adalah alur normal, bukan kegagalan:
        //   0xE06D7363 = C++ exception (throw/catch)
        //   0xE0434352 = exception terkelola .NET
        // Keduanya terjadi ribuan kali pada aplikasi yang sehat; kalau ikut
        // dicatat, log akan tenggelam dan justru menyembunyikan penyebabnya.
        if (kode == 0xE06D7363 || kode == 0xE0434352)
        {
            return EXCEPTION_CONTINUE_SEARCH;
        }

        // Selain itu, SEMUA kode dicatat — termasuk kode yang tidak terduga.
        // Ini disengaja: kematian senyap sebelumnya tidak tertangkap karena
        // penyaring yang hanya menerima access violation. Tapi tetap dibatasi
        // satu baris per kode unik supaya tidak membanjiri berkas log.
        {
            static DWORD kodeTercatat[32] = { 0 };
            static int jumlahTercatat = 0;

            for (int i = 0; i < jumlahTercatat; ++i)
            {
                if (kodeTercatat[i] == kode)
                {
                    return EXCEPTION_CONTINUE_SEARCH;
                }
            }

            if (jumlahTercatat < 32)
            {
                kodeTercatat[jumlahTercatat++] = kode;
            }
        }

        void* alamat = info->ExceptionRecord->ExceptionAddress;

        HMODULE modul = nullptr;
        char jalur[MAX_PATH] = { 0 };
        if (GetModuleHandleExA(
                GET_MODULE_HANDLE_EX_FLAG_FROM_ADDRESS | GET_MODULE_HANDLE_EX_FLAG_UNCHANGED_REFCOUNT,
                reinterpret_cast<LPCSTR>(alamat), &modul) && modul != nullptr)
        {
            GetModuleFileNameA(modul, jalur, MAX_PATH);
        }

        const ULONG_PTR rva = modul != nullptr
            ? reinterpret_cast<ULONG_PTR>(alamat) - reinterpret_cast<ULONG_PTR>(modul)
            : 0;

        char teks[640];
        snprintf(teks, sizeof(teks),
                 "[swl2d] PENGEcualian fatal kode=0x%08lX alamat=%p modul=%s rva=0x%llX tid=%lu",
                 static_cast<unsigned long>(kode), alamat,
                 jalur[0] != '\0' ? jalur : "(tidak diketahui)",
                 static_cast<unsigned long long>(rva),
                 static_cast<unsigned long>(GetCurrentThreadId()));
        Catat(teks);

        if (kode == EXCEPTION_ACCESS_VIOLATION &&
            info->ExceptionRecord->NumberParameters >= 2)
        {
            snprintf(teks, sizeof(teks),
                     "[swl2d] AV: operasi=%s alamat target=%p",
                     info->ExceptionRecord->ExceptionInformation[0] == 1 ? "TULIS" : "BACA",
                     reinterpret_cast<void*>(info->ExceptionRecord->ExceptionInformation[1]));
            Catat(teks);
        }

        // Kalau alamat kesalahan berada DI LUAR semua modul, berarti proses
        // melompat ke penunjuk fungsi yang rusak — dan nama fungsinya mustahil
        // didapat dari alamat itu sendiri. Satu-satunya jalan adalah membaca
        // kembali alamat-alamat kembali (return address) dari tumpukan:
        // setiap nilai yang jatuh di dalam DLL ini diterjemahkan ke RVA,
        // lalu dicari di SilverWolf.Live2D.map. Itulah rantai pemanggilnya.
        // Tumpukan hanya dibuang untuk kode yang benar-benar mematikan.
        // Untuk kode lain (mis. breakpoint 0x80000003 saat debugger menempel)
        // isinya tidak informatif dan hanya mengotori log.
        const bool perluTumpukan =
            kode == EXCEPTION_ACCESS_VIOLATION ||
            kode == EXCEPTION_ILLEGAL_INSTRUCTION ||
            kode == EXCEPTION_STACK_OVERFLOW ||
            kode == EXCEPTION_INT_DIVIDE_BY_ZERO ||
            kode == EXCEPTION_PRIV_INSTRUCTION;

        if (perluTumpukan && info->ContextRecord != nullptr)
        {
            const ULONG_PTR sp = static_cast<ULONG_PTR>(info->ContextRecord->Rsp);
            if (sp != 0)
            {
                HMODULE modulIni = nullptr;
                GetModuleHandleExA(
                    GET_MODULE_HANDLE_EX_FLAG_FROM_ADDRESS | GET_MODULE_HANDLE_EX_FLAG_UNCHANGED_REFCOUNT,
                    reinterpret_cast<LPCSTR>(&CatatPengecualian), &modulIni);
                const ULONG_PTR dasarIni = reinterpret_cast<ULONG_PTR>(modulIni);

                // Batas atas kasar: berhenti kalau tumpukan tidak bisa dibaca.
                MEMORY_BASIC_INFORMATION mbi = {};
                if (VirtualQuery(reinterpret_cast<LPCVOID>(sp), &mbi, sizeof(mbi)) != 0 &&
                    mbi.State == MEM_COMMIT &&
                    mbi.Protect != PAGE_NOACCESS &&
                    mbi.Protect != PAGE_GUARD)
                {
                    const ULONG_PTR* tumpukan = reinterpret_cast<const ULONG_PTR*>(sp);
                    for (int i = 0; i < 32; ++i)
                    {
                        const ULONG_PTR nilai = tumpukan[i];
                        if (nilai < 0x10000) continue;

                        HMODULE pemilik = nullptr;
                        if (!GetModuleHandleExA(
                                GET_MODULE_HANDLE_EX_FLAG_FROM_ADDRESS | GET_MODULE_HANDLE_EX_FLAG_UNCHANGED_REFCOUNT,
                                reinterpret_cast<LPCSTR>(nilai), &pemilik))
                        {
                            continue;
                        }

                        char nama[MAX_PATH] = { 0 };
                        GetModuleFileNameA(pemilik, nama, MAX_PATH);
                        const char* pendek = strrchr(nama, '\\');
                        pendek = pendek != nullptr ? pendek + 1 : nama;

                        snprintf(teks, sizeof(teks),
                                 "[swl2d]   stack[%d] %s+0x%llX",
                                 i, pendek,
                                 static_cast<unsigned long long>(nilai - reinterpret_cast<ULONG_PTR>(pemilik)));
                        Catat(teks);

                        if (nilai - dasarIni < 0x10000000ULL && strstr(pendek, "SilverWolf.Live2D") != nullptr)
                        {
                            snprintf(teks, sizeof(teks),
                                     "[swl2d]   ^^ kandidat di DLL ini, rva=0x%llX",
                                     static_cast<unsigned long long>(nilai - dasarIni));
                            Catat(teks);
                        }
                    }
                }
            }
        }

        return EXCEPTION_CONTINUE_SEARCH;
    }

    /// <summary>
    /// Pencatat khusus HRESULT. Dipakai untuk menjejaki tiap langkah COM/D3D
    /// selama penyidikan: baris terakhir yang muncul di crash.log persis
    /// menunjuk langkah yang menjatuhkan proses.
    /// </summary>
    void CatatHr(const char* langkah, long hasil)
    {
        char teks[320];
        snprintf(teks, sizeof(teks), "[swl2d] %s -> 0x%08lX", langkah,
                 static_cast<unsigned long>(hasil));
        Catat(teks);
    }

    // ── Alokator ──────────────────────────────────────────────────────────
    class Alokator : public ICubismAllocator
    {
    public:
        void* Allocate(const csmSizeType size) override { return malloc(size); }
        void  Deallocate(void* memory) override { free(memory); }

        void* AllocateAligned(const csmSizeType size, const csmUint32 alignment) override
        {
            return _aligned_malloc(size, alignment);
        }

        void DeallocateAligned(void* alignedMemory) override { _aligned_free(alignedMemory); }
    };

    Alokator g_alokator;

    // ── Opsi CubismFramework ──────────────────────────────────────────────
    //
    // JEBAKAN YANG PERNAH MENGHABISKAN SATU PUTARAN PENUH — jangan diubah
    // menjadi variabel lokal:
    //
    // CubismFramework::StartUp() MENYIMPAN PENUNJUK, bukan salinan:
    //
    //     csmBool CubismFramework::StartUp(ICubismAllocator*, const Option* option)
    //     {
    //         ...
    //         s_option = option;      // CubismFramework.cpp:61
    //     }
    //
    // `s_option` adalah penunjuk statis. Kalau objek Option ditaruh sebagai
    // variabel LOKAL di swl2d_init, kerangka stack-nya hilang begitu fungsi
    // itu kembali. Sesudah itu CubismFramework::GetLoadFileFunction() membaca
    // memori mati dan mengembalikan penunjuk fungsi sampah.
    //
    // Akibatnya sangat menyesatkan: pemuatan model dan tekstur TETAP berhasil
    // (kode kita memanggil MuatBerkas langsung, bukan lewat kerangka), lalu
    // proses mati mendadak hanya ketika Cubism SENDIRI memuat berkas —
    // yaitu saat mengompilasi shader di CubismShader_D3D11::GenerateShaders.
    // Jejak tumpukannya menunjuk ke sana, tetapi alamat kesalahannya di luar
    // semua modul, jadi seolah-olah Cubism yang rusak.
    //
    // Contoh resmi aman karena Option-nya anggota kelas LAppDelegate
    // (singleton yang hidup sepanjang aplikasi) — LAppDelegate.cpp:348.
    CubismFramework::Option g_opsi;

    // ── Pemuat berkas Cubism ──────────────────────────────────────────────
    // Jalur dari Cubism ber-UTF-8; Windows butuh UTF-16 agar folder
    // beraksara Indonesia (mis. "tekstur") terbuka.
    std::wstring KeUtf16(const std::string& utf8)
    {
        if (utf8.empty()) return std::wstring();
        const int butuh = MultiByteToWideChar(CP_UTF8, 0, utf8.c_str(), -1, nullptr, 0);
        std::wstring hasil(static_cast<size_t>(butuh), L'\0');
        MultiByteToWideChar(CP_UTF8, 0, utf8.c_str(), -1, &hasil[0], butuh);
        while (!hasil.empty() && hasil.back() == L'\0') hasil.pop_back();
        return hasil;
    }

    csmByte* MuatBerkas(const std::string jalur, csmSizeInt* ukuran)
    {
        // Jejak masuk: Cubism memanggil fungsi ini lewat POINTER FUNGSI saat
        // mengompilasi shader. Kalau baris ini muncul tapi proses lalu mati,
        // berarti crash terjadi di dalam fungsi ini; kalau tidak muncul sama
        // sekali padahal "sebelum CreateRenderer" sudah tercatat, berarti
        // pointer fungsinya sendiri yang rusak.
        Catat("[swl2d] MuatBerkas dipanggil: " + jalur);

        FILE* berkas = nullptr;

        // 1) apa adanya — jalur absolut atau relatif terhadap CWD
        if (_wfopen_s(&berkas, KeUtf16(jalur).c_str(), L"rb") != 0)
        {
            berkas = nullptr;
        }

        // 2) relatif terhadap direktori DLL. Inilah yang menyelamatkan
        //    pemuatan shader: Cubism memintanya tanpa awalan direktori.
        if (berkas == nullptr && !g_direktoriDll.empty())
        {
            const std::wstring calon = g_direktoriDll + L"\\" + KeUtf16(jalur);
            if (_wfopen_s(&berkas, calon.c_str(), L"rb") != 0)
            {
                berkas = nullptr;
            }
        }

        if (berkas == nullptr)
        {
            Catat("[swl2d] berkas tidak terbuka: " + jalur);
            *ukuran = 0;
            return nullptr;
        }

        fseek(berkas, 0, SEEK_END);
        const long panjang = ftell(berkas);
        fseek(berkas, 0, SEEK_SET);

        if (panjang <= 0)
        {
            fclose(berkas);
            *ukuran = 0;
            return nullptr;
        }

        auto* penyangga = static_cast<csmByte*>(malloc(static_cast<size_t>(panjang)));
        const size_t terbaca = fread(penyangga, 1, static_cast<size_t>(panjang), berkas);
        fclose(berkas);

        *ukuran = static_cast<csmSizeInt>(terbaca);
        return penyangga;
    }

    void BebasBerkas(csmByte* penyangga) { free(penyangga); }

    // ── Dekode PNG lewat WIC → ShaderResourceView ─────────────────────────
    IWICImagingFactory* g_wic = nullptr;

    ID3D11ShaderResourceView* BuatTekstur(ID3D11Device* perangkat, const std::string& jalur, int& lebar, int& tinggi)
    {
        Microsoft::WRL::ComPtr<IWICBitmapDecoder> dekoder;
        if (FAILED(g_wic->CreateDecoderFromFilename(
                KeUtf16(jalur).c_str(), nullptr, GENERIC_READ,
                WICDecodeMetadataCacheOnDemand, &dekoder)))
        {
            Catat("[swl2d] dekoder WIC gagal: " + jalur);
            return nullptr;
        }

        Microsoft::WRL::ComPtr<IWICBitmapFrameDecode> bingkai;
        if (FAILED(dekoder->GetFrame(0, &bingkai))) return nullptr;

        Microsoft::WRL::ComPtr<IWICFormatConverter> ubah;
        if (FAILED(g_wic->CreateFormatConverter(&ubah))) return nullptr;
        if (FAILED(ubah->Initialize(bingkai.Get(), GUID_WICPixelFormat32bppRGBA,
                                    WICBitmapDitherTypeNone, nullptr, 0.0,
                                    WICBitmapPaletteTypeCustom))) return nullptr;

        UINT w = 0, h = 0;
        ubah->GetSize(&w, &h);
        lebar = static_cast<int>(w);
        tinggi = static_cast<int>(h);

        std::vector<BYTE> piksel(static_cast<size_t>(w) * h * 4);
        if (FAILED(ubah->CopyPixels(nullptr, w * 4, static_cast<UINT>(piksel.size()), piksel.data())))
            return nullptr;

        // Rantai mip WAJIB ada.
        //
        // Tekstur model ini 4096x4096, sedangkan di layar seluruh karakter hanya
        // menempati sekitar 580 piksel tinggi — jadi teksturnya DIPERKECIL
        // sekitar 6-7 kali. Tanpa mip, sampler hanya punya level 0, sehingga
        // detail halus (tulisan di visor, garis tipis) pecah dan berkelip —
        // itulah yang terlihat "seperti piksel". Dengan rantai mip, MIN filter
        // bisa memilih level yang sesuai dan hasilnya halus.
        //
        // Tekstur dibuat TANPA data awal (pInitialData = nullptr) karena dengan
        // MipLevels = 0 D3D menuntut data untuk SEMUA sub-sumber; lebih aman
        // isi level 0 lewat UpdateSubresource lalu panggil GenerateMips.
        D3D11_TEXTURE2D_DESC desc = {};
        desc.Width = w;
        desc.Height = h;
        desc.MipLevels = 0;                       // 0 = rantai penuh
        desc.ArraySize = 1;
        desc.Format = DXGI_FORMAT_R8G8B8A8_UNORM;
        desc.SampleDesc.Count = 1;
        desc.Usage = D3D11_USAGE_DEFAULT;
        desc.BindFlags = D3D11_BIND_SHADER_RESOURCE | D3D11_BIND_RENDER_TARGET;
        desc.MiscFlags = D3D11_RESOURCE_MISC_GENERATE_MIPS;

        Microsoft::WRL::ComPtr<ID3D11Texture2D> tekstur;
        if (FAILED(perangkat->CreateTexture2D(&desc, nullptr, &tekstur))) return nullptr;

        Microsoft::WRL::ComPtr<ID3D11DeviceContext> konteks;
        perangkat->GetImmediateContext(&konteks);
        if (konteks == nullptr) return nullptr;

        konteks->UpdateSubresource(tekstur.Get(), 0, nullptr, piksel.data(), w * 4, 0);

        ID3D11ShaderResourceView* tampilan = nullptr;
        if (FAILED(perangkat->CreateShaderResourceView(tekstur.Get(), nullptr, &tampilan)))
            return nullptr;

        konteks->GenerateMips(tampilan);

        return tampilan;
    }

    // ── Model panggung ────────────────────────────────────────────────────
    class ModelPanggung : public CubismUserModel
    {
    public:
        ModelPanggung(const std::string& dir, ID3D11Device* perangkat, int lebarTarget, int tinggiTarget)
            : _dir(dir), _perangkat(perangkat),
              _lebarTarget(lebarTarget > 0 ? static_cast<csmUint32>(lebarTarget) : 1u),
              _tinggiTarget(tinggiTarget > 0 ? static_cast<csmUint32>(tinggiTarget) : 1u)
        {
        }

        bool Muat(const std::string& namaJson)
        {
            Catat("[swl2d] Muat: masuk, dir=" + _dir + " json=" + namaJson);

            csmSizeInt ukuranJson = 0;
            csmByte* isiJson = MuatBerkas(Gabung(_dir, namaJson), &ukuranJson);
            if (isiJson == nullptr)
            {
                Catat("[swl2d] model3.json tidak terbaca");
                return false;
            }
            Catat("[swl2d] Muat: model3.json terbaca, " + std::to_string(ukuranJson) + " bita");

            _setelan = new CubismModelSettingJson(isiJson, ukuranJson);
            BebasBerkas(isiJson);
            Catat("[swl2d] Muat: CubismModelSettingJson dibuat, berkas model = "
                  + std::string(_setelan->GetModelFileName() ? _setelan->GetModelFileName() : "(kosong)")
                  + ", tekstur=" + std::to_string(_setelan->GetTextureCount()));

            // moc3
            csmSizeInt ukuranMoc = 0;
            csmByte* isiMoc = MuatBerkas(Gabung(_dir, _setelan->GetModelFileName()), &ukuranMoc);
            if (isiMoc == nullptr)
            {
                Catat("[swl2d] .moc3 tidak terbaca");
                return false;
            }
            Catat("[swl2d] Muat: .moc3 terbaca, " + std::to_string(ukuranMoc) + " bita");
            LoadModel(isiMoc, ukuranMoc);
            BebasBerkas(isiMoc);
            Catat("[swl2d] Muat: LoadModel selesai, drawable="
                  + std::to_string(GetModel() ? GetModel()->GetDrawableCount() : -1));

            // physics
            if (const csmChar* namaFisika = _setelan->GetPhysicsFileName())
            {
                csmSizeInt ukuran = 0;
                csmByte* isi = MuatBerkas(Gabung(_dir, namaFisika), &ukuran);
                if (isi != nullptr)
                {
                    _physics = CubismPhysics::Create(isi, ukuran);
                    BebasBerkas(isi);
                }
            }

            // tekstur
            for (csmInt32 i = 0; i < _setelan->GetTextureCount(); i++)
            {
                const csmString nama = _setelan->GetTextureFileName(i);
                Catat("[swl2d] Muat: muat tekstur[" + std::to_string(i) + "] = "
                      + std::string(nama.GetRawString() ? nama.GetRawString() : "(kosong)"));
                int lebar = 0, tinggi = 0;
                auto* tampilan = BuatTekstur(_perangkat, Gabung(_dir, nama.GetRawString()), lebar, tinggi);
                if (tampilan == nullptr)
                {
                    Catat("[swl2d] tekstur gagal: " + std::string(nama.GetRawString()));
                    return false;
                }
                Catat("[swl2d] Muat: tekstur[" + std::to_string(i) + "] ok "
                      + std::to_string(lebar) + "x" + std::to_string(tinggi));
                _tekstur.push_back(tampilan);
            }

            Catat("[swl2d] Muat: sebelum CreateRenderer");
            CreateRenderer(_lebarTarget, _tinggiTarget);
            Catat("[swl2d] Muat: CreateRenderer lewat");
            auto* perender = GetRenderer<Rendering::CubismRenderer_D3D11>();
            if (perender == nullptr)
            {
                Catat("[swl2d] renderer D3D11 gagal dibuat");
                return false;
            }
            // JANGAN panggil perender->Initialize() di sini. CubismUserModel::
            // CreateRenderer sudah memanggilnya sendiri:
            //     _renderer = Rendering::CubismRenderer::Create(width, height);
            //     _renderer->Initialize(_model, maskBufferCount);   // ← internal
            // (Framework/src/Model/CubismUserModel.cpp:292-294)
            // Memanggilnya lagi menghapus lalu membuat ulang seluruh render
            // target offscreen, dan versi 1-argumen memaksa maskBufferCount=1.
            Catat("[swl2d] Muat: renderer->Initialize dilewati (sudah dilakukan CreateRenderer)");
            for (size_t i = 0; i < _tekstur.size(); i++)
            {
                perender->BindTexture(static_cast<csmUint32>(i), _tekstur[i]);
            }
            Catat("[swl2d] Muat: BindTexture lewat (" + std::to_string(_tekstur.size()) + " tekstur)");

            // ekspresi
            for (csmInt32 i = 0; i < _setelan->GetExpressionCount(); i++)
            {
                const csmChar* nama = _setelan->GetExpressionName(i);
                const csmString berkas = _setelan->GetExpressionFileName(i);
                if (nama == nullptr || berkas.GetRawString() == nullptr) continue;

                csmSizeInt ukuran = 0;
                csmByte* isi = MuatBerkas(Gabung(_dir, berkas.GetRawString()), &ukuran);
                if (isi == nullptr) continue;

                auto* gerak = static_cast<CubismExpressionMotion*>(
                    CubismExpressionMotion::Create(isi, ukuran));
                BebasBerkas(isi);
                _ekspresi[std::string(nama)] = static_cast<ACubismMotion*>(gerak);
            }

            // gerakan
            for (csmInt32 g = 0; g < _setelan->GetMotionGroupCount(); g++)
            {
                const csmChar* grup = _setelan->GetMotionGroupName(g);
                if (grup == nullptr) continue;
                for (csmInt32 i = 0; i < _setelan->GetMotionCount(grup); i++)
                {
                    const csmString berkas = _setelan->GetMotionFileName(grup, i);
                    if (berkas.GetRawString() == nullptr) continue;

                    csmSizeInt ukuran = 0;
                    csmByte* isi = MuatBerkas(Gabung(_dir, berkas.GetRawString()), &ukuran);
                    if (isi == nullptr) continue;

                    auto* gerak = static_cast<CubismMotion*>(
                        CubismMotion::Create(isi, ukuran, nullptr, nullptr));
                    BebasBerkas(isi);
                    _gerakan[std::string(grup)].push_back(static_cast<ACubismMotion*>(gerak));
                }
            }

            SiapkanEfek();

            Catat("[swl2d] Muat: selesai — ekspresi=" + std::to_string(_ekspresi.size())
                  + " grupGerakan=" + std::to_string(_gerakan.size()));
            return true;
        }

        /// <summary>
        /// Siapkan efek hidup: kedip, napas, dan ikut kursor.
        ///
        /// Dipanggil sekali setelah model dan setelannya siap, karena
        /// CubismEyeBlink membaca daftar parameter dari model3.json
        /// (Groups -> EyeBlink) dan CubismLook butuh IdManager yang baru
        /// terisi setelah model dimuat.
        ///
        /// Angka pada BreathParameterData/LookParameterData diambil apa adanya
        /// dari contoh resmi LAppModel.cpp:211-276. Untuk pandangan, faktornya
        /// (30 untuk sudut X, 30 untuk Y, 10 untuk badan) sengaja sama dengan
        /// yang dulu ditulis tangan di AturPandang supaya perilakunya setara.
        /// </summary>
        void SiapkanEfek()
        {
            const auto id = [](const csmChar* nama)
            {
                return CubismFramework::GetIdManager()->GetId(nama);
            };

            // ── Kedip ─────────────────────────────────────────────────────
            // Hanya dipasang kalau model3.json memang mendeklarasikan grup
            // EyeBlink; kalau tidak, Create() tidak punya parameter untuk
            // ditulis dan kedipnya tidak akan terlihat.
            const csmInt32 jumlahKedip = _setelan->GetEyeBlinkParameterCount();
            if (jumlahKedip > 0)
            {
                _kedip = CubismEyeBlink::Create(_setelan);
                Catat("[swl2d] efek: kedip aktif, " + std::to_string(jumlahKedip) + " parameter");
            }
            else
            {
                Catat("[swl2d] efek: kedip dilewati (model3.json tidak punya grup EyeBlink)");
            }

            // ── Napas ─────────────────────────────────────────────────────
            _napas = CubismBreath::Create();
            {
                csmVector<CubismBreath::BreathParameterData> daftar;
                daftar.PushBack(CubismBreath::BreathParameterData(id(DefaultParameterId::ParamAngleX), 0.0f, 15.0f, 6.5345f, 0.5f));
                daftar.PushBack(CubismBreath::BreathParameterData(id(DefaultParameterId::ParamAngleY), 0.0f, 8.0f, 3.5345f, 0.5f));
                daftar.PushBack(CubismBreath::BreathParameterData(id(DefaultParameterId::ParamAngleZ), 0.0f, 10.0f, 5.5345f, 0.5f));
                daftar.PushBack(CubismBreath::BreathParameterData(id(DefaultParameterId::ParamBodyAngleX), 0.0f, 4.0f, 15.5345f, 0.5f));
                daftar.PushBack(CubismBreath::BreathParameterData(id(DefaultParameterId::ParamBreath), 0.5f, 0.5f, 3.2345f, 0.5f));
                _napas->SetParameters(daftar);
            }
            Catat("[swl2d] efek: napas aktif");

            // ── Pandangan ─────────────────────────────────────────────────
            // CubismLookUpdater sengaja TIDAK dipakai: ia butuh CubismTargetPoint
            // yang diurus lewat CubismUpdateScheduler. CubismLook::UpdateParameters
            // bisa dipanggil langsung, dan CubismTargetPoint kita urus sendiri
            // di Perbarui() — sama hasilnya, tanpa lapisan scheduler.
            _pandangan = CubismLook::Create();
            {
                csmVector<CubismLook::LookParameterData> daftar;
                daftar.PushBack(CubismLook::LookParameterData(id(DefaultParameterId::ParamAngleX), 30.0f));
                daftar.PushBack(CubismLook::LookParameterData(id(DefaultParameterId::ParamAngleY), 0.0f, 30.0f));
                daftar.PushBack(CubismLook::LookParameterData(id(DefaultParameterId::ParamAngleZ), 0.0f, 0.0f, -30.0f));
                daftar.PushBack(CubismLook::LookParameterData(id(DefaultParameterId::ParamBodyAngleX), 10.0f));
                daftar.PushBack(CubismLook::LookParameterData(id(DefaultParameterId::ParamEyeBallX), 1.0f));
                daftar.PushBack(CubismLook::LookParameterData(id(DefaultParameterId::ParamEyeBallY), 0.0f, 1.0f));
                _pandangan->SetParameters(daftar);
            }
            Catat("[swl2d] efek: pandangan aktif (dengan peredaman CubismTargetPoint)");

            // ── LipSync ───────────────────────────────────────────────────
            // CubismLipSyncUpdater resmi sengaja TIDAK dipakai: ia menuntut
            // CubismUpdateScheduler (sama seperti CubismLookUpdater). Yang
            // diambil hanya intinya — AddParameterValue dengan bobot 0,8 —
            // persis CubismLipSyncUpdater.cpp:42. Nilainya datang dari C#
            // berupa RMS audio nyata, bukan perkiraan.
            _indeksMulut = _model->GetParameterIndex(id(DefaultParameterId::ParamMouthOpenY));
            if (_indeksMulut >= 0)
            {
                Catat("[swl2d] efek: lipsync aktif (ParamMouthOpenY)");
            }
            else
            {
                Catat("[swl2d] efek: lipsync dilewati (model tidak punya ParamMouthOpenY)");
            }
        }

        virtual ~ModelPanggung()
        {
            for (auto* t : _tekstur) if (t) t->Release();
            _tekstur.clear();
            for (auto& kv : _ekspresi) ACubismMotion::Delete(kv.second);
            for (auto& kv : _gerakan) for (auto* g : kv.second) ACubismMotion::Delete(g);
            CubismEyeBlink::Delete(_kedip);
            CubismBreath::Delete(_napas);
            CubismLook::Delete(_pandangan);
            CubismPhysics::Delete(_physics);
            delete _setelan;
            DeleteRenderer();
        }

        void Perbarui(float dt)
        {
            if (_model == nullptr) return;

            _model->LoadParameters();

            if (_ujiDiam)
            {
                // Mode uji: napas, kedip, dan pandangan dimatikan supaya pose
                // tidak berubah sendiri. Loop idle juga TIDAK dihidupkan ulang
                // (AktifkanUjiDiam sudah menghentikannya) — tetapi gerakan yang
                // sedang diuji tetap harus dimajukan, kalau tidak ia beku di
                // bingkai pertama dan uji gerakan jadi tidak bermakna.
                if (!_penggerak.IsFinished()) _penggerak.UpdateMotion(_model, dt);

                _ekspresiManager.UpdateMotion(_model, dt);
                if (_physics != nullptr) _physics->Evaluate(_model, dt);
                _model->Update();
                return;
            }

            if (_penggerak.IsFinished())
            {
                // Paritas dengan aplikasi web (App.jsx:94): gerakan diam adalah
                // grup "isyarat" indeks 2 = siklus, dan berkas motion-nya
                // menyatakan Loop=true. Nama grup diambil dari model3.json,
                // BUKAN dari nama foldernya ("gerakan").
                MainkanGerakan("isyarat", 2);
            }
            else
            {
                _penggerak.UpdateMotion(_model, dt);
            }

            // SaveParameters SEBELUM efek hidup, bukan sesudah. Alasannya sama
            // dengan contoh resmi LAppModel::Update() (LAppModel.cpp:384-400):
            // yang disimpan adalah hasil gerakan, sedangkan napas/kedip/
            // pandangan menumpang di atasnya dan TIDAK boleh ikut tersimpan —
            // kalau ikut, nilainya menumpuk terus setiap bingkai.
            _model->SaveParameters();

            // Urutan mengikuti CubismUpdateOrder di ICubismUpdater.hpp:
            //   kedip(200) -> ekspresi(300) -> pandangan(400) -> napas(500) -> fisika(600)
            if (_kedip != nullptr) _kedip->UpdateParameters(_model, dt);

            _ekspresiManager.UpdateMotion(_model, dt);

            if (_pandangan != nullptr)
            {
                // Peredaman ada di sini: titik pandang bergerak menuju sasaran
                // dengan kelembaman, jadi kepala tidak melompat saat kursor
                // berpindah. Sasaran diisi AturPandang().
                _titikPandang.Update(dt);
                _pandangan->UpdateParameters(_model, _titikPandang.GetX(), _titikPandang.GetY());
            }

            if (_napas != nullptr) _napas->UpdateParameters(_model, dt);

            if (_physics != nullptr) _physics->Evaluate(_model, dt);

            // ── LipSync (urutan 700 = SESUDAH fisika) ─────────────────────
            // CubismUpdateOrder_LipSync = 700 (ICubismUpdater.hpp:23), jadi
            // memang paling akhir. Nilai dari C# datang ~60 kali per detik;
            // di sini dibatasi lajunya supaya mulut tidak bergetar mengikuti
            // derau RMS — naik cepat, turun lebih lambat, seperti mulut orang.
            if (_indeksMulut >= 0)
            {
                const csmFloat32 selisih = _mulutTarget - _mulutNilai;
                const csmFloat32 langkah = (selisih > 0.0f ? KecepatanBuka : KecepatanTutup) * dt;
                if (selisih > langkah) _mulutNilai += langkah;
                else if (selisih < -langkah) _mulutNilai -= langkah;
                else _mulutNilai = _mulutTarget;

                // Bobot 0,8 sama dengan CubismLipSyncUpdater resmi.
                _model->AddParameterValue(_indeksMulut, _mulutNilai, 0.8f);
            }

            _model->Update();
        }

        // Cubism 5: SetMvpMatrix menerima CubismMatrix44* yang TIDAK konstan,
        // jadi proyeksi diambil secara nilai (salinan) agar bisa dialamatkan.
        void Gambar(ID3D11DeviceContext* konteks, CubismMatrix44 proyeksi)
        {
            auto* perender = GetRenderer<Rendering::CubismRenderer_D3D11>();
            if (perender == nullptr) return;

            // StartFrame/EndFrame WAJIB mengapit DrawModel. Contoh resmi selalu
            // melakukannya:
            //   Samples/D3D11/Demo/.../LAppLive2DManager.cpp:252
            //   Samples/D3D11/Demo/.../LAppLive2DManager.cpp:283
            // Tanpa StartFrame, CubismRenderer_D3D11::GetRenderState()->
            // StartFrame() (CubismRenderer_D3D11.cpp:539-545) tidak pernah
            // dipanggil sehingga state D3D tidak disiapkan sebelum menggambar —
            // akibatnya layar kosong, dan pada sebagian driver berujung crash.
            perender->StartFrame(konteks);
            perender->SetMvpMatrix(&proyeksi);
            perender->DrawModel();
            perender->EndFrame();
        }

        CubismModel* AmbilModel() { return GetModel(); }

        // ── Alat uji: apakah semua ekspresi & gerakan benar-benar berfungsi ──
        //
        // Aktif hanya bila SWL2D_UJI=ekspresi atau SWL2D_UJI=gerakan. Saat
        // aktif, pose dibekukan (lihat _ujiDiam di Perbarui) sehingga tiap
        // ekspresi/gerakan bisa dinilai dari sidik pikselnya sendiri.

        void AktifkanUjiDiam(bool nyala)
        {
            _ujiDiam = nyala;
            if (nyala) _penggerak.StopAllMotions();
        }

        int JumlahEkspresi() const { return static_cast<int>(_ekspresi.size()); }

        int JumlahGerakan() const
        {
            int jumlah = 0;
            for (const auto& kv : _gerakan) jumlah += static_cast<int>(kv.second.size());
            return jumlah;
        }

        /// <summary>Terapkan ekspresi ke-i (urut nama). Mengembalikan namanya.</summary>
        std::string TerapkanEkspresiKe(int i)
        {
            int k = 0;
            for (const auto& kv : _ekspresi)
            {
                if (k == i)
                {
                    AturEkspresi(kv.first);
                    return kv.first;
                }
                ++k;
            }
            return "(tidak ada)";
        }

        /// <summary>
        /// Mainkan gerakan ke-i (semua grup digabung, urut nama grup).
        /// Prioritas 3 = memaksa, supaya loop idle tidak menimpanya.
        /// </summary>
        std::string TerapkanGerakanKe(int i)
        {
            int k = 0;
            for (const auto& kv : _gerakan)
            {
                for (size_t j = 0; j < kv.second.size(); ++j)
                {
                    if (k == i)
                    {
                        _penggerak.StartMotionPriority(kv.second[j], false, 3);
                        return kv.first + "[" + std::to_string(j) + "]";
                    }
                    ++k;
                }
            }
            return "(tidak ada)";
        }

        /// <summary>
        /// Sesuaikan ukuran render target dengan ukuran panel yang sebenarnya.
        ///
        /// WAJIB dipanggil setiap kali ukuran panel berubah — termasuk sekali
        /// tepat setelah panggung dibuat. Cubism memakai nilai ini untuk
        /// MENYETEL VIEWPORT:
        ///   CubismRenderer_D3D11::PreDraw()
        ///     -> SetDefaultRenderState(_modelRenderTargetWidth, _modelRenderTargetHeight)
        ///       -> GetRenderState()->SetViewport(_context, 0, 0, width, height, 0, 1)
        ///
        /// Kalau nilainya 1x1, seluruh model digambar ke satu piksel di sudut
        /// kiri atas. Present() tetap mengembalikan S_OK dan layar tampak
        /// kosong — tanpa satu pun galat atau peringatan. Itu sebabnya panggung
        /// dibuat dengan ukuran 1x1 lalu segera disesuaikan dari sisi C#.
        ///
        /// CreateRenderer() dipanggil ULANG (bukan sekadar mengubah field),
        /// karena render target offscreen/mask dibuat di dalam Initialize()
        /// memakai ukuran saat itu. Polanya sama dengan
        /// LAppModel::ReloadRenderer() di contoh resmi:
        /// DeleteRenderer -> CreateRenderer(w, h) -> SetupTextures.
        /// </summary>
        void AturUkuranTarget(int lebar, int tinggi)
        {
            if (lebar <= 0 || tinggi <= 0) return;

            const csmUint32 baruLebar = static_cast<csmUint32>(lebar);
            const csmUint32 baruTinggi = static_cast<csmUint32>(tinggi);
            if (baruLebar == _lebarTarget && baruTinggi == _tinggiTarget) return;

            _lebarTarget = baruLebar;
            _tinggiTarget = baruTinggi;

            CreateRenderer(_lebarTarget, _tinggiTarget);

            auto* perender = GetRenderer<Rendering::CubismRenderer_D3D11>();
            if (perender == nullptr) return;

            // Renderer yang baru dibuat tidak mewarisi tekstur yang sudah diikat.
            for (size_t i = 0; i < _tekstur.size(); i++)
            {
                perender->BindTexture(static_cast<csmUint32>(i), _tekstur[i]);
            }
        }

        bool AturEkspresi(const std::string& nama)
        {
            auto cari = _ekspresi.find(nama);
            if (cari == _ekspresi.end()) return false;

            // CubismExpressionMotionManager adalah turunan
            // CubismMotionQueueManager, BUKAN CubismMotionManager — jadi tidak
            // punya StartMotionPriority. Yang tersedia hanya StartMotion.
            _ekspresiManager.StartMotion(cari->second, false);
            return true;
        }

        bool MainkanGerakan(const std::string& grup, int no)
        {
            auto cari = _gerakan.find(grup);
            if (cari == _gerakan.end() || cari->second.empty()) return false;

            const int indeks = (no < 0 || no >= static_cast<int>(cari->second.size()))
                ? rand() % static_cast<int>(cari->second.size())
                : no;

            _penggerak.StartMotionPriority(cari->second[indeks], false, 2);
            return true;
        }

        /// <summary>
        /// Putar satu gerakan sebagai loop latar (idle).
        ///
        /// Bedanya dengan MainkanGerakan: gerakannya di-set berulang
        /// (SetLoop) dan prioritasnya paling rendah (1). Prioritas rendah itu
        /// penting — dengan begitu gerakan lain (prioritas 2-3) bisa menyela
        /// tanpa perlu menghentikan loop ini lebih dulu. Persis perilaku
        /// aplikasi web yang memutar "siklus" terus-menerus selama menunggu.
        /// </summary>
        bool MainkanGerakanBerulang(const std::string& grup, int no)
        {
            auto cari = _gerakan.find(grup);
            if (cari == _gerakan.end() || cari->second.empty()) return false;

            const int indeks = (no < 0 || no >= static_cast<int>(cari->second.size()))
                ? 0
                : no;

            ACubismMotion* gerakan = cari->second[indeks];
            gerakan->SetLoop(true);
            _penggerak.StartMotionPriority(gerakan, false, 1);
            return true;
        }

        /// <summary>
        /// Atur arah pandang model, x/y dalam rentang -1..1.
        ///
        /// Fungsi ini TIDAK lagi menulis parameter sendiri seperti sebelumnya.
        /// Yang disimpan hanya sasarannya; penulisan sebenarnya terjadi di
        /// Perbarui() lewat CubismLook, sesudah disaring CubismTargetPoint.
        /// Itu yang membuat kepala bergerak halus, bukan melompat-lompat.
        ///
        /// Faktor lama (30 untuk sudut X/Y, 10 untuk badan) tidak hilang —
        /// pindah ke LookParameterData di SiapkanEfek().
        /// </summary>
        void AturPandang(float x, float y)
        {
            _titikPandang.Set(x, y);
        }

        /// <summary>
        /// Terima SASARAN bukaan mulut 0..1 dari LipSync. Nilai akhirnya
        /// dihaluskan di Perbarui(); lihat komentar di sana.
        /// </summary>
        void AturMulut(csmFloat32 buka)
        {
            if (buka < 0.0f) buka = 0.0f;
            if (buka > 1.0f) buka = 1.0f;
            _mulutTarget = buka;
        }

    private:
        static std::string Gabung(const std::string& dir, const std::string& nama)
        {
            if (dir.empty()) return nama;
            const char ujung = dir.back();
            if (ujung == '/' || ujung == '\\') return dir + nama;
            return dir + "\\" + nama;
        }

        std::string _dir;
        ID3D11Device* _perangkat = nullptr;
        csmUint32 _lebarTarget = 1;
        csmUint32 _tinggiTarget = 1;
        CubismModelSettingJson* _setelan = nullptr;
        CubismPhysics* _physics = nullptr;

        // Efek hidup. Ketiganya nullptr bila belum disiapkan / tidak tersedia.
        CubismEyeBlink* _kedip = nullptr;
        CubismBreath* _napas = nullptr;
        CubismLook* _pandangan = nullptr;

        // Peredaman arah pandang: sasaran dari AturPandang(), nilai halusnya
        // dibaca di Perbarui().
        CubismTargetPoint _titikPandang;

        // LipSync. Indeks -1 berarti model tidak punya ParamMouthOpenY dan
        // seluruh jalur ini dilewati.
        //
        // Kecepatan dibatasinya asimetris dengan sengaja: mulut membuka lebih
        // cepat daripada menutup, sehingga terlihat seperti sedang berbicara
        // dan bukan bergetar. Satuannya "per detik", bukan "per bingkai",
        // supaya perilakunya sama pada 30 maupun 60 fps.
        static constexpr csmFloat32 KecepatanBuka = 14.0f;
        static constexpr csmFloat32 KecepatanTutup = 8.0f;

        csmInt32 _indeksMulut = -1;
        csmFloat32 _mulutTarget = 0.0f;
        csmFloat32 _mulutNilai = 0.0f;

        // Mode uji (SWL2D_UJI): bekukan pose supaya sidik piksel tiap
        // ekspresi/gerakan bisa dibandingkan.
        bool _ujiDiam = false;

        CubismMotionManager _penggerak;
        CubismExpressionMotionManager _ekspresiManager;
        std::vector<ID3D11ShaderResourceView*> _tekstur;
        std::map<std::string, ACubismMotion*> _ekspresi;
        std::map<std::string, std::vector<ACubismMotion*>> _gerakan;
    };

    // ── Panggung ──────────────────────────────────────────────────────────
    struct Panggung
    {
        ID3D11Device* perangkat = nullptr;
        ID3D11DeviceContext* konteks = nullptr;
        IDXGISwapChain1* swapChain = nullptr;
        ID3D11RenderTargetView* target = nullptr;
        ISwapChainPanelNativeWinUI* panel = nullptr;
        ModelPanggung* model = nullptr;
        int lebar = 1;
        int tinggi = 1;
        float perbesaran = 1.0f;
        float geserX = 0.0f;
        float jangkarY = 0.0f;
    };

    bool g_siap = false;
    std::vector<Panggung*> g_panggung;
}

// ── Titik masuk DLL ──────────────────────────────────────────────────────
BOOL APIENTRY DllMain(HMODULE modul, DWORD alasan, LPVOID)
{
    if (alasan == DLL_PROCESS_ATTACH)
    {
        wchar_t jalur[MAX_PATH];
        if (GetModuleFileNameW(modul, jalur, MAX_PATH) > 0)
        {
            const std::wstring penuh(jalur);
            const auto potong = penuh.find_last_of(L"\\/");
            g_direktoriDll = (potong == std::wstring::npos) ? L"" : penuh.substr(0, potong);
        }
    }

    return TRUE;
}

// ── API C ────────────────────────────────────────────────────────────────

extern "C" void swl2d_set_log(void (*sink)(const char* message))
{
    g_log = sink;
}

extern "C" int swl2d_init(void)
{
    if (g_siap) return SWL2D_OK;

    // Penangkap diagnostik HANYA dipasang bila diminta lewat lingkungan.
    //
    // Jangan diaktifkan secara bawaan. Handler ini memanggil kembali ke kode
    // terkelola (lewat g_log) dari dalam proses pengecualian, dan pada
    // aplikasi .NET itu merusak runtime: proses mati dengan
    //   "Fatal error. Invalid Program: attempted to call a
    //    UnmanagedCallersOnly method from managed code."
    // Terbukti lewat uji A/B — tanpa DLL ini aplikasi hidup normal.
    //
    // Jadi: alat penyidikan, bukan bagian dari operasi normal.
    //   set SWL2D_DIAG=1
    if (getenv("SWL2D_DIAG") != nullptr)
    {
        AddVectoredExceptionHandler(1, CatatPengecualian);
        Catat("[swl2d] diagnostik pengecualian AKTIF (SWL2D_DIAG)");
    }

    CoInitializeEx(nullptr, COINIT_APARTMENTTHREADED);

    if (FAILED(CoCreateInstance(CLSID_WICImagingFactory, nullptr, CLSCTX_INPROC_SERVER,
                                IID_PPV_ARGS(&g_wic))))
    {
        Catat("[swl2d] WIC tidak tersedia");
        return SWL2D_ERR_DEVICE;
    }

    // Pakai g_opsi (statis berkas), BUKAN variabel lokal — lihat penjelasan
    // panjang di deklarasi g_opsi. StartUp hanya menyimpan penunjuknya.
    //
    // Sudah diuji A/B (2026-10-07): dengan Option lokal, build Debug mati
    // senyap tepat setelah kedua shader .fx dibaca; dengan g_opsi statis,
    // build yang sama selesai dan model tampil. Jadi "Debug mati di
    // GenerateShaders" BUKAN stack overflow — itu cacat penunjuk menggantung
    // yang sama, hanya terlihat di Debug karena kerangka stack Debug diisi
    // pola racun sehingga penunjuk fungsi sampahnya pasti tidak bisa dipanggil.
    g_opsi.LogFunction = [](const csmChar* pesan) { Catat(std::string("[cubism] ") + pesan); };
    g_opsi.LoggingLevel = CubismFramework::Option::LogLevel_Verbose;
    g_opsi.LoadFileFunction = MuatBerkas;
    g_opsi.ReleaseBytesFunction = BebasBerkas;

    if (!CubismFramework::StartUp(&g_alokator, &g_opsi))
    {
        Catat("[swl2d] CubismFramework::StartUp gagal");
        return SWL2D_ERR_FRAMEWORK;
    }

    CubismFramework::Initialize();
    g_siap = true;
    Catat("[swl2d] siap");
    return SWL2D_OK;
}

extern "C" void swl2d_shutdown(void)
{
    Catat("[swl2d] shutdown: mulai");
    if (!g_siap) { Catat("[swl2d] shutdown: dilewati (belum siap)"); return; }
    // Handle adalah nomor urut 1..N, bukan penunjuk — jadi dihancurkan
    // berurutan dari belakang.
    for (int i = static_cast<int>(g_panggung.size()); i >= 1; i--)
    {
        Catat("[swl2d] shutdown: hancurkan panggung " + std::to_string(i));
        swl2d_stage_destroy(i);
    }
    g_panggung.clear();
    Catat("[swl2d] shutdown: lepas panggung selesai");

    // ── CubismFramework::Dispose() SENGAJA TIDAK DIPANGGIL ────────────────
    //
    // Ini bukan kelalaian, melainkan hasil uji. Pada build Debug, memanggilnya
    // membuat CRT menampilkan dialog
    //   "Microsoft Visual C++ Runtime Library — Debug Error!
    //    abort() has been called"
    // tepat setelah jendela ditutup: jendelanya hilang, tetapi prosesnya
    // tertahan oleh dialog itu dan baru benar-benar berhenti setelah ditekan.
    //
    // Bukti (2026-10-07, semuanya diulang dan konsisten):
    //
    //   | Variasi                                  | Hasil                        |
    //   |------------------------------------------|------------------------------|
    //   | Dispose() dipanggil                      | dialog abort, proses tertahan |
    //   | Dispose() dilewati                       | keluar bersih, tanpa dialog   |
    //   | DLL native dilepas sama sekali           | keluar bersih                 |
    //   | Dispose() + SWL2D_NOLOG=1 (log native)   | dialog abort                  |
    //   | Dispose() TANPA model dimuat sama sekali | dialog abort                  |
    //
    // Dua baris terakhir menyingkirkan dua dugaan: bukan panggilan balik ke
    // .NET, dan bukan pembongkaran model/panggung — abort terjadi murni di
    // tingkat framework. Penanda "shutdown: selesai" selalu tercapai sebelum
    // abort, jadi kematiannya terjadi saat destruktor statis DLL berjalan,
    // sesudah Dispose().
    //
    // Melewatinya aman: prosesnya memang sedang berakhir, dan sistem operasi
    // tetap merebut seluruh memori. Yang perlu diperhatikan kalau nanti ada
    // yang mengubah ini: jangan hidupkan lagi tanpa menjalankan ulang uji tutup
    // jendela pada build Debug — kerusakannya tidak terlihat pada build Release.
    //
    // Petunjuk untuk penyelidikan lanjutan kalau suatu saat perlu:
    // `CubismRenderer_D3D11` menyimpan `s_device` sebagai penunjuk statis
    // (lihat SetConstantSettings) dan `CubismRenderer::StaticRelease()` untuk
    // D3D11 KOSONG — jadi perangkat yang sudah dilepas tetap ditunjuk.

    Catat("[swl2d] shutdown: lepas WIC");
    if (g_wic) { g_wic->Release(); g_wic = nullptr; }
    g_siap = false;
    Catat("[swl2d] shutdown: selesai");
}

extern "C" int swl2d_stage_create(void* panelNative, const char* modelDir, const char* modelJson)
{
    if (!g_siap) return SWL2D_ERR_NOT_READY;
    if (panelNative == nullptr) return SWL2D_ERR_PANEL;

    auto* panggung = new Panggung();

    // Panel: IInspectable SwapChainPanel → ISwapChainPanelNative
    auto* sumber = static_cast<IUnknown*>(panelNative);

    // Diagnostik. Kegagalan QI punya dua penyebab yang sangat berbeda:
    //   (a) penunjuk dari C# bukan objek COM yang hidup, atau
    //   (b) SwapChainPanel benar-benar tidak mengekspos ISwapChainPanelNative.
    // Tanpa pencatat ini keduanya tidak bisa dibedakan, dan keduanya berujung
    // pada layar kosong yang sama.
    IUnknown* identitas = nullptr;
    const HRESULT hrIdentitas = sumber->QueryInterface(IID_IUnknown,
                                                       reinterpret_cast<void**>(&identitas));
    if (identitas != nullptr) identitas->Release();

    char teks[192];

    // Siapa objek yang sebenarnya kita pegang? Kalau nama kelas runtime bukan
    // SwapChainPanel, berarti penunjuk dari C# sudah salah sejak awal.
    IInspectable* inspeksi = nullptr;
    const HRESULT hrInspeksi = sumber->QueryInterface(__uuidof(IInspectable),
                                                      reinterpret_cast<void**>(&inspeksi));
    if (SUCCEEDED(hrInspeksi) && inspeksi != nullptr)
    {
        HSTRING nama = nullptr;
        if (SUCCEEDED(inspeksi->GetRuntimeClassName(&nama)) && nama != nullptr)
        {
            UINT32 panjang = 0;
            const wchar_t* mentah = WindowsGetStringRawBuffer(nama, &panjang);
            const int butuh = WideCharToMultiByte(CP_UTF8, 0, mentah, static_cast<int>(panjang),
                                                  nullptr, 0, nullptr, nullptr);
            std::string sempit(static_cast<size_t>(butuh), '\0');
            WideCharToMultiByte(CP_UTF8, 0, mentah, static_cast<int>(panjang),
                                sempit.data(), butuh, nullptr, nullptr);
            Catat("[swl2d] kelas runtime = " + sempit);
            WindowsDeleteString(nama);
        }

        const HRESULT hrDariInspeksi = inspeksi->QueryInterface(
            IID_ISwapChainPanelNativeWinUI, reinterpret_cast<void**>(&panggung->panel));
        snprintf(teks, sizeof(teks), "[swl2d] QI ISwapChainPanelNative(WinUI) lewat IInspectable=0x%08lX",
                 static_cast<unsigned long>(hrDariInspeksi));
        Catat(teks);
        inspeksi->Release();
    }

    const HRESULT hrPanel = sumber->QueryInterface(IID_ISwapChainPanelNativeWinUI,
                                                   reinterpret_cast<void**>(&panggung->panel));

    snprintf(teks, sizeof(teks),
             "[swl2d] QI IUnknown=0x%08lX  QI ISwapChainPanelNative(WinUI)=0x%08lX  penunjuk=%p",
             static_cast<unsigned long>(hrIdentitas),
             static_cast<unsigned long>(hrPanel),
             panelNative);
    Catat(teks);

    if (FAILED(hrPanel))
    {
        delete panggung;
        return SWL2D_ERR_PANEL;
    }

    // Perangkat D3D11
    Catat("[swl2d] sebelum D3D11CreateDevice");
    UINT bendera = D3D11_CREATE_DEVICE_BGRA_SUPPORT;
    D3D_FEATURE_LEVEL tingkat[] = { D3D_FEATURE_LEVEL_11_0, D3D_FEATURE_LEVEL_10_1, D3D_FEATURE_LEVEL_10_0 };

    const HRESULT hrPerangkat = D3D11CreateDevice(nullptr, D3D_DRIVER_TYPE_HARDWARE, nullptr, bendera,
                                                  tingkat, ARRAYSIZE(tingkat), D3D11_SDK_VERSION,
                                                  &panggung->perangkat, nullptr, &panggung->konteks);
    CatatHr("D3D11CreateDevice(HARDWARE)", hrPerangkat);

    if (FAILED(hrPerangkat))
    {
        Catat("[swl2d] D3D11 hardware gagal, coba WARP");
        const HRESULT hrWarp = D3D11CreateDevice(nullptr, D3D_DRIVER_TYPE_WARP, nullptr, bendera,
                                                 tingkat, ARRAYSIZE(tingkat), D3D11_SDK_VERSION,
                                                 &panggung->perangkat, nullptr, &panggung->konteks);
        CatatHr("D3D11CreateDevice(WARP)", hrWarp);
        if (FAILED(hrWarp))
        {
            Catat("[swl2d] D3D11 tidak tersedia sama sekali");
            panggung->panel->Release();
            delete panggung;
            return SWL2D_ERR_DEVICE;
        }
    }

    // Penyiapan device-info dilakukan SENDIRI di sini, sebelum model dimuat.
    //
    // Alasannya: CubismRenderer_D3D11::OnDeviceChanged() memanggil
    // CubismDeviceInfo_D3D11::GetDeviceInfo(), yang di dalamnya membuat
    // CubismRenderState_D3D11 lalu memanggil _shader.SetupShader(device) —
    // dan SetupShader mengompilasi shader lewat D3DCompile. Jejak tumpukan
    // menunjukkan crash terjadi di dalam GenerateShaders
    // (CubismShader_D3D11::GenerateShaders). Dengan memanggilnya lebih awal
    // di sini, lokasi crash jadi terpisah dari CreateRenderer sehingga bisa
    // dibedakan: kalau log berhenti di sini, sumbernya device-info; kalau
    // berhenti di CreateRenderer, sumbernya renderer.
    // Probe D3DCompile.
    //
    // Cubism mengompilasi shader .fx lewat D3DCompile saat pertama kali
    // device-info dibuat. Proses mati tepat setelah kedua berkas shader
    // terbaca, yaitu di langkah ini. Probe ini memisahkan dua kemungkinan yang
    // sangat berbeda: (a) D3DCompile di mesin ini memang tidak sehat, atau
    // (b) D3DCompile sehat dan masalahnya ada pada cara Cubism memakainya.
    {
        Catat("[swl2d] probe: sebelum D3DCompile");
        static const char* SUMBER =
            "float4 main() : SV_Target { return float4(1.0, 0.0, 0.0, 1.0); }";

        ID3DBlob* kode = nullptr;
        ID3DBlob* galat = nullptr;
        const HRESULT hr = D3DCompile(
            SUMBER, strlen(SUMBER), "probe.hlsl", nullptr, nullptr,
            "main", "ps_4_0", 0, 0, &kode, &galat);

        char teks[256];
        snprintf(teks, sizeof(teks), "[swl2d] probe: D3DCompile -> 0x%08lX",
                 static_cast<unsigned long>(hr));
        Catat(teks);

        if (galat != nullptr)
        {
            Catat(std::string("[swl2d] probe: pesan = ") +
                  static_cast<const char*>(galat->GetBufferPointer()));
            galat->Release();
        }
        if (kode != nullptr) kode->Release();

        Catat("[swl2d] probe: D3DCompile selesai");
    }

    // Probe 2: tiru PERSIS langkah CubismShader_D3D11::GenerateShaders.
    //
    // Probe 1 membuktikan D3DCompile sehat untuk shader sepele. Probe ini
    // memakai berkas .fx yang sama, digabung dengan cara yang sama, lalu
    // diompilasi dengan entry point yang sama. Jadi hasilnya memisahkan dua
    // kemungkinan yang tersisa:
    //   - mati di sini  -> sumber shader / ukurannya yang bermasalah;
    //   - lolos ke sini -> D3DCompile bukan penyebabnya, melainkan cara Cubism
    //                      menyimpan hasilnya (CubismShader_D3D11/_shaderSets).
    {
        Catat("[swl2d] probe2: memuat berkas shader Cubism");
        csmSizeInt ukuranEfek = 0;
        csmSizeInt ukuranBlend = 0;
        csmByte* efek = MuatBerkas("FrameworkShaders/CubismEffect.fx", &ukuranEfek);
        csmByte* blend = MuatBerkas("FrameworkShaders/CubismBlendMode.fx", &ukuranBlend);

        char teks[320];
        snprintf(teks, sizeof(teks), "[swl2d] probe2: efek=%d bita blend=%d bita",
                 static_cast<int>(ukuranEfek), static_cast<int>(ukuranBlend));
        Catat(teks);

        if (efek != nullptr && blend != nullptr && ukuranEfek > 0 && ukuranBlend > 0)
        {
            std::string gabung(reinterpret_cast<const char*>(efek),
                               static_cast<size_t>(ukuranEfek));
            gabung.append(reinterpret_cast<const char*>(blend),
                          static_cast<size_t>(ukuranBlend));

            snprintf(teks, sizeof(teks), "[swl2d] probe2: gabungan=%d bita",
                     static_cast<int>(gabung.size()));
            Catat(teks);

            ID3DBlob* kode = nullptr;
            ID3DBlob* galat = nullptr;
            const HRESULT hr = D3DCompile(
                gabung.data(), gabung.size(), "Cubism.fx", nullptr, nullptr,
                "VertCopy", "vs_4_0", 0, 0, &kode, &galat);

            snprintf(teks, sizeof(teks), "[swl2d] probe2: D3DCompile(VertCopy) -> 0x%08lX",
                     static_cast<unsigned long>(hr));
            Catat(teks);

            if (galat != nullptr)
            {
                Catat(std::string("[swl2d] probe2: pesan = ") +
                      static_cast<const char*>(galat->GetBufferPointer()));
                galat->Release();
            }
            if (kode != nullptr) kode->Release();

            Catat("[swl2d] probe2: selesai");
        }
        else
        {
            Catat("[swl2d] probe2: berkas shader tidak lengkap — dilewati");
        }

        if (efek != nullptr) free(efek);
        if (blend != nullptr) free(blend);
    }

    Catat("[swl2d] sebelum GetDeviceInfo");
    {
        auto* info = Rendering::CubismDeviceInfo_D3D11::GetDeviceInfo(panggung->perangkat);
        Catat(info != nullptr ? "[swl2d] GetDeviceInfo ok" : "[swl2d] GetDeviceInfo NULL");
    }

    Catat("[swl2d] sebelum SetConstantSettings");
    Rendering::CubismRenderer_D3D11::SetConstantSettings(1, panggung->perangkat);
    Catat("[swl2d] SetConstantSettings lewat");

    // Swap chain komposisi
    Microsoft::WRL::ComPtr<IDXGIDevice2> perangkatDxgi;
    const HRESULT hrPerangkatDxgi = panggung->perangkat->QueryInterface(IID_PPV_ARGS(&perangkatDxgi));
    CatatHr("QI IDXGIDevice2", hrPerangkatDxgi);
    if (FAILED(hrPerangkatDxgi))
    {
        panggung->panel->Release();
        delete panggung;
        return SWL2D_ERR_SWAPCHAIN;
    }

    Microsoft::WRL::ComPtr<IDXGIAdapter> adaptor;
    const HRESULT hrAdaptor = perangkatDxgi->GetParent(IID_PPV_ARGS(&adaptor));
    CatatHr("GetParent(IDXGIAdapter)", hrAdaptor);
    if (FAILED(hrAdaptor) || adaptor.Get() == nullptr)
    {
        Catat("[swl2d] IDXGIAdapter tidak diperoleh");
        panggung->panel->Release();
        delete panggung;
        return SWL2D_ERR_SWAPCHAIN;
    }

    Microsoft::WRL::ComPtr<IDXGIFactory2> pabrik;
    const HRESULT hrPabrik = adaptor->GetParent(IID_PPV_ARGS(&pabrik));
    CatatHr("GetParent(IDXGIFactory2)", hrPabrik);
    if (FAILED(hrPabrik) || pabrik.Get() == nullptr)
    {
        Catat("[swl2d] IDXGIFactory2 tidak diperoleh");
        panggung->panel->Release();
        delete panggung;
        return SWL2D_ERR_SWAPCHAIN;
    }

    DXGI_SWAP_CHAIN_DESC1 deskripsi = {};
    deskripsi.Width = 1;
    deskripsi.Height = 1;
    deskripsi.Format = DXGI_FORMAT_B8G8R8A8_UNORM;
    deskripsi.SampleDesc.Count = 1;
    deskripsi.BufferUsage = DXGI_USAGE_RENDER_TARGET_OUTPUT;
    deskripsi.BufferCount = 2;
    deskripsi.Scaling = DXGI_SCALING_STRETCH;
    deskripsi.SwapEffect = DXGI_SWAP_EFFECT_FLIP_SEQUENTIAL;
    deskripsi.AlphaMode = DXGI_ALPHA_MODE_PREMULTIPLIED;

    Catat("[swl2d] sebelum CreateSwapChainForComposition");
    const HRESULT hrSwap = pabrik->CreateSwapChainForComposition(panggung->perangkat, &deskripsi,
                                                                 nullptr, &panggung->swapChain);
    CatatHr("CreateSwapChainForComposition", hrSwap);
    if (FAILED(hrSwap))
    {
        Catat("[swl2d] CreateSwapChainForComposition gagal");
        panggung->panel->Release();
        delete panggung;
        return SWL2D_ERR_SWAPCHAIN;
    }

    Catat("[swl2d] sebelum panel->SetSwapChain");
    const HRESULT hrPasang = panggung->panel->SetSwapChain(panggung->swapChain);
    CatatHr("panel->SetSwapChain", hrPasang);
    if (FAILED(hrPasang))
    {
        Catat("[swl2d] SetSwapChain gagal");
        panggung->swapChain->Release();
        panggung->panel->Release();
        delete panggung;
        return SWL2D_ERR_SWAPCHAIN;
    }

    // Model
    Catat("[swl2d] sebelum ModelPanggung::Muat");
    panggung->model = new ModelPanggung(modelDir ? modelDir : "", panggung->perangkat, 1, 1);
    if (!panggung->model->Muat(modelJson ? modelJson : "silverwolf.model3.json"))
    {
        delete panggung->model;
        panggung->model = nullptr;
        panggung->swapChain->Release();
        panggung->panel->Release();
        delete panggung;
        return SWL2D_ERR_MOC;
    }
    Catat("[swl2d] ModelPanggung::Muat selesai");

    g_panggung.push_back(panggung);

    const int handle = static_cast<int>(g_panggung.size());
    Catat("[swl2d] panggung dibuat, handle=" + std::to_string(handle));
    return handle;
}

extern "C" void swl2d_stage_destroy(int stage)
{
    if (stage <= 0 || stage > static_cast<int>(g_panggung.size())) return;
    auto* p = g_panggung[static_cast<size_t>(stage) - 1];
    if (p == nullptr) return;

    delete p->model;
    if (p->target) p->target->Release();
    if (p->swapChain) p->swapChain->Release();
    if (p->panel) p->panel->Release();
    if (p->konteks) p->konteks->Release();
    if (p->perangkat) p->perangkat->Release();
    delete p;

    g_panggung[static_cast<size_t>(stage) - 1] = nullptr;
}

extern "C" int swl2d_stage_resize(int stage, int width, int height)
{
    if (stage <= 0 || stage > static_cast<int>(g_panggung.size())) return SWL2D_ERR_HANDLE;
    auto* p = g_panggung[static_cast<size_t>(stage) - 1];
    if (p == nullptr || p->swapChain == nullptr) return SWL2D_ERR_HANDLE;

    p->lebar = width < 1 ? 1 : width;
    p->tinggi = height < 1 ? 1 : height;

    if (p->target) { p->target->Release(); p->target = nullptr; }

    const HRESULT hasil = p->swapChain->ResizeBuffers(2, static_cast<UINT>(p->lebar),
                                                      static_cast<UINT>(p->tinggi),
                                                      DXGI_FORMAT_B8G8R8A8_UNORM, 0);
    if (FAILED(hasil)) return SWL2D_ERR_SWAPCHAIN;

    Microsoft::WRL::ComPtr<ID3D11Texture2D> penyangga;
    if (FAILED(p->swapChain->GetBuffer(0, IID_PPV_ARGS(&penyangga)))) return SWL2D_ERR_SWAPCHAIN;
    if (FAILED(p->perangkat->CreateRenderTargetView(penyangga.Get(), nullptr, &p->target)))
        return SWL2D_ERR_SWAPCHAIN;

    // Ukuran render target Cubism harus ikut berubah. Tanpa ini viewport tetap
    // 1x1 (ukuran saat panggung dibuat) dan model hanya menempati satu piksel
    // di sudut kiri atas — Present() tetap sukses, layar tetap kosong.
    if (p->model != nullptr)
    {
        p->model->AturUkuranTarget(p->lebar, p->tinggi);
    }

    return SWL2D_OK;
}

/// <summary>
/// Baca kembali isi back buffer lalu hitung piksel yang tidak transparan.
///
/// Kenapa ini ada: "Present() mengembalikan S_OK" dan "model benar-benar
/// tergambar" adalah dua hal berbeda — swap chain yang seluruhnya tembus
/// pandang tetap Present dengan sukses, dan di layar hasilnya kosong. Tanpa
/// pembacaan kembali, layar kosong tidak bisa dibedakan dari model yang gagal
/// dimuat. Kotak pembatasnya sekaligus menunjukkan model berada di posisi dan
/// ukuran yang masuk akal, bukan sekadar satu piksel nyasar.
/// </summary>
static void PeriksaPiksel(Panggung* p, const char* label)
{
    Microsoft::WRL::ComPtr<ID3D11Texture2D> penyangga;
    if (FAILED(p->swapChain->GetBuffer(0, IID_PPV_ARGS(&penyangga))))
    {
        Catat("[swl2d] periksa piksel: GetBuffer gagal");
        return;
    }

    D3D11_TEXTURE2D_DESC deskripsi = {};
    penyangga->GetDesc(&deskripsi);

    D3D11_TEXTURE2D_DESC salinan = deskripsi;
    salinan.Usage = D3D11_USAGE_STAGING;
    salinan.BindFlags = 0;
    salinan.CPUAccessFlags = D3D11_CPU_ACCESS_READ;
    salinan.MiscFlags = 0;

    Microsoft::WRL::ComPtr<ID3D11Texture2D> panggungSalin;
    if (FAILED(p->perangkat->CreateTexture2D(&salinan, nullptr, &panggungSalin)))
    {
        Catat("[swl2d] periksa piksel: CreateTexture2D gagal");
        return;
    }

    p->konteks->CopyResource(panggungSalin.Get(), penyangga.Get());

    D3D11_MAPPED_SUBRESOURCE dipetakan = {};
    if (FAILED(p->konteks->Map(panggungSalin.Get(), 0, D3D11_MAP_READ, 0, &dipetakan)))
    {
        Catat("[swl2d] periksa piksel: Map gagal");
        return;
    }

    size_t jumlah = 0;
    int minX = static_cast<int>(deskripsi.Width);
    int maxX = -1;
    int minY = static_cast<int>(deskripsi.Height);
    int maxY = -1;

    for (UINT y = 0; y < deskripsi.Height; ++y)
    {
        const uint8_t* baris = static_cast<const uint8_t*>(dipetakan.pData) +
                               static_cast<size_t>(y) * dipetakan.RowPitch;
        for (UINT x = 0; x < deskripsi.Width; ++x)
        {
            const uint8_t* piksel = baris + static_cast<size_t>(x) * 4;  // BGRA
            if (piksel[0] != 0 || piksel[1] != 0 || piksel[2] != 0 || piksel[3] != 0)
            {
                ++jumlah;
                if (static_cast<int>(x) < minX) minX = static_cast<int>(x);
                if (static_cast<int>(x) > maxX) maxX = static_cast<int>(x);
                if (static_cast<int>(y) < minY) minY = static_cast<int>(y);
                if (static_cast<int>(y) > maxY) maxY = static_cast<int>(y);
            }
        }
    }

    p->konteks->Unmap(panggungSalin.Get(), 0);

    char teks[360];
    snprintf(teks, sizeof(teks),
             "[swl2d] periksa piksel (%s): %ux%u, terisi=%zu, kotak=(%d,%d)-(%d,%d)",
             label, deskripsi.Width, deskripsi.Height, jumlah, minX, minY, maxX, maxY);
    Catat(teks);
}

/// <summary>
/// Buang isi back buffer ke berkas BMP 32-bit.
///
/// Kenapa perlu: penalaan pembingkaian (perbesaran, geser, jangkar) tidak bisa
/// dinilai dari angka — `PeriksaPiksel` hanya memberi kotak pembatas, bukan
/// bentuk. Memotret layar juga tidak bisa diandalkan: jendela aplikasi sering
/// tertutup jendela lain, dan `SetForegroundWindow` dari proses latar ditolak
/// Windows. Membaca kembali buffer gambar sendiri selalu berhasil dan persis.
///
/// Aktif hanya bila SWL2D_TANGKAP berisi jalur berkas tujuan.
/// </summary>
static void SimpanBingkai(Panggung* p, const std::string& jalur)
{
    Microsoft::WRL::ComPtr<ID3D11Texture2D> penyangga;
    if (FAILED(p->swapChain->GetBuffer(0, IID_PPV_ARGS(&penyangga)))) return;

    D3D11_TEXTURE2D_DESC deskripsi = {};
    penyangga->GetDesc(&deskripsi);

    D3D11_TEXTURE2D_DESC salinan = deskripsi;
    salinan.Usage = D3D11_USAGE_STAGING;
    salinan.BindFlags = 0;
    salinan.CPUAccessFlags = D3D11_CPU_ACCESS_READ;
    salinan.MiscFlags = 0;

    Microsoft::WRL::ComPtr<ID3D11Texture2D> panggungSalin;
    if (FAILED(p->perangkat->CreateTexture2D(&salinan, nullptr, &panggungSalin))) return;

    p->konteks->CopyResource(panggungSalin.Get(), penyangga.Get());

    D3D11_MAPPED_SUBRESOURCE dipetakan = {};
    if (FAILED(p->konteks->Map(panggungSalin.Get(), 0, D3D11_MAP_READ, 0, &dipetakan))) return;

    const UINT lebar = deskripsi.Width;
    const UINT tinggi = deskripsi.Height;
    const UINT ukuranBaris = lebar * 4;

    FILE* berkas = nullptr;
    if (_wfopen_s(&berkas, KeUtf16(jalur).c_str(), L"wb") != 0 || berkas == nullptr)
    {
        p->konteks->Unmap(panggungSalin.Get(), 0);
        return;
    }

    // BMP 32-bit menyimpan baris dari BAWAH ke atas, sedangkan tekstur D3D
    // dari atas ke bawah — jadi barisnya dibalik. Byte alpha diisi 255 supaya
    // berkasnya tampil utuh di penampil mana pun (kanal alfa BMP diabaikan).
    const UINT ukuranPiksel = ukuranBaris * tinggi;
    const UINT offsetPiksel = 14 + 40;
    const UINT ukuranBerkas = offsetPiksel + ukuranPiksel;

    unsigned char kepalaBerkas[14] = { 'B', 'M' };
    auto tulis32 = [](unsigned char* tujuan, unsigned int nilai)
    {
        tujuan[0] = static_cast<unsigned char>(nilai & 0xFF);
        tujuan[1] = static_cast<unsigned char>((nilai >> 8) & 0xFF);
        tujuan[2] = static_cast<unsigned char>((nilai >> 16) & 0xFF);
        tujuan[3] = static_cast<unsigned char>((nilai >> 24) & 0xFF);
    };
    tulis32(kepalaBerkas + 2, ukuranBerkas);
    tulis32(kepalaBerkas + 10, offsetPiksel);
    fwrite(kepalaBerkas, 1, sizeof(kepalaBerkas), berkas);

    unsigned char kepalaInfo[40] = {};
    tulis32(kepalaInfo + 0, 40);
    tulis32(kepalaInfo + 4, lebar);
    tulis32(kepalaInfo + 8, tinggi);
    kepalaInfo[12] = 1;
    kepalaInfo[14] = 32;
    tulis32(kepalaInfo + 20, ukuranPiksel);
    fwrite(kepalaInfo, 1, sizeof(kepalaInfo), berkas);

    std::vector<unsigned char> baris(ukuranBaris);
    for (int y = static_cast<int>(tinggi) - 1; y >= 0; --y)
    {
        const unsigned char* sumber = static_cast<const unsigned char*>(dipetakan.pData) +
                                      static_cast<size_t>(y) * dipetakan.RowPitch;
        memcpy(baris.data(), sumber, ukuranBaris);
        for (UINT x = 0; x < lebar; ++x) baris[x * 4 + 3] = 0xFF;
        fwrite(baris.data(), 1, ukuranBaris, berkas);
    }

    fclose(berkas);
    p->konteks->Unmap(panggungSalin.Get(), 0);

    Catat("[swl2d] bingkai disimpan: " + jalur + " (" + std::to_string(lebar)
          + "x" + std::to_string(tinggi) + ")");
}

extern "C" int swl2d_stage_render(int stage){
    if (stage <= 0 || stage > static_cast<int>(g_panggung.size())) return SWL2D_ERR_HANDLE;
    auto* p = g_panggung[static_cast<size_t>(stage) - 1];
    if (p == nullptr || p->target == nullptr || p->model == nullptr) return SWL2D_ERR_HANDLE;

    static ULONGLONG terakhir = 0;
    const ULONGLONG sekarang = GetTickCount64();
    const float dt = terakhir == 0 ? 0.016f : (sekarang - terakhir) / 1000.0f;
    terakhir = sekarang;

    p->model->Perbarui(dt);

    const FLOAT warna[4] = { 0.0f, 0.0f, 0.0f, 0.0f };
    p->konteks->OMSetRenderTargets(1, &p->target, nullptr);
    p->konteks->ClearRenderTargetView(p->target, warna);

    D3D11_VIEWPORT lihat = {};
    lihat.TopLeftX = 0.0f;
    lihat.TopLeftY = 0.0f;
    lihat.Width = static_cast<FLOAT>(p->lebar);
    lihat.Height = static_cast<FLOAT>(p->tinggi);
    lihat.MinDepth = 0.0f;
    lihat.MaxDepth = 1.0f;
    p->konteks->RSSetViewports(1, &lihat);

    // Pola resmi contoh D3D11 (LAppModel::Draw baris 496-499):
    //   proyeksi dikali matriks model, lalu diberikan ke renderer.
    //
    // Koreksi aspek: contoh resmi mengoreksi sisi yang LEBIH KECIL, bukan
    // selalu sumbu Y — lihat LAppLive2DManager.cpp:256-263:
    //     if (lebar > tinggi)  proyeksi.Scale(1.0f, lebar / tinggi);   // lanskap
    //     else                 proyeksi.Scale(tinggi / lebar, 1.0f);   // potret
    // Kode sebelumnya SELALU memakai bentuk lanskap. Untuk panel potret
    // (mis. 408x528) itu memampatkan model ke arah vertikal sehingga model
    // tampak kecil dan gepeng — bukan karena perbesarannya kurang.
    CubismMatrix44 proyeksi;
    const float aspek = p->tinggi > 0
        ? static_cast<float>(p->lebar) / static_cast<float>(p->tinggi)
        : 1.0f;
    if (p->lebar > p->tinggi)
    {
        proyeksi.Scale(1.0f, aspek);
    }
    else
    {
        proyeksi.Scale(aspek > 0.0f ? 1.0f / aspek : 1.0f, 1.0f);
    }

    auto* model = p->model->AmbilModel();
    if (model != nullptr)
    {
        // Matriks dibuat lokal tiap bingkai supaya perbesarannya tidak
        // menumpuk dari bingkai ke bingkai.
        CubismModelMatrix matriks(model->GetCanvasWidth(), model->GetCanvasHeight());

        // SetHeight(), BUKAN Scale().
        //
        // CubismMatrix44::Scale(x, y) MENIMPA elemen skala matriks:
        //     void CubismMatrix44::Scale(csmFloat32 x, csmFloat32 y)
        //     { _tr[0] = x; _tr[5] = y; }
        // (Framework/src/Math/CubismMatrix44.cpp:94-98)
        //
        // Konstruktor CubismModelMatrix sudah menyetel tinggi model agar
        // memetakan ke rentang [-1, 1] lewat SetHeight(2.0f). Memanggil
        // Scale() sesudahnya menghapus pemetaan kanvas itu sehingga model
        // tergambar pada ukuran dan posisi yang salah.
        //
        // SetHeight(h) menghitung sendiri faktornya (h / _height), jadi
        // pemetaan kanvas tetap benar dan perbesarannya relatif:
        //     tinggi tampil = 2.0 * perbesaran  (dalam ruang ternormalisasi)
        matriks.SetHeight(2.0f * p->perbesaran);
        matriks.SetPosition(p->geserX, p->jangkarY);
        proyeksi.MultiplyByMatrix(&matriks);
    }

    // Sekali saja: ukuran kanvas model menentukan arti "perbesaran", dan tanpa
    // angka ini penyetelan visual hanya bisa ditebak-tebak.
    static bool pernahLaporKanvas = false;
    if (!pernahLaporKanvas && model != nullptr)
    {
        pernahLaporKanvas = true;
        char teks[256];
        snprintf(teks, sizeof(teks),
                 "[swl2d] kanvas model = %.1f x %.1f, perbesaran=%.2f, geser=(%.2f,%.2f)",
                 model->GetCanvasWidth(), model->GetCanvasHeight(),
                 p->perbesaran, p->geserX, p->jangkarY);
        Catat(teks);
    }

    p->model->Gambar(p->konteks, proyeksi);

    // Pembacaan kembali dilakukan beberapa bingkai setelah bingkai pertama:
    // satu bingkai terlalu dini (parameter model belum sempat diperbarui),
    // dan pembacaan ini mahal sehingga tidak boleh tiap bingkai.
    static int nomorBingkai = 0;
    ++nomorBingkai;
    if (nomorBingkai == 10)
    {
        PeriksaPiksel(p, "bingkai ke-10");

        if (const char* jalurTangkap = getenv("SWL2D_TANGKAP"))
        {
            SimpanBingkai(p, jalurTangkap);
        }
    }

    // ── Uji diagnostik: apakah semua ekspresi / gerakan berfungsi ──────────
    //
    //   set SWL2D_UJI=ekspresi    (atau gerakan)
    //
    // Tiap entri diberi 20 bingkai: cukup untuk CubismExpressionMotionManager
    // memudarkan nilainya sampai mantap, lalu sidik pikselnya dicatat. Pose
    // dibekukan lebih dulu oleh AktifkanUjiDiam() — tanpa itu gerakan idle,
    // napas, dan kedip membuat angkanya berubah sendiri sehingga tidak bisa
    // disimpulkan apa pun.
    {
        constexpr int JEDA_BINGKAI = 20;
        static bool ujiSiap = false;
        static bool ujiGerakan = false;
        static int ujiTotal = 0;
        static int ujiKe = 0;
        static int ujiBingkai = 0;
        static std::string ujiNama;

        if (!ujiSiap)
        {
            if (const char* mode = getenv("SWL2D_UJI"))
            {
                ujiGerakan = (std::string(mode) == "gerakan");
                ujiTotal = ujiGerakan ? p->model->JumlahGerakan() : p->model->JumlahEkspresi();
                ujiSiap = true;
                ujiKe = 0;
                ujiBingkai = 0;
                p->model->AktifkanUjiDiam(true);
                ujiNama = ujiGerakan ? p->model->TerapkanGerakanKe(0)
                                     : p->model->TerapkanEkspresiKe(0);
                Catat("[swl2d] uji " + std::string(mode) + ": " + std::to_string(ujiTotal) + " entri");
            }
        }
        else if (ujiKe < ujiTotal)
        {
            ++ujiBingkai;
            if (ujiBingkai == JEDA_BINGKAI)
            {
                const std::string label = (ujiGerakan ? "gerakan " : "ekspresi ") + ujiNama;
                PeriksaPiksel(p, label.c_str());

                // Simpan juga bingkainya per entri supaya bisa dinilai dengan
                // mata, bukan hanya dari jumlah piksel. Nama entri disisipkan
                // sebelum ekstensi: "uji.bmp" -> "uji_senyum.bmp".
                if (const char* jalurTangkap = getenv("SWL2D_TANGKAP"))
                {
                    std::string dasar(jalurTangkap);
                    std::string nama = ujiNama;
                    for (char& c : nama) if (c == '[' || c == ']' || c == '/') c = '_';
                    const size_t titik = dasar.find_last_of('.');
                    const std::string jalur = (titik == std::string::npos)
                        ? dasar + "_" + nama
                        : dasar.substr(0, titik) + "_" + nama + dasar.substr(titik);
                    SimpanBingkai(p, jalur);
                }

                ++ujiKe;
                ujiBingkai = 0;
                if (ujiKe < ujiTotal)
                {
                    ujiNama = ujiGerakan ? p->model->TerapkanGerakanKe(ujiKe)
                                         : p->model->TerapkanEkspresiKe(ujiKe);
                }
                else
                {
                    Catat("[swl2d] uji selesai: " + std::to_string(ujiTotal) + " entri diuji");
                }
            }
        }
    }

    const HRESULT hasilPresent = p->swapChain->Present(1, 0);

    // Sekali saja: memastikan bingkai benar-benar sampai ke layar. Tanpa ini,
    // "render jalan" dan "render jalan tapi tidak terlihat" tampak identik.
    static bool pernahLapor = false;
    if (!pernahLapor)
    {
        pernahLapor = true;
        char teks[256];
        snprintf(teks, sizeof(teks),
                 "[swl2d] bingkai pertama: %dx%d Present=0x%08lX",
                 p->lebar, p->tinggi, static_cast<unsigned long>(hasilPresent));
        Catat(teks);
    }

    return SWL2D_OK;
}

extern "C" int swl2d_stage_set_expression(int stage, const char* name)
{
    if (stage <= 0 || stage > static_cast<int>(g_panggung.size())) return SWL2D_ERR_HANDLE;
    auto* p = g_panggung[static_cast<size_t>(stage) - 1];
    if (p == nullptr || p->model == nullptr || name == nullptr) return SWL2D_ERR_HANDLE;
    return p->model->AturEkspresi(name) ? SWL2D_OK : SWL2D_ERR_MODEL_JSON;
}

extern "C" int swl2d_stage_play_motion(int stage, const char* group, int no)
{
    if (stage <= 0 || stage > static_cast<int>(g_panggung.size())) return SWL2D_ERR_HANDLE;
    auto* p = g_panggung[static_cast<size_t>(stage) - 1];
    if (p == nullptr || p->model == nullptr || group == nullptr) return SWL2D_ERR_HANDLE;
    return p->model->MainkanGerakan(group, no) ? SWL2D_OK : SWL2D_ERR_MODEL_JSON;
}

extern "C" int swl2d_stage_play_idle(int stage, const char* group, int no)
{
    if (stage <= 0 || stage > static_cast<int>(g_panggung.size())) return SWL2D_ERR_HANDLE;
    auto* p = g_panggung[static_cast<size_t>(stage) - 1];
    if (p == nullptr || p->model == nullptr || group == nullptr) return SWL2D_ERR_HANDLE;
    return p->model->MainkanGerakanBerulang(group, no) ? SWL2D_OK : SWL2D_ERR_MODEL_JSON;
}

extern "C" int swl2d_stage_set_look(int stage, float x, float y)
{
    if (stage <= 0 || stage > static_cast<int>(g_panggung.size())) return SWL2D_ERR_HANDLE;
    auto* p = g_panggung[static_cast<size_t>(stage) - 1];
    if (p == nullptr || p->model == nullptr) return SWL2D_ERR_HANDLE;
    p->model->AturPandang(x, y);
    return SWL2D_OK;
}

extern "C" int swl2d_stage_set_mulut(int stage, float buka)
{
    if (stage <= 0 || stage > static_cast<int>(g_panggung.size())) return SWL2D_ERR_HANDLE;
    auto* p = g_panggung[static_cast<size_t>(stage) - 1];
    if (p == nullptr || p->model == nullptr) return SWL2D_ERR_HANDLE;
    p->model->AturMulut(buka);
    return SWL2D_OK;
}

extern "C" int swl2d_stage_set_view(int stage, float zoom, float offsetX, float anchorY)
{
    if (stage <= 0 || stage > static_cast<int>(g_panggung.size())) return SWL2D_ERR_HANDLE;
    auto* p = g_panggung[static_cast<size_t>(stage) - 1];
    if (p == nullptr) return SWL2D_ERR_HANDLE;

    // Penimpaan diagnostik untuk menala pembingkaian tanpa membangun ulang
    // aplikasi C#. Nilai dari sisi C# tetap yang dipakai bila tidak diset.
    //   set SWL2D_PERBESARAN=1.20
    //   set SWL2D_GESER_X=0.0
    //   set SWL2D_JANGKAR_Y=-0.15
    if (const char* v = getenv("SWL2D_PERBESARAN")) zoom = static_cast<float>(atof(v));
    if (const char* v = getenv("SWL2D_GESER_X"))     offsetX = static_cast<float>(atof(v));
    if (const char* v = getenv("SWL2D_JANGKAR_Y"))   anchorY = static_cast<float>(atof(v));

    p->perbesaran = zoom;
    p->geserX = offsetX;
    p->jangkarY = anchorY;
    return SWL2D_OK;
}
