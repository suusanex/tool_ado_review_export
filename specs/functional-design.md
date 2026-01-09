# 外部仕様書: Azure DevOps PR Review Comment Exporter

**Feature Branch**: `001-ado-review-export`  
**Date**: January 7, 2026  
**Status**: Design Complete  
**Version**: 1.0.0

---

## 概要

Azure DevOps の単一リポジトリから Pull Request のレビューコメント（スレッド・コメント）を収集し、LLM で分析可能な構造化 JSON ファイルとしてエクスポートする Windows デスクトップアプリケーション。GUI での対話的操作と、CLI でのバッチ実行の両方に対応する。

**主要機能**:
- Azure DevOps REST API v7.2 を利用した PR・スレッド・コメントの取得
- 投稿者フィルタによる特定ユーザーのコメント抽出（オプション）
- GUI モード: WinUI 3 による直感的な操作画面、進捗表示、エラーハンドリング
- CLI モード: 自動化・CI/CD 対応、標準出力へのログ、終了コード
- LLM 消費を考慮したフラット JSON スキーマ（1コメント=1レコード）
- 大量データ対応: ページング、ストリーミング出力、メモリ効率化

---

## 機能

### 1. データ収集機能

#### 1.1. Azure DevOps 接続
- **Organization / Project / Repository の指定**: ユーザーが対象リポジトリを明示的に指定
- **Personal Access Token (PAT) 認証**: `vso.code` (Read) スコープの PAT で認証
- **接続検証**: 最初の API 呼び出しで権限・接続を検証し、エラー時にはわかりやすいメッセージを表示

#### 1.2. Pull Request 取得
- **全 PR の列挙**: リポジトリ内のすべての PR を取得（Active / Completed / Abandoned を含む）
- **ページング処理**: Azure DevOps API の `$top` / `$skip` パラメータで分割取得（1ページ50件）
- **取得データ**: PR 番号、タイトル、説明、作成者、作成日時、更新日時、ステータス、ソース/ターゲットブランチ

#### 1.3. レビューコメント取得
- **スレッド取得**: 各 PR のレビューコメントスレッドを全件取得
- **コメント取得**: 各スレッド内のすべてのコメントを取得（返信含む）
- **取得データ**:
  - **スレッド**: スレッド ID、ファイルパス、行番号、ステータス（Active / Fixed / Closed など）、公開日時
  - **コメント**: コメント ID、親コメント ID、投稿者、公開日時、最終更新日時、コメントタイプ、本文
- **削除コメントの扱い**: Azure DevOps API が返す情報をそのまま保持（`isDeleted` フラグも記録）

#### 1.4. フィルタリング（オプション）
- **投稿者フィルタ**: カンマ区切りで複数のユーザー名を指定可能（例: `alice,bob`）
- **フィルタロジック**: コメントの `author.displayName` または `author.uniqueName` と部分一致（大文字小文字を区別しない）
- **PR 情報の保持**: フィルタが適用されても、PR 情報はすべて出力される（コメントのみがフィルタされる）

#### 1.5. エラーハンドリング・リトライ
- **接続エラー**: タイムアウト、ネットワークエラーが発生した場合、指数バックオフで最大3回リトライ（1秒 → 2秒 → 4秒）
- **API レート制限**: HTTP 429 (Too Many Requests) が返された場合、`Retry-After` ヘッダーを尊重してリトライ
- **権限エラー**: HTTP 401 / 403 が返された場合、リトライせずにエラーメッセージを表示
- **リトライ失敗時**: 最大リトライ回数を超えた場合、エラーを表示してエクスポートを中断

### 2. データ出力機能

#### 2.1. JSON 出力スキーマ
- **ファイル形式**: UTF-8 エンコーディングの JSON ファイル（1ファイル）
- **ファイル名のデフォルト**: `ado-review-export-{timestamp}.json`（例: `ado-review-export-20260107-103000.json`）
- **スキーマ構造**: フラット形式（1コメント=1レコード）で、各レコードに PR / Thread / Comment のすべての情報を含む

**スキーマ例**:
```json
{
  "meta": {
    "schemaVersion": "1.0.0",
    "exportedAt": "2026-01-07T10:30:00Z",
    "organization": "contoso",
    "project": "MyProject",
    "repository": "my-repo",
    "repositoryId": "abc123-def456",
    "filters": {
      "authors": ["alice", "bob"]
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
        "title": "Feature: Add new API endpoint",
        "description": "This PR adds a new API endpoint for user management.",
        "url": "https://dev.azure.com/contoso/MyProject/_git/my-repo/pullrequest/1234",
        "status": "completed",
        "createdBy": {
          "displayName": "Alice Smith",
          "uniqueName": "alice@contoso.com",
          "id": "user-id-123"
        },
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
          "filePath": "/src/Controllers/UserController.cs",
          "rightFileStart": {
            "line": 42,
            "offset": 0
          },
          "rightFileEnd": {
            "line": 45,
            "offset": 0
          }
        },
        "properties": {}
      },
      "comment": {
        "id": 91011,
        "parentCommentId": 0,
        "author": {
          "displayName": "Bob Johnson",
          "uniqueName": "bob@contoso.com",
          "id": "user-id-456"
        },
        "publishedDate": "2025-12-02T14:20:00Z",
        "lastUpdatedDate": "2025-12-02T14:20:00Z",
        "commentType": "text",
        "content": "このメソッドは null チェックが必要です。\n引数が null の場合の動作を明確にしてください。",
        "isDeleted": false
      }
    }
  ]
}
```

