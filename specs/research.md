# Technical Research: Azure DevOps PR Review Comment Exporter

**Feature Branch**: `001-ado-review-export`  
**Date**: January 7, 2026  
**Status**: Research Complete

このドキュメントは、仕様実現のための技術選定と設計判断の根拠を記録する。

---

## 1. .NET 10 + WinUI 3 Unpackaged 実行の実現方式

### Decision: Unpackaged WinUI 3 アプリケーションとして実装

### Rationale

- **Unpackaged の利点**:
  - MSIX パッケージングが不要で、単一 exe として配布可能
  - インストーラー不要（xcopy deployment）
  - 企業環境での配布が容易（グループポリシーやスクリプトでコピー可能）
  - 開発・デバッグが高速（F5 実行で即起動）
  
- **.NET 10 対応**:
  - Windows App SDK 1.7+ (.NET 10 対応予定、2026年1月時点では 1.6 が最新だが Preview で 1.7 利用可能)
  - `<WindowsAppSDK>` パッケージ参照で WinUI 3 を利用
  - `<OutputType>WinExe</OutputType>` で GUI アプリとしてビルド
  
- **Unpackaged での制約**:
  - UWP API の一部（制限付き機能）は利用不可 → 本アプリでは利用しないため問題なし
  - ファイルシステムへのフルアクセスが可能（Packaged より自由度が高い）

### Alternatives Considered

1. **Packaged (MSIX)**:
   - **却下理由**: インストールプロセスが必要、企業環境での配布が複雑、外部ロケーション (External Location) も検討余地はあるが、初期リリースではシンプルな Unpackaged を優先
   
2. **WPF**:
   - **却下理由**: WinUI 3 の方がモダンな UI/UX を提供し、将来的な Windows のネイティブ UI として推奨されている

3. **Avalonia / MAUI**:
   - **却下理由**: クロスプラットフォーム対応は不要（Windows 専用）、WinUI 3 の方が Windows ネイティブとして高品質

---

## 2. 同一 exe での GUI/CLI 両対応パターン

### Decision: コマンドライン引数による早期分岐 + `OutputType=WinExe`

### Rationale

- **実現方式**:
  ```csharp
  // Program.cs または App.xaml.cs の Main
  [STAThread]
  static int Main(string[] args)
  {
      // 早期にコマンドライン引数を解析
      if (args.Length > 0 && args[0].StartsWith("--"))
      {
          // CLI モード: WinUI 初期化をスキップ
          return RunCliMode(args);
      }
      else
      {
          // GUI モード: WinUI アプリケーションを起動
          Application.Start((p) =>
          {
              var context = new DispatcherQueueSynchronizationContext(
                  DispatcherQueue.GetForCurrentThread());
              SynchronizationContext.SetSynchronizationContext(context);
              new App();
          });
          return 0;
      }
  }
  ```

- **利点**:
  - 同一バイナリで両モードをサポート
  - CLI モードでは WinUI の重い初期化をスキップしてパフォーマンス向上
  - `OutputType=WinExe` でコンソールウィンドウを抑制（GUI 起動時にコンソールが出ない）
  - CLI 実行時には `AttachConsole(-1)` または別途コンソール割り当てで標準出力を利用可能

- **コンソール出力の扱い**:
  - CLI モードでは `Console.WriteLine` で進捗ログを出力
  - `OutputType=WinExe` でも、コマンドラインから起動すれば親コンソールに出力可能
  - 必要なら `kernel32.dll` の `AttachConsole` / `AllocConsole` をP/Invokeで呼ぶ

### Alternatives Considered

1. **2つの exe を用意（GUI 用と CLI 用）**:
   - **却下理由**: 配布物が増える、コアロジックの共有が複雑、ユーザーが混乱

2. **OutputType=Exe（常にコンソールウィンドウが出る）**:
   - **却下理由**: GUI 起動時にコンソールウィンドウが表示されてしまい UX が悪い

