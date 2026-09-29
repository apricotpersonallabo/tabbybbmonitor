(() => {
  "use strict";

  const STORAGE_KEY = "battery-tray-docs-language";
  const SUPPORTED_LANGUAGES = new Set(["ja", "en"]);
  // Partner Centerへの提出後、実際のMicrosoft Store商品ページURLへ置き換える。
  const MICROSOFT_STORE_URL = "https://apps.microsoft.com/detail/REPLACE_WITH_STORE_PRODUCT_ID";

  const translations = {
    ja: {
      documentTitle: "ユーザーマニュアル | TabbyBBMonitor",
      metaDescription: "TabbyBBMonitorのインストール、初期設定、使用方法を説明するユーザーマニュアルです。",
      overviewDocumentTitle: "概要 | TabbyBBMonitor",
      overviewMetaDescription: "TabbyBBMonitorの機能と対応環境を紹介する概要ページです。",
      privacyDocumentTitle: "プライバシーポリシー | TabbyBBMonitor",
      privacyMetaDescription: "TabbyBBMonitorのプライバシーポリシーです。",
      privacyLabel: "プライバシーポリシー",
      privacyKicker: "Apricot personal labo",
      privacyTitle: "プライバシーポリシー",
      privacyLead: "TabbyBBMonitorは、個人情報を収集、外部送信、販売または第三者提供しません。Bluetooth機器の情報とアプリ設定は、機能を提供するためにお使いのWindows端末内だけで処理されます。",
      effectiveDateLabel: "施行日",
      effectiveDateValue: "2026年9月23日",
      operatorLabel: "提供者",
      productLabel: "対象製品",
      footerNavLabel: "関連ページ",
      privacyNavLabel: "プライバシーポリシーの目次",
      privacyNavCommitment: "基本方針",
      privacyNavLocalData: "端末内の情報",
      privacyNavControls: "ユーザーによる管理",
      privacyNavContact: "お問い合わせ",
      commitmentTitle: "1. 基本方針",
      commitmentBody: "本ポリシーは、Microsoft Storeで提供するTabbyBBMonitor（以下「本アプリ」）に適用されます。本アプリの提供者はApricot personal laboです。本アプリは、利用者を識別する情報を収集せず、利用者の端末から開発者または外部サービスへ情報を送信しません。",
      summaryTitle: "本アプリが行わないこと",
      summaryNoAccounts: "アカウントの作成やログイン",
      summaryNoTelemetry: "利用状況解析、クラッシュレポートまたは遠隔測定",
      summaryNoAds: "広告表示または広告識別子の利用",
      summaryNoSharing: "個人情報の販売、共有または第三者提供",
      deviceInformationTitle: "2. 本アプリが端末上で参照する情報",
      deviceInformationIntro: "Bluetooth機器の電池監視機能を提供するため、本アプリはWindowsが公開する次の情報を端末上で参照します。",
      deviceInformationNames: "Bluetooth機器の表示名",
      deviceInformationIds: "Windows上の機器識別子",
      deviceInformationBattery: "電池残量、接続状態および利用可能状態",
      deviceInformationClarification: "機器の表示名には、利用者が設定した文字列が含まれる場合があります。これらの情報は監視対象の表示、電池状態の判定および通知のためだけに利用され、開発者または第三者へ送信されません。",
      localDataTitle: "3. 端末内に保存する設定",
      localDataIntro: "本アプリは次の設定を、利用者のWindowsアカウントのローカル領域にある設定ファイルへ保存します。",
      localDataDevices: "監視対象の機器名、機器識別子、監視の有効／無効および通知しきい値",
      localDataPreferences: "確認間隔、通知の有効／無効、表示言語および初回設定の完了状態",
      localDataPath: "設定ファイルの保存先：",
      localDataStartup: "サインイン時の自動起動状態はWindowsのStartupTaskとしてWindowsが管理し、本アプリの設定ファイルには重複して保存しません。",
      networkSharingTitle: "4. 通信、第三者提供および外部サービス",
      networkSharingBody: "本アプリはインターネット通信を行わず、解析サービス、広告サービスまたは外部データベースを利用しません。本アプリが参照または保存する情報を、開発者を含む第三者へ開示、販売または送信することはありません。通知はWindowsの機能を使って端末上で生成されます。",
      microsoftServicesPrefix: "Microsoft StoreおよびWindowsはMicrosoftが提供するサービスであり、それらのサービスにおける情報の取扱いには",
      microsoftPrivacyLink: "Microsoftのプライバシーステートメント",
      microsoftServicesSuffix: "が適用されます。",
      permissionsTitle: "5. 権限とWindows連携",
      permissionsIntro: "本アプリは、説明した機能を提供するために次のWindows機能を使用します。",
      permissionBluetooth: "Bluetooth：ペアリング済み機器を列挙し、Windowsが公開する電池情報を読み取るため",
      permissionFullTrust: "デスクトップアプリ実行：通知領域で常駐し、電池状態を確認するため",
      permissionStartup: "StartupTask：利用者が明示的に有効にした場合だけ、Windowsへのサインイン時に起動するため",
      permissionsExcluded: "本アプリは、位置情報、連絡先、写真、文書、カメラ、マイク、メールまたはMicrosoftアカウント情報へアクセスしません。",
      securityRetentionTitle: "6. 保存期間と安全性",
      securityRetentionBody: "設定は利用者の端末内にのみ保存され、利用者が変更または削除するまで保持されます。情報をネットワーク経由で転送しないため、開発者側に保管されるコピーはありません。設定ファイルへのアクセスはWindowsのユーザーアカウントによる保護に従います。",
      controlsTitle: "7. ユーザーによる管理と削除",
      controlsIntro: "利用者は、いつでも次の方法で本アプリによる処理を管理できます。",
      controlMonitoring: "設定画面で機器ごとの監視を無効にする",
      controlNotifications: "通知、自動起動または定期確認を変更する",
      controlDeviceSettings: "未検出機器の設定をアプリから削除する",
      controlDeleteFile: "本アプリを終了し、設定ファイルを削除してローカル設定をすべて消去する",
      controlUninstall: "Windowsの設定から本アプリをアンインストールする",
      controlsRemote: "開発者は利用者の端末内の情報へアクセスできないため、遠隔操作による参照、修正または削除はできません。",
      childrenTitle: "8. 子どものプライバシー",
      childrenBody: "本アプリは子どもを対象として設計されたサービスではなく、年齢にかかわらず利用者の個人情報を意図的に収集しません。",
      changesTitle: "9. 本ポリシーの変更",
      changesBody: "本アプリの機能または適用される要件が変わった場合、本ポリシーを更新します。更新したポリシーはこのページへ掲載し、施行日を改定します。",
      contactTitle: "10. お問い合わせ",
      contactBody: "本ポリシーに関するお問い合わせは、次のメールアドレスへご連絡ください。",
      skipLink: "本文へ移動",
      manualLabel: "ユーザーマニュアル",
      overviewLabel: "製品概要",
      overviewLinkLabel: "TabbyBBMonitorの概要へ",
      languageSwitcherLabel: "表示言語",
      sectionNavLabel: "マニュアルの目次",
      overviewNavLabel: "概要ページの目次",
      overviewNav: "概要",
      featuresNav: "主な機能",
      workflowNav: "使い始める",
      manualNav: "マニュアル",
      overviewKicker: "Windows向け通知領域アプリ",
      overviewTitle: "Bluetooth機器の電池切れを、早めに気づけるように",
      overviewCopy: "TabbyBBMonitorは、Windowsが取得した機器の電池残量を定期的に確認します。状態は通知領域のアイコンで確認でき、設定したしきい値をまたぐと通知を受け取れます。",
      storeDownloadLink: "Microsoft Storeで入手",
      openManual: "ユーザーマニュアルを開く",
      statusSummaryLabel: "電池状態の表示例",
      statusSummaryTitle: "電池状態",
      statusExampleDevice: "Bluetooth機器",
      statusSummaryBody: "機器ごとの状態を、通知領域からすぐ確認できます。",
      quickFactsLabel: "主な仕様",
      factIntervalLabel: "確認間隔",
      factIntervalValue: "既定30秒",
      factThresholdLabel: "通知しきい値",
      factThresholdValue: "機器ごとに3段階",
      factStartupLabel: "自動起動",
      factStartupValue: "必要な場合のみ有効化",
      featuresKicker: "日常の確認をシンプルに",
      featuresTitle: "主な機能",
      featureTrayTitle: "通知領域で状態を確認",
      featureTrayBody: "アプリを開いたままにせず、電池状態をアイコンと右クリックメニューから確認できます。",
      featureNotificationTitle: "しきい値をまたいだら通知",
      featureNotificationBody: "危険・低下・注意のしきい値を設定し、状態が変化したときにWindowsの通知を表示します。",
      featureDeviceTitle: "機器ごとに個別設定",
      featureDeviceBody: "監視のオン／オフやしきい値を機器ごとに調整し、必要な機器だけを監視できます。",
      featureRefreshTitle: "定期確認と手動更新",
      featureRefreshBody: "10～3600秒の確認間隔を選べるほか、「今すぐ確認」で必要なときに更新できます。",
      workflowKicker: "3つのステップ",
      workflowTitle: "使い始めるまで",
      workflowStep1Title: "機器をWindowsとペアリング",
      workflowStep1Body: "WindowsのBluetooth設定で、監視したい機器をペアリングします。",
      workflowStep2Title: "初回画面で監視対象を確認",
      workflowStep2Body: "アプリを起動すると機器を自動検索します。監視する機器と自動起動の設定を確認します。",
      workflowStep3Title: "通知領域から状態を確認",
      workflowStep3Body: "「監視を開始」を選ぶと、アプリが通知領域に常駐して確認を始めます。",
      manualCtaTitle: "詳しい操作を確認する",
      manualCtaBody: "インストール、初期設定、設定画面の使い方はユーザーマニュアルで確認できます。",
      overviewCompatibilityTitle: "利用できるBluetooth機器",
      overviewCompatibilityBody: "Windowsとペアリング済みで、機器またはドライバーが電池残量をWindowsへ公開している機器が対象です。機器によっては残量を取得できない場合があります。",
      navInstall: "インストール",
      navSetup: "初期設定",
      navUsage: "使用方法",
      introKicker: "はじめに",
      pageTitle: "Bluetooth機器の電池残量を、通知領域から確認する",
      introCopy: "TabbyBBMonitorは、Windowsが取得したBluetooth機器の電池残量を監視し、状態の変化を通知する常駐アプリです。このページでは、インストールから日常の使い方までを順に案内します。",
      supportedOsLabel: "対応OS",
      supportedOsValue: "Windows 10 バージョン2004以降 / Windows 11",
      targetDeviceLabel: "対象機器",
      targetDeviceValue: "Windowsとペアリング済みのBluetooth機器",
      installKicker: "まずアプリを準備します",
      installTitle: "インストール手順",
      installStep1Title: "Microsoft Storeの商品ページを開く",
      installStep1Body: "下のボタンからTabbyBBMonitorの商品ページを開きます。",
      installStep2Title: "アプリをインストールする",
      installStep2Body: "商品ページで「入手」または「インストール」を選択します。",
      installStep3Title: "アプリを起動する",
      installStep3Body: "インストール完了後に「開く」を選択すると、初期設定が始まります。",
      installNoteTitle: "追加のランタイムは不要です",
      installNoteBody: "Microsoft Store版には動作に必要なファイルが含まれています。別途.NET Desktop Runtimeをインストールする必要はありません。",
      setupKicker: "初回だけ行います",
      setupTitle: "初期設定手順",
      setupStep1Title: "自動検索が終わるまで待つ",
      setupStep1Body: "初回起動では「ようこそ」画面が開き、ペアリング済みのBluetooth機器を自動的に検索します。「検出中...」の表示が変わるまで待ちます。",
      setupStep2Title: "監視する機器を確認する",
      setupStep2Body: "検出された機器を選択し、「このデバイスを監視する」が有効になっていることを確認します。監視しない機器はチェックを外せます。",
      setupStep3Title: "必要に応じて自動起動を選ぶ",
      setupStep3Body: "「サインイン時に自動起動する」は既定でオフです。Windowsへのサインイン後も自動で監視したい場合だけ有効にします。",
      setupStep4Title: "監視を開始する",
      setupStep4Body: "「監視を開始」を選択すると設定が保存され、アプリが通知領域に常駐します。",
      noDeviceTitle: "機器が見つからない場合",
      noDeviceBody: "「Bluetooth設定を開く」からWindowsの設定を確認し、機器をペアリングしてください。機器が0台でも監視を開始でき、あとから「再検出」できます。",
      cancelSetupTitle: "設定を中断した場合",
      cancelSetupBody: "初回画面で「キャンセル」を選ぶか画面を閉じると、設定を保存せずアプリを終了します。次回起動時に初期設定がもう一度表示されます。",
      usageKicker: "セットアップ後の操作",
      usageTitle: "使用手順",
      trayCardLabel: "通知領域",
      trayCardTitle: "電池状態を確認する",
      trayCardBody: "タスクバーの通知領域にあるアイコンが、監視中の電池状態を示します。Windows 11で見えない場合は、タスクバー右端の隠しアイコンを開いてください。",
      menuCardLabel: "右クリックメニュー",
      menuCardTitle: "必要な操作をすぐ実行する",
      menuCardBody: "「今すぐ確認」で電池情報を更新できます。「設定...」で設定画面を開き、「終了」でアプリを終了します。アイコンのダブルクリックでも設定画面を開けます。",
      deviceCardLabel: "デバイスタブ",
      deviceCardTitle: "機器ごとに監視方法を調整する",
      deviceCardBody: "監視のオン／オフ、通知のしきい値、機器の再検出を設定できます。選択したしきい値をすべての機器へ一括適用することもできます。",
      generalCardLabel: "全般タブ",
      generalCardTitle: "アプリ全体の動作を変更する",
      generalCardBody: "確認間隔、通知、自動起動、表示言語、新しく検出する機器の初期しきい値を変更できます。確認間隔は10～3600秒で、既定は30秒です。",
      thresholdKicker: "既定の設定",
      thresholdTitle: "電池状態のしきい値",
      thresholdIntro: "残量がしきい値をまたぐと通知します。しきい値は機器ごとに変更できます。",
      thresholdTableRegionLabel: "電池状態のしきい値表",
      tableStatus: "状態",
      tableRange: "既定の残量",
      tableMeaning: "目安",
      statusCritical: "危険",
      rangeCritical: "0～15%",
      meaningCritical: "すぐに充電してください",
      statusLow: "低下",
      rangeLow: "16～35%",
      meaningLow: "充電を準備してください",
      statusCaution: "注意",
      rangeCaution: "36～70%",
      meaningCaution: "残量を確認してください",
      statusGood: "良好",
      rangeGood: "71～100%",
      meaningGood: "十分な残量があります",
      thresholdNote: "変更する場合は「危険 < 低下 < 注意」の順になるよう設定してください。",
      compatibilityTitle: "対応機器について",
      compatibilityBody: "監視できるのは、Windowsとペアリング済みで、機器またはドライバーが電池残量をWindowsへ公開しているBluetooth機器だけです。対応していても未接続時には「残量不明」になる場合があります。",
      footerManual: "ユーザーマニュアル",
      backToTop: "ページ上部へ戻る"
    },
    en: {
      documentTitle: "User Manual | TabbyBBMonitor",
      metaDescription: "Learn how to install, set up, and use TabbyBBMonitor on Windows.",
      overviewDocumentTitle: "Overview | TabbyBBMonitor",
      overviewMetaDescription: "Explore the features, supported environment, and workflow of TabbyBBMonitor.",
      privacyDocumentTitle: "Privacy Policy | TabbyBBMonitor",
      privacyMetaDescription: "Privacy Policy for TabbyBBMonitor.",
      privacyLabel: "Privacy Policy",
      privacyKicker: "Apricot personal labo",
      privacyTitle: "Privacy Policy",
      privacyLead: "TabbyBBMonitor does not collect, transmit, sell, or disclose personal information. Bluetooth device information and app settings are processed only on your Windows device to provide the app's features.",
      effectiveDateLabel: "Effective date",
      effectiveDateValue: "September 23, 2026",
      operatorLabel: "Provider",
      productLabel: "Product",
      footerNavLabel: "Related pages",
      privacyNavLabel: "Privacy Policy contents",
      privacyNavCommitment: "Commitment",
      privacyNavLocalData: "Local information",
      privacyNavControls: "Your controls",
      privacyNavContact: "Contact",
      commitmentTitle: "1. Privacy commitment",
      commitmentBody: "This policy applies to TabbyBBMonitor (the “App”), distributed through Microsoft Store and provided by Apricot personal labo. The App does not collect information that identifies you and does not send information from your device to the developer or any external service.",
      summaryTitle: "The App does not use",
      summaryNoAccounts: "Accounts or sign-in",
      summaryNoTelemetry: "Usage analytics, crash reporting, or telemetry",
      summaryNoAds: "Advertising or advertising identifiers",
      summaryNoSharing: "Sale, sharing, or disclosure of personal information",
      deviceInformationTitle: "2. Information accessed on your device",
      deviceInformationIntro: "To monitor Bluetooth device batteries, the App locally accesses the following information made available by Windows:",
      deviceInformationNames: "Bluetooth device display names",
      deviceInformationIds: "Windows device identifiers",
      deviceInformationBattery: "Battery level, connection state, and availability",
      deviceInformationClarification: "A device display name may contain text chosen by you. This information is used only to display monitored devices, determine battery status, and generate notifications. It is never transmitted to the developer or any third party.",
      localDataTitle: "3. Settings stored on your device",
      localDataIntro: "The App stores the following settings in a local settings file associated with your Windows user account:",
      localDataDevices: "Monitored device names and identifiers, monitoring status, and notification thresholds",
      localDataPreferences: "Check interval, notification preference, display language, and first-run setup status",
      localDataPath: "Settings file location:",
      localDataStartup: "Windows manages the start-at-sign-in state as a StartupTask. The App does not duplicate this state in its settings file.",
      networkSharingTitle: "4. Network use, disclosure, and external services",
      networkSharingBody: "The App does not communicate over the internet and does not use analytics, advertising services, or external databases. Information accessed or stored by the App is not disclosed, sold, or transmitted to any third party, including the developer. Notifications are generated locally through Windows.",
      microsoftServicesPrefix: "Microsoft Store and Windows are services provided by Microsoft. Information handled by those services is governed by the",
      microsoftPrivacyLink: "Microsoft Privacy Statement",
      microsoftServicesSuffix: ".",
      permissionsTitle: "5. Permissions and Windows integration",
      permissionsIntro: "The App uses the following Windows capabilities solely to provide the features described in this policy:",
      permissionBluetooth: "Bluetooth: to enumerate paired devices and read battery information made available by Windows",
      permissionFullTrust: "Desktop app execution: to remain available in the notification area and check battery status",
      permissionStartup: "StartupTask: to start at Windows sign-in only when you explicitly enable this option",
      permissionsExcluded: "The App does not access your location, contacts, photos, documents, camera, microphone, email, or Microsoft account information.",
      securityRetentionTitle: "6. Retention and security",
      securityRetentionBody: "Settings remain only on your device until you change or delete them. Because the App does not transfer information over a network, the developer keeps no remote copy. Access to the settings file is subject to the protections of your Windows user account.",
      controlsTitle: "7. Your controls and deletion",
      controlsIntro: "You can control the App's processing at any time by using the following options:",
      controlMonitoring: "Disable monitoring for individual devices in Settings",
      controlNotifications: "Change notifications, start-at-sign-in, or the check interval",
      controlDeviceSettings: "Remove settings for unavailable devices from the App",
      controlDeleteFile: "Exit the App and delete its settings file to erase all local settings",
      controlUninstall: "Uninstall the App from Windows Settings",
      controlsRemote: "The developer cannot access information on your device and therefore cannot view, correct, or delete it remotely.",
      childrenTitle: "8. Children's privacy",
      childrenBody: "The App is not designed as a service directed to children and does not knowingly collect personal information from users of any age.",
      changesTitle: "9. Changes to this policy",
      changesBody: "We will update this policy if the App's features or applicable requirements change. The revised policy will be posted on this page with an updated effective date.",
      contactTitle: "10. Contact",
      contactBody: "For questions about this policy, contact us at the following email address:",
      skipLink: "Skip to main content",
      manualLabel: "User manual",
      overviewLabel: "Product overview",
      overviewLinkLabel: "Go to the TabbyBBMonitor overview",
      languageSwitcherLabel: "Display language",
      sectionNavLabel: "Manual contents",
      overviewNavLabel: "Overview page contents",
      overviewNav: "Overview",
      featuresNav: "Main features",
      workflowNav: "Get started",
      manualNav: "Manual",
      overviewKicker: "Notification area app for Windows",
      overviewTitle: "Know sooner when a Bluetooth device needs charging",
      overviewCopy: "TabbyBBMonitor regularly checks the device battery levels reported by Windows. See each status from the notification area and receive a notification when a level crosses a threshold you set.",
      storeDownloadLink: "Get it from Microsoft Store",
      openManual: "Open the user manual",
      statusSummaryLabel: "Example battery status",
      statusSummaryTitle: "Battery status",
      statusExampleDevice: "Bluetooth device",
      statusSummaryBody: "Check the status of each device directly from the notification area.",
      quickFactsLabel: "Key specifications",
      factIntervalLabel: "Check interval",
      factIntervalValue: "30 seconds by default",
      factThresholdLabel: "Notification thresholds",
      factThresholdValue: "Three levels per device",
      factStartupLabel: "Automatic startup",
      factStartupValue: "Enable only when needed",
      featuresKicker: "Simpler everyday checks",
      featuresTitle: "Main features",
      featureTrayTitle: "See status in the notification area",
      featureTrayBody: "Check battery status from the icon and right-click menu without keeping the app window open.",
      featureNotificationTitle: "Get notified at your thresholds",
      featureNotificationBody: "Set Critical, Low, and Caution thresholds and receive a Windows notification when the status changes.",
      featureDeviceTitle: "Configure each device separately",
      featureDeviceBody: "Turn monitoring on or off and adjust thresholds per device, so only the devices you need are monitored.",
      featureRefreshTitle: "Scheduled and manual checks",
      featureRefreshBody: "Choose a check interval from 10 to 3600 seconds, or use “Check now” whenever you need an update.",
      workflowKicker: "Three steps",
      workflowTitle: "Start monitoring",
      workflowStep1Title: "Pair the device with Windows",
      workflowStep1Body: "Use Windows Bluetooth settings to pair the device you want to monitor.",
      workflowStep2Title: "Review devices on first launch",
      workflowStep2Body: "The app automatically searches for devices. Review which devices to monitor and whether to start at sign-in.",
      workflowStep3Title: "Check status from the notification area",
      workflowStep3Body: "Select “Start monitoring” to keep the app running in the notification area and begin checking devices.",
      manualCtaTitle: "See the detailed instructions",
      manualCtaBody: "The user manual explains installation, initial setup, and how to use the settings window.",
      overviewCompatibilityTitle: "Compatible Bluetooth devices",
      overviewCompatibilityBody: "A device must be paired with Windows, and its device or driver must report the battery level to Windows. Battery information may not be available for every device.",
      navInstall: "Install",
      navSetup: "Initial setup",
      navUsage: "How to use",
      introKicker: "Getting started",
      pageTitle: "Check Bluetooth battery levels from the notification area",
      introCopy: "TabbyBBMonitor runs in the background, monitors the battery levels reported by Windows for your Bluetooth devices, and notifies you when their status changes. This guide takes you from installation through everyday use.",
      supportedOsLabel: "Supported OS",
      supportedOsValue: "Windows 10 version 2004 or later / Windows 11",
      targetDeviceLabel: "Supported devices",
      targetDeviceValue: "Bluetooth devices paired with Windows",
      installKicker: "Prepare the app",
      installTitle: "Installation",
      installStep1Title: "Open the Microsoft Store product page",
      installStep1Body: "Use the button below to open the TabbyBBMonitor product page.",
      installStep2Title: "Install the app",
      installStep2Body: "Select “Get” or “Install” on the product page.",
      installStep3Title: "Launch the app",
      installStep3Body: "When installation finishes, select “Open” to begin initial setup.",
      installNoteTitle: "No additional runtime is required",
      installNoteBody: "The Microsoft Store version includes the files it needs to run. You do not need to install the .NET Desktop Runtime separately.",
      setupKicker: "Complete this once",
      setupTitle: "Initial setup",
      setupStep1Title: "Wait for automatic discovery",
      setupStep1Body: "On first launch, the Welcome screen opens and automatically searches for paired Bluetooth devices. Wait until the “Discovering...” message changes.",
      setupStep2Title: "Review the devices to monitor",
      setupStep2Body: "Select a detected device and make sure “Monitor this device” is enabled. Clear the check box for any device you do not want to monitor.",
      setupStep3Title: "Choose whether to start at sign-in",
      setupStep3Body: "“Start at sign-in” is off by default. Enable it only if you want monitoring to start automatically after you sign in to Windows.",
      setupStep4Title: "Start monitoring",
      setupStep4Body: "Select “Start monitoring” to save your settings and keep the app running in the notification area.",
      noDeviceTitle: "If no devices are found",
      noDeviceBody: "Select “Open Bluetooth settings” to check Windows settings and pair your device. You can start monitoring with no devices and use “Rediscover” later.",
      cancelSetupTitle: "If you stop setup",
      cancelSetupBody: "Selecting “Cancel” or closing the first-run window exits the app without saving. Initial setup appears again the next time you launch the app.",
      usageKicker: "After setup",
      usageTitle: "How to use the app",
      trayCardLabel: "Notification area",
      trayCardTitle: "Check battery status",
      trayCardBody: "The icon in the taskbar notification area shows the current battery status of monitored devices. If it is not visible in Windows 11, open the hidden icons area at the right end of the taskbar.",
      menuCardLabel: "Right-click menu",
      menuCardTitle: "Run common actions quickly",
      menuCardBody: "Use “Check now” to refresh battery information, “Settings...” to open settings, and “Exit” to close the app. You can also double-click the icon to open settings.",
      deviceCardLabel: "Devices tab",
      deviceCardTitle: "Adjust monitoring for each device",
      deviceCardBody: "Turn monitoring on or off, change notification thresholds, and rediscover devices. You can also apply the selected thresholds to every device.",
      generalCardLabel: "General tab",
      generalCardTitle: "Change app-wide behavior",
      generalCardBody: "Change the check interval, notifications, startup behavior, display language, and default thresholds for newly detected devices. The interval can be 10–3600 seconds and defaults to 30 seconds.",
      thresholdKicker: "Default settings",
      thresholdTitle: "Battery status thresholds",
      thresholdIntro: "The app notifies you when the battery level crosses a threshold. You can customize thresholds for each device.",
      thresholdTableRegionLabel: "Battery status threshold table",
      tableStatus: "Status",
      tableRange: "Default range",
      tableMeaning: "Guidance",
      statusCritical: "Critical",
      rangeCritical: "0–15%",
      meaningCritical: "Charge the device now",
      statusLow: "Low",
      rangeLow: "16–35%",
      meaningLow: "Prepare to charge the device",
      statusCaution: "Caution",
      rangeCaution: "36–70%",
      meaningCaution: "Keep an eye on the level",
      statusGood: "Good",
      rangeGood: "71–100%",
      meaningGood: "The battery level is sufficient",
      thresholdNote: "When changing thresholds, keep them in the order Critical < Low < Caution.",
      compatibilityTitle: "Device compatibility",
      compatibilityBody: "The app can monitor only Bluetooth devices that are paired with Windows and whose device or driver reports a battery level to Windows. A supported device may still show “Battery level unknown” while disconnected.",
      footerManual: "User manual",
      backToTop: "Back to top"
    }
  };

  function readQueryLanguage() {
    try {
      const value = new URL(window.location.href).searchParams.get("lang");
      return SUPPORTED_LANGUAGES.has(value) ? value : null;
    } catch {
      return null;
    }
  }

  function readStoredLanguage() {
    try {
      const value = window.localStorage.getItem(STORAGE_KEY);
      return SUPPORTED_LANGUAGES.has(value) ? value : null;
    } catch {
      return null;
    }
  }

  function readBrowserLanguage() {
    const languages = Array.isArray(navigator.languages) && navigator.languages.length > 0
      ? navigator.languages
      : [navigator.language];
    const primaryLanguage = languages.find((language) => typeof language === "string" && language.length > 0) ?? "";
    return primaryLanguage.toLowerCase().startsWith("ja")
      ? "ja"
      : "en";
  }

  function storeLanguage(language) {
    try {
      window.localStorage.setItem(STORAGE_KEY, language);
    } catch {
      // Local storage may be unavailable when the manual is opened directly from disk.
    }
  }

  function updateUrl(language) {
    try {
      const url = new URL(window.location.href);
      url.searchParams.set("lang", language);
      window.history.replaceState(null, "", url.toString());
    } catch {
      // Some browsers restrict history changes for file:// pages. The switch still works.
    }
  }

  function applyLanguage(language, persist = false) {
    const dictionary = translations[language];
    if (!dictionary) {
      return;
    }

    document.documentElement.lang = language;
    const page = document.body?.dataset.page ?? "manual";
    const metadataKeys = {
      overview: ["overviewDocumentTitle", "overviewMetaDescription"],
      privacy: ["privacyDocumentTitle", "privacyMetaDescription"],
      manual: ["documentTitle", "metaDescription"]
    };
    const [titleKey, descriptionKey] = metadataKeys[page] ?? metadataKeys.manual;
    document.title = dictionary[titleKey];

    const description = document.querySelector('meta[name="description"]');
    if (description) {
      description.setAttribute("content", dictionary[descriptionKey]);
    }

    document.querySelectorAll("[data-i18n]").forEach((element) => {
      const key = element.dataset.i18n;
      if (Object.prototype.hasOwnProperty.call(dictionary, key)) {
        element.textContent = dictionary[key];
      }
    });

    document.querySelectorAll("[data-i18n-aria-label]").forEach((element) => {
      const key = element.dataset.i18nAriaLabel;
      if (Object.prototype.hasOwnProperty.call(dictionary, key)) {
        element.setAttribute("aria-label", dictionary[key]);
      }
    });

    document.querySelectorAll("[data-language]").forEach((button) => {
      button.setAttribute("aria-pressed", String(button.dataset.language === language));
    });

    document.querySelectorAll("[data-language-link]").forEach((link) => {
      const target = link.dataset.languageLink;
      if (target) {
        link.setAttribute("href", target + "?lang=" + encodeURIComponent(language));
      }
    });

    document.querySelectorAll("[data-store-link]").forEach((link) => {
      link.setAttribute("href", MICROSOFT_STORE_URL);
    });

    if (persist) {
      storeLanguage(language);
      updateUrl(language);
    }
  }

  const initialLanguage = readQueryLanguage() ?? readStoredLanguage() ?? readBrowserLanguage();
  applyLanguage(initialLanguage);

  document.querySelectorAll("[data-language]").forEach((button) => {
    button.addEventListener("click", () => {
      const language = button.dataset.language;
      if (SUPPORTED_LANGUAGES.has(language)) {
        applyLanguage(language, true);
      }
    });
  });
})();