#### 2.2. 出力ファイルの保存
- **保存先の指定**: ユーザーがファイル選択ダイアログ（GUI）またはコマンドライン引数（CLI）で指定
- **デフォルト保存先**: GUI の場合は「ドキュメント」フォルダ、CLI の場合はカレントディレクトリ
- **上書き確認**: ファイルが既に存在する場合、GUI では確認ダイアログを表示、CLI では上書き実行
- **書き込みエラー**: ディスクスペース不足、権限エラーの場合、エラーメッセージを表示してエクスポートを中断

#### 2.3. ストリーミング出力
- **メモリ効率化**: 全データをメモリに保持せず、`Utf8JsonWriter` で順次ファイルに書き込み
- **処理フロー**:
  1. ファイルを開き、メタ情報を書き込み
  2. PR をページングで取得しながら、スレッド・コメントを取得
  3. 取得したコメントを即座に JSON レコードとして書き込み
  4. すべての PR 処理が完了したらファイルを閉じる

---

## ユーザーインターフェース

### 1. GUI モード（WinUI 3）

#### 1.1. メインウィンドウ

**画面構成**:
```
┌─────────────────────────────────────────────────────┐
│  AdoReviewExport - Azure DevOps Review Exporter     │
├─────────────────────────────────────────────────────┤
│                                                       │
│  [Organization] [textbox: contoso               ]   │
│  [Project]      [textbox: MyProject             ]   │
│  [Repository]   [textbox: my-repo               ]   │
│  [PAT]          [passwordbox: ******************]   │
│  [Output Path]  [textbox: C:\Users\...\export.json] [📁] │
│                                                       │
│  [Authors (Optional)] [textbox: alice,bob       ]   │
│                                                       │
│  [Start Export]  [Cancel]                           │
│                                                       │
│  Progress: ████████████░░░░░░░░ 60%                │
│  Status: Processing PR 120 / 200                    │
│                                                       │
│  ┌─────────────────────────────────────────────┐   │
│  │ Log:                                         │   │
│  │ [10:30:01] Connecting to Azure DevOps...    │   │
│  │ [10:30:02] Connection successful             │   │
│  │ [10:30:03] Fetching pull requests...        │   │
│  │ [10:30:05] Processing PR 1234                │   │
│  │ ...                                          │   │
│  └─────────────────────────────────────────────┘   │
│                                                       │
└─────────────────────────────────────────────────────┘
```

**入力フィールド**:
- **Organization**: Azure DevOps の Organization 名（必須）
- **Project**: Project 名（必須）
- **Repository**: Repository 名または Repository ID（必須）
- **PAT**: Personal Access Token（必須、マスク表示）
- **Output Path**: 出力ファイルパス（必須、ファイル選択ボタンでブラウズ可能）
- **Authors (Optional)**: 投稿者フィルタ（カンマ区切り、オプション）

**ボタン**:
- **Start Export**: エクスポート開始（すべての必須フィールドが入力されている場合のみ有効）
- **Cancel**: エクスポート中断（実行中のみ有効）
- **📁（ファイル選択）**: ファイル保存ダイアログを表示

**進捗表示**:
- **Progress Bar**: 全体の進捗率（0〜100%）
- **Status Text**: 現在の処理状況（例: `Processing PR 120 / 200`）
- **Log View**: 処理ログ（スクロール可能、最大1000行まで保持）

#### 1.2. 完了ダイアログ

エクスポート完了時に表示されるダイアログ:
```
┌─────────────────────────────────────────────┐
│  Export Completed Successfully               │
├─────────────────────────────────────────────┤
│                                               │
│  ✓ Export finished successfully!            │
│                                               │
│  Summary:                                    │
│  - Total Pull Requests: 200                 │
│  - Total Comments: 1,250                    │
│  - Output File: C:\Users\...\export.json    │
│                                               │
│  [Open File]  [Open Folder]  [Close]        │
│                                               │
└─────────────────────────────────────────────┘
```

**ボタン**:
- **Open File**: 出力ファイルをデフォルトの JSON ビューア（またはテキストエディタ）で開く
- **Open Folder**: 出力ファイルが保存されているフォルダをエクスプローラーで開く
- **Close**: ダイアログを閉じる

#### 1.3. エラーダイアログ

エラー発生時に表示されるダイアログの例:

**認証エラー**:
```
┌─────────────────────────────────────────────┐
│  Authentication Error                        │
├─────────────────────────────────────────────┤
│                                               │
│  ✗ Failed to authenticate with Azure DevOps │
│                                               │
│  Error: The provided PAT is invalid or does │
│  not have the required permissions.         │
│                                               │
│  Required permissions:                       │
│  - Code (Read)                              │
│                                               │
│  Please check your PAT and try again.       │
│                                               │
│  [OK]                                        │
│                                               │
└─────────────────────────────────────────────┘
```

