# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]
### Added
- 検索欄を追加。名前 / パス / GUID / メモ を対象にインクリメンタル検索（スペース区切りで AND 検索）
- 検索中はグループを自動展開してヒット項目を表示

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
