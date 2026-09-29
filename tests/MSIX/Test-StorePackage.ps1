#Requires -Version 7.0

[CmdletBinding()]
param(
    [switch]$GitHubActionsSideload,
    [switch]$KeepArtifacts
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

if (-not $IsWindows) {
    throw 'MSIX package tests can only run on Windows.'
}

if ($GitHubActionsSideload) {
    if (-not [string]::Equals($env:GITHUB_ACTIONS, 'true', [StringComparison]::OrdinalIgnoreCase)) {
        throw 'The -GitHubActionsSideload test can only run in GitHub Actions. No package or certificate changes were made.'
    }
    $currentIdentity = [Security.Principal.WindowsIdentity]::GetCurrent()
    $principal = [Security.Principal.WindowsPrincipal]::new($currentIdentity)
    if (-not $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
        throw 'The GitHub Actions sideload test requires an administrator runner. No package or certificate changes were made.'
    }
}

$repositoryRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..'))
$expectedLanguages = @(
    'ar-SA', 'de-DE', 'en-US', 'es-ES', 'fr-FR', 'hi-IN', 'id-ID', 'it-IT', 'ja-JP',
    'ko-KR', 'pl-PL', 'pt-BR', 'ru-RU', 'th-TH', 'tr-TR', 'vi-VN', 'zh-Hans', 'zh-Hant'
)
$expectedAssets = @(
    'Square150x150Logo.png',
    'Square310x310Logo.png',
    'Square44x44Logo.png',
    'StoreLogo.png',
    'Wide310x150Logo.png'
)

function Assert-True {
    param(
        [Parameter(Mandatory)][bool]$Condition,
        [Parameter(Mandatory)][string]$Message
    )

    if (-not $Condition) {
        throw "Assertion failed: $Message"
    }
}

function Assert-Equal {
    param(
        [AllowNull()][object]$Expected,
        [AllowNull()][object]$Actual,
        [Parameter(Mandatory)][string]$Message
    )

    if (-not [string]::Equals([string]$Expected, [string]$Actual, [StringComparison]::Ordinal)) {
        throw "Assertion failed: $Message. Expected '$Expected', actual '$Actual'."
    }
}

function Assert-SetEqual {
    param(
        [Parameter(Mandatory)][string[]]$Expected,
        [Parameter(Mandatory)][string[]]$Actual,
        [Parameter(Mandatory)][string]$Message
    )

    $expectedSet = @($Expected | Sort-Object -Unique)
    $actualSet = @($Actual | Sort-Object -Unique)
    $difference = @(Compare-Object -ReferenceObject $expectedSet -DifferenceObject $actualSet)
    if ($difference.Count -gt 0) {
        $details = $difference | ForEach-Object { "$($_.SideIndicator) $($_.InputObject)" }
        throw "Assertion failed: $Message. Difference: $($details -join ', ')."
    }
}

function Invoke-NativeChecked {
    param(
        [Parameter(Mandatory)][scriptblock]$Command,
        [Parameter(Mandatory)][string]$Description
    )

    $commandOutput = @(& $Command 2>&1)
    $exitCode = $LASTEXITCODE
    if ($exitCode -ne 0) {
        throw "$Description failed with exit code $exitCode.$([Environment]::NewLine)$($commandOutput -join [Environment]::NewLine)"
    }
}

function Resolve-WindowsSdkTool {
    param([Parameter(Mandatory)][string]$Name)

    $toolsProject = Join-Path $repositoryRoot 'build\StorePackaging\StorePackaging.csproj'
    $buildToolsOutput = dotnet msbuild $toolsProject -getProperty:PkgMicrosoft_Windows_SDK_BuildTools -nologo
    if ($LASTEXITCODE -ne 0) {
        throw 'Windows SDK BuildTools path could not be resolved.'
    }

    $buildToolsRootLines = @($buildToolsOutput | Where-Object { -not [string]::IsNullOrWhiteSpace($_) })
    Assert-True ($buildToolsRootLines.Count -gt 0) 'Windows SDK BuildTools path was returned by MSBuild'
    $buildToolsRoot = $buildToolsRootLines[-1].Trim()
    Assert-True (Test-Path -LiteralPath $buildToolsRoot -PathType Container) "Windows SDK BuildTools directory exists ($buildToolsRoot)"

    $tool = Get-ChildItem -LiteralPath $buildToolsRoot -Recurse -Filter "$Name.exe" |
        Where-Object { $_.FullName -match "[\\/]x64[\\/]$([regex]::Escape($Name))\.exe$" } |
        Select-Object -First 1 -ExpandProperty FullName
    Assert-True (-not [string]::IsNullOrWhiteSpace($tool)) "$Name.exe was found under $buildToolsRoot"
    return $tool
}

function New-NamespaceManager {
    param(
        [Parameter(Mandatory)][xml]$Document,
        [Parameter(Mandatory)][hashtable]$Namespaces
    )

    $manager = [Xml.XmlNamespaceManager]::new($Document.NameTable)
    foreach ($entry in $Namespaces.GetEnumerator()) {
        $manager.AddNamespace([string]$entry.Key, [string]$entry.Value)
    }
    return ,$manager
}

function Test-PriResources {
    param(
        [Parameter(Mandatory)][string]$PriPath,
        [Parameter(Mandatory)][string]$DumpPath,
        [Parameter(Mandatory)][string]$IdentityName,
        [Parameter(Mandatory)][string]$DisplayName,
        [Parameter(Mandatory)][string]$MakePriPath
    )

    Assert-True (Test-Path -LiteralPath $PriPath -PathType Leaf) "resources.pri exists ($PriPath)"
    Assert-True ((Get-Item -LiteralPath $PriPath).Length -gt 0) "resources.pri is not empty ($PriPath)"
    Invoke-NativeChecked {
        & $MakePriPath dump /if $PriPath /of $DumpPath /dt Detailed /o
    } "MakePri dump ($PriPath)"

    [xml]$priDump = Get-Content -LiteralPath $DumpPath -Raw
    $resourceMap = $priDump.SelectSingleNode('/PriInfo/ResourceMap')
    Assert-True ($null -ne $resourceMap) 'PRI contains a primary resource map'
    Assert-Equal $IdentityName $resourceMap.GetAttribute('name') 'PRI resource map uses the package identity'

    $languageNodes = @($priDump.SelectNodes("//Qualifier[@name='Language']"))
    $languages = @($languageNodes | ForEach-Object { $_.GetAttribute('value') } | Sort-Object -Unique)
    Assert-SetEqual $expectedLanguages $languages 'PRI contains the complete language set'

    foreach ($resourceName in @('AppDisplayName', 'AppDescription')) {
        $resource = $priDump.SelectSingleNode("//ResourceMapSubtree[@name='Resources']/NamedResource[@name='$resourceName']")
        Assert-True ($null -ne $resource) "PRI contains $resourceName"
        $candidates = @($resource.SelectNodes('Candidate'))
        Assert-Equal '18' ([string]$candidates.Count) "$resourceName has one candidate per language"

        $candidateLanguages = [Collections.Generic.List[string]]::new()
        foreach ($candidate in $candidates) {
            $qualifier = $candidate.SelectSingleNode("QualifierSet/Qualifier[@name='Language']")
            $value = $candidate.SelectSingleNode('Value')
            Assert-True ($null -ne $qualifier) "$resourceName candidate has a language qualifier"
            Assert-True ($null -ne $value -and -not [string]::IsNullOrWhiteSpace($value.InnerText)) "$resourceName candidate has a value"
            $candidateLanguages.Add($qualifier.GetAttribute('value'))
            if ($resourceName -eq 'AppDisplayName') {
                Assert-Equal $DisplayName $value.InnerText 'Localized display name uses the test display name'
            }
        }
        Assert-SetEqual $expectedLanguages $candidateLanguages.ToArray() "$resourceName covers all languages"
    }
}

function Test-MsixPackage {
    param(
        [Parameter(Mandatory)][string]$PackagePath,
        [Parameter(Mandatory)][string]$Architecture,
        [Parameter(Mandatory)][string]$UnpackRoot,
        [Parameter(Mandatory)][string]$IdentityName,
        [Parameter(Mandatory)][string]$Publisher,
        [Parameter(Mandatory)][string]$Version,
        [Parameter(Mandatory)][string]$DisplayName,
        [Parameter(Mandatory)][string]$MakeAppxPath,
        [Parameter(Mandatory)][string]$MakePriPath
    )

    Assert-True (Test-Path -LiteralPath $PackagePath -PathType Leaf) "$Architecture MSIX exists"
    Invoke-NativeChecked {
        & $MakeAppxPath unpack /p $PackagePath /d $UnpackRoot /o
    } "MakeAppx unpack ($Architecture)"

    $manifestPath = Join-Path $UnpackRoot 'AppxManifest.xml'
    Assert-True (Test-Path -LiteralPath $manifestPath -PathType Leaf) "$Architecture manifest exists"
    $manifestText = Get-Content -LiteralPath $manifestPath -Raw
    Assert-True (-not $manifestText.Contains('@@')) "$Architecture manifest has no unresolved template token"
    [xml]$manifest = $manifestText
    $namespaces = New-NamespaceManager $manifest @{
        f = 'http://schemas.microsoft.com/appx/manifest/foundation/windows10'
        uap = 'http://schemas.microsoft.com/appx/manifest/uap/windows10'
        desktop = 'http://schemas.microsoft.com/appx/manifest/desktop/windows10'
        rescap = 'http://schemas.microsoft.com/appx/manifest/foundation/windows10/restrictedcapabilities'
    }

    $identity = $manifest.SelectSingleNode('/f:Package/f:Identity', $namespaces)
    Assert-Equal $IdentityName $identity.GetAttribute('Name') "$Architecture identity name"
    Assert-Equal $Publisher $identity.GetAttribute('Publisher') "$Architecture publisher"
    Assert-Equal $Version $identity.GetAttribute('Version') "$Architecture version"
    Assert-Equal $Architecture $identity.GetAttribute('ProcessorArchitecture') "$Architecture processor architecture"

    $propertyDisplayName = $manifest.SelectSingleNode('/f:Package/f:Properties/f:DisplayName', $namespaces)
    Assert-Equal 'ms-resource:AppDisplayName' $propertyDisplayName.InnerText "$Architecture package display-name resource"

    $resourceNodes = @($manifest.SelectNodes('/f:Package/f:Resources/f:Resource', $namespaces))
    $manifestLanguages = @($resourceNodes | ForEach-Object { $_.GetAttribute('Language') })
    Assert-SetEqual $expectedLanguages $manifestLanguages "$Architecture manifest language declarations"

    $targetFamily = $manifest.SelectSingleNode('/f:Package/f:Dependencies/f:TargetDeviceFamily', $namespaces)
    Assert-Equal 'Windows.Desktop' $targetFamily.GetAttribute('Name') "$Architecture target device family"
    Assert-Equal '10.0.19041.0' $targetFamily.GetAttribute('MinVersion') "$Architecture minimum Windows version"

    $application = $manifest.SelectSingleNode('/f:Package/f:Applications/f:Application', $namespaces)
    Assert-Equal 'App\BatteryTray.exe' $application.GetAttribute('Executable') "$Architecture application executable"
    Assert-Equal 'Windows.FullTrustApplication' $application.GetAttribute('EntryPoint') "$Architecture application entry point"

    $visualElements = $manifest.SelectSingleNode('/f:Package/f:Applications/f:Application/uap:VisualElements', $namespaces)
    Assert-Equal 'ms-resource:AppDisplayName' $visualElements.GetAttribute('DisplayName') "$Architecture visual display-name resource"
    Assert-Equal 'ms-resource:AppDescription' $visualElements.GetAttribute('Description') "$Architecture visual description resource"

    $startupExtension = $manifest.SelectSingleNode("/f:Package/f:Applications/f:Application/f:Extensions/desktop:Extension[@Category='windows.startupTask']", $namespaces)
    Assert-True ($null -ne $startupExtension) "$Architecture startupTask extension exists"
    Assert-Equal 'App\BatteryTray.exe' $startupExtension.GetAttribute('Executable') "$Architecture startupTask executable"
    $startupTask = $startupExtension.SelectSingleNode('desktop:StartupTask', $namespaces)
    Assert-Equal 'BatteryTrayStartup' $startupTask.GetAttribute('TaskId') "$Architecture startupTask ID"
    Assert-Equal 'false' $startupTask.GetAttribute('Enabled') "$Architecture startupTask is disabled by default"

    $fullTrust = $manifest.SelectSingleNode("/f:Package/f:Capabilities/rescap:Capability[@Name='runFullTrust']", $namespaces)
    $bluetooth = $manifest.SelectSingleNode("/f:Package/f:Capabilities/f:DeviceCapability[@Name='bluetooth']", $namespaces)
    Assert-True ($null -ne $fullTrust) "$Architecture runFullTrust capability exists"
    Assert-True ($null -ne $bluetooth) "$Architecture bluetooth capability exists"

    foreach ($relativePath in @(
        'App\BatteryTray.exe',
        'App\BatteryTray.dll',
        'App\BatteryTray.deps.json',
        'App\coreclr.dll',
        'App\hostfxr.dll'
    )) {
        Assert-True (Test-Path -LiteralPath (Join-Path $UnpackRoot $relativePath) -PathType Leaf) "$Architecture contains $relativePath"
    }
    foreach ($asset in $expectedAssets) {
        Assert-True (Test-Path -LiteralPath (Join-Path $UnpackRoot "Assets\$asset") -PathType Leaf) "$Architecture contains $asset"
    }

    $reswFiles = @(Get-ChildItem -LiteralPath $UnpackRoot -Recurse -Filter *.resw -File)
    Assert-Equal '0' ([string]$reswFiles.Count) "$Architecture package does not include raw RESW files"

    Test-PriResources `
        -PriPath (Join-Path $UnpackRoot 'resources.pri') `
        -DumpPath (Join-Path $UnpackRoot 'resources.pri.xml') `
        -IdentityName $IdentityName `
        -DisplayName $DisplayName `
        -MakePriPath $MakePriPath
}

function Test-Bundle {
    param(
        [Parameter(Mandatory)][string]$BundlePath,
        [Parameter(Mandatory)][string]$UnpackRoot,
        [Parameter(Mandatory)][string]$IdentityName,
        [Parameter(Mandatory)][string]$Publisher,
        [Parameter(Mandatory)][string]$Version,
        [Parameter(Mandatory)][string]$MakeAppxPath
    )

    Assert-True (Test-Path -LiteralPath $BundlePath -PathType Leaf) 'MSIX bundle exists'
    Invoke-NativeChecked {
        & $MakeAppxPath unbundle /p $BundlePath /d $UnpackRoot /o
    } 'MakeAppx unbundle'

    $bundleManifestPath = Join-Path $UnpackRoot 'AppxMetadata\AppxBundleManifest.xml'
    Assert-True (Test-Path -LiteralPath $bundleManifestPath -PathType Leaf) 'Bundle manifest exists'
    [xml]$bundleManifest = Get-Content -LiteralPath $bundleManifestPath -Raw
    $namespaces = New-NamespaceManager $bundleManifest @{
        b = 'http://schemas.microsoft.com/appx/2013/bundle'
    }

    $identity = $bundleManifest.SelectSingleNode('/b:Bundle/b:Identity', $namespaces)
    Assert-Equal $IdentityName $identity.GetAttribute('Name') 'Bundle identity name'
    Assert-Equal $Publisher $identity.GetAttribute('Publisher') 'Bundle publisher'
    Assert-Equal $Version $identity.GetAttribute('Version') 'Bundle version'

    $packages = @($bundleManifest.SelectNodes('/b:Bundle/b:Packages/b:Package', $namespaces))
    Assert-Equal '2' ([string]$packages.Count) 'Bundle contains two application packages'
    $architectures = @($packages | ForEach-Object { $_.GetAttribute('Architecture') })
    Assert-SetEqual @('x64', 'arm64') $architectures 'Bundle contains x64 and ARM64 packages'
    foreach ($package in $packages) {
        $architecture = $package.GetAttribute('Architecture')
        Assert-Equal 'application' $package.GetAttribute('Type') "$architecture bundle package type"
        Assert-Equal $Version $package.GetAttribute('Version') "$architecture bundle package version"
        $fileName = $package.GetAttribute('FileName')
        Assert-True (Test-Path -LiteralPath (Join-Path $UnpackRoot $fileName) -PathType Leaf) "Bundle contains $fileName"
        $resources = @($package.SelectNodes('b:Resources/b:Resource', $namespaces) | ForEach-Object { $_.GetAttribute('Language') })
        Assert-SetEqual $expectedLanguages $resources "$architecture bundle language metadata"
    }
}

function Test-Upload {
    param(
        [Parameter(Mandatory)][string]$UploadPath,
        [Parameter(Mandatory)][string]$BundlePath
    )

    Assert-True (Test-Path -LiteralPath $UploadPath -PathType Leaf) 'MSIX upload exists'
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $archive = [IO.Compression.ZipFile]::OpenRead($UploadPath)
    try {
        $entries = @($archive.Entries | Where-Object { -not [string]::IsNullOrWhiteSpace($_.Name) })
        Assert-Equal '1' ([string]$entries.Count) 'MSIX upload contains exactly one file'
        Assert-Equal ([IO.Path]::GetFileName($BundlePath)) $entries[0].FullName 'MSIX upload contains the generated bundle'
        Assert-Equal ([string](Get-Item -LiteralPath $BundlePath).Length) ([string]$entries[0].Length) 'Uploaded bundle length matches the generated bundle'
    }
    finally {
        $archive.Dispose()
    }
}

function Remove-TestDirectory {
    param(
        [Parameter(Mandatory)][string]$Path,
        [Parameter(Mandatory)][string]$ExpectedParent
    )

    $fullPath = [IO.Path]::GetFullPath($Path)
    $fullParent = [IO.Path]::GetFullPath($ExpectedParent).TrimEnd([IO.Path]::DirectorySeparatorChar) + [IO.Path]::DirectorySeparatorChar
    if (-not $fullPath.StartsWith($fullParent, [StringComparison]::OrdinalIgnoreCase) -or $fullPath.Length -le $fullParent.Length) {
        throw "Refusing to remove an unsafe test path: $fullPath"
    }
    if (Test-Path -LiteralPath $fullPath) {
        Remove-Item -LiteralPath $fullPath -Recurse -Force
    }
}

do {
    $suffix = [guid]::NewGuid().ToString('N').Substring(0, 12)
    $minor = [Convert]::ToUInt16($suffix.Substring(0, 4), 16)
    $build = [Convert]::ToUInt16($suffix.Substring(4, 4), 16)
    if ($minor -eq 0) { $minor = 1 }
    if ($build -eq 0) { $build = 1 }
    $version = "65000.$minor.$build.0"
    $buildRoot = Join-Path $repositoryRoot ".store-build\$version"
    $artifactsRoot = Join-Path $repositoryRoot "artifacts\store\$version"
} while ((Test-Path -LiteralPath $buildRoot) -or (Test-Path -LiteralPath $artifactsRoot))

$identityName = "BatteryTray.Test.$suffix"
$publisher = "CN=BatteryTray MSIX Test $suffix"
$publisherDisplayName = 'BatteryTray MSIX Test'
$displayName = 'TabbyBBMonitor MSIX Test'
$buildScript = Join-Path $repositoryRoot 'scripts\Build-StorePackage.ps1'
$settingsPath = Join-Path $repositoryRoot 'packaging\store-settings.json'
$inspectionRoot = Join-Path $buildRoot 'package-test'
$bundlePath = Join-Path $artifactsRoot "BatteryTray_$version.msixbundle"
$uploadPath = Join-Path $artifactsRoot "BatteryTray_$version.msixupload"
$certificateThumbprint = $null
$testIdentityWasUnused = $false
$testSucceeded = $false
$primaryError = $null
$cleanupErrors = [Collections.Generic.List[string]]::new()

try {
    Write-Host "Building isolated MSIX test package $identityName ($version)..."
    & $buildScript `
        -SettingsPath $settingsPath `
        -IdentityName $identityName `
        -Publisher $publisher `
        -PublisherDisplayName $publisherDisplayName `
        -DisplayName $displayName `
        -Version $version `
        -Architectures @('x64', 'arm64') `
        -SkipObfuscation

    $makeAppx = Resolve-WindowsSdkTool 'makeappx'
    $makePri = Resolve-WindowsSdkTool 'makepri'
    $signTool = if ($GitHubActionsSideload) { Resolve-WindowsSdkTool 'signtool' } else { $null }

    New-Item -ItemType Directory -Path $inspectionRoot -Force | Out-Null
    Test-Bundle `
        -BundlePath $bundlePath `
        -UnpackRoot (Join-Path $inspectionRoot 'bundle') `
        -IdentityName $identityName `
        -Publisher $publisher `
        -Version $version `
        -MakeAppxPath $makeAppx

    foreach ($architecture in @('x64', 'arm64')) {
        Test-MsixPackage `
            -PackagePath (Join-Path $artifactsRoot "BatteryTray_${version}_${architecture}.msix") `
            -Architecture $architecture `
            -UnpackRoot (Join-Path $inspectionRoot $architecture) `
            -IdentityName $identityName `
            -Publisher $publisher `
            -Version $version `
            -DisplayName $displayName `
            -MakeAppxPath $makeAppx `
            -MakePriPath $makePri
    }
    Test-Upload -UploadPath $uploadPath -BundlePath $bundlePath

    if ($GitHubActionsSideload) {
        $existingPackages = @(Get-AppxPackage -Name $identityName | Where-Object { $_.Name -eq $identityName })
        Assert-Equal '0' ([string]$existingPackages.Count) 'The isolated test package identity is not already installed'
        $testIdentityWasUnused = $true

        Write-Host 'Creating and trusting a temporary package-signing certificate...'
        $certificate = New-SelfSignedCertificate `
            -Type Custom `
            -Subject $publisher `
            -FriendlyName "BatteryTray MSIX test $suffix" `
            -KeyUsage DigitalSignature `
            -KeyAlgorithm RSA `
            -KeyLength 2048 `
            -HashAlgorithm SHA256 `
            -CertStoreLocation 'Cert:\CurrentUser\My' `
            -NotAfter (Get-Date).AddDays(14) `
            -TextExtension @(
                '2.5.29.37={text}1.3.6.1.5.5.7.3.3',
                '2.5.29.19={text}'
            )
        $certificateThumbprint = $certificate.Thumbprint
        Assert-Equal $publisher $certificate.Subject 'Certificate subject exactly matches the package publisher'

        $certificateFile = Join-Path $artifactsRoot "BatteryTray_${version}_TestCertificate.cer"
        Export-Certificate -Cert $certificate -FilePath $certificateFile -Force | Out-Null
        $trustedCertificate = Import-Certificate -FilePath $certificateFile -CertStoreLocation 'Cert:\LocalMachine\TrustedPeople'
        Assert-Equal $certificateThumbprint $trustedCertificate.Thumbprint 'Trusted certificate thumbprint matches the signing certificate'

        Write-Host 'Signing and verifying the MSIX bundle...'
        Invoke-NativeChecked {
            & $signTool sign /fd SHA256 /sha1 $certificateThumbprint /s My $bundlePath
        } 'SignTool sign'
        Invoke-NativeChecked {
            & $signTool verify /pa /v $bundlePath
        } 'SignTool verify'

        Write-Host 'Installing the signed test bundle without launching the application...'
        Add-AppxPackage -Path $bundlePath
        $installedPackages = @(Get-AppxPackage -Name $identityName | Where-Object { $_.Name -eq $identityName })
        Assert-Equal '1' ([string]$installedPackages.Count) 'Exactly one isolated test package is installed'
        $installedPackage = $installedPackages[0]
        Assert-Equal $identityName $installedPackage.Name 'Installed package identity'
        Assert-Equal $version $installedPackage.Version.ToString() 'Installed package version'
        Assert-True (Test-Path -LiteralPath $installedPackage.InstallLocation -PathType Container) 'Installed package location exists'
        Assert-True ($installedPackage.Architecture.ToString() -in @('X64', 'Arm64')) 'Installed package selected a supported architecture'
    }

    $testSucceeded = $true
}
catch {
    $primaryError = $_
}
finally {
    if ($GitHubActionsSideload -and $testIdentityWasUnused) {
        try {
            $packagesToRemove = @(Get-AppxPackage -Name $identityName | Where-Object { $_.Name -eq $identityName })
            foreach ($package in $packagesToRemove) {
                Remove-AppxPackage -Package $package.PackageFullName
            }
            $remainingPackages = @(Get-AppxPackage -Name $identityName | Where-Object { $_.Name -eq $identityName })
            if ($remainingPackages.Count -gt 0) {
                throw "The test package is still installed: $identityName"
            }
        }
        catch {
            $cleanupErrors.Add("Package cleanup failed: $($_.Exception.Message)")
        }
    }

    if (-not [string]::IsNullOrWhiteSpace($certificateThumbprint)) {
        foreach ($certificatePath in @(
            "Cert:\LocalMachine\TrustedPeople\$certificateThumbprint",
            "Cert:\CurrentUser\My\$certificateThumbprint"
        )) {
            try {
                if (Test-Path -LiteralPath $certificatePath) {
                    Remove-Item -LiteralPath $certificatePath -Force
                }
                if (Test-Path -LiteralPath $certificatePath) {
                    throw "Certificate still exists at $certificatePath"
                }
            }
            catch {
                $cleanupErrors.Add("Certificate cleanup failed for $certificatePath`: $($_.Exception.Message)")
            }
        }
    }

    if ($testSucceeded -and -not $KeepArtifacts) {
        foreach ($directory in @(
            @{ Path = $buildRoot; Parent = (Join-Path $repositoryRoot '.store-build') },
            @{ Path = $artifactsRoot; Parent = (Join-Path $repositoryRoot 'artifacts\store') }
        )) {
            try {
                Remove-TestDirectory -Path $directory.Path -ExpectedParent $directory.Parent
            }
            catch {
                $cleanupErrors.Add("Artifact cleanup failed for $($directory.Path): $($_.Exception.Message)")
            }
        }
    }
    else {
        Write-Host "Test build retained: $buildRoot"
        Write-Host "Test artifacts retained: $artifactsRoot"
    }
}

if ($null -ne $primaryError) {
    foreach ($cleanupError in $cleanupErrors) {
        Write-Warning $cleanupError
    }
    throw $primaryError
}
if ($cleanupErrors.Count -gt 0) {
    throw ($cleanupErrors -join [Environment]::NewLine)
}

Write-Host ''
Write-Host 'MSIX package tests passed.' -ForegroundColor Green
Write-Host 'Validated: x64 MSIX, ARM64 MSIX, bundle, upload, manifest, StartupTask, capabilities, and 18-language PRI.'
if ($GitHubActionsSideload) {
    Write-Host 'Validated: temporary certificate trust, bundle signature, package installation, and complete cleanup.'
}
