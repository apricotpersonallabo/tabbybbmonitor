# Microsoft Store certification notes

Use the English text below in Partner Center's certification notes. It is written so a reviewer can test the app without a supported Bluetooth peripheral.

## Notes for certification

TabbyBBMonitor is a user-level WinForms notification-area utility for Windows 10 version 2004 or later and Windows 11. It does not require administrator privileges, a user account, a subscription, a product key, or a network connection.

On first launch, the setup window opens automatically. The reviewer can complete setup even when no compatible Bluetooth device is available. After setup, the app runs in the notification area. To reopen Settings, launch TabbyBBMonitor again or use the notification-area icon. Only one process instance is kept running.

The app reads the display name, identifier, connection state, and battery level that Windows exposes for paired Bluetooth/Bluetooth LE devices. Some devices or drivers do not expose a battery level; those devices may be absent or show an unknown battery level. This is a hardware/driver limitation and is explained in the Store description.

All preferences are stored locally in `%LOCALAPPDATA%\BatteryTray\settings.json`. The app has no advertising, analytics, telemetry, crash-report upload, or other external data transmission.

Suggested test steps:

1. Launch the app and review the first-run setup screen.
2. Select “Start monitoring” even if no compatible device is listed.
3. Reopen the app to display Settings.
4. Confirm that the Devices and General tabs are usable.
5. If a compatible paired Bluetooth device is available, use “Rediscover” and confirm that its battery level appears.
6. Save Settings and confirm the app remains available in the Windows notification area.

## `runFullTrust` 制限付き機能の承認申請文（日本語・貼り付け用）

TabbyBBMonitorは、Windows 10／11の通知領域で動作する、パッケージ化された.NET 10 WinFormsデスクトップアプリです。MSIXからWinFormsの実行ファイルを`Windows.FullTrustApplication`として起動し、通常のデスクトップUI、通知領域アイコン（`NotifyIcon`）、ユーザー操作による設定画面、およびユーザーセッション中の定期的な電池確認を提供するために、`runFullTrust`機能が必要です。

本アプリは、Windowsへペアリング済みのBluetooth／Bluetooth Low Energy機器について、Windowsが公開する機器の表示名、識別子、接続・利用可能状態、電池残量を定期的に読み取ります。取得した残量をユーザーが設定した3段階のしきい値と比較し、状態がしきい値をまたいだ場合に端末上のWindows通知を表示します。Bluetooth機器情報へのアクセスは、マニフェストで別途宣言している`bluetooth`デバイス機能を使用します。

`runFullTrust`で使用するデスクトップ機能は、通知領域での常駐、WinForms設定画面の表示、ローカル通知の表示、単一インスタンス制御、および`%LOCALAPPDATA%\BatteryTray\settings.json`へのユーザー設定の保存に限定されます。アプリは対話ユーザーの標準権限で動作し、管理者権限や昇格を要求しません。サインイン時の自動起動は既定で無効であり、ユーザーが設定画面で明示的に有効にした場合だけ、パッケージの`windows.startupTask`を使用します。

本アプリは、Windowsサービスやドライバーのインストール、保護されたシステム領域やシステム設定の変更、他プロセスへのコード挿入・操作、任意コマンドの実行、ダウンロードしたコードの実行を行いません。また、アカウント登録、広告、解析、遠隔測定、クラッシュレポート送信を使用せず、取得したBluetooth機器情報やユーザー設定を開発者または外部サービスへ送信しません。

## `runFullTrust` restricted capability justification (English)

TabbyBBMonitor is a packaged WinForms desktop application that runs with the interactive user's standard privileges. The `runFullTrust` capability is required to run the WinForms executable, remain in the Windows notification area, read Bluetooth battery information exposed by Windows, show local Windows notifications, and save user preferences locally. The app does not request elevation, install a service or driver, modify protected system locations, inject into other processes, or execute downloaded code.

## Bluetooth capability justification

The `bluetooth` device capability is used only to enumerate Bluetooth/Bluetooth LE devices already known to Windows and read the battery level, display name, and connection/availability information that Windows exposes. The app does not initiate pairing, transfer files, record communications, or send Bluetooth device information off the device.

## Startup task explanation

The packaged `windows.startupTask` extension is disabled by default. The app requests startup only when the user explicitly enables “Start at sign-in” in the first-run or General settings screen. The user can disable it again in the app or in Windows Settings.