3. **WinUI のバックグラウンドタスク拡張を利用**:
   - **却下理由**: Packaged アプリ専用の機能、Unpackaged では利用不可

---

## 3. Azure DevOps REST API の利用方法

### Decision: Azure DevOps REST API v7.2 を利用し、HttpClient + System.Text.Json で実装

### Rationale

- **API バージョン**: 
  - v7.2 (2025年時点で最新かつ安定)
  - Pull Request API、Threads API、Comments API をサポート
  - ドキュメント: https://learn.microsoft.com/en-us/rest/api/azure/devops/

- **主要なエンドポイント**:
  ```
  GET https://dev.azure.com/{organization}/{project}/_apis/git/repositories/{repositoryId}/pullrequests?api-version=7.2
  GET https://dev.azure.com/{organization}/{project}/_apis/git/repositories/{repositoryId}/pullrequests/{pullRequestId}/threads?api-version=7.2
  GET https://dev.azure.com/{organization}/{project}/_apis/git/repositories/{repositoryId}/pullrequests/{pullRequestId}/threads/{threadId}/comments?api-version=7.2
  ```

- **認証**:
  - Personal Access Token (PAT) を `Authorization: Basic {base64(":"+ pat)}` ヘッダーで送信
  - 必要なスコープ: `vso.code` (Code Read)

- **ページング**:
  - `$top` / `$skip` パラメータでページサイズと開始位置を指定
  - レスポンスに `count` と `value` 配列が含まれる
  - 継続トークンは一部のエンドポイントで `continuationToken` ヘッダーとして返される

- **リトライ・レート制限**:
  - HTTP 429 (Too Many Requests) が返された場合は `Retry-After` ヘッダーを尊重
  - 指数バックオフで最大3回リトライ (1秒 → 2秒 → 4秒)
  - タイムアウトは 30秒/リクエスト

### Alternatives Considered

1. **Azure DevOps .NET Client Libraries (Microsoft.TeamFoundationServer.Client)**:
   - **却下理由**: 重厚なライブラリで依存関係が多い、REST API で十分に対応可能

2. **GraphQL API**:
   - **却下理由**: Azure DevOps の GraphQL は限定的な機能のみ、PR/コメント取得には REST が主流

---

## 4. JSON スキーマ設計（LLM 消費を考慮）

### Decision: フラット化された 1コメント=1レコード 形式

### Rationale

- **スキーマ構造**:
  ```json
  {
    "meta": {
      "schemaVersion": "1.0.0",
      "exportedAt": "2026-01-07T10:30:00Z",
      "organization": "contoso",
      "project": "MyProject",
      "repository": "my-repo",
      "repositoryId": "abc123",
      "filters": {
        "authors": ["user1", "user2"],
        "status": "completed"
      },
      "summary": {
        "totalPullRequests": 150,
        "totalThreads": 320,
        "totalComments": 850
      },
      "appVersion": "1.0.0"
    },
    "items": [
      {
        "pr": {
          "id": 1234,
          "title": "Feature: Add new API",
          "url": "https://dev.azure.com/contoso/MyProject/_git/my-repo/pullrequest/1234",
          "status": "completed",
          "createdBy": {"displayName": "Alice", "uniqueName": "alice@contoso.com"},
          "creationDate": "2025-12-01T10:00:00Z",
          "closedDate": "2025-12-05T15:30:00Z",
          "sourceRefName": "refs/heads/feature/new-api",
          "targetRefName": "refs/heads/main"
        },
        "thread": {
          "id": 5678,
          "status": "fixed",
          "publishedDate": "2025-12-02T14:20:00Z",
          "threadContext": {
            "filePath": "/src/api.cs",
            "rightFileStart": {"line": 42, "offset": 0},
            "rightFileEnd": {"line": 45, "offset": 0}
          }
        },
        "comment": {
          "id": 91011,
          "parentCommentId": 0,
          "author": {"displayName": "Bob", "uniqueName": "bob@contoso.com"},
          "publishedDate": "2025-12-02T14:20:00Z",
          "lastUpdatedDate": "2025-12-02T14:20:00Z",
          "commentType": "text",
          "content": "このメソッドは null チェックが必要です。"
        }
      }
    ]
  }
  ```

