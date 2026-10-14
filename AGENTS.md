# Repository agent rules

## GitHub Pages migration state

現在のGitHub Pages本番ソースは `docs/` です。

`site/` は共通サイト基盤へ移行するための次期ソースであり、現時点では本番デプロイ元ではありません。

製品変更が公開ドキュメントへ影響する場合:

1. 実装とテストを確認してからドキュメントを変更する。
2. 必要に応じて現在の本番ドキュメント `docs/` を更新する。
3. `site/` にある対応する製品情報も意味的に同期する。
4. `docs/` を削除、置換、生成物で上書きしない。
5. 移行切替が明示されたタスク以外ではGitHub Pagesの公開元を変更しない。

## Responsibility boundary

このリポジトリが所有するもの:

- 製品仕様と製品固有の説明
- マニュアルとプライバシー本文
- Store/GitHubリンク
- 製品固有の翻訳とメディア

共通サイト基盤が所有するもの:

- 共通レイアウトとHTML shell
- Header/Footer/Navigationの共通動作
- 共通CSSとdesign token
- アクセシビリティの基礎実装
- preview/build処理

共通UIの問題を製品側へ複製実装しないこと。
