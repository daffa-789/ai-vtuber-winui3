using System.Diagnostics;
using System.Globalization;
using SilverWolf.Core.Configuration;

namespace SilverWolf.Services.Llama;

/// <summary>
/// Pengelola proses <c>llama-server.exe</c> — port kelas <c>Process</c> dari
/// <c>apps/server-node/src/llama.js</c>.
///
/// Dua hal yang penting untuk paritas:
/// 1. <b>WorkingDirectory harus akar repo.</b> llama-server butuh menemukan
///    ~20 DLL Vulkan di sebelahnya, dan folder instalasi MSIX bersifat read-only
///    — itu salah satu alasan aplikasi ini didistribusikan unpackaged.
/// 2. Susunan argumen (termasuk <c>--jinja</c>, <c>-rea</c>, dan kuantisasi
///    KV cache Vulkan yang dapat diatur lewat <c>AppConfig.VulkanCacheType</c>)
///    menentukan kualitas dan kecepatan jawaban model, jadi tidak boleh diubah
///    seenaknya. Kuantisasi cache boleh diubah via <c>VTUBER_VULKAN_CACHE_TYPE</c>
///    di <c>.env</c> bila diperlukan.
/// </summary>
public sealed class LlamaServerProcess
{
    /// <summary>
    /// Batas menunggu llama-server siap, dalam milidetik.
    ///
    /// <b>Kenapa 120 detik, bukan 30.</b> Nilai lama adalah 120 percobaan x
    /// 250 ms = <b>30 detik</b>, dan itu selalu kalah lomba dengan kenyataan:
    /// GGUF Gemma 4B (5,12 GB) pada mesin ini butuh <b>38-45 detik</b> dari
    /// `loading model` sampai `listening on http://127.0.0.1:8788` — terukur
    /// di `tools/bukti/`. Akibatnya loop kehabisan percobaan tepat saat model
    /// hampir siap, `StartAsync` mengembalikan "waktu tunggu llama-server
    /// habis", dan pemanggilnya menjalankan `Stop()` sehingga
    /// <b>llama-server yang sebenarnya sehat dimatikan dari bawah</b>.
    /// Gejalanya persis seperti crash: aplikasi hidup, jendela tampil, lalu
    /// hilang beberapa detik kemudian.
    ///
    /// Perlambatan normal lain (pemuatan .gguf pertama kali sesudah cold boot,
    /// antivirus memindai 5 GB, disk sibuk) juga ikut tertampung.
    /// </summary>
    private const int MaksPercobaanSehat = 480;
    private const int JedaSehatMs = 250;
    private const int BatasSehatMs = 1000;
    private const int JedaPaksaMs = 1500;

    private static readonly HttpClient Http = new();

    private readonly AppConfig _config;
    private readonly int _port;
    private readonly Action<string>? _log;
    private Process? _proc;

    public LlamaServerProcess(AppConfig config, int port, Action<string>? log = null)
    {
        _config = config;
        _port = port;
        _log = log;
    }

    public readonly record struct HasilMula(bool Ok, string Reason);

    /// <summary>Port <c>start(signal)</c>.</summary>
    public async Task<HasilMula> StartAsync(CancellationToken ct = default)
    {
        var binary = ModelLocator.CariLlamaServer(_config);
        if (binary is null)
        {
            return new HasilMula(false, $"llama-server tidak ditemukan di {_config.LlamaServer}");
        }

        var model = ModelLocator.CariModel(_config, pesan => _log?.Invoke(pesan));
        if (!model.Ok)
        {
            return new HasilMula(false, "model GGUF tidak ditemukan");
        }

        var vulkan = _config.LlmProvider == "vulkan";
        var ctx = vulkan ? _config.VulkanCtx : _config.LocalModelCtx;
        var ngl = vulkan ? _config.VulkanNgl : 0;

        var args = new List<string>
        {
            "-m", model.Path,
            "-a", ModelLocator.AliasModel(_config, model.Path),
            "--host", "127.0.0.1",
            "--port", _port.ToString(CultureInfo.InvariantCulture),
            "-c", ctx.ToString(CultureInfo.InvariantCulture),
            "-t", _config.LocalModelThreads.ToString(CultureInfo.InvariantCulture),
            "-ngl", ngl.ToString(CultureInfo.InvariantCulture),
            "-np", "1",
            "-b", "2048",
            "-ub", "512",
        };

        if (vulkan)
        {
            args.AddRange(["-ctk", _config.VulkanCacheType, "-ctv", _config.VulkanCacheType]);
            if (_config.VulkanFa)
            {
                args.AddRange(["--flash-attn", "on"]);
            }
        }

        args.AddRange([
            "--jinja",
            "--min-p", _config.LocalMinP.ToString(CultureInfo.InvariantCulture),
            "--top-p", _config.LocalTopP.ToString(CultureInfo.InvariantCulture),
            "-rea", _config.LocalReasoning,
        ]);

        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = binary,
                WorkingDirectory = _config.Akar,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
            };

