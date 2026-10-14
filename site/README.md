# Site migration

このディレクトリは共通サイト基盤へ移行するための次期サイトソースです。

**現在のGitHub Pages本番ソースは引き続き `docs/` です。**

明示的な切替PRがマージされるまでは:

- `docs/` を本番品質で維持する。
- 製品仕様に関する `docs/` と `site/` の内容を意味的に同期する。
- `site/` をGitHub Pagesへデプロイしない。
- 生成物を `docs/` に出力しない。
- 新構成は Site preview workflow のartifactで確認する。

レイアウト、共通CSS、レンダリング、共通アクセシビリティ処理は共通サイト基盤側の責務です。
