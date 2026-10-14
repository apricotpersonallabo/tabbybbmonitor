# Dependency-free HTTP mocks: this test must never make a network request.
[CmdletBinding()]
param()
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$submitScript = Join-Path $PSScriptRoot '../../scripts/Submit-StorePackage.ps1'
$testRoot = Join-Path ([IO.Path]::GetTempPath()) ('tabby-submit-tests-' + [Guid]::NewGuid().ToString('N'))
$null = New-Item -ItemType Directory -Path $testRoot
$settingsPath = Join-Path $testRoot 'settings.json'
$packagePath = Join-Path $testRoot 'BatteryTray_1.1.0.0.msixbundle'
$summaryPath = Join-Path $testRoot 'summary.md'
$mockState = @{}
$savedEnvironment = @{}
foreach ($name in @('MSSTORE_TENANT_ID', 'MSSTORE_CLIENT_ID', 'MSSTORE_CLIENT_SECRET', 'GITHUB_ACTIONS', 'GITHUB_STEP_SUMMARY')) {
    $savedEnvironment[$name] = [Environment]::GetEnvironmentVariable($name)
}

function Assert-True([bool]$Condition, [string]$Message) {
    if (-not $Condition) { throw "Assertion failed: $Message" }
}

function New-TestBundle([string]$Version = '1.1.0.0') {
    if (Test-Path -LiteralPath $packagePath) { Remove-Item -LiteralPath $packagePath }
    $archive = [IO.Compression.ZipFile]::Open($packagePath, [IO.Compression.ZipArchiveMode]::Create)
    try {
        $entry = $archive.CreateEntry('AppxMetadata/AppxBundleManifest.xml')
        $writer = [IO.StreamWriter]::new($entry.Open())
        try { $writer.Write("<Bundle><Identity Name='Test.Tabby' Publisher='CN=Test' Version='$Version'/></Bundle>") }
        finally { $writer.Dispose() }
    } finally { $archive.Dispose() }
}

