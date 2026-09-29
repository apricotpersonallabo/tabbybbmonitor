[CmdletBinding()]
param(
    [string]$SettingsPath,
    [string]$IdentityName,
    [string]$Publisher,
    [string]$PublisherDisplayName,
    [string]$DisplayName,
    [string]$Version,
    [ValidateSet('x64', 'arm64')]
    [string[]]$Architectures,
    [switch]$AllowPlaceholderIdentity,
    [switch]$SkipObfuscation
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repositoryRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
if ([string]::IsNullOrWhiteSpace($SettingsPath)) {
    $SettingsPath = Join-Path $repositoryRoot 'packaging\store-settings.json'
}
$SettingsPath = [IO.Path]::GetFullPath($SettingsPath)
if (-not (Test-Path -LiteralPath $SettingsPath -PathType Leaf)) {
    throw "Store settings file was not found: $SettingsPath"
}

$settings = Get-Content -LiteralPath $SettingsPath -Raw | ConvertFrom-Json
if ([string]::IsNullOrWhiteSpace($IdentityName)) { $IdentityName = [string]$settings.IdentityName }
if ([string]::IsNullOrWhiteSpace($Publisher)) { $Publisher = [string]$settings.Publisher }
if ([string]::IsNullOrWhiteSpace($PublisherDisplayName)) { $PublisherDisplayName = [string]$settings.PublisherDisplayName }
if ([string]::IsNullOrWhiteSpace($DisplayName)) { $DisplayName = [string]$settings.DisplayName }
if ([string]::IsNullOrWhiteSpace($Version)) { $Version = [string]$settings.Version }
if (-not $Architectures -or $Architectures.Count -eq 0) { $Architectures = @($settings.Architectures) }

if (-not $AllowPlaceholderIdentity -and
    ($IdentityName.Contains('REPLACE_WITH_') -or $Publisher.Contains('REPLACE_WITH_') -or
     $PublisherDisplayName.Contains('REPLACE_WITH_'))) {
    throw 'packaging\store-settings.json に Partner Center の Identity、Publisher、PublisherDisplayName を設定してください。'
}
if ($IdentityName -notmatch '^[A-Za-z0-9][A-Za-z0-9.-]{2,49}$') {
    throw 'IdentityName は3～50文字の英数字、ピリオド、ハイフンで指定してください。'
}
if ($Publisher -notmatch '^CN=.+') {
    throw 'Publisher は Partner Center に表示された CN=... の値をそのまま指定してください。'
}
if ($Architectures.Count -eq 0 -or @($Architectures | Where-Object { $_ -notin @('x64', 'arm64') }).Count -gt 0) {
    throw 'Architectures には x64、arm64 のいずれかを指定してください。'
}

$packageVersion = $null
if (-not [System.Version]::TryParse($Version, [ref]$packageVersion) -or
    $packageVersion.Major -le 0 -or
    $packageVersion.Major -gt 65535 -or $packageVersion.Minor -gt 65535 -or
    $packageVersion.Build -lt 0 -or $packageVersion.Build -gt 65535 -or
    $packageVersion.Revision -ne 0) {
    throw 'Version は 1.0.0.0 のような4区分で、各値を0～65535、先頭を1以上、末尾を0にしてください。'
}

function Invoke-Checked {
    param([Parameter(Mandatory)][scriptblock]$Command, [Parameter(Mandatory)][string]$Description)
    & $Command
    if ($LASTEXITCODE -ne 0) {
        throw "$Description failed with exit code $LASTEXITCODE."
    }
}

function ConvertTo-XmlText([string]$Value) {
    return [Security.SecurityElement]::Escape($Value)
}

function Invoke-Obfuscation {
    param(
        [Parameter(Mandatory)][string]$InputDirectory,
        [Parameter(Mandatory)][string]$WorkingDirectory,
        [Parameter(Mandatory)][string]$MapDestination
    )

    $outputDirectory = Join-Path $WorkingDirectory 'output'
    $mapping = Join-Path $WorkingDirectory 'obfuscation-map.txt'
    New-Item -ItemType Directory -Path $outputDirectory -Force | Out-Null
    $configTemplate = Get-Content -LiteralPath (Join-Path $repositoryRoot 'obfuscation\obfuscar.xml.template') -Raw
    $inputAssembly = Join-Path $InputDirectory 'BatteryTray.dll'
    $config = $configTemplate.Replace('@@INPUT_PATH@@', (ConvertTo-XmlText $InputDirectory))
    $config = $config.Replace('@@OUTPUT_PATH@@', (ConvertTo-XmlText $outputDirectory))
    $config = $config.Replace('@@LOG_PATH@@', (ConvertTo-XmlText $mapping))
    $config = $config.Replace('@@INPUT_ASSEMBLY@@', (ConvertTo-XmlText $inputAssembly))
    $configPath = Join-Path $WorkingDirectory 'obfuscar.xml'
    [IO.File]::WriteAllText($configPath, $config, [Text.UTF8Encoding]::new($false))

    Invoke-Checked { dotnet tool run obfuscar.console -- $configPath | Out-Null } 'Obfuscar'
    $obfuscatedAssembly = Join-Path $outputDirectory 'BatteryTray.dll'
    if (-not (Test-Path -LiteralPath $obfuscatedAssembly -PathType Leaf)) {
        throw "Obfuscar did not create $obfuscatedAssembly."
    }
    Copy-Item -LiteralPath $obfuscatedAssembly -Destination $inputAssembly -Force

    if (-not (Test-Path -LiteralPath $mapping -PathType Leaf)) {
        throw "Obfuscar did not create the rename map: $mapping"
    }
    Copy-Item -LiteralPath $mapping -Destination $MapDestination -Force
}

function New-StoreAsset {
    param(
        [Parameter(Mandatory)][string]$Path,
        [Parameter(Mandatory)][int]$Width,
        [Parameter(Mandatory)][int]$Height
    )

    Add-Type -AssemblyName System.Drawing
    $bitmap = [Drawing.Bitmap]::new($Width, $Height, [Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $graphics = [Drawing.Graphics]::FromImage($bitmap)
    try {
        $graphics.SmoothingMode = [Drawing.Drawing2D.SmoothingMode]::AntiAlias
        $graphics.Clear([Drawing.Color]::FromArgb(0, 120, 212))
        $unit = [Math]::Min($Width, $Height)
        $batteryWidth = [Math]::Round($unit * 0.58)
        $batteryHeight = [Math]::Round($unit * 0.34)
        $left = [Math]::Round(($Width - $batteryWidth) / 2 - $unit * 0.02)
        $top = [Math]::Round(($Height - $batteryHeight) / 2)
        $stroke = [Math]::Max(2, [Math]::Round($unit * 0.055))
        $pen = [Drawing.Pen]::new([Drawing.Color]::White, $stroke)
        $pen.LineJoin = [Drawing.Drawing2D.LineJoin]::Round
        $brush = [Drawing.SolidBrush]::new([Drawing.Color]::White)
        try {
            $graphics.DrawRectangle($pen, $left, $top, $batteryWidth, $batteryHeight)
            $terminalWidth = [Math]::Max(2, [Math]::Round($unit * 0.07))
            $terminalHeight = [Math]::Round($batteryHeight * 0.45)
            $graphics.FillRectangle($brush, $left + $batteryWidth + $stroke, $top + ($batteryHeight - $terminalHeight) / 2, $terminalWidth, $terminalHeight)
            $padding = [Math]::Max(3, [Math]::Round($unit * 0.10))
            $fillWidth = [Math]::Round(($batteryWidth - 2 * $padding) * 0.72)
            $graphics.FillRectangle($brush, $left + $padding, $top + $padding, $fillWidth, $batteryHeight - 2 * $padding)
        }
        finally {
            $pen.Dispose()
            $brush.Dispose()
        }
        $bitmap.Save($Path, [Drawing.Imaging.ImageFormat]::Png)
    }
    finally {
        $graphics.Dispose()
        $bitmap.Dispose()
    }
}

$toolsProject = Join-Path $repositoryRoot 'build\StorePackaging\StorePackaging.csproj'
Invoke-Checked { dotnet restore $toolsProject --verbosity minimal } 'Windows SDK BuildTools restore'
if (-not $SkipObfuscation) {
    Invoke-Checked { dotnet tool restore --tool-manifest (Join-Path $repositoryRoot '.config\dotnet-tools.json') } 'Obfuscar tool restore'
}

$buildToolsRootOutput = dotnet msbuild $toolsProject -getProperty:PkgMicrosoft_Windows_SDK_BuildTools -nologo
if ($LASTEXITCODE -ne 0) { throw 'Windows SDK BuildTools path could not be resolved.' }
$buildToolsRootLines = @($buildToolsRootOutput | Where-Object { -not [string]::IsNullOrWhiteSpace($_) })
if ($buildToolsRootLines.Count -eq 0) { throw 'Windows SDK BuildTools path was empty.' }
$buildToolsRoot = $buildToolsRootLines[-1].Trim()
if ([string]::IsNullOrWhiteSpace($buildToolsRoot) -or -not (Test-Path -LiteralPath $buildToolsRoot -PathType Container)) {
    throw "Windows SDK BuildTools path is invalid: $buildToolsRoot"
}
$makeAppx = Get-ChildItem -LiteralPath $buildToolsRoot -Recurse -Filter makeappx.exe |
    Where-Object { $_.FullName -match '[\\/]x64[\\/]makeappx\.exe$' } |
    Select-Object -First 1 -ExpandProperty FullName
if ([string]::IsNullOrWhiteSpace($makeAppx)) {
    throw "MakeAppx.exe was not found under $buildToolsRoot."
}
$makePri = Get-ChildItem -LiteralPath $buildToolsRoot -Recurse -Filter makepri.exe |
    Where-Object { $_.FullName -match '[\\/]x64[\\/]makepri\.exe$' } |
    Select-Object -First 1 -ExpandProperty FullName
if ([string]::IsNullOrWhiteSpace($makePri)) {
    throw "MakePri.exe was not found under $buildToolsRoot."
}

$buildRoot = [IO.Path]::GetFullPath((Join-Path $repositoryRoot ".store-build\$Version"))
$artifactsRoot = [IO.Path]::GetFullPath((Join-Path $repositoryRoot "artifacts\store\$Version"))
foreach ($path in @($buildRoot, $artifactsRoot)) {
    if (-not $path.StartsWith($repositoryRoot, [StringComparison]::OrdinalIgnoreCase)) {
        throw "Unsafe build path: $path"
    }
    if (Test-Path -LiteralPath $path) {
        Remove-Item -LiteralPath $path -Recurse -Force
    }
    New-Item -ItemType Directory -Path $path | Out-Null
}

$manifestTemplate = Get-Content -LiteralPath (Join-Path $repositoryRoot 'packaging\Package.appxmanifest.template') -Raw
$applicationProject = Join-Path $repositoryRoot 'src\BatteryTray\BatteryTray.csproj'
$packages = [Collections.Generic.List[string]]::new()

foreach ($architecture in $Architectures) {
    $runtimeIdentifier = "win-$architecture"
    $architectureRoot = Join-Path $buildRoot $architecture
    $publishRoot = Join-Path $architectureRoot 'publish'
    $layoutRoot = Join-Path $architectureRoot 'layout'
    $appRoot = Join-Path $layoutRoot 'App'
    $assetRoot = Join-Path $layoutRoot 'Assets'
    New-Item -ItemType Directory -Path $appRoot, $assetRoot -Force | Out-Null

    Invoke-Checked {
        dotnet publish $applicationProject -c Release -r $runtimeIdentifier --self-contained true `
            -p:PublishSingleFile=false -p:DebugSymbols=false -p:DebugType=None -p:Version=$Version `
            -o $publishRoot --verbosity minimal
    } "dotnet publish ($architecture)"

    if (-not $SkipObfuscation) {
        $obfuscationRoot = Join-Path $architectureRoot 'obfuscation'
        New-Item -ItemType Directory -Path $obfuscationRoot -Force | Out-Null
        $mapDestination = Join-Path $artifactsRoot "BatteryTray_${Version}_${architecture}_obfuscation-map.txt"
        Invoke-Obfuscation -InputDirectory $publishRoot -WorkingDirectory $obfuscationRoot -MapDestination $mapDestination
    }
    Copy-Item -Path (Join-Path $publishRoot '*') -Destination $appRoot -Recurse -Force

    New-StoreAsset -Path (Join-Path $assetRoot 'StoreLogo.png') -Width 50 -Height 50
    New-StoreAsset -Path (Join-Path $assetRoot 'Square44x44Logo.png') -Width 44 -Height 44
    New-StoreAsset -Path (Join-Path $assetRoot 'Square150x150Logo.png') -Width 150 -Height 150
    New-StoreAsset -Path (Join-Path $assetRoot 'Wide310x150Logo.png') -Width 310 -Height 150
    New-StoreAsset -Path (Join-Path $assetRoot 'Square310x310Logo.png') -Width 310 -Height 310

    $manifest = $manifestTemplate
    $manifest = $manifest.Replace('@@IDENTITY_NAME@@', (ConvertTo-XmlText $IdentityName))
    $manifest = $manifest.Replace('@@PUBLISHER@@', (ConvertTo-XmlText $Publisher))
    $manifest = $manifest.Replace('@@PUBLISHER_DISPLAY_NAME@@', (ConvertTo-XmlText $PublisherDisplayName))
    $manifest = $manifest.Replace('@@DISPLAY_NAME@@', (ConvertTo-XmlText $DisplayName))
    $manifest = $manifest.Replace('@@VERSION@@', $Version)
    $manifest = $manifest.Replace('@@ARCHITECTURE@@', $architecture)
    if ($manifest.Contains('@@')) {
        throw "AppxManifest.xml contains an unresolved template token ($architecture)."
    }
    $manifestPath = Join-Path $layoutRoot 'AppxManifest.xml'
    [IO.File]::WriteAllText($manifestPath, $manifest, [Text.UTF8Encoding]::new($false))

    $priSourceRoot = Join-Path $architectureRoot 'pri-source'
    $priStringsRoot = Join-Path $priSourceRoot 'Strings'
    New-Item -ItemType Directory -Path $priSourceRoot -Force | Out-Null
    Copy-Item -LiteralPath $manifestPath -Destination (Join-Path $priSourceRoot 'AppxManifest.xml')
    Copy-Item -LiteralPath (Join-Path $repositoryRoot 'packaging\Strings') -Destination $priStringsRoot -Recurse
    foreach ($resourceFile in Get-ChildItem -LiteralPath $priStringsRoot -Recurse -Filter Resources.resw) {
        $resourceContent = Get-Content -LiteralPath $resourceFile.FullName -Raw
        $resourceContent = $resourceContent.Replace('TabbyBBMonitor', (ConvertTo-XmlText $DisplayName))
        [IO.File]::WriteAllText($resourceFile.FullName, $resourceContent, [Text.UTF8Encoding]::new($false))
    }

    $priConfig = Join-Path $architectureRoot 'priconfig.xml'
    Invoke-Checked {
        & $makePri createconfig /cf $priConfig /dq en-US /pv 10.0.0 /o | Out-Null
    } 'MakePri createconfig'
    [xml]$priConfigXml = Get-Content -LiteralPath $priConfig -Raw
    $resourcePackagingNode = $priConfigXml.SelectSingleNode('/resources/packaging')
    if ($null -eq $resourcePackagingNode) {
        throw 'MakePri configuration does not contain the expected resource packaging settings.'
    }
    [void]$resourcePackagingNode.ParentNode.RemoveChild($resourcePackagingNode)
    $priConfigXml.Save($priConfig)
    Invoke-Checked {
        & $makePri new /pr $priSourceRoot /cf $priConfig /mn (Join-Path $priSourceRoot 'AppxManifest.xml') `
            /of (Join-Path $layoutRoot 'resources.pri') /o | Out-Null
    } "MakePri new ($architecture)"

    $packagePath = Join-Path $artifactsRoot "BatteryTray_${Version}_${architecture}.msix"
    Invoke-Checked { & $makeAppx pack /d $layoutRoot /p $packagePath /o | Out-Null } "MSIX pack ($architecture)"
    $packages.Add($packagePath)
}

$submissionPackage = $packages[0]
if ($packages.Count -gt 1) {
    $bundleInput = Join-Path $buildRoot 'bundle-input'
    New-Item -ItemType Directory -Path $bundleInput -Force | Out-Null
    foreach ($package in $packages) {
        Copy-Item -LiteralPath $package -Destination $bundleInput
    }
    $submissionPackage = Join-Path $artifactsRoot "BatteryTray_$Version.msixbundle"
    Invoke-Checked { & $makeAppx bundle /d $bundleInput /p $submissionPackage /bv $Version /o | Out-Null } 'MSIX bundle'
}

$uploadContent = Join-Path $buildRoot 'upload-content'
New-Item -ItemType Directory -Path $uploadContent -Force | Out-Null
Copy-Item -LiteralPath $submissionPackage -Destination $uploadContent
$uploadZip = Join-Path $artifactsRoot "BatteryTray_$Version.zip"
Compress-Archive -Path (Join-Path $uploadContent '*') -DestinationPath $uploadZip -CompressionLevel Optimal
$uploadPath = Join-Path $artifactsRoot "BatteryTray_$Version.msixupload"
Move-Item -LiteralPath $uploadZip -Destination $uploadPath

Write-Output ''
Write-Output 'Microsoft Store package build completed.'
Write-Output "Store upload: $uploadPath"
Write-Output "Package:      $submissionPackage"
Write-Output "Obfuscation:  $(if ($SkipObfuscation) { 'disabled' } else { 'enabled' })"
Write-Output 'The package is intentionally unsigned. Microsoft Store re-signs submitted MSIX packages.'
