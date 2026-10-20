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
- 起動・再接続・残量不明からの復帰時にも低下・危険の機器を通知し、同じ残量帯の通知は1時間抑制
- 機器別の通知条件（すべての閾値変化・危険のみ・通知しない）と、全体・機器別の1時間／8時間の通知休止
- 最後に確認できた残量と確認日時を端末内に保存し、切断・残量不明時にも前回情報を表示
- 設定を `%LOCALAPPDATA%\BatteryTray\settings.json` に保存
- Microsoft Store版は起動時・ヘルプのボタンから更新を確認し、利用者の操作で更新

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

## 電池通知の休止と前回残量

トレイの「電池通知」から全体の電池通知を1時間・8時間休止、または再開できます。設定画面のデバイス詳細では、機器別の通知条件と同じ休止操作を設定し、「保存」で適用します。監視ON/OFFと通知条件は独立しています。全体の通知OFF、監視OFF、機器別の「通知しない」、全体または機器別の休止中は電池通知を表示しません。「危険のみ」では起動・再接続時も危険帯だけを通知します。

休止中も監視と残量表示は継続します。休止期限は再起動後も維持し、期限切れ・手動再開後の次の取得成功時に低残量を再判定します。同じ残量帯の起動・再接続・再開通知は1時間以内に繰り返さず、危険帯への悪化は通知します。通常の閾値変化通知は継続し、複数機器の同時通知はまとめて表示します。更新通知と取得エラー通知は電池通知の休止対象外です。

一覧の残量欄と詳細には、最後に確認できた残量と日時を表示します。切断・残量不明時は「前回」と明示し、現在の電池状態判定には使いません。切断中の機器は「未検出も表示」から確認できます。トレイでも、接続中に残量不明となった機器の前回情報を表示します。確認日時はアプリがWindowsから値を取得した時刻で、機器自体の測定時刻ではありません。

通知条件・休止期限は`settings.json`へ、機器ごとの最新1件の残量・確認日時と通知重複抑制用の状態は`%LOCALAPPDATA%\BatteryTray\monitor-state.json`へ保存します。取得エラー・切断・残量不明で前回情報を上書きしません。状態ファイルが破損した場合は保存済み情報なしで監視を継続します。保存に失敗した場合は通知し、監視を継続しますが再起動後に情報が失われる場合があります。設定・監視状態をすべて消去するにはアプリを終了し、両ファイルを削除してください。

## アプリの更新

Microsoft Storeからインストールした製品版は、起動時に一度、Storeで配信可能な更新を非同期で確認します。更新があると通知し、通知をクリックすると設定画面の「ヘルプ」が開きます。「ヘルプ」の「更新をチェック」でも確認できます。起動時に最新だった場合や確認に失敗した場合は通知せず、ヘルプに状態を表示します。

更新がある場合は「更新する」を選択し、確認画面で承認すると、Microsoft Store経由でダウンロード・インストールを開始します。進捗はヘルプに表示します。適用時にアプリが終了する場合があるため、設定変更は先に保存してください。インストール要求中は設定画面を閉じられません。キャンセル・失敗時は監視を継続し、再試行できます。更新でアプリが終了した場合はWindowsが自動で起動し直し、設定画面を開かず監視を再開します。起動直後の更新では、自動再起動の準備に最大60秒待機します。

通常exe実行版、テスト署名・サイドロード版ではこの機能は非対応です。GitHub Releaseの添付物を自己更新に使用しません。二重起動による画面表示では再チェックせず、定期チェックやアプリ独自の自動インストールは行いません。

検証は `dotnet test .\BatteryTray.slnx -c Release` で更新サービスのモックテスト・画面テストを実行できます。実際のStore更新は、Storeから取得した旧版と、その利用者に配信可能な新版を用意して、起動時通知、ヘルプからの確認、同意後の更新、設定の維持を別途確認してください。ローカルの未パッケージ実行やテスト署名MSIXでは実更新を検証できません。

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

Releaseビルド・単体テストの成功後、正式なStore Identityを使用してx64／ARM64をビルドします。x64ではMSIXから展開した実行ファイルで起動時メニューの検証を行い、失敗した場合は公開を停止します。Releaseには各アーキテクチャの `.msix` 2ファイル、`.msixbundle`、`.msixupload` の計4ファイルを添付します。中間ファイルは公開しません。添付物は未署名のStore提出用であり、そのままインストールするための署名済み配布物ではありません。ビルド・Release公開はここで完了し、Store提出は別の「Submit Store Release」で実行します。