**ネットワークエラー（リトライ失敗）**:
```
┌─────────────────────────────────────────────┐
│  Network Error                               │
├─────────────────────────────────────────────┤
│                                               │
│  ✗ Failed to connect to Azure DevOps        │
│                                               │
│  Error: The request timed out after 3       │
│  retry attempts.                            │
│                                               │
│  Possible causes:                            │
│  - Network connectivity issues              │
│  - Azure DevOps service is temporarily down │
│                                               │
│  Please check your connection and try again.│
│                                               │
│  [OK]                                        │
│                                               │
└─────────────────────────────────────────────┘
```

#### 1.4. メッセージ一覧

| メッセージ ID | 表示条件 | メッセージ内容（日本語） |
|--------------|---------|------------------------|
| MSG-001 | Organization が未入力 | Organization を入力してください。 |
| MSG-002 | Project が未入力 | Project を入力してください。 |
| MSG-003 | Repository が未入力 | Repository を入力してください。 |
| MSG-004 | PAT が未入力 | Personal Access Token を入力してください。 |
| MSG-005 | Output Path が未入力 | 出力ファイルパスを指定してください。 |
| MSG-006 | エクスポート開始 | エクスポートを開始します... |
| MSG-007 | Azure DevOps 接続中 | Azure DevOps に接続しています... |
| MSG-008 | 接続成功 | 接続が成功しました。 |
| MSG-009 | PR 取得中 | Pull Request を取得しています... |
| MSG-010 | PR 処理中 | Processing PR {number} / {total} |
| MSG-011 | スレッド取得中 | Fetching threads for PR {number}... |
| MSG-012 | コメント取得中 | Fetching comments for thread {threadId}... |
| MSG-013 | JSON 書き込み中 | Writing to JSON file... |
| MSG-014 | エクスポート完了 | エクスポートが完了しました！ |
| MSG-015 | エクスポート中断 | エクスポートがキャンセルされました。 |
| ERR-001 | PAT が無効 | The provided PAT is invalid or does not have the required permissions. Required permissions: Code (Read) |
| ERR-002 | ネットワークエラー | Failed to connect to Azure DevOps. The request timed out after {retryCount} retry attempts. |
| ERR-003 | API エラー | Azure DevOps API returned an error: {statusCode} - {message} |
| ERR-004 | ファイル書き込みエラー | Failed to write to the output file: {filePath}. Error: {errorMessage} |
| ERR-005 | 不明なエラー | An unexpected error occurred: {errorMessage} |

---

### 2. CLI モード

#### 2.1. コマンドライン構文

```powershell
AdoReviewExport.exe export --org <organization> --project <project> --repo <repository> --pat <token> --output <filepath> [--authors <author1,author2>]
```

**引数**:
- `--org <organization>`: Azure DevOps Organization 名（必須）
- `--project <project>`: Project 名（必須）
- `--repo <repository>`: Repository 名または Repository ID（必須）
- `--pat <token>`: Personal Access Token（必須、または環境変数 `AZDO_PAT` から読み取り）
- `--output <filepath>`: 出力ファイルパス（必須）
- `--authors <author1,author2>`: 投稿者フィルタ（カンマ区切り、オプション）

**環境変数**:
- `AZDO_PAT`: PAT を環境変数で指定可能（コマンドライン引数より優先度が低い）

#### 2.2. 実行例

**基本的な実行**:
```powershell
AdoReviewExport.exe export --org contoso --project MyProject --repo my-repo --pat abc123def456 --output C:\exports\review.json
```

**環境変数で PAT を指定**:
```powershell
$env:AZDO_PAT = "abc123def456"
AdoReviewExport.exe export --org contoso --project MyProject --repo my-repo --output C:\exports\review.json
```

**投稿者フィルタを使用**:
```powershell
AdoReviewExport.exe export --org contoso --project MyProject --repo my-repo --pat abc123def456 --output C:\exports\review.json --authors "alice,bob"
```

#### 2.3. 標準出力

CLI モードでは、進捗ログを標準出力に出力する:

```
[2026-01-07 10:30:00] INFO: Starting export...
[2026-01-07 10:30:00] INFO: Connecting to Azure DevOps (org: contoso, project: MyProject, repo: my-repo)...
[2026-01-07 10:30:01] INFO: Connection successful
[2026-01-07 10:30:01] INFO: Fetching pull requests...
[2026-01-07 10:30:02] INFO: Found 200 pull requests
[2026-01-07 10:30:02] INFO: Processing PR 1 / 200
[2026-01-07 10:30:03] INFO: Processing PR 2 / 200
...
[2026-01-07 10:35:00] INFO: Processing PR 200 / 200
[2026-01-07 10:35:01] INFO: Writing JSON file: C:\exports\review.json
[2026-01-07 10:35:02] INFO: Export completed successfully
[2026-01-07 10:35:02] INFO: Summary: 200 PRs, 320 threads, 1,250 comments
```

