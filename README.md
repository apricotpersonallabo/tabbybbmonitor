# TabbyBBMonitor

Windows が公開する Bluetooth / Bluetooth LE デバイスの電池残量を監視する、.NET 10 WinForms 製のタスクトレイ常駐アプリです。

## 主な機能

- Bluetooth デバイスを30秒ごと（10～3600秒で変更可能）に確認
- Bluetooth Classic / LE デバイスインターフェイスとPnPデバイスノードを統合して列挙
- デバイスごとに3つの閾値（危険・低下・注意）を設定
- デバイス検索、再検出、未検出設定の整理、一括閾値適用
- 新しく検出したデバイス用の共通初期値と通知ON/OFF
- 初回起動時のデバイス検索とセットアップ案内
- ユーザーが選択した場合のサインイン時自動起動
- 危険・低下・注意・良好・不明で異なるトレイアイコンを表示
- 閾値をまたいだときに Windows のバルーン通知を表示
- 設定を `%LOCALAPPDATA%\BatteryTray\settings.json` に保存

## 必要環境

- Windows 10 バージョン 2004 以降、または Windows 11
- .NET 10 Desktop Runtime（フレームワーク依存で配布する場合）
- 電池残量を Windows のデバイスプロパティとして公開する Bluetooth 機器

機器やドライバーが電池残量を Windows に公開しない場合、アプリには「残量不明」と表示されます。Bluetooth GATT Battery Service を公開していても、未接続時には取得できない機器があります。

## ビルドと実行

```powershell
dotnet build .\BatteryTray.slnx -c Release
dotnet run --project .\src\BatteryTray\BatteryTray.csproj
```

初回起動時は設定画面が自動的に開き、ペアリング済みBluetoothデバイスを検索します。対象機器が見つからない場合も監視を開始でき、あとから「再検出」を実行できます。「サインイン時に自動起動する」は既定でOFFで、必要な場合だけ初回画面または「全般」タブで有効にします。

セットアップ後は通知領域に常駐します。Windows 11でアイコンが見えない場合は、タスクバー右端の隠しアイコン（`^`）を確認してください。アイコンのダブルクリック、または右クリックメニューの「設定...」からデバイス別の閾値を変更できます。同じアプリをもう一度起動すると、新しいプロセスは作らず既存の設定画面を表示します。

このアプリが取得できるのは、Windowsとペアリング済みで、機器またはドライバーが電池残量をWindowsへ公開しているデバイスだけです。見つからない場合は初回画面の「Bluetooth設定を開く」からペアリング状態を確認してください。

## 配布用 publish

64-bit Windows 向けの単一ファイル自己完結版:

```powershell
dotnet publish .\src\BatteryTray\BatteryTray.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true
```

出力先は `src\BatteryTray\bin\Release\net10.0-windows10.0.22621.0\win-x64\publish` です。

## Microsoft Store向けMSIX

Store提出用には、x64／ARM64の自己完結型MSIXをまとめた `.msixbundle` と、Partner Centerへアップロードする `.msixupload` を生成できます。Visual StudioやWindows SDKを別途インストールする必要はなく、パッケージツールは公式NuGetパッケージから復元します。

1. Partner Centerでアプリ名を予約します。
2. Partner Centerの「製品管理 > 製品 ID」に表示される値を `packaging\store-settings.json` の `IdentityName`、`Publisher`、`PublisherDisplayName`へ正確に転記します。
3. Store提出ごとに `Version` を増やします。4番目の値は必ず `0` のままにします（例: `1.0.1.0`）。
4. PowerShell 7で次を実行します。

```powershell
pwsh -NoLogo -NoProfile -File .\scripts\Build-StorePackage.ps1
```

提出ファイルは `artifacts\store\<version>\BatteryTray_<version>.msixupload` に生成されます。MSIXはStore側で再署名されるため、提出用ファイルにはローカル証明書による署名を行いません。

### MSIXパッケージテスト

