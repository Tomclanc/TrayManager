param([switch]$NoLaunch)
$ErrorActionPreference = 'Stop'
$destination = Join-Path $env:LOCALAPPDATA 'Programs\Tomclanc\TrayManager'
$source = [IO.Path]::GetFullPath($PSScriptRoot)
if ([IO.Path]::GetFullPath($destination) -ne $source) {
    if (Get-Process TrayManager -ErrorAction SilentlyContinue | Where-Object Path -EQ (Join-Path $destination 'TrayManager.exe')) {
        throw '请先在托盘管理窗口中点击“退出程序”，再安装。'
    }
    New-Item -ItemType Directory -Path $destination -Force | Out-Null
    Get-ChildItem -LiteralPath $source -File | Copy-Item -Destination $destination -Force
}
$shell = New-Object -ComObject WScript.Shell
$shortcut = $shell.CreateShortcut((Join-Path ([Environment]::GetFolderPath('Programs')) '托盘管理.lnk'))
$shortcut.TargetPath = Join-Path $destination 'TrayManager.exe'
$shortcut.WorkingDirectory = $destination
$shortcut.IconLocation = Join-Path $destination 'TrayManager.ico'
$shortcut.Save()
if (!$NoLaunch) { Start-Process -FilePath (Join-Path $destination 'TrayManager.exe') }
Write-Output "已安装：$destination"
