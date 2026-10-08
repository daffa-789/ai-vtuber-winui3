Add-Type -AssemblyName System.Drawing
Add-Type @"
using System;
using System.Text;
using System.Collections.Generic;
using System.Runtime.InteropServices;
public class CariJendela {
  public delegate bool EnumProc(IntPtr h, IntPtr l);
  [DllImport("user32.dll")] public static extern bool EnumWindows(EnumProc cb, IntPtr l);
  [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr h);
  [DllImport("user32.dll", CharSet=CharSet.Unicode)] public static extern int GetWindowTextW(IntPtr h, StringBuilder s, int n);
  [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
  [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr h);
  [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h, out RECT r);
  [StructLayout(LayoutKind.Sequential)] public struct RECT { public int Left, Top, Right, Bottom; }

  public static List<string> Daftar(uint pidTarget) {
    var hasil = new List<string>();
    EnumWindows((h, l) => {
      uint pid; GetWindowThreadProcessId(h, out pid);
      if (pid == pidTarget && IsWindowVisible(h)) {
        var sb = new StringBuilder(512); GetWindowTextW(h, sb, 512);
        RECT r; GetWindowRect(h, out r);
        hasil.Add(h.ToInt64() + "|" + sb.ToString() + "|" + (r.Right-r.Left) + "x" + (r.Bottom-r.Top));
      }
      return true;
    }, IntPtr.Zero);
    return hasil;
  }
}
"@
$out = @()
$proc = Get-Process SilverWolf.App -ErrorAction SilentlyContinue
if (-not $proc) { "PROSES TIDAK ADA"; exit 1 }
$out += "pid=" + $proc.Id
$win = [CariJendela]::Daftar([uint32]$proc.Id)
$out += $win
$out | Out-File "C:\Users\Daffa\Desktop\AI Vtuber Project\AI Vtuber WINUI3\tools\bukti\daftar-jendela.txt" -Encoding UTF8
foreach ($w in $win) {
  $bagian = $w -split '\|'
  $h = [IntPtr][int64]$bagian[0]
  $uk = $bagian[2] -split 'x'
  if ([int]$uk[0] -lt 300) { continue }
  [CariJendela]::SetForegroundWindow($h) | Out-Null
  Start-Sleep -Milliseconds 1200
  $r = New-Object CariJendela+RECT
  [CariJendela]::GetWindowRect($h, [ref]$r) | Out-Null
  $w2 = $r.Right-$r.Left; $h2 = $r.Bottom-$r.Top
  $bmp = New-Object System.Drawing.Bitmap($w2, $h2)
  $g = [System.Drawing.Graphics]::FromImage($bmp)
  $g.CopyFromScreen($r.Left, $r.Top, 0, 0, $bmp.Size)
  $bmp.Save("C:\Users\Daffa\Desktop\AI Vtuber Project\AI Vtuber WINUI3\tools\bukti\tampilan-chat-saja.png", [System.Drawing.Imaging.ImageFormat]::Png)
  $g.Dispose(); $bmp.Dispose()
  "TERSIMPAN: ${w2}x${h2}"
  break
}