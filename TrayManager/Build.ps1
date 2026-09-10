param([string]$Output = (Join-Path $PSScriptRoot '..\outputs\TrayManager-1.1.7'))
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
$bitmap = [Drawing.Bitmap]::new(64,64)
$graphics = [Drawing.Graphics]::FromImage($bitmap)
$graphics.SmoothingMode = 'AntiAlias'
$graphics.Clear([Drawing.Color]::Transparent)
$blue = [Drawing.SolidBrush]::new([Drawing.Color]::FromArgb(0,103,192))
$path = [Drawing.Drawing2D.GraphicsPath]::new()
$path.AddArc(2,2,20,20,180,90); $path.AddArc(42,2,20,20,270,90)
$path.AddArc(42,42,20,20,0,90); $path.AddArc(2,42,20,20,90,90); $path.CloseFigure()
$graphics.FillPath($blue,$path)
foreach ($x in @(14,28,42)) { $graphics.FillRectangle([Drawing.Brushes]::White,$x,21,8,8) }
$pen = [Drawing.Pen]::new([Drawing.Color]::White,4)
$graphics.DrawLines($pen,[Drawing.Point[]]@([Drawing.Point]::new(13,38),[Drawing.Point]::new(13,46),[Drawing.Point]::new(51,46),[Drawing.Point]::new(51,38)))
$stream = [IO.MemoryStream]::new(); $bitmap.Save($stream,[Drawing.Imaging.ImageFormat]::Png)
$png=$stream.ToArray()
$file=[IO.File]::Create((Join-Path $PSScriptRoot 'TrayManager.ico')); $writer=[IO.BinaryWriter]::new($file)
$writer.Write([uint16]0); $writer.Write([uint16]1); $writer.Write([uint16]1)
$writer.Write([byte]64);$writer.Write([byte]64);$writer.Write([byte]0);$writer.Write([byte]0)
$writer.Write([uint16]1);$writer.Write([uint16]32);$writer.Write([uint32]$png.Length);$writer.Write([uint32]22);$writer.Write($png)
$writer.Dispose();$stream.Dispose();$pen.Dispose();$path.Dispose();$blue.Dispose();$graphics.Dispose();$bitmap.Dispose()
dotnet publish (Join-Path $PSScriptRoot 'TrayManager.csproj') -c Release -o $Output --nologo -p:DebugType=None -p:DebugSymbols=false
if ($LASTEXITCODE -ne 0) { throw 'Build failed' }
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'Install.ps1'),(Join-Path $PSScriptRoot 'README.md') -Destination $Output
$zip = [IO.Path]::GetFullPath($Output) + '.zip'
Compress-Archive -Path (Join-Path $Output '*') -DestinationPath $zip -Force
Get-Item -LiteralPath $zip | Select-Object FullName,Length