#### 2.4. 終了コード

| 終了コード | 説明 |
|-----------|------|
| 0 | 成功 |
| 1 | 入力エラー（引数不足、形式不正） |
| 2 | 認証エラー（PAT が無効、権限不足） |
| 3 | API エラー（Azure DevOps への接続失敗、リトライ失敗） |
| 4 | 出力エラー（ファイル書き込み失敗） |
| 130 | ユーザーによる中断（Ctrl+C） |

#### 2.5. ヘルプメッセージ

```powershell
AdoReviewExport.exe --help
```

**出力**:
```
AdoReviewExport v1.0.0
Azure DevOps Pull Request Review Comment Exporter

USAGE:
  AdoReviewExport.exe export --org <organization> --project <project> --repo <repository> --pat <token> --output <filepath> [OPTIONS]

ARGUMENTS:
  --org <organization>      Azure DevOps Organization name (required)
  --project <project>       Project name (required)
  --repo <repository>       Repository name or ID (required)
  --pat <token>             Personal Access Token (required, or set AZDO_PAT environment variable)
  --output <filepath>       Output JSON file path (required)

OPTIONS:
  --authors <author1,author2>   Filter comments by author display names (comma-separated, optional)
  --help                        Show this help message

EXAMPLES:
  AdoReviewExport.exe export --org contoso --project MyProject --repo my-repo --pat abc123 --output review.json
  AdoReviewExport.exe export --org contoso --project MyProject --repo my-repo --output review.json --authors "alice,bob"

ENVIRONMENT VARIABLES:
  AZDO_PAT    Personal Access Token (alternative to --pat argument)

EXIT CODES:
  0    Success
  1    Input error (missing or invalid arguments)
  2    Authentication error (invalid PAT or insufficient permissions)
  3    API error (connection failure, retry exhausted)
  4    Output error (file write failure)
  130  User interruption (Ctrl+C)

For more information, visit: https://github.com/yourorg/ado-review-export
```

---

## ソフトウェアインターフェース

### 1. Azure DevOps REST API

本アプリケーションは Azure DevOps REST API v7.2 を使用する。

#### 1.1. 認証

- **方式**: Basic 認証（PAT を Base64 エンコード）
- **ヘッダー**: `Authorization: Basic {base64(":" + pat)}`
- **必要なスコープ**: `vso.code` (Read)

#### 1.2. エンドポイント

**Pull Request 一覧取得**:
```http
GET https://dev.azure.com/{organization}/{project}/_apis/git/repositories/{repositoryId}/pullrequests?api-version=7.2&$top={pageSize}&$skip={offset}
```

**レスポンス例**:
```json
{
  "value": [
    {
      "pullRequestId": 1234,
      "title": "Feature: Add new API endpoint",
      "description": "This PR adds a new API endpoint for user management.",
      "status": "completed",
      "createdBy": {
        "displayName": "Alice Smith",
        "uniqueName": "alice@contoso.com",
        "id": "user-id-123"
      },
      "creationDate": "2025-12-01T10:00:00Z",
      "closedDate": "2025-12-05T15:30:00Z",
      "sourceRefName": "refs/heads/feature/new-api",
      "targetRefName": "refs/heads/main"
    }
  ],
  "count": 1
}
```

**スレッド一覧取得**:
```http
GET https://dev.azure.com/{organization}/{project}/_apis/git/repositories/{repositoryId}/pullrequests/{pullRequestId}/threads?api-version=7.2
```

**レスポンス例**:
```json
{
  "value": [
    {
      "id": 5678,
      "status": "fixed",
      "publishedDate": "2025-12-02T14:20:00Z",
      "threadContext": {
        "filePath": "/src/Controllers/UserController.cs",
        "rightFileStart": {"line": 42, "offset": 0},
        "rightFileEnd": {"line": 45, "offset": 0}
      },
      "comments": [
        {
          "id": 91011,
          "parentCommentId": 0,
          "author": {
            "displayName": "Bob Johnson",
            "uniqueName": "bob@contoso.com",
            "id": "user-id-456"
          },
          "publishedDate": "2025-12-02T14:20:00Z",
          "lastUpdatedDate": "2025-12-02T14:20:00Z",
          "commentType": "text",
          "content": "このメソッドは null チェックが必要です。",
          "isDeleted": false
        }
      ]
    }
  ],
  "count": 1
}
```

#### 1.3. ページング

- **ページサイズ**: `$top=50`（1回のリクエストで最大50件）
- **オフセット**: `$skip={offset}`（次のページは `$skip=50`, `$skip=100` のように増やす）
- **総件数**: レスポンスの `count` フィールドで確認

#### 1.4. レート制限

- **HTTP 429 (Too Many Requests)**: レート制限に達した場合、`Retry-After` ヘッダー（秒）を尊重してリトライ
- **デフォルトのリトライ間隔**: 指数バックオフ（1秒 → 2秒 → 4秒）

---

