# Partner Center 提出チェックリスト

## 1. 提出前の必須差し替え

- [ ] `REPLACE_WITH_PUBLIC_PRODUCT_URL` を公開済み製品ページへ置換
- [ ] `REPLACE_WITH_SUPPORT_EMAIL_OR_URL` を有効なサポート連絡先へ置換
- [ ] `REPLACE_WITH_PUBLIC_PRIVACY_POLICY_URL` を公開済みポリシーURLへ置換
- [ ] `docs/privacy.html` の連絡先メールアドレスを置換して公開
- [ ] Partner Centerで予約した製品名が `TabbyBBMonitor` であることを確認
- [ ] `packaging/store-settings.json` のIdentity、Publisher、PublisherDisplayNameをPartner Centerの値と照合
- [ ] 申請ごとにパッケージVersionを増やし、4番目の値を`0`にする

## 2. 価格と提供状況

- [ ] 価格（無料／有料）を決定
- [ ] 公開市場と公開日時を決定
- [ ] 年齢区分アンケートへ実際の内容どおり回答
- [ ] 組織向けライセンスを許可するか決定

## 3. プロパティ

- [ ] カテゴリ: `Utilities + tools`
- [ ] サブカテゴリ: なし
- [ ] 対応デバイスファミリ: Desktop
- [ ] OS: Windows 10 version 2004以降／Windows 11
- [ ] Bluetoothを必要なハードウェア機能として指定
- [ ] ドライバーまたはNTサービスへの依存: なし
- [ ] アカウント、サブスクリプション、外部購入: なし
- [ ] 広告: なし
- [ ] 個人情報を外部収集・送信: なし
- [ ] プライバシーポリシー: 必要として公開URLを登録

## 4. パッケージ

- [ ] `pwsh -NoLogo -NoProfile -File .\scripts\Build-StorePackage.ps1` を実行
- [ ] `artifacts\store\<version>\BatteryTray_<version>.msixupload` をアップロード
- [ ] x64とARM64の両パッケージが認識されることを確認
- [ ] 最小OSバージョンが10.0.19041.0であることを確認
- [ ] package flightや段階配信を使う場合は対象を確認

## 5. ストア掲載情報

- [ ] 日本語掲載へ `listing-ja-JP.md` を入力
- [ ] 英語掲載へ `listing-en-US.md` を入力
- [ ] 各言語のスクリーンショット4枚を指定順でアップロード
- [ ] 各スクリーンショットのキャプションを入力
- [ ] `images/brand/store-logo-300x300.png` を1:1ストアロゴへ登録
- [ ] 必要なら `images/brand/poster-art-720x1080.png` を2:3ポスターアートへ登録
- [ ] 初回申請では「このバージョンの新機能」を空欄にする
- [ ] 説明文に対応機器の制約が明記されていることを確認

## 6. 申請オプションと審査メモ

- [ ] `certification-notes.md` のNotes for certificationを審査メモへ貼り付け
- [ ] `runFullTrust` の用途説明を申請オプションへ入力
- [ ] Bluetooth capabilityの用途説明を必要な欄へ入力
- [ ] ログイン情報欄は「不要」とする
- [ ] 審査担当者がBluetooth機器なしでも初回設定を完了できることを確認

## 7. 最終確認

- [ ] Releaseビルドと自動テストが成功
- [ ] 生成した`.msixupload`のテストが成功
- [ ] 実機でインストール、初回起動、保存、再起動、アンインストールを確認
- [ ] ストア説明・画像・パッケージ内の製品名が一致
- [ ] 画像に実在する機器名、アカウント名、通知、個人情報が写っていない
- [ ] 全プレースホルダーがなくなっていることを検索
- [ ] 送信前のPartner Centerプレビューを確認
