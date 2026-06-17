$ErrorActionPreference = "Stop"

$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$framework64 = Join-Path $env:WINDIR "Microsoft.NET\Framework64\v4.0.30319\csc.exe"
$framework32 = Join-Path $env:WINDIR "Microsoft.NET\Framework\v4.0.30319\csc.exe"

if (Test-Path $framework64) {
    $compiler = $framework64
} elseif (Test-Path $framework32) {
    $compiler = $framework32
} else {
    throw "C# compiler not found. Install .NET Framework 4.x developer tools or the .NET SDK."
}

$outDir = Join-Path $root "bin"
$distDir = Join-Path $root "dist"
New-Item -ItemType Directory -Force -Path $outDir | Out-Null
New-Item -ItemType Directory -Force -Path $distDir | Out-Null

$out = Join-Path $outDir "PromptBar.exe"
$portableOut = Join-Path $distDir "PromptBarPortable.exe"
$sources = Get-ChildItem -Path (Join-Path $root "src") -Filter "*.cs" | Sort-Object Name | ForEach-Object { $_.FullName }
$frameworkDir = Split-Path -Parent $compiler
$wpfDir = Join-Path $frameworkDir "WPF"
$assetDir = Join-Path $root "assets"
$sourcePng = Join-Path $assetDir "icon.png"
$generatedIcon = Join-Path $assetDir "promptbar.ico"

if ((Test-Path $sourcePng) -and !(Test-Path $generatedIcon)) {
    New-Item -ItemType Directory -Force -Path $assetDir | Out-Null
    Add-Type -AssemblyName System.Drawing
    Add-Type @"
using System;
using System.Runtime.InteropServices;
public static class IconNativeMethods {
    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool DestroyIcon(IntPtr hIcon);
}
"@

    $sourceImage = [System.Drawing.Image]::FromFile((Resolve-Path $sourcePng))
    try {
        $bitmap = New-Object System.Drawing.Bitmap 256, 256
        $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
        try {
            $graphics.Clear([System.Drawing.Color]::Transparent)
            $graphics.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
            $graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::HighQuality
            $graphics.DrawImage($sourceImage, 0, 0, 256, 256)
        } finally {
            $graphics.Dispose()
        }

        $hIcon = $bitmap.GetHicon()
        try {
            $icon = [System.Drawing.Icon]::FromHandle($hIcon)
            $stream = [System.IO.File]::Create($generatedIcon)
            try {
                $icon.Save($stream)
            } finally {
                $stream.Dispose()
                $icon.Dispose()
            }
        } finally {
            [IconNativeMethods]::DestroyIcon($hIcon) | Out-Null
            $bitmap.Dispose()
        }
    } finally {
        $sourceImage.Dispose()
    }
}

$references = @(
    "/reference:System.dll",
    "/reference:System.Core.dll",
    "/reference:System.Drawing.dll",
    "/reference:System.IO.Compression.dll",
    "/reference:System.IO.Compression.FileSystem.dll",
    "/reference:System.Windows.Forms.dll",
    "/reference:System.Xml.dll",
    "/reference:$(Join-Path $frameworkDir 'System.Xml.Linq.dll')",
    "/reference:$(Join-Path $wpfDir 'WindowsBase.dll')",
    "/reference:$(Join-Path $wpfDir 'PresentationCore.dll')",
    "/reference:$(Join-Path $wpfDir 'PresentationFramework.dll')",
    "/reference:$(Join-Path $frameworkDir 'System.Xaml.dll')"
)

$arguments = @(
    "/nologo",
    "/codepage:65001",
    "/target:winexe",
    "/platform:anycpu",
    "/optimize+",
    "/main:PromptBar.Program",
    "/out:$out"
) + $references + $sources

if (Test-Path $generatedIcon) {
    $arguments = @("/win32icon:$generatedIcon") + $arguments
}

& $compiler @arguments
if ($LASTEXITCODE -ne 0) {
    exit $LASTEXITCODE
}

Write-Host "Built $out"
Copy-Item -LiteralPath $out -Destination $portableOut -Force
Write-Host "Portable exe $portableOut"