- **フラット化の利点**:
  - LLM が直接パースしやすい（ネストが浅い）
  - 1レコードごとに PR + Thread + Comment の全コンテキストが含まれる
  - SQL や Pandas でフィルタ・集計しやすい

- **代替案（階層構造）との比較**:
  - 階層構造 (PR → Threads → Comments) はデータ重複が少ないが、LLM が参照を追う必要がある
  - フラット構造は PR/Thread 情報が重複するが、データサイズは許容範囲内（1万コメントでも数MB程度）

### Alternatives Considered

1. **階層構造 (PR → Threads → Comments のネスト)**:
   - **却下理由**: LLM が複数レベルの参照を追う必要があり、解析が複雑

2. **NDJSON (Newline Delimited JSON)**:
   - **却下理由**: ストリーミング処理には向いているが、メタ情報を先頭に置きにくい

3. **CSV 形式**:
   - **却下理由**: コメント本文に改行や特殊文字が含まれるとエスケープが複雑、JSON の方が構造化に適している

---

## 5. 大量データ処理の効率化（メモリ・パフォーマンス）

### Decision: ページング取得 + バッチ書き込み + Utf8JsonWriter によるストリーミング出力

### Rationale

- **メモリ効率化**:
  - Azure DevOps API から取得したデータをすべてメモリに保持せず、バッチごとに処理
  - `Utf8JsonWriter` を使用してファイルに直接ストリーミング書き込み（`JsonSerializer.Serialize` の一括書き込みを避ける）
  
- **処理フロー**:
  1. PR をページング取得（例: 50件ずつ）
  2. 各 PR について Threads を取得
  3. 各 Thread について Comments を取得
  4. 取得したコメントを即座に JSON に書き込み（メモリから解放）
  
- **並列処理**:
  - PR の Threads 取得は並列化可能（`Parallel.ForEachAsync` または `Task.WhenAll`）
  - ただし API レート制限を考慮して並列度を制限（例: 最大5並列）
  
- **進捗報告**:
  - GUI: 進捗バーと「現在処理中の PR 番号」をリアルタイム更新
  - CLI: 標準出力に「Processing PR 10/200」などを出力

### Alternatives Considered

1. **全データをメモリに読み込んでから一括出力**:
   - **却下理由**: 大量の PR（1000件以上）で OutOfMemoryException のリスク

2. **SQLite などのローカルDBに一時保存**:
   - **却下理由**: 追加の依存関係、最終的には JSON 出力するため中間ストレージは不要

3. **非同期ストリームでの完全ストリーミング処理**:
   - **却下理由**: 実装が複雑、進捗率の計算が困難（総件数を事前に取得する必要がある）

---

## 6. エラーハンドリングとリトライ戦略

### Decision: ドメイン例外の階層 + Polly によるリトライポリシー

### Rationale

- **例外の分類**:
  - `AdoExportException` (基底クラス): すべてのドメイン例外の親
    - `InputValidationException`: 入力パラメータ不正
    - `AuthenticationException`: PAT 無効・権限不足
    - `ApiException`: Azure DevOps API エラー（404, 500など）
    - `NetworkException`: タイムアウト・接続エラー
    - `OutputException`: ファイル書き込みエラー
  
- **リトライ対象**:
  - `NetworkException` および HTTP 429 (Too Many Requests), 503 (Service Unavailable)
  - 指数バックオフ: 1秒 → 2秒 → 4秒（最大3回）
  
- **リトライ不可**:
  - `AuthenticationException` (PAT が無効な場合、リトライしても無駄)
  - `InputValidationException` (入力エラーはユーザーが修正する必要がある)
  