function Invoke-RestMethod {
    [CmdletBinding()]
    param($Method, $Uri, $ContentType, $Body, $Headers, $TimeoutSec, $InFile)
    $path = ([Uri]$Uri).AbsolutePath
    $mockState.calls.Add("$Method $path")
    if ($path.StartsWith('/v1.0/my/')) {
        Assert-True ($ContentType -eq 'application/json; charset=utf-8') 'Store API requires JSON Content-Type even for bodyless requests'
        if ($Method -eq 'POST') { Assert-True ($null -eq $Body) 'creation and commit POSTs have no request body' }
    }
    $httpCode = 0
    $payload = $null
    if ($path.EndsWith('/token') -and $mockState.scenario -eq 'auth-http') {
        $httpCode = 401
        $payload = @{ error = 'invalid_client'; error_codes = @(7000215); error_description = 'Invalid client secret: TEST-CLIENT-SECRET. Token TEST-ACCESS-TOKEN. URL https://test.blob.core.windows.net/upload?sig=TEST-SAS' } | ConvertTo-Json
    }
    if ($Method -eq 'POST' -and $path.EndsWith('/submissions')) {
        switch ($mockState.scenario) {
            'create-http-forbidden' { $httpCode = 403; $payload = @{ error = @{ code = 'AccessDenied'; message = 'Manager role is required. TEST-CLIENT-SECRET TEST-ACCESS-TOKEN sig=TEST-SAS https://test.blob.core.windows.net/upload?sig=TEST-SAS' } } | ConvertTo-Json }
            'create-http-conflict' { $httpCode = 409; $payload = @{ errors = @(@{ code = 'InvalidState'; message = 'Resolve the pending submission. sig=TEST-SAS' }, @{ errorCode = 'Conflict' }) } | ConvertTo-Json }
            'create-http-unstructured' { $httpCode = 500; $payload = 'TEST-CLIENT-SECRET TEST-ACCESS-TOKEN https://test.blob.core.windows.net/upload?sig=TEST-SAS' }
            'create-http-unsafe-code' { $httpCode = 400; $payload = @{ code = 'TEST-ACCESS-TOKEN'; errorCode = 'https://test.blob.core.windows.net/upload?sig=TEST-SAS' } | ConvertTo-Json }
            'create-http-code-secret' { $httpCode = 400; $payload = @{ code = 'PrefixClientSecret123'; errorCode = 'ClientSecret123' } | ConvertTo-Json }
            'create-http-message' {
                $httpCode = 400
                $payload = @{ code = 'InvalidParameterValue'; message = "Parameter applicationId is invalid. TEST-CLIENT-SECRET TEST-ACCESS-TOKEN 9TEST `r`n::warning:: <script> https://example.com/?sig=TEST-SAS TEST-SAS customer@example.com 33333333-3333-3333-3333-333333333333" } | ConvertTo-Json
            }
            'create-http-encoded-message' {
                $httpCode = 400
                $encodedSecret = [Uri]::EscapeDataString($env:MSSTORE_CLIENT_SECRET)
                $payload = @{ error = @{ code = 'InvalidParameterValue'; details = @(@{ code = 'InvalidField'; message = "Parameter package is invalid. $encodedSecret Bearer UnknownBearerToken123 sig=UnknownSignature123 UnknownSignature123" }) } } | ConvertTo-Json -Depth 6
            }
            'create-http-long-message' { $httpCode = 400; $payload = @{ code = 'InvalidParameterValue'; message = ('Invalid parameter. ' * 100) + ' TEST-CLIENT-SECRET' } | ConvertTo-Json }
        }
    }
    if ($InFile -and $mockState.scenario -eq 'upload-http') {
        $httpCode = 403
        $payload = '<Error><Code>AuthenticationFailed</Code><Message>TEST-SAS TEST-CLIENT-SECRET</Message></Error>'
    }
    if ($httpCode) {
        $response = [Net.Http.HttpResponseMessage]::new([Net.HttpStatusCode]$httpCode)
        $exception = [Microsoft.PowerShell.Commands.HttpResponseException]::new('TEST-CLIENT-SECRET TEST-ACCESS-TOKEN TEST-SAS', $response)
        $record = [Management.Automation.ErrorRecord]::new($exception, 'MockHttpFailure', [Management.Automation.ErrorCategory]::InvalidOperation, $Uri)
        $record.ErrorDetails = [Management.Automation.ErrorDetails]::new($payload)
        throw $record
    }
    if (($mockState.scenario -eq 'auth-failure' -and $path.EndsWith('/token')) -or
        ($mockState.scenario -eq 'upload-failure' -and $InFile) -or
        ($mockState.scenario -eq 'commit-failure' -and $path.EndsWith('/commit')) -or
        ($mockState.scenario -eq 'create-failure' -and $Method -eq 'POST' -and $path.EndsWith('/submissions'))) {
        throw 'HTTP secret=TEST-CLIENT-SECRET token=TEST-ACCESS-TOKEN https://test.blob.core.windows.net/upload?sig=TEST-SAS'
    }
    if ($path.EndsWith('/token')) {
        Assert-True ($Body.resource -eq 'https://manage.devcenter.microsoft.com') 'token audience'
        return @{ access_token = 'TEST-ACCESS-TOKEN' }
    }
    Assert-True ($InFile -or $Headers.Authorization -eq 'Bearer TEST-ACCESS-TOKEN') 'API bearer token'
    if ($path.EndsWith('/applications/9TEST')) {
        $app = @{
            packageIdentityName = 'Test.Tabby'; publisherName = 'CN=Test'
            lastPublishedApplicationSubmission = @{ id = '100' }
            pendingApplicationSubmission = $null
        }
        switch ($mockState.scenario) {
            'identity-mismatch' { $app.packageIdentityName = 'Other.App' }
            'publisher-mismatch' { $app.publisherName = 'CN=Other' }
            'pending' { $app.pendingApplicationSubmission = @{ id = '123' } }
            'unpublished' { $app.Remove('lastPublishedApplicationSubmission') }
            'missing-pending-field' { $app.Remove('pendingApplicationSubmission') }
        }
        return $app
    }
    if ($path.EndsWith('/submissions/100')) {
        $oldVersion = switch ($mockState.scenario) { 'same-version' { '1.1.0.0' } 'older-version' { '1.2.0.0' } default { '1.0.0.0' } }
        return @{ applicationPackages = @(@{ version = $oldVersion }) }
    }
    if ($path.EndsWith('/submissions') -and $Method -eq 'POST') {
        $created = @{
            id = '200'; fileUploadUrl = 'https://test.blob.core.windows.net/upload?sig=TEST-SAS'
            applicationPackages = @(@{ fileName = 'old.msixbundle'; fileStatus = 'Uploaded'; version = '1.0.0.0'; id = '101' })
            listings = @{ 'ja-jp' = @{ baseListing = @{ description = 'Keep description'; images = @(@{ fileName = 'existing.png'; fileStatus = 'Uploaded' }) } } }
            pricing = @{ priceId = 'Free' }; targetPublishMode = 'Manual'; targetPublishDate = '2026-12-01T00:00:00Z'
            packageDeliveryOptions = @{ packageRollout = @{ isPackageRollout = $true; packageRolloutPercentage = 20 } }
        }
        switch ($mockState.scenario) {
            'create-missing-id' { $created.Remove('id') }
            'create-invalid-id' { $created.id = 'TEST-SAS' }
            'create-missing-upload' { $created.Remove('fileUploadUrl') }
            'create-unexpected-upload' { $created.fileUploadUrl = 'https://example.com/upload?sig=TEST-SAS' }
        }
        return $created
    }
    if ($path.EndsWith('/submissions/200') -and $Method -eq 'PUT') {
        $mockState.update = [Text.Encoding]::UTF8.GetString($Body) | ConvertFrom-Json -AsHashtable
        return @{}
    }
    if ($InFile) {
        Assert-True ($Headers['x-ms-blob-type'] -eq 'BlockBlob') 'upload blob type'
        $archive = [IO.Compression.ZipFile]::OpenRead($InFile)
        try {
            Assert-True ($archive.Entries.Count -eq 1) 'upload ZIP contains only bundle'
            Assert-True ($archive.Entries[0].FullName -eq 'BatteryTray_1.1.0.0.msixbundle') 'ZIP filename matches API'
            Assert-True ($archive.Entries[0].Length -eq (Get-Item -LiteralPath $packagePath).Length) 'bundle bytes preserved'
        } finally { $archive.Dispose() }
        $mockState.uploadPath = $InFile
        return $null
    }
    if ($path.EndsWith('/commit')) { return $null }
    if ($path.EndsWith('/status')) {
        $mockState.polls++
        $state = switch ($mockState.scenario) {
            'commit-rejected' { 'CommitFailed' }
            'certification-rejected' { 'CertificationFailed' }
            'unknown-status' { 'TEST-CLIENT-SECRET TEST-SAS TEST-ACCESS-TOKEN' }
            'timeout' { 'CommitStarted' }
            default { if ($mockState.polls -eq 1) { 'CommitStarted' } else { 'PreProcessing' } }
        }
        return @{ status = $state }
    }
    throw "Unexpected mock request: $Method $path"
}

