````markdown
# Implementation Plan: Azure DevOps PR Review Comment Exporter

**Branch**: `001-ado-review-export` | **Date**: 2026-01-07 | **Spec**: [spec.md](spec.md)
**Input**: Feature specification from `/specs/001-ado-review-export/spec.md`

## Summary

Windows デスクトップアプリケーションとして、Azure DevOps の単一リポジトリから Pull Request のレビューコメントを収集し、LLM で分析可能な構造化 JSON ファイルとしてエクスポートする。GUI での対話的操作と CLI でのバッチ実行の両方に対応し、同一 exe で両モードを実現する。

技術スタックは .NET 10 + WinUI 3 (Unpackaged) を採用し、Azure DevOps REST API v7.2 からデータを取得する。大量データ処理ではページング取得とストリーミング出力でメモリ効率を最適化し、エラーハンドリングは Polly によるリトライポリシーで信頼性を確保する。

## Technical Context

**Language/Version**: .NET 10 (C# 13)  
**Primary Dependencies**: WinUI 3 (Windows App SDK 1.7+), System.Net.Http, System.Text.Json, Polly 8.x, Microsoft.Extensions.DependencyInjection, Microsoft.Extensions.Logging  
**Storage**: ファイル (JSON 出力)  
**Testing**: xUnit + Moq (単体テスト), スタブ API サーバー (統合テスト)  
**Target Platform**: Windows 10 (21H2) 以降、Windows 11  
**Project Type**: Desktop application (single exe, GUI/CLI hybrid)  
**Performance Goals**: 1000 PRs を 30分以内にエクスポート、メモリ使用量 2GB 以下  
**Constraints**: Unpackaged (非 MSIX) 配布、PAT をメモリ上でのみ保持（ファイル保存禁止）、API レート制限への対応  
**Scale/Scope**: 単一リポジトリ、最大 10,000 PRs / 100,000 コメント想定

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

### Initial Check (Pre-Research)

**Status**: ⚠️ CONDITIONAL PASS

⚠️ **注意**: Constitution ファイルはテンプレート状態（具体的な原則未定義）です。以下の一般的な開発原則に基づいて評価されています。プロジェクト初期化時に憲章を確定してください。

1. **テスト戦略**: ✓ 統合テストはスタブ API を使用し、実OS環境を変更しない（憲章原則5に準拠）
2. **依存関係管理**: ✓ レイヤードアーキテクチャにより、UI → Application → Infrastructure の明確な依存関係
3. **単一責任**: ✓ 各レイヤーが明確な責務を持つ（UI: プレゼンテーション、Application: ビジネスロジック、Infrastructure: 外部API通信）
4. **エラーハンドリング**: ✓ ドメイン例外の階層により、エラー種別を明確に分類（copilot-instructions 原則3に準拠）

### Post-Design Check (Phase 1 Complete)

**Status**: ⚠️ CONDITIONAL PASS

⚠️ 憲章の最終確定待ち。以下は一般原則ベースの評価：

1. **テスト可能性**: ✓ すべての外部依存（Azure DevOps API）はインターフェース化され、テスト時にモック可能
2. **セキュリティ**: ✓ PAT のメモリ上保持、ログマスキング、最小権限の原則を遵守
3. **パフォーマンス**: ✓ ストリーミング出力とページング処理でメモリ効率を最適化
4. **保守性**: ✓ 構造化ログ、エラーハンドリング、ドキュメント（外部仕様書、統合テスト計画、クイックスタート）の整備

**違反なし**: 複雑性の正当化は不要

## Project Structure

### Documentation (this feature)

```text
specs/001-ado-review-export/
├── spec.md              # 機能仕様
├── plan.md              # このファイル (実装計画)
├── research.md          # Phase 0 出力 (技術調査)
├── functional-design.md # Phase 1 出力 (外部仕様)
├── integration-test.md  # Phase 1 出力 (統合テスト計画)
├── quickstart.md        # Phase 1 出力 (クイックスタート)
└── tasks.md             # Phase 2 出力 (タスク分解) ✓ COMPLETED
```

### Source Code (repository root)

```text
AdoReviewExport/
├── AdoReviewExport.sln
├── AdoReviewExport.UI/                 # WinUI 3 GUI + CLI エントリポイント
│   ├── App.xaml
│   ├── App.xaml.cs
│   ├── Program.cs                      # Main (GUI/CLI 分岐)
│   ├── Views/
│   │   ├── MainWindow.xaml
│   │   └── MainWindow.xaml.cs
│   ├── ViewModels/
│   │   └── MainViewModel.cs
│   └── Helpers/
│       └── ConsoleHelper.cs            # CLI モードのコンソール出力
├── AdoReviewExport.Application/        # ビジネスロジック
│   ├── Services/
│   │   ├── IExportService.cs
│   │   └── ExportService.cs
│   ├── Models/
│   │   ├── ExportRequest.cs
│   │   ├── ExportResult.cs
│   │   └── ExportProgress.cs
│   └── Exceptions/
│       ├── AdoExportException.cs       # 基底例外
│       ├── InputValidationException.cs
│       ├── AuthenticationException.cs
│       ├── ApiException.cs
│       └── OutputException.cs
├── AdoReviewExport.Infrastructure/     # 外部API通信・永続化
│   ├── AzureDevOps/
│   │   ├── IAdoApiClient.cs
│   │   ├── AdoApiClient.cs
│   │   └── Dtos/
│   │       ├── PullRequestDto.cs
│   │       ├── ThreadDto.cs
│   │       ├── CommentDto.cs
│   │       ├── IdentityDto.cs
│   │       └── ThreadContextDto.cs
│   ├── Json/
│   │   ├── IJsonExporter.cs
│   │   └── JsonExporter.cs
│   └── Http/
│       └── RetryPolicyFactory.cs       # Polly リトライポリシー
├── tests/
│   ├── Unit/                           # 単体テスト (xUnit)
│   │   ├── ExportServiceTests.cs
│   │   └── AdoApiClientTests.cs
│   ├── Integration/                    # 統合テスト (xUnit)
│   │   ├── GuiModeTests.cs
│   │   ├── CliModeTests.cs
│   │   ├── DataOutputTests.cs
│   │   ├── ErrorHandlingTests.cs
│   │   └── SecurityTests.cs
│   └── StubApi/                        # スタブ Azure DevOps API
│       ├── Program.cs
│       └── Controllers/
│           └── PullRequestsController.cs
└── .github/
    ├── agents/
    │   └── copilot-instructions.md     # GitHub Copilot コンテキスト
    └── workflows/
        └── ci.yml                      # CI/CD パイプライン
```

**Structure Decision**: Desktop application (single exe) 構成を採用。WinUI 3 の UI プロジェクトをエントリポイントとし、Application / Infrastructure レイヤーをクラスライブラリとして分離。これにより、ビジネスロジックと UI の疎結合を実現し、テスト可能性を確保する。

## Complexity Tracking

> **違反なし**: Constitution Check で違反が検出されなかったため、このセクションは空欄

---

## Phase Summary

### Phase 0: Research (Complete ✓)

**成果物**: [research.md](research.md)

**主要な技術判断**:
1. .NET 10 + WinUI 3 Unpackaged の採用決定
2. 同一 exe での GUI/CLI 両対応パターンの確立
3. Azure DevOps REST API v7.2 の利用方針
4. JSON スキーマ設計（フラット形式）
5. 大量データ処理の効率化戦略（ストリーミング出力）
6. エラーハンドリング戦略（ドメイン例外 + Polly）
7. PAT 保護戦略（メモリ上保持 + ログマスキング）
8. プロジェクト構成（レイヤードアーキテクチャ）

### Phase 1: Design (Complete ✓)

**成果物**:
- [functional-design.md](functional-design.md) - 外部仕様書
- [integration-test.md](integration-test.md) - 統合テスト計画
- [quickstart.md](quickstart.md) - クイックスタートガイド
- [.github/agents/copilot-instructions.md](../../.github/agents/copilot-instructions.md) - エージェントコンテキスト

**設計の主要ポイント**:
1. **GUI モード**: WinUI 3 による直感的な操作画面、進捗表示、エラーダイアログ
2. **CLI モード**: 標準出力へのログ、終了コード、環境変数での PAT 指定
3. **データ取得**: ページング処理、並列スレッド取得、リトライ・バックオフ
4. **JSON 出力**: フラット形式（1コメント=1レコード）、UTF-8 エンコーディング
5. **統合テスト**: スタブ API サーバーで実環境を変更せずにテスト
6. **セキュリティ**: PAT のメモリ上保持、ログマスキング、HTTPS 通信

### Phase 2: Tasks (Not Yet Started)

次のステップ: `/speckit.implement` コマンドを実行して実装を開始する（Speckit Analysis 結果の既知課題を反映済み）

---

**Version**: 1.0.0  
**Last Updated**: 2026-01-07
````