- **Polly ライブラリの利用**:
  ```csharp
  var retryPolicy = Policy
      .Handle<HttpRequestException>()
      .Or<TaskCanceledException>()
      .OrResult<HttpResponseMessage>(r => 
          r.StatusCode == HttpStatusCode.TooManyRequests ||
          r.StatusCode == HttpStatusCode.ServiceUnavailable)
      .WaitAndRetryAsync(3, retryAttempt => 
          TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)));
  ```

### Alternatives Considered

1. **例外をすべて `Exception` で捕捉**:
   - **却下理由**: エラーの種類を区別できず、適切なエラーメッセージをユーザーに提示できない

2. **リトライロジックを手動実装**:
   - **却下理由**: Polly の方が実績があり、テスト済み

---

## 7. セキュリティ: PAT の保護

### Decision: メモリ上でのみ保持 + SecureString は使用しない

### Rationale

- **PAT の取り扱い**:
  - GUI: PasswordBox (WinUI 3) で入力、プレーンテキストとしてメモリ保持（処理完了後に明示的にクリア）
  - CLI: コマンドライン引数または環境変数 (`AZDO_PAT`) から読み取り
  - ファイルに保存しない（設定ファイルや履歴に残さない）
  
- **ログからのマスキング**:
  - ログ出力前に PAT を `***REDACTED***` に置換
  - 例外メッセージにも PAT を含めない（`HttpClient` のログフィルタを設定）
  
- **SecureString を使わない理由**:
  - .NET Core/.NET 5+ では SecureString は非推奨（クロスプラットフォームでサポートされない）
  - メモリダンプ攻撃への防御効果は限定的（攻撃者がメモリにアクセスできる場合、他の方法でも取得可能）
  - シンプルな `string` を使い、使用後に GC を促す方が現実的

### Alternatives Considered

1. **PAT を暗号化してファイルに保存**:
   - **却下理由**: 暗号キーの管理が必要、ユーザーが明示的に保存を選ばない限り不要

2. **Windows Credential Manager に保存**:
   - **却下理由**: 初期リリースではスコープ外、将来的な拡張として検討可能

---

## 8. プロジェクト構成と依存関係管理

### Decision: レイヤードアーキテクチャ（UI / Application / Infrastructure）

### Rationale

- **プロジェクト分割**:
  ```
  AdoReviewExport.sln
  ├── AdoReviewExport.UI (WinUI 3 - exe)
  │   ├── Views/
  │   ├── ViewModels/
  │   └── App.xaml.cs
  ├── AdoReviewExport.Application (Class Library)
  │   ├── Services/
  │   │   ├── IExportService.cs
  │   │   └── ExportService.cs
  │   ├── Models/
  │   └── Exceptions/
  ├── AdoReviewExport.Infrastructure (Class Library)
  │   ├── AzureDevOps/
  │   │   ├── IAdoApiClient.cs
  │   │   └── AdoApiClient.cs
  │   └── Json/
  │       └── JsonExporter.cs
  └── AdoReviewExport.CLI (単なるエントリポイント、UI プロジェクト内で Main 分岐で実現可能)
  ```

- **依存関係**:
  - UI → Application → Infrastructure
  - Application は Infrastructure のインターフェース (`IAdoApiClient`) に依存
  - テスト時は Infrastructure をモックに差し替え可能

- **主要なライブラリ**:
  - `Microsoft.WindowsAppSDK` (WinUI 3)
  - `System.Net.Http` (HttpClient)
  - `System.Text.Json` (JSON シリアライズ)
  - `Polly` (リトライポリシー)
  - `Microsoft.Extensions.DependencyInjection` (DI コンテナ)
  - `Microsoft.Extensions.Logging` (構造化ログ)

### Alternatives Considered

1. **単一プロジェクト構成**:
   - **却下理由**: UI とロジックが密結合、テストが困難

