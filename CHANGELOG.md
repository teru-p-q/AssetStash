# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]
### Added
- 検索欄を追加。名前 / パス / GUID / メモ を対象にインクリメンタル検索（スペース区切りで AND 検索）
- 検索中はグループを自動展開してヒット項目を表示
- 参照先が存在しないブックマークを警告アイコンと色で表示
- 「欠損を削除」ボタンを追加（欠損が 1 件以上あるときのみ表示）
- Undo / Redo に対応（Ctrl+Z / Ctrl+Y、macOS は Cmd+Z / Cmd+Shift+Z）。追加・削除・並べ替え・リセット・メモ / グループ名の編集が対象
- 右クリックメニューに「元に戻す」を追加
- リセット時と、子を持つグループの削除時に確認ダイアログを表示

### Fixed
- 削除済みアセットのブックマークを右クリックすると例外が発生する問題
- 欠損したブックマークに対して「開く」「アセットの場所を示す」が表示されていた問題
- メモ / グループ名を編集しても保存されず、他の操作をせずに Unity を終了すると編集内容が失われる問題
- インライン編集中に Esc を押しても入力内容が確定されてしまう問題
- インライン編集の確定が二重に実行され、コンソールに例外が出る問題

## [0.1.6] - 2026-08-01
### Added
- カラム幅やカラムの表示状態を保存し、次回起動時に復元するように

### Changed
- ウィンドウ幅より広くカラムを広げられるように（横スクロール対応）
- 内部リファクタリング（プロパティ共通処理を基底クラスに集約）

## [0.1.5] - 2026-05-19
### Fixed
- DnD の不具合修正

## [0.1.2] - 2026-04-23
### Fixed
- Improved context menu behavior
- Fixed external file detection logic
- Changed "Create Folder" to "Create Group"

## [0.1.1] - 2026-04-17
### Fixed
- Memo not displaying on UI

## [0.1.0] - 2026-04-16
- First release
