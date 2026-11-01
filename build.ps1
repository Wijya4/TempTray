<#
    build.ps1 - downloads the required libraries and compiles TempTray.exe
    Visual Studio is not needed. Only the .NET Framework 4.8 that ships with Windows 10/11.
#>
[CmdletBinding()]
param(
    [string]$OutDir
)

$ErrorActionPreference = 'Stop'

$Root = $PSScriptRoot
if (-not $Root) { $Root = Split-Path -Parent $MyInvocation.MyCommand.Path }
if (-not $OutDir) { $OutDir = Join-Path $Root 'build' }

# NuGet packages: id, version, and the dll path inside each package
$packages = @(
    @{ Id = 'LibreHardwareMonitorLib';   Version = '0.9.6';  Dll = 'runtimes/win-x64/lib/net472/LibreHardwareMonitorLib.dll' },
    @{ Id = 'HidSharp';                  Version = '2.6.4';  Dll = 'lib/netstandard2.0/HidSharp.dll' },
    @{ Id = 'DiskInfoToolkit';           Version = '1.1.2';  Dll = 'lib/net472/DiskInfoToolkit.dll' },
    @{ Id = 'RAMSPDToolkit-NDD';         Version = '1.4.2';  Dll = 'lib/net472/RAMSPDToolkit-NDD.dll' },
    @{ Id = 'System.Memory';             Version = '4.6.3';  Dll = 'lib/net462/System.Memory.dll' },
    @{ Id = 'System.Buffers';            Version = '4.6.1';  Dll = 'lib/net462/System.Buffers.dll' },
    @{ Id = 'System.Numerics.Vectors';   Version = '4.6.1';  Dll = 'lib/net462/System.Numerics.Vectors.dll' },
    @{ Id = 'System.Runtime.CompilerServices.Unsafe'; Version = '6.1.2'; Dll = 'lib/net462/System.Runtime.CompilerServices.Unsafe.dll' },
    @{ Id = 'System.Threading.AccessControl';         Version = '10.0.3'; Dll = 'lib/net462/System.Threading.AccessControl.dll' }
)

$depsDir = Join-Path $Root 'deps'
New-Item -ItemType Directory -Force -Path $OutDir, $depsDir | Out-Null

$csc = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (-not (Test-Path $csc)) { throw "csc.exe not found. The .NET Framework 4.x is required." }

Add-Type -AssemblyName System.IO.Compression.FileSystem

foreach ($p in $packages) {
    $nupkg = Join-Path $depsDir ("{0}.{1}.nupkg" -f $p.Id, $p.Version)
    if (-not (Test-Path $nupkg)) {
        $lower = $p.Id.ToLower()
        $url = "https://api.nuget.org/v3-flatcontainer/$lower/$($p.Version)/$lower.$($p.Version).nupkg"
        Write-Host "downloading $($p.Id) $($p.Version)"
        Invoke-WebRequest -Uri $url -OutFile $nupkg -UseBasicParsing
    }
    $zip = Join-Path $depsDir ("{0}.{1}.zip" -f $p.Id, $p.Version)
    Copy-Item $nupkg $zip -Force
    $extract = Join-Path $depsDir ("{0}.{1}" -f $p.Id, $p.Version)
    if (-not (Test-Path $extract)) {
        [System.IO.Compression.ZipFile]::ExtractToDirectory($zip, $extract)
    }
    $src = Join-Path $extract ($p.Dll -replace '/', '\')
    if (-not (Test-Path $src)) { throw "dll not found inside package $($p.Id): $($p.Dll)" }
    Copy-Item $src (Join-Path $OutDir (Split-Path $src -Leaf)) -Force
}

$source = Join-Path $Root 'src\TempTray.cs'
$refs = Get-ChildItem $OutDir -Filter *.dll | ForEach-Object { '/r:' + $_.FullName }
$args = @(
    '/nologo', '/target:winexe', '/platform:x64', '/optimize+',
    ('/out:' + (Join-Path $OutDir 'TempTray.exe')),
    '/r:System.dll', '/r:System.Core.dll', '/r:System.Drawing.dll',
    '/r:System.Windows.Forms.dll', '/r:System.Management.dll'
) + $refs + $source

& $csc @args
if ($LASTEXITCODE -ne 0) { throw "compilation failed" }

Write-Host ""
Write-Host "done: $(Join-Path $OutDir 'TempTray.exe')"
Get-ChildItem $OutDir | ForEach-Object { "  {0} ({1:N0} KB)" -f $_.Name, ($_.Length / 1KB) }