2. **Clean Architecture (4層: Domain, Application, Infrastructure, Presentation)**:
   - **却下理由**: 本アプリの規模には過剰、3層で十分

---

## 9. テスト戦略

### Decision: 単体テストは HttpMessageHandler モック、統合テストはスタブ API

### Rationale

- **単体テスト (xUnit + Moq)**:
  - `AdoApiClient` のテスト: `HttpMessageHandler` を差し替えて疑似応答を返す
  - `ExportService` のテスト: `IAdoApiClient` をモックして各種エラーケースを検証
  - JSON スキーマのスナップショットテスト: 期待される JSON 構造と実際の出力を比較

- **統合テスト**:
  - スタブの Azure DevOps API サーバーを用意（ASP.NET Core Minimal API で簡易実装）
  - 実際の HTTP 通信を行うが、実環境を変更しない
  - CI で実行可能（Docker コンテナでスタブ API を起動）

- **パフォーマンステスト**:
  - 1000 PR, 10万コメントの疑似データでメモリ使用量・処理時間を計測
  - CI では小規模データ（100 PR）で実行、ローカルで大規模テスト

### Alternatives Considered

1. **実際の Azure DevOps 環境でテスト**:
   - **却下理由**: CI で実行できない、テスト用のリポジトリ管理が必要

2. **統合テストを完全にスキップ**:
   - **却下理由**: HTTP 通信のエラーハンドリングを検証できない

---

## 10. ログ設計

### Decision: Microsoft.Extensions.Logging + 構造化ログ

### Rationale

- **ログレベル**:
  - `Trace`: API リクエスト/レスポンスの詳細（開発時のみ）
  - `Debug`: 処理フロー（PR 取得開始、完了など）
  - `Information`: ユーザーに見せる進捗（「Processing PR 10/200」）
  - `Warning`: リトライ発生、一部データ欠落
  - `Error`: 例外発生（スタックトレース含む）
  - `Critical`: アプリケーションクラッシュ

- **ログ出力先**:
  - GUI: メモリ内バッファ → UI のログビューに表示
  - CLI: 標準出力 (Information 以上) + ファイル (Debug 以上)
  - ファイル: `%TEMP%\AdoReviewExport\logs\app-{date}.log` (ローテーション付き)

- **PII/Secret のマスキング**:
  - PAT を含む文字列を検出し、`***REDACTED***` に置換
  - ユーザー名・メールアドレスはそのまま記録（レビューコメント分析に必要）

### Alternatives Considered

1. **Serilog**:
   - **却下理由**: `Microsoft.Extensions.Logging` で十分、追加依存を避ける

2. **Application Insights / Azure Monitor**:
   - **却下理由**: クラウド依存、オフライン環境で動作しない

---

## 技術スタック一覧

| カテゴリ | 技術 | バージョン | 用途 |
|---------|------|-----------|------|
| フレームワーク | .NET | 10 | アプリケーションランタイム |
| UI | WinUI 3 (Windows App SDK) | 1.7+ | GUI 実装 |
| HTTP クライアント | System.Net.Http | .NET 10 組み込み | Azure DevOps API 通信 |
| JSON | System.Text.Json | .NET 10 組み込み | シリアライズ・デシリアライズ |
| リトライ | Polly | 8.x | API リトライポリシー |
| DI | Microsoft.Extensions.DependencyInjection | .NET 10 組み込み | 依存性注入 |
| ログ | Microsoft.Extensions.Logging | .NET 10 組み込み | 構造化ログ |
| テスト | xUnit + Moq | 最新 | 単体テスト |
| ビルド | MSBuild / Visual Studio 2025 | - | プロジェクトビルド |

---

## まとめ

すべての技術選定が完了し、NEEDS CLARIFICATION はゼロとなった。次のフェーズで外部仕様書（functional-design.md）および統合テスト計画（integration-test.md）を作成する。
