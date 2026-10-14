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

初回起動時は設定画面の「デバイス」タブが自動的に開き、Bluetoothデバイスを検索します。対象機器が見つからない場合も監視を開始でき、あとから「再検出」を実行できます。MSIX版の「自動起動」は既定でONで、「全般」タブの「有効」チェックボックスで変更できます。Windows側で無効化された設定や管理者の制限は維持します。通常実行版でも項目は表示しますが、自動起動は非対応のため操作できません。

Windowsのアプリ一覧・スタートメニューから手動起動すると、設定済みでも設定画面を表示します。既に常駐中なら同じ設定画面を前面に出し、残量を再取得します。サインイン時の自動起動では、初回設定が完了していれば設定画面を開かずトレイに常駐します。

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

### GitHub Releaseへの公開

「Actions > Publish Store Release > Run workflow」でビルド対象のブランチを選択し、手動実行します。`packaging/store-settings.json` の `Version` を使い、実行時のコミットにバージョンタグ（例: `v1.1.0.0`）を付けて、正式なRelease `TabbyBBMonitor 1.1.0.0` を公開します。ワークフローを追加しただけでは公開されません。

Releaseビルド・単体テストの成功後、正式なStore Identityと既定の難読化を使用してx64／ARM64をビルドします。Releaseには各アーキテクチャの `.msix` 2ファイル、`.msixbundle`、`.msixupload` の計4ファイルを添付します。難読化対応表と中間ファイルは公開しません。添付物は未署名のStore提出用であり、そのままインストールするための署名済み配布物ではありません。Release公開後、専用の `submit` ジョブが公開済みの `.msixbundle` を取得し、Microsoft Storeの審査へ自動提出します。以下の認証設定が必要です。

同名タグまたはRelease（下書きを含む）が存在すると、上書きせず失敗します。次の公開前にバージョンを更新してください。アップロード中に失敗した場合は下書きが残ることがあります。その場合は内容を確認し、不要な下書きと同名タグを手動で削除してから再実行するか、バージョンを更新します。全添付物のアップロード完了後にのみ正式公開します。

認証にはGitHub標準の `GITHUB_TOKEN` と `contents: write` を使用し、追加のPATや署名用証明書は不要です。タグ保護ルールなどで作成が制限されている場合は、そのルールに従って実行権限を設定してください。

### Microsoft Storeへの自動提出の設定

公開済みアプリの更新が対象です。初回公開と年齢区分の入力はPartner Centerで完了しておいてください。自動提出には[Microsoft Store Submission API](https://learn.microsoft.com/en-us/windows/uwp/monetize/manage-app-submissions)を使用します。

1. [Microsoftの認証設定手順](https://learn.microsoft.com/en-us/windows/uwp/monetize/create-and-manage-submissions-using-windows-store-services)に従い、Partner CenterアカウントにMicrosoft Entraテナントを関連付けます。
2. Partner Centerの「アカウント設定 > ユーザー」から自動提出用Entraアプリケーションを追加し、`Manager`ロールを付与します。Tenant ID、Client IDを控え、クライアントシークレット（キー）を作成します。キーの値は作成時に保存し、有効期限前に更新してください。
3. GitHubリポジトリの「Settings > Secrets and variables > Actions」で次の値を設定します。

| 種類 | 名前 | 値 |
| --- | --- | --- |
| Secret | `MSSTORE_TENANT_ID` | EntraのTenant ID（GUID） |
| Secret | `MSSTORE_CLIENT_ID` | 自動提出用アプリケーションのClient ID（GUID） |
| Secret | `MSSTORE_CLIENT_SECRET` | クライアントシークレットの**値**（シークレットIDではありません） |
| Variable | `MSSTORE_APP_ID` | 提出対象製品のStore ID（例: `9N…`）。IdentityNameやEntraのClient IDとは別の値です |

4. `packaging/store-settings.json`のIdentity・PublisherをPartner Centerと一致させ、Versionを公開済みパッケージより増やします。
5. 「Actions > Publish Store Release > Run workflow」を実行します。GitHub Release公開後にStore申請を作成し、既存パッケージを新しいx64／ARM64 bundleで置き換え、審査へ提出します。

掲載文、画像、リリースノート、価格、段階配信、審査後の公開方法・日時は最後の公開済み申請から引き継ぎます。`Immediate`の場合は審査後に自動公開され、`Manual`の場合は公開操作が引き続き必要です。ワークフローは受付状態を15秒間隔、最大15分間確認し、`PreProcessing`などの正常な受付状態になれば成功します。Store審査の完了・公開完了はPartner Centerで確認してください。

Identity・Publisher不一致、進行中申請の存在、未公開アプリ、バージョン不足の場合は申請作成前に停止します。トークン、クライアントシークレット、アップロード用SAS URLはエラー詳細やSummaryに出力しません。SummaryにはReleaseタグ・申請ID・受付状態を記録します。

提出に失敗してもGitHub Releaseは残ります。申請作成前の失敗は原因を修正して「Re-run failed jobs」で `submit` ジョブだけを再実行してください。全ジョブを再実行すると既存タグの検査で失敗します。申請作成後（作成要求の応答を受信できなかった場合も含む）はPartner Centerで残った申請と状態を確認し、進行中なら完了を待ち、不要な失敗申請・下書きなら削除してから提出ジョブだけ再実行してください。スクリプトは申請を自動削除・上書きしません。commit済み申請の受付確認だけがタイムアウトした場合は、まずPartner Centerで受付済みか確認してください。

APIで作成した申請をPartner Center画面で編集すると、その後APIで更新・commitできなくなる場合があります。画面から編集した申請はそのまま画面で提出するか、不要なら削除して新しく作成してください。API非対応機能によるエラーが出た場合もPartner Centerで提出します。

提出処理のHTTPモックテスト（認証情報・ネットワーク接続不要）は、PowerShell 7で実行できます。PR、masterへのpush、Release公開前にも実行します。

```powershell
pwsh -NoLogo -NoProfile -File .\tests\StoreSubmission\Test-SubmitStorePackage.ps1
```

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