同名タグまたはRelease（下書きを含む）が存在すると、上書きせず失敗します。次の公開前にバージョンを更新してください。アップロード中に失敗した場合は下書きが残ることがあります。その場合は内容を確認し、不要な下書きと同名タグを手動で削除してから再実行するか、バージョンを更新します。全添付物のアップロード完了後にのみ正式公開します。

認証にはGitHub標準の `GITHUB_TOKEN` と `contents: write` を使用し、追加のPATや署名用証明書は不要です。タグ保護ルールなどで作成が制限されている場合は、そのルールに従って実行権限を設定してください。

### Microsoft Storeへの自動提出の設定

公開済みアプリの更新が対象です。初回公開と年齢区分の入力はPartner Centerで完了しておいてください。自動提出には[Microsoft Store Submission API](https://learn.microsoft.com/en-us/windows/uwp/monetize/manage-app-submissions)を使用します。

1. [Microsoftの認証設定手順](https://learn.microsoft.com/en-us/windows/uwp/monetize/create-and-manage-submissions-using-windows-store-services)に従い、Partner CenterアカウントにMicrosoft Entraテナントを関連付けます。
2. Partner Centerの「アカウント設定 > ユーザー」から自動提出用Entraアプリケーションを追加し、`Manager`ロールを付与します。Tenant ID、Client IDを控え、クライアントシークレット（キー）を作成します。キーの値は作成時に保存し、有効期限前に更新してください。
3. GitHubリポジトリの「Settings > Environments > ms-store」で次の値を設定します。Environmentがない場合は`ms-store`という名前で作成してください。提出ジョブはこのEnvironmentのSecretsとVariablesを参照します。

| 種類 | 名前 | 値 |
| --- | --- | --- |
| Environment Secret | `MSSTORE_TENANT_ID` | EntraのTenant ID（GUID） |
| Environment Secret | `MSSTORE_CLIENT_ID` | 自動提出用アプリケーションのClient ID（GUID） |
| Environment Secret | `MSSTORE_CLIENT_SECRET` | クライアントシークレットの**値**（シークレットIDではありません） |
| Environment SecretまたはVariable | `MSSTORE_APP_ID` | 提出対象製品のStore ID（例: `9N…`）。IdentityNameやEntraのClient IDとは別の値です。Secretを優先し、未設定ならVariableを使用します |

`ms-store`に承認者やブランチ制限がある場合、提出ジョブはその保護ルールに従います。Repository Secretsへの再登録は不要です。

4. `packaging/store-settings.json`のIdentity・PublisherをPartner Centerと一致させ、Versionを公開済みパッケージより増やします。
5. 新しいビルドが必要な場合は「Actions > Publish Store Release > Run workflow」でビルド・GitHub Release公開を実行します。公開済みReleaseを提出する場合はこの工程は不要で、バージョン更新や再ビルドも不要です。
6. 「Actions > Submit Store Release > Run workflow」で、提出ツールを実行するブランチは通常`main`を選びます。`release_tag`を空欄にするとGitHubでLatestに指定された最新の正式Releaseを提出します。過去版を提出するときだけReleaseタグ（例: `v1.1.1.0`）を入力してください。既存パッケージをそのReleaseのx64／ARM64 bundleで置き換え、審査へ提出します。

提出ワークフローはビルドやGitHub Release作成を行いません。最新Releaseは開始時に一度だけ解決し、確定したタグの公開済みbundleと、同じタグの`packaging/store-settings.json`を取得して照合するため、実行中に新Releaseが公開されても提出対象は変わりません。main側のVersionが先に進んでいても以前のReleaseを提出できます。提出スクリプトは実行時に選択したブランチのコミットを使用します。下書き・プレリリース・タグとVersionが一致しないReleaseは提出できません。

掲載文、画像、リリースノート、価格、段階配信、審査後の公開方法・日時は最後の公開済み申請から引き継ぎます。`Immediate`の場合は審査後に自動公開され、`Manual`の場合は公開操作が引き続き必要です。ワークフローは受付状態を15秒間隔、最大15分間確認し、`PreProcessing`などの正常な受付状態になれば成功します。Store審査の完了・公開完了はPartner Centerで確認してください。

Identity・Publisher不一致、進行中申請の存在、未公開アプリ、バージョン不足の場合は申請作成前に停止します。トークン、クライアントシークレット、アップロード用SAS URLはエラー詳細やSummaryに出力しません。失敗時はログとSummaryに処理段階、Releaseタグ、取得済みの申請ID、受付状態、HTTPステータス、Microsoftの構造化エラーコード（EntraのAADSTSコードを含む）を記録します。JSONの既知のエラーフィールドに説明文があれば、`Microsoft message (redacted)`として記録します。説明文内の既知の認証値（URLエンコードされた値を含む）、ラベル付き認証値、URL、JWT、GUID、メールアドレスを除去し、改行・HTMLを無害化して最大600文字に制限します。生の応答本文や例外メッセージは出力しません。HTTP応答がない場合は取得できなかった旨を表示し、ローカル検証エラーには固定の理由、それ以外には例外の種類と行番号を記録します。申請作成APIの失敗と、作成成功後の応答検証の失敗も区別します。

提出に失敗してもGitHub Releaseは残ります。申請作成前の失敗は原因を修正して「Submit Store Release」を同じタグで再実行してください。SecretsやVariablesだけを修正した場合は「Re-run failed jobs」も利用できます。ワークフローやスクリプトを修正した場合は、更新後のmainを選んで新しく実行します。申請作成後（作成要求の応答を受信できなかった場合も含む）はPartner Centerで残った申請と状態を確認し、進行中なら完了を待ち、不要な失敗申請・下書きなら削除してから提出を再実行してください。スクリプトは申請を自動削除・上書きしません。commit済み申請の受付確認だけがタイムアウトした場合は、まずPartner Centerで受付済みか確認してください。

APIで作成した申請をPartner Center画面で編集すると、その後APIで更新・commitできなくなる場合があります。画面から編集した申請はそのまま画面で提出するか、不要なら削除して新しく作成してください。API非対応機能によるエラーが出た場合もPartner Centerで提出します。

提出処理のHTTPモックテスト（認証情報・ネットワーク接続不要）は、PowerShell 7で実行できます。PR、main／masterへのpush、Release公開前にも実行します。

```powershell
pwsh -NoLogo -NoProfile -File .\tests\StoreSubmission\Test-SubmitStorePackage.ps1
```

### MSIXパッケージテスト

テスト専用の一意なIdentityを使ってx64／ARM64のMSIX、bundle、msixuploadを生成し、マニフェスト、StartupTask、capability、自己完結ランタイム、18言語の`resources.pri`を展開検査できます。x64はパッケージ内の実行ファイルによる起動時メニュー検証も行います。通常テストは証明書ストアやインストール済みアプリを変更しません。

```powershell
pwsh -NoLogo -NoProfile -File .\tests\MSIX\Test-StorePackage.ps1
```

PRと`main`／`master`へのpushでは、GitHub Actionsの「MSIX Tests」がReleaseビルド、xUnit、上記のMSIX検査を実行します。テスト署名とサイドロード検査はGitHub-hosted Windows runnerだけで実行し、ローカルでは生成しません。

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

### 配布成果物の起動時検証

`Build-StorePackage.ps1`はx64 MSIXの生成後に、パッケージから展開した`BatteryTray.exe --startup-smoke-test`を実行します。この内部検証モードはWinForms初期化と通常起動と共通の通知メニュー生成を行い、18言語の表示および1時間休止・8時間休止・再開を検証します。成功時は終了コード0、失敗時は診断情報と非0を返し、30秒以内に終了しない場合もビルドを停止します。

検証モードは常駐処理、既存プロセスへの通知、Bluetooth検索、Store更新、設定ファイルの読み書きを行いません。MSIXのインストールや証明書変更も不要です。ARM64はパッケージ内容を検証し、ARM64実機での起動確認とは区別します。通常のアプリ起動全体やWindows通知の表示を自動検証するものではありません。

### ビルド時の設定指定

CIなどで設定ファイルを変更しない場合は、値を引数でも指定できます。

```powershell
pwsh -NoLogo -NoProfile -File .\scripts\Build-StorePackage.ps1 `
  -IdentityName 'PartnerCenter.IdentityName' `
  -Publisher 'CN=PartnerCenterPublisherId' `
  -PublisherDisplayName 'Publisher name' `
  -Version '1.0.1.0'
```

マニフェストにはデスクトップアプリに必要な `runFullTrust` と、Bluetoothアクセス用の `bluetooth` capabilityを宣言しています。Partner Centerの「申請オプション」で、`runFullTrust` は「ユーザー権限で動作するWinFormsの通知領域アプリとしてBluetooth電池情報を監視するため」と説明してください。