### 2. 内部クラスライブラリ API（Application Layer）

#### 2.1. IExportService インターフェース

エクスポート処理の中核を担うサービスインターフェース。

```csharp
namespace AdoReviewExport.Application.Services
{
    /// <summary>
    /// Azure DevOps のレビューコメントをエクスポートするサービスのインターフェース
    /// </summary>
    public interface IExportService
    {
        /// <summary>
        /// エクスポート処理を実行する
        /// </summary>
        /// <param name="request">エクスポート要求パラメータ</param>
        /// <param name="progress">進捗通知用のコールバック（オプション）</param>
        /// <param name="cancellationToken">キャンセルトークン</param>
        /// <returns>エクスポート結果</returns>
        /// <exception cref="InputValidationException">入力パラメータが不正な場合</exception>
        /// <exception cref="AuthenticationException">認証に失敗した場合</exception>
        /// <exception cref="ApiException">Azure DevOps API でエラーが発生した場合</exception>
        /// <exception cref="OutputException">ファイル書き込みに失敗した場合</exception>
        Task<ExportResult> ExportAsync(
            ExportRequest request,
            IProgress<ExportProgress>? progress = null,
            CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// エクスポート要求パラメータ
    /// </summary>
    public record ExportRequest(
        string Organization,
        string Project,
        string Repository,
        string PersonalAccessToken,
        string OutputFilePath,
        IReadOnlyList<string>? AuthorFilters = null);

    /// <summary>
    /// エクスポート進捗情報
    /// </summary>
    public record ExportProgress(
        int TotalPullRequests,
        int ProcessedPullRequests,
        int TotalComments,
        string CurrentStatus);

    /// <summary>
    /// エクスポート結果
    /// </summary>
    public record ExportResult(
        bool Success,
        string OutputFilePath,
        int TotalPullRequests,
        int TotalThreads,
        int TotalComments,
        TimeSpan ElapsedTime);
}
```

#### 2.2. IAdoApiClient インターフェース

Azure DevOps REST API クライアントのインターフェース。

```csharp
namespace AdoReviewExport.Infrastructure.AzureDevOps
{
    /// <summary>
    /// Azure DevOps REST API クライアントのインターフェース
    /// </summary>
    public interface IAdoApiClient
    {
        /// <summary>
        /// 指定リポジトリの Pull Request 一覧を取得する
        /// </summary>
        /// <param name="organization">Organization 名</param>
        /// <param name="project">Project 名</param>
        /// <param name="repositoryId">Repository ID</param>
        /// <param name="pageSize">1ページあたりの取得件数</param>
        /// <param name="skip">スキップする件数</param>
        /// <param name="cancellationToken">キャンセルトークン</param>
        /// <returns>Pull Request のリスト</returns>
        Task<IReadOnlyList<PullRequestDto>> GetPullRequestsAsync(
            string organization,
            string project,
            string repositoryId,
            int pageSize = 50,
            int skip = 0,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// 指定 Pull Request のスレッド一覧を取得する
        /// </summary>
        /// <param name="organization">Organization 名</param>
        /// <param name="project">Project 名</param>
        /// <param name="repositoryId">Repository ID</param>
        /// <param name="pullRequestId">Pull Request ID</param>
        /// <param name="cancellationToken">キャンセルトークン</param>
        /// <returns>スレッドのリスト</returns>
        Task<IReadOnlyList<ThreadDto>> GetThreadsAsync(
            string organization,
            string project,
            string repositoryId,
            int pullRequestId,
            CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// Pull Request の DTO
    /// </summary>
    public record PullRequestDto(
        int Id,
        string Title,
        string? Description,
        string Url,
        string Status,
        IdentityDto CreatedBy,
        DateTime CreationDate,
        DateTime? ClosedDate,
        string SourceRefName,
        string TargetRefName);

    /// <summary>
    /// スレッドの DTO
    /// </summary>
    public record ThreadDto(
        int Id,
        string Status,
        DateTime PublishedDate,
        ThreadContextDto? ThreadContext,
        IReadOnlyList<CommentDto> Comments);

    /// <summary>
    /// コメントの DTO
    /// </summary>
    public record CommentDto(
        int Id,
        int ParentCommentId,
        IdentityDto Author,
        DateTime PublishedDate,
        DateTime LastUpdatedDate,
        string CommentType,
        string Content,
        bool IsDeleted);

    /// <summary>
    /// ユーザー情報の DTO
    /// </summary>
    public record IdentityDto(
        string DisplayName,
        string UniqueName,
        string Id);

    /// <summary>
    /// スレッドコンテキスト（ファイルパス・行番号）の DTO
    /// </summary>
    public record ThreadContextDto(
        string? FilePath,
        FilePositionDto? RightFileStart,
        FilePositionDto? RightFileEnd);

    /// <summary>
    /// ファイル位置の DTO
    /// </summary>
    public record FilePositionDto(
        int Line,
        int Offset);
}
```

---

## 実現方式

### 1. アーキテクチャ

#### 1.1. レイヤー構成

