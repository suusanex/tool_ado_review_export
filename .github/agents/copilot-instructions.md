````chatagent
# tool_ado_review_export Development Guidelines

Auto-generated from all feature plans. Last updated: 2026-01-07

## Active Technologies

### Language & Framework
- .NET 10 (C# 13)
- WinUI 3 (Windows App SDK 1.7+) - Unpackaged deployment
- Desktop application (single exe, GUI/CLI hybrid)

### Dependencies
- System.Net.Http - Azure DevOps REST API v7.2 通信
- System.Text.Json - JSON シリアライズ/デシリアライズ
- Polly 8.x - API リトライポリシー
- Microsoft.Extensions.DependencyInjection - 依存性注入
- Microsoft.Extensions.Logging - 構造化ログ

### Storage
- ファイル (JSON 出力) - フラット形式（1コメント=1レコード）

### Testing
- xUnit + Moq (単体テスト)
- スタブ API サーバー (統合テスト)

## Project Structure

```text
AdoReviewExport/
├── AdoReviewExport.sln
├── AdoReviewExport.UI/                 # WinUI 3 GUI + CLI エントリポイント
├── AdoReviewExport.Application/        # ビジネスロジック
├── AdoReviewExport.Infrastructure/     # 外部API通信・永続化
└── tests/
    ├── Unit/                           # 単体テスト
    ├── Integration/                    # 統合テスト
    └── StubApi/                        # スタブ Azure DevOps API
```

## Architecture

- **レイヤードアーキテクチャ**: UI → Application → Infrastructure
- **依存関係の方向**: Presentation → Application → Infrastructure (インターフェース経由)
- **テスト戦略**: 実OS環境を変更しない、スタブ/モックを使用

## Key Patterns

- **同一 exe での GUI/CLI 両対応**: Program.Main でコマンドライン引数を解析し早期分岐
- **ストリーミング出力**: Utf8JsonWriter で大量データをメモリ効率的に処理
- **リトライポリシー**: Polly による指数バックオフ（HTTP 429, 503 対応）
- **ドメイン例外**: AdoExportException 基底クラスから派生した例外階層

## Security

- **PAT 保護**: メモリ上でのみ保持、ファイル保存禁止
- **ログマスキング**: PAT を `***REDACTED***` に置換
- **最小権限**: `vso.code` (Read) スコープのみ

## Commands

### Build
```powershell
dotnet build -c Release
```

### Test
```powershell
# 単体テスト
dotnet test tests/Unit

# 統合テスト（要: スタブ API 起動）
cd tests/StubApi; dotnet run &
dotnet test tests/Integration
```

### Run
```powershell
# GUI モード
.\AdoReviewExport.exe

# CLI モード
.\AdoReviewExport.exe export --org contoso --project MyProject --repo my-repo --pat YOUR_PAT --output review.json
```

## Code Style

### 一般
- ドキュメント（コミットログ、issue）: 日本語
- コード内コメント・XML ドキュメントコメント: 日本語
- ソースコード・ログ出力: 英語

### エラーハンドリング
- 処理失敗時のフォールバックは行わず、エラー・例外を返す
- 全ての例外は Exception.ToString() をトレースログに出力
- 例外を再throwせずに捨てる場合は、その場でトレースログを出力

### テスト
- UnitTest と IntegrationTest は実OS環境を変更しない
- OS依存処理はインターフェースで抽象化し、テスト時はスタブ/モックを注入

## Recent Changes
- 001-ado-review-export: 初回設計完了（.NET 10 + WinUI 3 Unpackaged、GUI/CLI 両対応、Azure DevOps REST API v7.2）

<!-- MANUAL ADDITIONS START -->
<!-- MANUAL ADDITIONS END -->

````
