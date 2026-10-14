[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$PackagePath,
    [string]$SettingsPath = (Join-Path $PSScriptRoot '../packaging/store-settings.json'),
    [Parameter(Mandatory)][ValidatePattern('^[A-Za-z0-9]+$')][string]$ApplicationId,
    [ValidateRange(1, 900)][int]$PollTimeoutSeconds = 900,
    [ValidateRange(1, 15)][int]$PollIntervalSeconds = 15
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
# Never let HTTP diagnostics print credentials, response bodies or SAS URLs.
$VerbosePreference = 'SilentlyContinue'
$DebugPreference = 'SilentlyContinue'
$phase = 'local validation'
$submissionId = $null
$creationAttempted = $false
$status = 'NotStarted'
$temporaryDirectory = $null
$releaseTag = 'unknown'
$script:accessToken = $null
$script:failureReason = $null
$script:httpDiagnostic = $null
$script:requestSecrets = [Collections.Generic.List[string]]::new()

function Stop-StoreSubmission {
    param([string]$Reason)
    # Only call this with fixed messages from this script, never server text.
    $script:failureReason = $Reason
    throw $Reason
}

function Protect-StoreDiagnosticText {
    param([string]$Text)
    if ([string]::IsNullOrWhiteSpace($Text)) { return '' }
    if ($Text.Length -gt 32768) { return '[message omitted: too long]' }
    $sensitive = [Collections.Generic.List[string]]::new()
    foreach ($value in @($env:MSSTORE_CLIENT_SECRET, $script:accessToken, $env:MSSTORE_TENANT_ID,
        $env:MSSTORE_CLIENT_ID, $ApplicationId, $env:GH_TOKEN, $env:GITHUB_TOKEN) + @($script:requestSecrets)) {
        if (-not [string]::IsNullOrWhiteSpace($value)) { $sensitive.Add($value) }
    }
    # Learn labelled credentials before removing URLs, so repeated unlabelled
    # copies of a SAS signature or bearer token are redacted too.
    $credentialPattern = '(?i)\b(?:client[ _-]?secret|access[ _-]?token|refresh[ _-]?token|id[ _-]?token|password|sig|signature|sharedaccesssignature|api[ _-]?key|token|secret)\b(?:\s*[:=]\s*|\s+)["'']?([^\s"''<>;&]+)'
    foreach ($match in [regex]::Matches($Text, $credentialPattern)) {
        $sensitive.Add($match.Groups[1].Value)
        $sensitive.Add([Uri]::UnescapeDataString($match.Groups[1].Value))
    }
    foreach ($match in [regex]::Matches($Text, '(?i)\bBearer\s+([^\s"''<>;,]+)')) { $sensitive.Add($match.Groups[1].Value) }
    foreach ($value in ($sensitive | Select-Object -Unique | Sort-Object Length -Descending)) {
        foreach ($representation in @($value, [Uri]::EscapeDataString($value), [Net.WebUtility]::HtmlEncode($value))) {
            $Text = [regex]::Replace($Text, [regex]::Escape($representation), '[REDACTED]', [Text.RegularExpressions.RegexOptions]::IgnoreCase)
        }
    }
    $Text = [regex]::Replace($Text, $credentialPattern, '[credential REDACTED]')
    $Text = [regex]::Replace($Text, '(?i)\bBearer\s+[^\s"''<>;,]+', 'Bearer [REDACTED]')
    $Text = [regex]::Replace($Text, '(?i)\b(?:https?|ftp)://[^\s<>"'']+|\bwww\.[^\s<>"'']+', '[URL REDACTED]')
    $Text = [regex]::Replace($Text, '\b[A-Za-z0-9_-]{10,}\.[A-Za-z0-9_-]{10,}\.[A-Za-z0-9_-]{10,}\b', '[JWT REDACTED]')
    $Text = [regex]::Replace($Text, '(?i)\b[0-9a-f]{8}(?:-[0-9a-f]{4}){3}-[0-9a-f]{12}\b', '[ID REDACTED]')
    $Text = [regex]::Replace($Text, '(?i)\b[A-Z0-9._%+-]+@[A-Z0-9.-]+\.[A-Z]{2,}\b', '[EMAIL REDACTED]')
    $Text = [regex]::Replace($Text, '[\x00-\x1f\x7f]+', ' ').Replace('::', ': :').Trim()
    # Encode HTML before putting server text in a Markdown summary.
    $Text = [Net.WebUtility]::HtmlEncode($Text)
    if ($Text.Length -gt 600) { $Text = $Text.Substring(0, 600) + ' [truncated]' }
    return $Text
}

function Get-StoreHttpDiagnostic {
    param([System.Management.Automation.ErrorRecord]$Failure)
    $parts = [Collections.Generic.List[string]]::new()
    $responseProperty = $Failure.Exception.PSObject.Properties['Response']
    if ($responseProperty -and $responseProperty.Value) {
        $response = $responseProperty.Value
        $statusProperty = $response.PSObject.Properties['StatusCode']
        if ($statusProperty) {
            try {
                $number = [int]$statusProperty.Value
                if ($number -ge 100 -and $number -le 599) { $parts.Add("HTTP $number") }
            } catch { } # An unexpected status representation must not hide the original failure.
        }
    }
    # Extract only known JSON error fields. Free-form exception text, response
    # bodies and headers are never printed. Message fields are redacted below.
    if ($Failure.ErrorDetails -and $Failure.ErrorDetails.Message) {
        try {
            $details = $Failure.ErrorDetails.Message | ConvertFrom-Json -AsHashtable -ErrorAction Stop
            $candidates = @()
            if ($details -is [Collections.IDictionary]) {
                $errorObjects = @($details)
                if ($details['error'] -is [string]) { $candidates += $details['error'] }
                if ($details['error'] -is [Collections.IDictionary]) {
                    $errorObjects += $details['error']
                    $errorObjects += @($details['error']['details'])
                }
                $errorObjects += @($details['errors'])
                $messages = @()
                foreach ($item in $errorObjects) {
                    if ($item -is [Collections.IDictionary]) {
                        $candidates += $item['code'], $item['errorCode']
                        foreach ($field in @('message', 'error_description')) {
                            if ($item[$field] -is [string]) { $messages += $item[$field] }
                        }
                    }
                }
                foreach ($aadCode in @($details['error_codes'])) {
                    if ([string]$aadCode -match '^\d{5,8}$') { $candidates += "AADSTS$aadCode" }
                }
                $codes = @($candidates | Where-Object {
                    $candidate = $_
                    $safe = $candidate -is [string] -and $candidate -cmatch '^[A-Za-z][A-Za-z0-9_.]{0,79}$'
                    if ($safe) { $safe = (Protect-StoreDiagnosticText $candidate) -ceq $candidate }
                    $safe
                } | Select-Object -Unique -First 5)
                if ($codes.Count) { $parts.Add('Microsoft code: ' + ($codes -join ', ')) }
                $safeMessage = Protect-StoreDiagnosticText (($messages | Select-Object -Unique -First 3) -join ' | ')
                if ($safeMessage) { $parts.Add('Microsoft message (redacted): ' + $safeMessage) }
            }
        } catch {
            # Invalid or unexpected response bodies are deliberately omitted.
        }
    }
    if ($parts.Count -eq 0) { return 'HTTP status / Microsoft code unavailable (network error or unstructured response).' }
    return $parts -join '; '
}

function Invoke-SubmissionHttp {
    param([hashtable]$Request)
    # Track SAS signatures from the current request without ever logging its URI.
    $uri = [Uri]$Request.Uri
    foreach ($pair in $uri.Query.TrimStart('?').Split('&')) {
        $field = $pair.Split('=', 2)
        if ($field.Count -eq 2 -and $field[0] -in @('sig', 'signature', 'token', 'access_token', 'client_secret')) {
            $script:requestSecrets.Add($field[1])
            $script:requestSecrets.Add([Uri]::UnescapeDataString($field[1]))
        }
    }
    try { Invoke-RestMethod @Request }
    catch {
        $script:httpDiagnostic = Get-StoreHttpDiagnostic $_
        # The outer handler emits only this diagnostic and fixed local messages.
        Stop-StoreSubmission 'Submission HTTP request failed.'
    }
}

function Invoke-StoreRequest {
    param([string]$Method, [string]$Path, $Body, [int]$TimeoutSeconds = 120)
    $request = @{
        Method = $Method
        Uri = "https://manage.devcenter.microsoft.com/v1.0/my/$Path"
        Headers = @{ Authorization = "Bearer $script:accessToken" }
        ErrorAction = 'Stop'
        TimeoutSec = $TimeoutSeconds
        Verbose = $false
        Debug = $false
    }
    if ($null -ne $Body) {
        $request.ContentType = 'application/json; charset=utf-8'
        $request.Body = [Text.Encoding]::UTF8.GetBytes(($Body | ConvertTo-Json -Depth 100 -Compress))
    }
    $response = Invoke-SubmissionHttp $request
    if ($null -ne $response) { $response | ConvertTo-Json -Depth 100 | ConvertFrom-Json -AsHashtable }
}

try {
    foreach ($name in @('MSSTORE_TENANT_ID', 'MSSTORE_CLIENT_ID', 'MSSTORE_CLIENT_SECRET')) {
        if ([string]::IsNullOrWhiteSpace([Environment]::GetEnvironmentVariable($name))) {
            Stop-StoreSubmission 'Required authentication configuration is missing.'
        }
    }
    $tenantId = [Guid]::Parse($env:MSSTORE_TENANT_ID).ToString()
    $null = [Guid]::Parse($env:MSSTORE_CLIENT_ID)
    $settings = Get-Content -LiteralPath $SettingsPath -Raw | ConvertFrom-Json -AsHashtable
    $versionText = [string]$settings.Version
    $version = $null
    if ($versionText -notmatch '^\d+\.\d+\.\d+\.0$' -or
        -not [Version]::TryParse($versionText, [ref]$version) -or
        $version.Major -lt 1 -or $version.Major -gt 65535 -or
        $version.Minor -gt 65535 -or $version.Build -gt 65535 -or
        $version.ToString(4) -ne $versionText) {
        Stop-StoreSubmission 'Invalid Store version.'
    }
    $releaseTag = "v$versionText"
    $package = Get-Item -LiteralPath $PackagePath
    if ($package.PSIsContainer -or $package.Extension -ne '.msixbundle' -or $package.Length -eq 0) {
        Stop-StoreSubmission 'A nonempty MSIX bundle is required.'
    }
    # Check the downloaded asset itself rather than trusting its filename.
    $bundle = [IO.Compression.ZipFile]::OpenRead($package.FullName)
    try {
        $entry = $bundle.GetEntry('AppxMetadata/AppxBundleManifest.xml')
        if ($null -eq $entry) { Stop-StoreSubmission 'Bundle manifest is missing.' }
        $reader = [IO.StreamReader]::new($entry.Open())
        try { [xml]$manifest = $reader.ReadToEnd() } finally { $reader.Dispose() }
        $identity = $manifest.Bundle.Identity
        if ($identity.Name -cne $settings.IdentityName -or
            $identity.Publisher -cne $settings.Publisher -or
            $identity.Version -ne $versionText) { Stop-StoreSubmission 'Bundle identity or version mismatch.' }
    } finally { $bundle.Dispose() }

    $phase = 'authentication'
    $token = Invoke-SubmissionHttp @{
        Method = 'Post'
        Uri = "https://login.microsoftonline.com/$tenantId/oauth2/token"
        ContentType = 'application/x-www-form-urlencoded'
        Body = @{
            grant_type = 'client_credentials'
            client_id = $env:MSSTORE_CLIENT_ID
            client_secret = $env:MSSTORE_CLIENT_SECRET
            resource = 'https://manage.devcenter.microsoft.com'
        }
        TimeoutSec = 120; ErrorAction = 'Stop'; Verbose = $false; Debug = $false
    }
    $script:accessToken = [string]$token.access_token
    if ([string]::IsNullOrWhiteSpace($script:accessToken)) { Stop-StoreSubmission 'Token is missing.' }
    if ($env:GITHUB_ACTIONS -eq 'true') { Write-Host "::add-mask::$script:accessToken" }

    $phase = 'application preflight (check Identity, Publisher, published version and pending submissions)'
    $appPath = "applications/$ApplicationId"
    $app = Invoke-StoreRequest GET $appPath
    if ($app.packageIdentityName -cne $settings.IdentityName -or
        $app.publisherName -cne $settings.Publisher) { Stop-StoreSubmission 'Partner Center identity mismatch.' }
    if ($app['pendingApplicationSubmission'] -and $app.pendingApplicationSubmission.id) {
        Stop-StoreSubmission 'An existing submission must be resolved in Partner Center.'
    }
    if (-not $app['lastPublishedApplicationSubmission'] -or -not $app.lastPublishedApplicationSubmission.id) {
        Stop-StoreSubmission 'Complete the initial publication in Partner Center first.'
    }
    $published = Invoke-StoreRequest GET "$appPath/submissions/$($app.lastPublishedApplicationSubmission.id)"
    if (@($published.applicationPackages).Count -eq 0) { Stop-StoreSubmission 'No published packages found.' }
    foreach ($oldPackage in $published.applicationPackages) {
        if ($version -le [Version]$oldPackage.version) { Stop-StoreSubmission 'Increase the package version before submission.' }
    }

    $temporaryDirectory = Join-Path ([IO.Path]::GetTempPath()) ("tabby-store-submit-" + [Guid]::NewGuid().ToString('N'))
    $null = New-Item -ItemType Directory -Path $temporaryDirectory
    $zipPath = Join-Path $temporaryDirectory 'submission.zip'
    $zip = [IO.Compression.ZipFile]::Open($zipPath, [IO.Compression.ZipArchiveMode]::Create)
    try {
        $null = [IO.Compression.ZipFileExtensions]::CreateEntryFromFile($zip, $package.FullName, $package.Name)
    } finally { $zip.Dispose() }

    $phase = 'submission creation'
    # A lost POST response may still leave a submission behind; never retry this POST automatically.
    $creationAttempted = $true
    $submission = Invoke-StoreRequest POST "$appPath/submissions"
    $phase = 'submission creation response validation'
    if (-not $submission -or [string]$submission['id'] -notmatch '^\d+$') { Stop-StoreSubmission 'Invalid or missing submission ID in the creation response.' }
    $submissionId = [string]$submission.id
    Write-Host "Created Store submission $submissionId ($releaseTag)."
    $uploadUri = $null
    if (-not [Uri]::TryCreate([string]$submission['fileUploadUrl'], [UriKind]::Absolute, [ref]$uploadUri) -or
        $uploadUri.Scheme -ne 'https' -or -not $uploadUri.Host.EndsWith('.blob.core.windows.net')) {
        Stop-StoreSubmission 'Invalid or unexpected upload endpoint in the creation response.'
    }
    if ($env:GITHUB_ACTIONS -eq 'true') { Write-Host "::add-mask::$($submission.fileUploadUrl)" }
    $submissionPath = "$appPath/submissions/$submissionId"
    # Convert the cloned response to a hashtable to retain nested metadata and arrays.
    $update = $submission | ConvertTo-Json -Depth 100 | ConvertFrom-Json -AsHashtable
    foreach ($oldPackage in $update.applicationPackages) { $oldPackage.fileStatus = 'PendingDelete' }
    $update.applicationPackages = @($update.applicationPackages) + @(@{
        fileName = $package.Name
        fileStatus = 'PendingUpload'
        minimumDirectXVersion = 'None'
        minimumSystemRam = 'None'
    })
    $phase = 'submission update'
    $null = Invoke-StoreRequest PUT $submissionPath $update

    $phase = 'package upload'
    $null = Invoke-SubmissionHttp @{
        Method = 'Put'; Uri = $uploadUri; InFile = $zipPath
        ContentType = 'application/zip'; Headers = @{ 'x-ms-blob-type' = 'BlockBlob' }
        TimeoutSec = 600; ErrorAction = 'Stop'; Verbose = $false; Debug = $false
    }

    $phase = 'submission commit'
    $null = Invoke-StoreRequest POST "$submissionPath/commit"
    $phase = 'submission acceptance polling'
    $timer = [Diagnostics.Stopwatch]::StartNew()
    $acceptedStates = @('PreProcessing', 'Certification', 'PendingPublication', 'Publishing', 'Published', 'Release')
    while ($true) {
        $remainingSeconds = $PollTimeoutSeconds - $timer.Elapsed.TotalSeconds
        if ($remainingSeconds -le 0) { Stop-StoreSubmission 'Acceptance polling timed out.' }
        $result = Invoke-StoreRequest GET "$submissionPath/status" -TimeoutSeconds ([int][Math]::Max(1, [Math]::Min(120, [Math]::Ceiling($remainingSeconds))))
        $status = if ([string]$result.status -in ($acceptedStates + @('CommitStarted', 'CommitFailed', 'PreProcessingFailed', 'CertificationFailed', 'PublishFailed', 'ReleaseFailed', 'Canceled', 'PendingCommit', 'None'))) {
            [string]$result.status
        } else { 'Unknown' }
        if ($status -in $acceptedStates) { break }
        if ($status -ne 'CommitStarted') { Stop-StoreSubmission 'Submission failed or returned an unexpected status.' }
        if ($timer.Elapsed.TotalSeconds -ge $PollTimeoutSeconds) { Stop-StoreSubmission 'Acceptance polling timed out.' }
        Start-Sleep -Seconds ([Math]::Min($PollIntervalSeconds, [Math]::Max(1, [Math]::Ceiling($PollTimeoutSeconds - $timer.Elapsed.TotalSeconds))))
    }
    Write-Host "Store submission $submissionId accepted: $status ($releaseTag)."
} catch {
    # Do not forward the original exception: HTTP errors can include secrets and SAS URLs.
    $diagnostic = if ($script:httpDiagnostic) { $script:httpDiagnostic }
        elseif ($script:failureReason) { $script:failureReason }
        else { 'Unexpected local error: ' + $_.Exception.GetType().Name + ' at script line ' + $_.InvocationInfo.ScriptLineNumber + '.' }
    $guidance = if ($creationAttempted) {
        'Check the pending submission in Partner Center; resolve it or delete it if appropriate before rerunning only the submit job. Do not edit API-created submissions in Partner Center and then attempt to commit them via API.'
    } else {
        'Check authentication settings, package identity, published version and existing submissions; then rerun only the submit job.'
    }
    if ($env:GITHUB_STEP_SUMMARY) {
        "Store submission failed during $phase. Diagnostic: $diagnostic Release: $releaseTag. Submission ID: $submissionId. Last status: $status. $guidance GitHub Release is retained." >> $env:GITHUB_STEP_SUMMARY
    }
    throw "Store submission failed during $phase. Diagnostic: $diagnostic Release: $releaseTag. Submission ID: $submissionId. Last status: $status. $guidance GitHub Release is retained."
} finally {
    $script:accessToken = $null
    if ($temporaryDirectory -and (Test-Path -LiteralPath $temporaryDirectory)) {
        # Only remove the unique directory created above under the system temp directory.
        $resolved = [IO.Path]::GetFullPath($temporaryDirectory)
        $tempRoot = [IO.Path]::GetFullPath([IO.Path]::GetTempPath()).TrimEnd([IO.Path]::DirectorySeparatorChar) + [IO.Path]::DirectorySeparatorChar
        if ($resolved.StartsWith($tempRoot, [StringComparison]::OrdinalIgnoreCase) -and
            [IO.Path]::GetFileName($resolved) -match '^tabby-store-submit-[0-9a-f]{32}$') {
            Remove-Item -LiteralPath $resolved -Recurse -Force
        }
    }
}
if ($env:GITHUB_STEP_SUMMARY) {
    "Store submission accepted. Release: $releaseTag. Submission ID: $submissionId. Status: $status. Certification and publication continue under the existing Partner Center settings." >> $env:GITHUB_STEP_SUMMARY
}
