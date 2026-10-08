Add-Type -AssemblyName System.Drawing
Add-Type -AssemblyName System.Windows.Forms

$bounds = [System.Windows.Forms.Screen]::PrimaryScreen.Bounds
$bmp = New-Object System.Drawing.Bitmap($bounds.Width, $bounds.Height)
$g = [System.Drawing.Graphics]::FromImage($bmp)
$g.CopyFromScreen($bounds.Location, [System.Drawing.Point]::Empty, $bounds.Size)
$tujuan = "C:\Users\Daffa\Desktop\AI Vtuber Project\AI Vtuber WINUI3\tools\bukti\layar-penuh.png"
$bmp.Save($tujuan, [System.Drawing.Imaging.ImageFormat]::Png)
$g.Dispose(); $bmp.Dispose()
"layar penuh ${($bounds.Width)}x${($bounds.Height)} -> $tujuan"