```
┌────────────────────────────────────────┐
│  Presentation Layer (UI / CLI)         │  ← WinUI 3 GUI / CLI エントリポイント
├────────────────────────────────────────┤
│  Application Layer (Services)          │  ← ExportService（ビジネスロジック）
├────────────────────────────────────────┤
│  Infrastructure Layer (API Client)     │  ← AdoApiClient, JsonExporter
└────────────────────────────────────────┘
```

- **Presentation Layer**: WinUI 3 による GUI、または CLI のエントリポイント
- **Application Layer**: エクスポート処理のオーケストレーション（PR → Thread → Comment の取得フロー）
- **Infrastructure Layer**: Azure DevOps REST API クライアント、JSON 出力処理

#### 1.2. 依存関係

- `Presentation` → `Application` → `Infrastructure`
- `Application` は `Infrastructure` のインターフェース（`IAdoApiClient`）に依存
- テスト時は `Infrastructure` をモックに差し替え可能（Dependency Injection）

#### 1.3. プロジェクト構成

```
AdoReviewExport.sln
├── AdoReviewExport.UI (WinUI 3 - OutputType=WinExe)
│   ├── App.xaml.cs          // アプリケーションエントリポイント
│   ├── Program.cs           // Main メソッド（GUI/CLI 分岐）
│   ├── Views/
│   │   └── MainWindow.xaml
│   ├── ViewModels/
│   │   └── MainViewModel.cs
│   └── Helpers/
│       └── ConsoleHelper.cs // CLI モードでのコンソール出力
├── AdoReviewExport.Application (Class Library)
│   ├── Services/
│   │   ├── IExportService.cs
│   │   └── ExportService.cs
│   ├── Models/
│   │   ├── ExportRequest.cs
│   │   ├── ExportResult.cs
│   │   └── ExportProgress.cs
│   └── Exceptions/
│       ├── AdoExportException.cs
│       ├── InputValidationException.cs
│       ├── AuthenticationException.cs
│       ├── ApiException.cs
│       └── OutputException.cs
├── AdoReviewExport.Infrastructure (Class Library)
│   ├── AzureDevOps/
│   │   ├── IAdoApiClient.cs
│   │   ├── AdoApiClient.cs
│   │   └── Dtos/ (PullRequestDto, ThreadDto, CommentDto, etc.)
│   ├── Json/
│   │   ├── IJsonExporter.cs
│   │   └── JsonExporter.cs
│   └── Http/
│       └── RetryPolicyFactory.cs // Polly によるリトライポリシー
└── AdoReviewExport.Tests (xUnit)
    ├── Unit/
    │   ├── ExportServiceTests.cs
    │   └── AdoApiClientTests.cs
    └── Integration/
        └── ExportIntegrationTests.cs
```

### 2. 処理フロー

#### 2.1. GUI モードの処理フロー

```
[ユーザー] → [MainWindow] → [MainViewModel]
                                  ↓
                          [ExportService.ExportAsync]
                                  ↓
                   ┌──────────────┴──────────────┐
                   ↓                             ↓
            [AdoApiClient]               [JsonExporter]
                   ↓                             ↓
         Azure DevOps REST API          ファイル書き込み
```

**詳細**:
1. ユーザーが入力フィールドに情報を入力し、「Start Export」ボタンをクリック
2. `MainViewModel` が `ExportService.ExportAsync` を呼び出し
3. `ExportService` は `AdoApiClient` を使って Azure DevOps API から PR / Thread / Comment を取得
4. 取得したデータを `JsonExporter` でファイルに書き込み
5. 進捗情報を `IProgress<ExportProgress>` で `MainViewModel` に通知
6. `MainViewModel` が進捗バー・ログビューを更新
7. 完了時に完了ダイアログを表示

#### 2.2. CLI モードの処理フロー

```
[コマンドライン] → [Program.Main]
                          ↓
                   [引数解析・検証]
                          ↓
              [ExportService.ExportAsync]
                          ↓
       （GUI モードと同じ処理フロー）
                          ↓
              [標準出力に進捗ログ]
                          ↓
                   [終了コード返却]
```

**詳細**:
1. `Main` メソッドでコマンドライン引数を解析
2. 必須引数が不足している場合、ヘルプメッセージを表示して終了コード 1 で終了
3. `ExportService.ExportAsync` を呼び出し
4. 進捗情報を標準出力に出力
5. 成功時は終了コード 0、エラー時は該当する終了コードで終了

#### 2.3. データ取得・書き込みフロー

```
1. [ExportService] PR 一覧を取得（ページング）
       ↓
2. 各 PR について:
   a. [AdoApiClient] スレッド一覧を取得
   b. 各スレッドについて、コメント一覧を取得
   c. [投稿者フィルタ] 適用（指定されている場合）
   d. [JsonExporter] JSON レコードとして書き込み
       ↓
3. すべての PR 処理完了後、ファイルを閉じる
```

**メモリ効率化**:
- 全データをメモリに保持せず、取得したデータを即座にファイルに書き込む
- `Utf8JsonWriter` を使用してストリーミング書き込み