テスト専用の一意なIdentityを使ってx64／ARM64のMSIX、bundle、msixuploadを生成し、マニフェスト、StartupTask、capability、自己完結ランタイム、18言語の`resources.pri`を展開検査できます。通常テストは証明書ストアやインストール済みアプリを変更しません。

```powershell
pwsh -NoLogo -NoProfile -File .\tests\MSIX\Test-StorePackage.ps1
```

PRと`master`へのpushでは、GitHub Actionsの「MSIX Tests」がReleaseビルド、xUnit、上記のMSIX検査を実行します。テスト署名とサイドロード検査はGitHub-hosted Windows runnerだけで実行し、ローカルでは生成しません。

署名済みテストbundleが必要な場合は、GitHubの「Actions > MSIX Tests > Run workflow」から手動実行します。成功したrunの`BatteryTray-MSIX-Test-<run-number>` artifactには、14日間有効な一時証明書で署名した`.msixbundle`と、公開鍵だけを含む`.cer`が保存されます。artifactの保存期間は7日間で、PFX、秘密鍵、Store提出用`.msixupload`は含まれません。

ダウンロードしたbundleを試す場合は、artifactを展開し、管理者として起動したPowerShell 7で次を実行します。同じウィンドウで後片付けまで実行してください。

```powershell
$existingPackages = @(Get-AppxPackage -Name 'BatteryTray.Test.*').PackageFullName
$certificateFile = (Get-ChildItem -Recurse -File -Filter '*_TestCertificate.cer' | Select-Object -First 1).FullName
$bundleFile = (Get-ChildItem -Recurse -File -Filter '*.msixbundle' | Select-Object -First 1).FullName
$certificate = Import-Certificate -FilePath $certificateFile -CertStoreLocation 'Cert:\LocalMachine\TrustedPeople'
Add-AppxPackage -Path $bundleFile
$installedPackages = Get-AppxPackage -Name 'BatteryTray.Test.*' |
    Where-Object { $_.PackageFullName -notin $existingPackages }

# テスト後の後片付け
$installedPackages | ForEach-Object { Remove-AppxPackage -Package $_.PackageFullName }
Remove-Item -LiteralPath "Cert:\LocalMachine\TrustedPeople\$($certificate.Thumbprint)" -Force
```

ローカルMSIX検査の成功時は一時成果物を自動削除します。調査のため残す場合は`-KeepArtifacts`を追加してください。失敗時は診断用に`.store-build\<test-version>`と`artifacts\store\<test-version>`を保持します。

### 難読化

Storeビルドでは、安定版Obfuscarをローカル.NETツールとして復元し、`BatteryTray.dll` に次の保護を自動適用します。

- 内部型、メソッド、フィールド、プロパティ、イベント名の変更
- 文字列定数の隠蔽
- 難読化名をアセンブリ全体で一意化
- 設定JSON用モデルを除外し、アップデート後も既存設定との互換性を維持

設定は `obfuscation\obfuscar.xml.template` にあります。障害解析用の名前対応表は `artifacts\store\<version>\*_obfuscation-map.txt` に保存され、MSIXには含まれません。このファイルは公開せず安全に保管してください。

問題の切り分けなどで一時的に無効化する場合だけ、`-SkipObfuscation` を指定します。

```powershell
pwsh -NoLogo -NoProfile -File .\scripts\Build-StorePackage.ps1 -SkipObfuscation
```

CIなどで設定ファイルを変更しない場合は、値を引数でも指定できます。

```powershell
pwsh -NoLogo -NoProfile -File .\scripts\Build-StorePackage.ps1 `
  -IdentityName 'PartnerCenter.IdentityName' `
  -Publisher 'CN=PartnerCenterPublisherId' `
  -PublisherDisplayName 'Publisher name' `
  -Version '1.0.1.0'
```

マニフェストにはデスクトップアプリに必要な `runFullTrust` と、Bluetoothアクセス用の `bluetooth` capabilityを宣言しています。Partner Centerの「申請オプション」で、`runFullTrust` は「ユーザー権限で動作するWinFormsの通知領域アプリとしてBluetooth電池情報を監視するため」と説明してください。
