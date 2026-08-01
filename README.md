# AssetStash

[![Unity 6000.1+](https://img.shields.io/badge/unity-6000.1%2B-important.svg)](#requirements)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE.md)

よく使うアセットやシーンオブジェクトをブックマークして、簡単にアクセスできるようにする生産性向上ツールです。
作業によって頻繁にアクセスするアセットをグループにまとめて整理したり、メモを追加して管理することができます。

<img width="593" height="292" alt="image" src="https://github.com/user-attachments/assets/c3a8ac6b-81c6-41fc-9894-702edf3bfc05" />

## Features | 主な機能

### クリック、右クリック
- ダブルクリックでアセットを開く(または、右クリック > 開く)
- 右クリック > アセットの場所で Project ツリーのオブジェクト位置をハイライト
### ドラッグアンドドロップ
- Project ウィンドウから AssetStashWindow にドラッグしてブックマークを追加
- MonoBehaviour Script を直接 Inspector にドラッグして GameObject にコンポーネントを追加
- AssetStashWindow ツリー内のアイテムはドラッグで並び替え可能
### 検索
- 検索欄で名前 / パス / GUID / メモ を対象に絞り込み
- スペース区切りで複数キーワードの AND 検索
- グループ名がヒットした場合は配下をすべて表示、子がヒットした場合は親グループごと表示
- 検索中は並び順の整合が取れないため、ドラッグアンドドロップは無効
### 取り消し / 削除の確認
- Ctrl+Z で元に戻す、Ctrl+Y（または Ctrl+Shift+Z）でやり直し（macOS は Cmd）。右クリック > 元に戻す でも可
- 対象は追加・削除・並べ替え・リセット（最大 50 履歴、ウィンドウを閉じるとクリア）
- リセット時と、中身のあるグループを削除する際は確認ダイアログを表示
### 欠損アセットの検出
- 削除・移動などで参照先が失われたブックマークを警告アイコンと色で表示（ツールチップに GUID / パスを表示）
- 欠損がある場合のみ「欠損を削除」ボタンが表示され、まとめて登録解除が可能
### ブックマークの管理
- フォルダ(グループ)を作成してブックマークを階層的に整理・分類(1階層のみ)
- フォルダ(グループ)をダブルクリックまたは F2 キーでグループ名編集
- 各ブックマークやグループにメモを追加・編集（右クリック > メモ編集）
- パス / GUID / メモ列の表示 / 非表示を切り替えてカスタマイズ
- フォルダ(グループ)の展開状態を EditorPrefs に自動保存
- Assets フォルダ外のファイルもブックマーク可能
 
## Installation | インストール方法

### Using Unity Package Manager (UPM)
1. Open the **Package Manager** window (`Window` > `Package Manager`).
2. Click the **"+"** button in the top-left corner.
3. Select **"Add package from git URL..."**.
4. Paste the following URL: https://github.com/teru-p-q/AssetStash.git

## 注意
- Unity 6000.73 以降で動作確認しています。
- UIElements を使用しているため、UIElements がサポートされている Unity バージョンが必要です。