            foreach (var a in args)
            {
                psi.ArgumentList.Add(a);
            }

            var proc = new Process { StartInfo = psi };
            proc.OutputDataReceived += (_, e) =>
            {
                if (e.Data is { } baris)
                {
                    _log?.Invoke($"[llama] {baris}");
                }
            };
            proc.ErrorDataReceived += (_, e) =>
            {
                if (e.Data is { } baris)
                {
                    _log?.Invoke($"[llama] {baris}");
                }
            };

            if (!proc.Start())
            {
                return new HasilMula(false, "proses llama-server tidak bisa dimulai");
            }

            proc.BeginOutputReadLine();
            proc.BeginErrorReadLine();
            _proc = proc;
        }
        catch (Exception error)
        {
            return new HasilMula(false, error.Message);
        }

        var alasan = "waktu tunggu llama-server habis";
        for (var i = 0; i < MaksPercobaanSehat; i++)
        {
            if (ct.IsCancellationRequested)
            {
                Stop();
                return new HasilMula(false, "dibatalkan");
            }

            if (_proc.HasExited)
            {
                return new HasilMula(false, $"llama-server berhenti (kode {_proc.ExitCode})");
            }

            try
            {
                using var batas = CancellationTokenSource.CreateLinkedTokenSource(ct);
                batas.CancelAfter(BatasSehatMs);
                using var res = await Http.GetAsync($"http://127.0.0.1:{_port}/health", batas.Token).ConfigureAwait(false);
                if ((int)res.StatusCode == 200)
                {
                    return new HasilMula(true, "siap");
                }

                // 503 = server hidup dan sedang memuat model ke VRAM. Itu
                // kemajuan, bukan kegagalan, jadi jangan dicatat sebagai
                // "alasan" — kalau loop benar-benar habis, yang dilaporkan
                // harus sebab terakhir yang benar-benar menghalangi.
                // Dipakai bersama OpenAiCompatibleProvider lewat HealthProbe
                // supaya kedua sisi mengenali bentuk badan yang sama
                // (docs/PROYEK.md §8.6).
                var badan = await res.Content.ReadAsStringAsync(batas.Token).ConfigureAwait(false);
                if (Core.Inference.HealthProbe.MenandakanSedangMemuat((int)res.StatusCode, badan))
                {
                    alasan = "model masih dimuat ke VRAM";
                }
                else
                {
                    alasan = $"health HTTP {(int)res.StatusCode}";
                }
            }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested)
            {
                // belum hidup
            }
            catch (Exception)
            {
                // belum hidup
            }

            try
            {
                await Task.Delay(JedaSehatMs, ct).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                Stop();
                return new HasilMula(false, "dibatalkan");
            }
        }

        Stop();
        return new HasilMula(false, alasan);
    }

    /// <summary>
    /// Port <c>stop()</c>. Di Windows, <c>proc.kill('SIGTERM')</c> pada
    /// Node berarti langsung mengakhiri proses, jadi di sini satu
    /// <see cref="Process.Kill()"/> sudah setara; jeda 1500 ms dipakai untuk
    /// memberi kesempatan proses benar-benar keluar sebelum dilepas.
    /// </summary>
    public void Stop()
    {
        var proc = _proc;
        _proc = null;
        if (proc is null)
        {
            return;
        }

        try
        {
            if (proc.HasExited)
            {
                return;
            }

            proc.Kill(entireProcessTree: true);
            proc.WaitForExit(JedaPaksaMs);
        }
        catch (InvalidOperationException)
        {
            // sudah mati
        }
        catch (System.ComponentModel.Win32Exception)
        {
            // sudah mati
        }
        finally
        {
            proc.Dispose();
        }
    }
}
