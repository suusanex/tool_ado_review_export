# クイックスタート: Azure DevOps PR Review Comment Exporter

**Feature Branch**: `001-ado-review-export`  
**Date**: January 7, 2026  
**Version**: 1.0.0

このドキュメントは、開発者およびユーザーが本アプリケーションを迅速に使い始めるためのガイドです。

---

## 目次

1. [ユーザー向けクイックスタート](#ユーザー向けクイックスタート)
   - [GUI モードで使う](#gui-モードで使う)
   - [CLI モードで使う](#cli-モードで使う)
2. [開発者向けクイックスタート](#開発者向けクイックスタート)
   - [開発環境のセットアップ](#開発環境のセットアップ)
   - [ビルドと実行](#ビルドと実行)
   - [テストの実行](#テストの実行)

---

## ユーザー向けクイックスタート

### 前提条件

- **OS**: Windows 10 (21H2) 以降、または Windows 11
- **Azure DevOps アカウント**: Personal Access Token (PAT) の発行が必要
- **権限**: PAT に `vso.code` (Read) スコープが必要

---

### GUI モードで使う

#### ステップ 1: Personal Access Token (PAT) の発行

1. Azure DevOps (https://dev.azure.com) にログイン
2. 右上のユーザーアイコン → 「Personal access tokens」をクリック
3. 「New Token」をクリック
4. トークン名を入力（例: `AdoReviewExport`）
5. Organization を選択
6. Scopes で「Code」→ 「Read」にチェック
7. 「Create」をクリック
8. 表示されたトークンをコピー（**再表示できないため、必ず保存してください**）

#### ステップ 2: アプリケーションのダウンロード

1. リリースページから最新版の zip ファイルをダウンロード
   - 例: `AdoReviewExport-v1.0.0-win-x64.zip`
2. zip ファイルを任意のフォルダに展開
   - 例: `C:\Tools\AdoReviewExport`

#### ステップ 3: アプリケーションの起動

1. 展開したフォルダ内の `AdoReviewExport.exe` をダブルクリック
2. メインウィンドウが表示されます

#### ステップ 4: 入力フィールドに情報を入力

```
[Organization]  contoso
[Project]       MyProject
[Repository]    my-repo
[PAT]           (ステップ1で取得した PAT をペースト)
[Output Path]   C:\exports\review.json  (または [📁] ボタンで選択)
[Authors (Optional)]  (任意: 例 alice,bob)
```

#### ステップ 5: エクスポート開始

1. 「Start Export」ボタンをクリック
2. 進捗バーとログが表示されます
3. 完了すると、完了ダイアログが表示されます
4. 「Open File」をクリックして JSON ファイルを開く、または「Open Folder」でフォルダを開く

#### トラブルシューティング

**エラー: "The provided PAT is invalid"**
- PAT が正しくコピーされているか確認してください
- PAT の有効期限が切れていないか確認してください
- PAT に `Code (Read)` 権限があるか確認してください

**エラー: "Failed to connect to Azure DevOps"**
- インターネット接続を確認してください
- プロキシ設定が必要な場合、Windows のシステムプロキシ設定を確認してください

---

### CLI モードで使う

#### ステップ 1: Personal Access Token (PAT) の発行

GUI モードのステップ 1 と同じ手順で PAT を発行してください。

#### ステップ 2: コマンドラインから実行

PowerShell またはコマンドプロンプトを開き、以下のコマンドを実行:

```powershell
cd C:\Tools\AdoReviewExport
.\AdoReviewExport.exe export --org contoso --project MyProject --repo my-repo --pat YOUR_PAT_HERE --output C:\exports\review.json
```

**環境変数で PAT を指定する場合**（推奨: コマンド履歴に PAT が残らない）:

```powershell
$env:AZDO_PAT = "YOUR_PAT_HERE"
.\AdoReviewExport.exe export --org contoso --project MyProject --repo my-repo --output C:\exports\review.json
```

#### ステップ 3: 進捗を確認

標準出力に進捗ログが表示されます:

```
[2026-01-07 10:30:00] INFO: Starting export...
[2026-01-07 10:30:00] INFO: Connecting to Azure DevOps...
[2026-01-07 10:30:01] INFO: Connection successful
[2026-01-07 10:30:01] INFO: Fetching pull requests...
[2026-01-07 10:30:02] INFO: Found 200 pull requests
[2026-01-07 10:30:02] INFO: Processing PR 1 / 200
...
[2026-01-07 10:35:02] INFO: Export completed successfully
[2026-01-07 10:35:02] INFO: Summary: 200 PRs, 1,250 comments
```

#### ステップ 4: 出力ファイルを確認

指定した出力パス（`C:\exports\review.json`）に JSON ファイルが生成されます。

#### オプション: 投稿者フィルタを使用

特定のユーザー（例: alice, bob）のコメントのみをエクスポート:

```powershell
.\AdoReviewExport.exe export --org contoso --project MyProject --repo my-repo --pat YOUR_PAT_HERE --output C:\exports\review.json --authors "alice,bob"
```

#### トラブルシューティング

**エラー: "Missing required argument: --org"**
- すべての必須引数（`--org`, `--project`, `--repo`, `--pat`, `--output`）が指定されているか確認してください

**終了コード 2 (認証エラー)**
- PAT が正しいか確認してください
- PAT に `Code (Read)` 権限があるか確認してください

**終了コード 3 (API エラー)**
- Azure DevOps への接続を確認してください
- Organization / Project / Repository 名が正しいか確認してください

**終了コード 4 (出力エラー)**
- 出力先ディレクトリが存在するか確認してください
- 出力先ディレクトリへの書き込み権限があるか確認してください

---

### 出力 JSON の使い方

#### JSON ファイルの構造

出力 JSON はフラット構造で、各レコードに PR / Thread / Comment の情報が含まれます:

```json
{
  "meta": {
    "schemaVersion": "1.0.0",
    "exportedAt": "2026-01-07T10:30:00Z",
    "organization": "contoso",
    "project": "MyProject",
    "repository": "my-repo",
    "summary": {
      "totalPullRequests": 200,
      "totalThreads": 450,
      "totalComments": 1250
    }
  },
  "items": [
    {
      "pr": { ... },
      "thread": { ... },
      "comment": { ... }
    }
  ]
}
```

#### LLM で分析する

**ChatGPT / Claude / Gemini などに JSON ファイルを渡す**:

1. JSON ファイルをテキストエディタで開く
2. 内容をコピー
3. LLM のチャットに以下のようなプロンプトと共にペースト:

```
以下は Azure DevOps の過去の PR レビューコメントデータです。
このデータから、よく指摘される項目トップ 10 を抽出してください。

[JSON データをペースト]
```

**ファイルをアップロード可能な LLM の場合**:
- ChatGPT Plus / Claude Pro などでファイルを直接アップロード

#### JSON ファイルのサイズが大きい場合

- **ファイルサイズ > 数十MB の場合**: LLM のコンテキストウィンドウに収まらない可能性があります
- **対処方法**:
  1. 投稿者フィルタを使って特定ユーザーのみをエクスポート
  2. 外部ツール（jq など）で JSON を分割
  3. LLM に一部のデータのみを渡して段階的に分析

---

## 開発者向けクイックスタート

### 前提条件

- **OS**: Windows 10 (21H2) 以降、または Windows 11
- **.NET SDK**: .NET 10 SDK
- **IDE**: Visual Studio 2025 (Preview) または Visual Studio Code
- **Git**: 2.x 以降

---

### 開発環境のセットアップ

#### ステップ 1: リポジトリのクローン

```powershell
git clone https://github.com/yourorg/tool_ado_review_export.git
cd tool_ado_review_export
```

#### ステップ 2: ブランチのチェックアウト

```powershell
git checkout 001-ado-review-export
```

#### ステップ 3: 依存関係の復元

```powershell
dotnet restore
```

#### ステップ 4: ビルド

```powershell
dotnet build -c Debug
```

---

### ビルドと実行

#### Visual Studio を使用する場合

1. `AdoReviewExport.sln` を Visual Studio で開く
2. スタートアッププロジェクトを `AdoReviewExport.UI` に設定
3. F5 キーを押してデバッグ実行

#### コマンドラインを使用する場合

**GUI モード**:
```powershell
cd AdoReviewExport.UI
dotnet run
```

**CLI モード**:
```powershell
cd AdoReviewExport.UI
dotnet run -- export --org contoso --project MyProject --repo my-repo --pat YOUR_PAT_HERE --output test.json
```

---

### テストの実行

#### 単体テストの実行

```powershell
dotnet test tests/Unit
```

#### 統合テストの実行

**事前準備: スタブ API の起動**:
```powershell
cd tests/StubApi
$env:STUB_MODE = "success"
dotnet run &
```

**統合テストの実行**:
```powershell
dotnet test tests/Integration
```

**スタブ API の停止**:
```powershell
taskkill /IM dotnet.exe /F
```

#### すべてのテストを実行

```powershell
dotnet test
```

---

### プロジェクト構成

```
AdoReviewExport/
├── AdoReviewExport.sln
├── AdoReviewExport.UI/           # WinUI 3 GUI + CLI エントリポイント
│   ├── App.xaml.cs
│   ├── Program.cs
│   ├── Views/
│   └── ViewModels/
├── AdoReviewExport.Application/  # ビジネスロジック
│   ├── Services/
│   ├── Models/
│   └── Exceptions/
├── AdoReviewExport.Infrastructure/  # Azure DevOps API クライアント
│   ├── AzureDevOps/
│   └── Json/
├── tests/
│   ├── Unit/                     # 単体テスト (xUnit)
│   ├── Integration/              # 統合テスト (xUnit)
│   └── StubApi/                  # スタブ Azure DevOps API
└── specs/
    └── 001-ado-review-export/
        ├── spec.md               # 機能仕様
        ├── plan.md               # 実装計画
        ├── research.md           # 技術調査
        ├── functional-design.md  # 外部仕様
        ├── integration-test.md   # 統合テスト計画
        └── quickstart.md         # このファイル
```

---

### 開発ワークフロー

#### 新機能の追加

1. `specs/001-ado-review-export/spec.md` を参照して要件を確認
2. `AdoReviewExport.Application` にビジネスロジックを追加
3. `AdoReviewExport.Infrastructure` に必要なインフラストラクチャを追加
4. `AdoReviewExport.UI` に GUI / CLI の実装を追加
5. `tests/Unit` に単体テストを追加
6. `tests/Integration` に統合テストを追加

#### コーディング規約

- **ドキュメント（コミットログ、issue）**: 日本語
- **コード内コメント・XML ドキュメントコメント**: 日本語
- **ソースコード・ログ出力**: 英語
- **例外処理**: 原則として処理失敗時のフォールバックは行わず、エラー・例外を返す
- **例外のログ出力**: `Exception.ToString()` の内容をトレースログに出力

#### デバッグ

**GUI モードのデバッグ**:
- Visual Studio で F5 を押してデバッグ実行
- ブレークポイントを設定して変数の値を確認

**CLI モードのデバッグ**:
- `Program.cs` の `Main` メソッドにブレークポイントを設定
- コマンドライン引数をシミュレート（Visual Studio の「プロジェクトのプロパティ」→「デバッグ」→「コマンドライン引数」に `export --org ... --project ... --repo ... --pat ... --output ...` を入力）

---

### リリースビルド

#### ステップ 1: Release モードでビルド

```powershell
dotnet build -c Release
```

#### ステップ 2: 出力ファイルの確認

ビルド成果物は以下に生成されます:
```
AdoReviewExport.UI\bin\Release\net10.0-windows10.0.19041.0\win-x64\
```

#### ステップ 3: 配布用 zip の作成

```powershell
cd AdoReviewExport.UI\bin\Release\net10.0-windows10.0.19041.0\win-x64
Compress-Archive -Path * -DestinationPath AdoReviewExport-v1.0.0-win-x64.zip
```

---

### よくある問題と解決方法

#### ビルドエラー: "Windows App SDK が見つかりません"

**原因**: Windows App SDK が正しくインストールされていない

**解決方法**:
```powershell
dotnet restore
```

または、Visual Studio Installer で「Windows App SDK」をインストール

#### 実行エラー: "DLL が見つかりません"

**原因**: 依存 DLL が出力ディレクトリにコピーされていない

**解決方法**:
```powershell
dotnet clean
dotnet build -c Release
```

#### 統合テストが失敗する

**原因**: スタブ API が起動していない

**解決方法**:
```powershell
cd tests/StubApi
dotnet run &
```

---

### 参考リンク

- **Azure DevOps REST API ドキュメント**: https://learn.microsoft.com/en-us/rest/api/azure/devops/
- **WinUI 3 ドキュメント**: https://learn.microsoft.com/en-us/windows/apps/winui/winui3/
- **.NET 10 ドキュメント**: https://learn.microsoft.com/en-us/dotnet/
- **Polly (リトライライブラリ)**: https://github.com/App-vNext/Polly

---

### サポート

質問や問題がある場合は、以下の方法でサポートを受けることができます:

- **GitHub Issues**: https://github.com/yourorg/tool_ado_review_export/issues
- **社内チャット**: #ado-review-export チャンネル
- **メール**: dev-support@yourorg.com

---

**文書バージョン**: 1.0.0  
**最終更新日**: 2026-01-07