**並列処理**:
- PR のスレッド取得を並列化（最大5並列）して処理時間を短縮
- ただし、JSON 書き込みは順次実行（ファイルへの同時書き込みを避ける）

### 3. 使用技術・ライブラリ

| カテゴリ | 技術/ライブラリ | バージョン | 用途 |
|---------|---------------|-----------|------|
| フレームワーク | .NET | 10 | アプリケーションランタイム |
| UI | WinUI 3 (Windows App SDK) | 1.7+ | GUI 実装 |
| HTTP | System.Net.Http | .NET 10 組み込み | Azure DevOps API 通信 |
| JSON | System.Text.Json | .NET 10 組み込み | シリアライズ・デシリアライズ |
| リトライ | Polly | 8.x | API リトライポリシー |
| DI | Microsoft.Extensions.DependencyInjection | .NET 10 組み込み | 依存性注入 |
| ログ | Microsoft.Extensions.Logging | .NET 10 組み込み | 構造化ログ |
| テスト | xUnit + Moq | 最新 | 単体テスト・モック |

### 4. Unpackaged WinUI 3 の設定

**プロジェクトファイル (.csproj)**:
```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>WinExe</OutputType>
    <TargetFramework>net10.0-windows10.0.19041.0</TargetFramework>
    <TargetPlatformMinVersion>10.0.17763.0</TargetPlatformMinVersion>
    <RootNamespace>AdoReviewExport.UI</RootNamespace>
    <UseWinUI>true</UseWinUI>
    <EnableMsixTooling>false</EnableMsixTooling> <!-- Unpackaged -->
    <WindowsAppSDKSelfContained>true</WindowsAppSDKSelfContained> <!-- 自己完結型 -->
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.WindowsAppSDK" Version="1.7.0" />
    <PackageReference Include="Microsoft.Extensions.DependencyInjection" Version="9.0.0" />
    <PackageReference Include="Microsoft.Extensions.Logging" Version="9.0.0" />
    <PackageReference Include="Polly" Version="8.0.0" />
  </ItemGroup>
</Project>
```

**Main メソッド（Program.cs）**:
```csharp
using Microsoft.UI.Xaml;
using System;
using System.Linq;

namespace AdoReviewExport.UI
{
    public class Program
    {
        [STAThread]
        static int Main(string[] args)
        {
            // CLI モード判定（引数が存在し、"--" で始まる場合）
            if (args.Length > 0 && args[0].StartsWith("--"))
            {
                // CLI モードで実行
                return RunCliMode(args);
            }
            else
            {
                // GUI モードで実行
                Application.Start((p) =>
                {
                    var context = new Microsoft.UI.Dispatching.DispatcherQueueSynchronizationContext(
                        Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread());
                    System.Threading.SynchronizationContext.SetSynchronizationContext(context);
                    new App();
                });
                return 0;
            }
        }

        static int RunCliMode(string[] args)
        {
            // CLI モードの処理（別のメソッドで実装）
            // コンソール出力、ExportService 呼び出し、終了コード返却
            // ...
            return 0;
        }
    }
}
```

---

## 保守機能

### 1. ログ機能

#### 1.1. ログレベル

| レベル | 用途 | 出力先 |
|-------|------|-------|
| Trace | API リクエスト/レスポンスの詳細（開発時のみ） | ファイルのみ |
| Debug | 処理フロー（PR 取得開始、完了など） | ファイルのみ |
| Information | ユーザーに見せる進捗（「Processing PR 10/200」） | GUI: ログビュー, CLI: 標準出力, ファイル |
| Warning | リトライ発生、一部データ欠落 | すべて |
| Error | 例外発生（スタックトレース含む） | すべて |
| Critical | アプリケーションクラッシュ | すべて |

#### 1.2. ログ出力先

- **GUI モード**:
  - メモリ内バッファ（最大1000行） → UI のログビューに表示
  - ファイル: `%TEMP%\AdoReviewExport\logs\app-{date}.log`
  
- **CLI モード**:
  - 標準出力: `Information` 以上のログ
  - ファイル: `Debug` 以上のログ（GUI と同じパス）

#### 1.3. ログファイルのローテーション

- **ローテーション条件**: 日次（1日1ファイル）
- **保持期間**: 直近7日分を保持、それ以前は自動削除
- **ファイル名形式**: `app-20260107.log`（年月日）

#### 1.4. PII/Secret のマスキング

- **PAT のマスキング**: ログ出力前に PAT を検出し、`***REDACTED***` に置換
- **正規表現**: PAT のパターン（長さ52文字の英数字）を検出
- **例外メッセージ**: `HttpClient` のログフィルタを設定し、Authorization ヘッダーを自動マスク

### 2. エラーログ

エラー発生時には、以下の情報をログファイルに記録する:

```
[2026-01-07 10:30:05] ERROR: Failed to fetch pull requests
Exception: AdoReviewExport.Application.Exceptions.ApiException: Azure DevOps API returned an error: 500 - Internal Server Error
   at AdoReviewExport.Infrastructure.AzureDevOps.AdoApiClient.GetPullRequestsAsync(...)
   at AdoReviewExport.Application.Services.ExportService.ExportAsync(...)
   at AdoReviewExport.UI.ViewModels.MainViewModel.StartExportAsync()
Context:
  Organization: contoso
  Project: MyProject
  Repository: my-repo
  Pull Request Count: 0 (fetch failed)
```

### 3. 設定ファイル（将来の拡張）

初期リリースでは設定ファイルは使用しないが、将来的な拡張として以下を検討:

- **設定ファイルパス**: `%APPDATA%\AdoReviewExport\config.json`
- **設定項目**:
  - デフォルトの Organization / Project（次回起動時に自動入力）
  - ログレベル（Debug, Information など）
  - API リトライ回数・間隔
  - 並列処理の最大並列度

**注意**: PAT は設定ファイルに保存しない（セキュリティリスク）

---

## 開発環境

### 1. 必要な環境

| 項目 | 要件 |
|------|------|
| OS | Windows 10 (21H2) 以降 または Windows 11 |
| .NET SDK | .NET 10 SDK |
| IDE | Visual Studio 2025 (Preview) または Visual Studio Code |
| Windows App SDK | 1.7+ (NuGet パッケージで自動取得) |
| Git | 2.x 以降 |

### 2. ビルド手順

**Visual Studio の場合**:
1. ソリューションファイル（`AdoReviewExport.sln`）を開く
2. ビルド構成を「Release」に設定
3. ビルド → ソリューションのビルド

**コマンドラインの場合**:
```powershell
cd AdoReviewExport
dotnet build -c Release
```

### 3. 実行手順

**GUI モード**:
```powershell
cd AdoReviewExport.UI\bin\Release\net10.0-windows10.0.19041.0\win-x64
.\AdoReviewExport.exe
```

**CLI モード**:
```powershell
.\AdoReviewExport.exe export --org contoso --project MyProject --repo my-repo --pat abc123 --output review.json
```

### 4. デプロイ

**配布方法**:
- **Unpackaged**: `AdoReviewExport.exe` および依存 DLL を zip で圧縮して配布
- **自己完結型**: `WindowsAppSDKSelfContained=true` により、.NET ランタイムを含む単一フォルダとして配布可能

**配布パッケージの内容**:
```
AdoReviewExport-v1.0.0/
├── AdoReviewExport.exe
├── AdoReviewExport.Application.dll
├── AdoReviewExport.Infrastructure.dll
├── Microsoft.WindowsAppSDK.dll
├── Polly.dll
├── System.Text.Json.dll
└── (その他の依存 DLL)
```

**インストール不要**:
- ユーザーは zip を展開して `AdoReviewExport.exe` を実行するだけ
- レジストリ変更・管理者権限不要

---

## セキュリティ考慮事項

### 1. PAT の保護

- **メモリ上でのみ保持**: ファイルや設定に保存しない
- **ログマスキング**: ログファイル・例外メッセージに PAT を出力しない
- **GUI**: PasswordBox でマスク表示
- **CLI**: 環境変数 `AZDO_PAT` で渡すことでコマンド履歴に残さない

### 2. 最小権限の原則

- **必要なスコープ**: `vso.code` (Read) のみ
- **書き込み権限不要**: Azure DevOps に対して読み取り専用のアクセス

### 3. HTTPS 通信

- Azure DevOps API は HTTPS で通信（TLS 1.2 以上）
- 中間者攻撃のリスクを軽減

### 4. ローカルファイルの保護

- **出力 JSON ファイル**: ユーザーが指定した場所に保存（権限はユーザー依存）
- **ログファイル**: `%TEMP%` 配下に保存（他のユーザーからアクセス可能な場合あり）
  - 将来の改善: ユーザープロファイル配下（`%APPDATA%`）に保存

---

## 既知の制限事項

1. **Windows 専用**: Linux / macOS では動作しない（WinUI 3 の制約）
2. **単一リポジトリのみ**: 複数リポジトリの一括エクスポートは不可
3. **大量データのパフォーマンス**: PR 数が 10,000 件を超える場合、処理に数時間かかる可能性あり
4. **JSON ファイルサイズ**: コメント数が 10 万件を超えると、出力ファイルが数百 MB になる可能性あり（LLM のコンテキストウィンドウに収まらない場合、ユーザーが手動で分割する必要がある）
5. **オフライン動作不可**: Azure DevOps への接続が必須

---

## 今後の拡張候補

- **期間フィルタ**: 特定の日付範囲の PR のみをエクスポート
- **PR ステータスフィルタ**: Completed / Active などで絞り込み
- **複数リポジトリ対応**: 組織内の全リポジトリを一括エクスポート
- **差分エクスポート**: 前回エクスポート以降の更新分のみを取得
- **CSV / Markdown 出力**: JSON 以外の形式でエクスポート
- **GUI での履歴管理**: 過去のエクスポート結果を保存・再実行
- **Packaged 版の提供**: Microsoft Store での配布

---

**文書バージョン**: 1.0.0  
**最終更新日**: 2026-01-07