try {
    $env:MSSTORE_TENANT_ID = '11111111-1111-1111-1111-111111111111'
    $env:MSSTORE_CLIENT_ID = '22222222-2222-2222-2222-222222222222'
    $env:MSSTORE_CLIENT_SECRET = 'TEST-CLIENT-SECRET'
    $env:GITHUB_ACTIONS = 'false'
    $env:GITHUB_STEP_SUMMARY = $summaryPath
    @{ IdentityName = 'Test.Tabby'; Publisher = 'CN=Test'; Version = '1.1.0.0' } | ConvertTo-Json | Set-Content -LiteralPath $settingsPath
    $scenarios = @('success', 'missing-pending-field', 'missing-auth', 'auth-failure', 'bundle-mismatch', 'identity-mismatch', 'publisher-mismatch', 'pending', 'unpublished', 'same-version', 'older-version', 'create-failure', 'upload-failure', 'commit-failure', 'commit-rejected', 'certification-rejected', 'unknown-status', 'timeout', 'auth-http', 'create-http-forbidden', 'create-http-conflict', 'create-http-unstructured', 'create-http-unsafe-code', 'create-http-code-secret', 'upload-http', 'create-missing-id', 'create-invalid-id', 'create-missing-upload', 'create-unexpected-upload', 'create-http-message', 'create-http-encoded-message', 'create-http-long-message')
    foreach ($scenario in $scenarios) {
        $mockState.scenario = $scenario
        $mockState.calls = [Collections.Generic.List[string]]::new()
        $mockState.update = $null
        $mockState.polls = 0
        $mockState.uploadPath = $null
        Set-Content -LiteralPath $summaryPath -Value ''
        $env:MSSTORE_CLIENT_SECRET = switch ($scenario) { 'missing-auth' { '' } 'create-http-code-secret' { 'ClientSecret123' } 'create-http-encoded-message' { 'SECRET+/=&VALUE' } default { 'TEST-CLIENT-SECRET' } }
        New-TestBundle -Version $(if ($scenario -eq 'bundle-mismatch') { '1.0.0.0' } else { '1.1.0.0' })
        $failure = $null
        $captured = @()
        $timeout = if ($scenario -eq 'timeout') { 1 } else { 10 }
        try { $captured = @(& $submitScript -PackagePath $packagePath -SettingsPath $settingsPath -ApplicationId '9TEST' -PollTimeoutSeconds $timeout -PollIntervalSeconds 1 6>&1) }
        catch { $failure = $_.Exception.Message }
        $expectSuccess = $scenario -in @('success', 'missing-pending-field')
        Assert-True (($null -eq $failure) -eq $expectSuccess) "$scenario outcome: $failure"
        $text = ($captured | Out-String) + $failure + (Get-Content -LiteralPath $summaryPath -Raw)
        foreach ($secret in @('TEST-CLIENT-SECRET', 'TEST-ACCESS-TOKEN', 'TEST-SAS', 'ClientSecret123', 'SECRET+/=&VALUE', 'SECRET%2B%2F%3D%26VALUE', 'UnknownBearerToken123', 'UnknownSignature123', 'customer@example.com', '33333333-3333-3333-3333-333333333333', '<script>', '::warning::', 'https://example.com/')) {
            Assert-True (-not $text.Contains($secret)) "$scenario does not log test value $secret"
        }
        $expectedDiagnostics = @{
            'auth-http' = @('HTTP 401', 'Microsoft code: invalid_client, AADSTS7000215')
            'create-http-forbidden' = @('HTTP 403', 'Microsoft code: AccessDenied', 'during submission creation.')
            'create-http-conflict' = @('HTTP 409', 'Microsoft code: InvalidState, Conflict')
            'create-http-unstructured' = @('HTTP 500')
            'create-http-unsafe-code' = @('HTTP 400')
            'create-http-code-secret' = @('HTTP 400')
            'create-http-message' = @('HTTP 400', 'Microsoft code: InvalidParameterValue', 'Microsoft message (redacted): Parameter applicationId is invalid.', '[REDACTED]')
            'create-http-encoded-message' = @('HTTP 400', 'Microsoft code: InvalidParameterValue, InvalidField', 'Parameter package is invalid.', '[REDACTED]')
            'create-http-long-message' = @('HTTP 400', 'Invalid parameter.', '[truncated]')
            'upload-http' = @('HTTP 403', 'during package upload.')
            'create-missing-id' = @('creation response validation', 'Invalid or missing submission ID')
            'create-invalid-id' = @('creation response validation', 'Invalid or missing submission ID')
            'create-missing-upload' = @('creation response validation', 'Invalid or unexpected upload endpoint', 'Submission ID: 200')
            'create-unexpected-upload' = @('creation response validation', 'Invalid or unexpected upload endpoint', 'Submission ID: 200')
        }
        if ($expectedDiagnostics.ContainsKey($scenario)) {
            $summary = Get-Content -LiteralPath $summaryPath -Raw
            foreach ($expected in $expectedDiagnostics[$scenario]) {
                Assert-True ($failure.Contains($expected) -and $summary.Contains($expected)) "$scenario diagnostic in error and summary: $expected"
            }
        }
        if ($expectSuccess) {
            $expectedCalls = @('Post /11111111-1111-1111-1111-111111111111/oauth2/token', 'GET /v1.0/my/applications/9TEST', 'GET /v1.0/my/applications/9TEST/submissions/100', 'POST /v1.0/my/applications/9TEST/submissions', 'PUT /v1.0/my/applications/9TEST/submissions/200', 'Put /upload', 'POST /v1.0/my/applications/9TEST/submissions/200/commit', 'GET /v1.0/my/applications/9TEST/submissions/200/status', 'GET /v1.0/my/applications/9TEST/submissions/200/status')
            Assert-True (($mockState.calls -join '|') -ceq ($expectedCalls -join '|')) 'request order'
            Assert-True ($mockState.update.applicationPackages.Count -eq 2) 'replace packages'
            Assert-True ($mockState.update.applicationPackages[0].fileStatus -eq 'PendingDelete') 'delete old package'
            Assert-True ($mockState.update.applicationPackages[1].fileStatus -eq 'PendingUpload') 'upload new package'
            Assert-True ($mockState.update.listings['ja-jp'].baseListing.description -eq 'Keep description') 'keep listing'
            Assert-True ($mockState.update.listings['ja-jp'].baseListing.images[0].fileStatus -eq 'Uploaded') 'keep images'
            Assert-True ($mockState.update.pricing.priceId -eq 'Free') 'keep pricing'
            Assert-True ($mockState.update.targetPublishMode -eq 'Manual') 'keep publish mode'
            Assert-True ([DateTimeOffset]$mockState.update.targetPublishDate -eq [DateTimeOffset]'2026-12-01T00:00:00Z') 'keep publish date'
            Assert-True ($mockState.update.packageDeliveryOptions.packageRollout.packageRolloutPercentage -eq 20) 'keep rollout'
        }
        if ($scenario -in @('missing-auth', 'auth-failure', 'bundle-mismatch', 'identity-mismatch', 'publisher-mismatch', 'pending', 'unpublished', 'same-version', 'older-version')) {
            Assert-True (-not ($mockState.calls -contains 'POST /v1.0/my/applications/9TEST/submissions')) 'preflight never creates submission'
        }
        Assert-True (@($mockState.calls | Where-Object { $_ -eq 'POST /v1.0/my/applications/9TEST/submissions' }).Count -le 1) 'no duplicate creation'
        Assert-True (-not ($mockState.calls | Where-Object { $_ -like 'DELETE *' })) 'never auto-delete submissions'
        if ($mockState.uploadPath) { Assert-True (-not (Test-Path -LiteralPath $mockState.uploadPath)) 'temporary ZIP removed' }
        Write-Host "PASS: $scenario"
    }
    Write-Host "$($scenarios.Count) Store submission scenarios passed (mock HTTP only)."
} finally {
    foreach ($name in $savedEnvironment.Keys) { [Environment]::SetEnvironmentVariable($name, $savedEnvironment[$name]) }
    $resolved = [IO.Path]::GetFullPath($testRoot)
    $tempRoot = [IO.Path]::GetFullPath([IO.Path]::GetTempPath()).TrimEnd([IO.Path]::DirectorySeparatorChar) + [IO.Path]::DirectorySeparatorChar
    if ($resolved.StartsWith($tempRoot, [StringComparison]::OrdinalIgnoreCase) -and
        [IO.Path]::GetFileName($resolved) -match '^tabby-submit-tests-[0-9a-f]{32}$') {
        Remove-Item -LiteralPath $resolved -Recurse -Force
    }
}
