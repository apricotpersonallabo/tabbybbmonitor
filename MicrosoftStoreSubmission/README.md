# Microsoft Store 提出素材

TabbyBBMonitor 1.0.0.0 の Microsoft Store（MSIX）初回申請用素材です。

## 収録内容

- `listing-ja-JP.md`: 日本語ストア掲載文と画像キャプション
- `listing-en-US.md`: 英語ストア掲載文と画像キャプション
- `certification-notes.md`: 審査担当者向けメモと制限付き機能の説明
- `submission-checklist.md`: Partner Center 入力・アップロード手順
- `requirements-sources.md`: 作成時に確認した Microsoft 公式要件
- `images/ja-JP/`: 日本語のデスクトップスクリーンショット4枚
- `images/en-US/`: 英語のデスクトップスクリーンショット4枚
- `images/brand/store-logo-300x300.png`: 1:1 ストアロゴ
- `images/brand/poster-art-720x1080.png`: 任意の2:3ポスターアート

## 画像について

スクリーンショットは製品の WinForms 画面をサンプル機器データで直接レンダリングしたものです。実在する機器名や個人情報は含みません。デスクトップ画像はすべて 1366×768 PNG です。

## 提出前に必要な差し替え

以下はリポジトリ内に確定値がないため、Partner Center へ送信する前に必ず設定してください。

1. 公開済みのサポートページ URL
2. 公開済みのプライバシーポリシー URL
3. サポート用メールアドレス
4. `docs/privacy.html` 内の `REPLACE_WITH_PRIVACY_CONTACT_EMAIL`
5. 必要なら `docs/index.html`、`docs/manual.html`、`docs/i18n.js` 内の Store 製品 ID

初回申請では「このバージョンの新機能」は空欄にします。掲載言語は、素材が揃っている `ja-JP` と `en-US` から始める想定です。
