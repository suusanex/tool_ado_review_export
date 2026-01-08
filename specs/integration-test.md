# 統合テスト計画: Azure DevOps PR Review Comment Exporter

**Feature Branch**: `001-ado-review-export`  
**Date**: January 7, 2026  
**Status**: Test Plan Complete

このドキュメントは、実OS環境で実行される統合テストの観点を定義する。単体テストレベルの観点は含まず、実際の動作環境における検証に焦点を当てる。

---

## 統合テストの原則

1. **実OS環境を変更しない**: レジストリ、サービス、ドライバ、デバイスなど実環境を変更する処理は行わない
2. **スタブ/モックの使用**: 外部システム（Azure DevOps API）はスタブサーバーで代替
3. **CI実行可能**: すべてのテストは管理者権限不要で、CIパイプラインで自動実行可能
4. **再現性**: テストは何度実行しても同じ結果を返す（外部状態に依存しない）

---

## 統合テストの観点

### 1. GUI モードの統合テスト

#### 1.1. アプリケーション起動・初期化

- **観点**: WinUI 3 アプリケーションが正常に起動し、メインウィンドウが表示される
- **検証内容**:
  - `AdoReviewExport.exe` を引数なしで起動
  - メインウィンドウが表示される
  - すべての入力フィールドが空の状態で表示される
  - 「Start Export」ボタンが無効状態（入力が不完全）
- **スタブ不要**: アプリケーション起動のみ

#### 1.2. 入力検証（バリデーション）

- **観点**: 必須フィールドが未入力の場合、エクスポートが開始されない
- **検証内容**:
  - Organization / Project / Repository のいずれかが空の場合、エラーメッセージが表示される
  - PAT が空の場合、エラーメッセージが表示される
  - Output Path が空の場合、エラーメッセージが表示される
  - すべての必須フィールドが入力されると「Start Export」ボタンが有効になる
- **スタブ不要**: UI バリデーションロジックのみ

#### 1.3. エクスポート処理の実行（スタブ API）

- **観点**: スタブ Azure DevOps API に対してエクスポート処理が正常に完了する
- **検証内容**:
  - スタブ API サーバーを起動（疑似 PR / Thread / Comment データを返す）
  - GUI で Organization / Project / Repository / PAT / Output Path を入力
  - 「Start Export」ボタンをクリック
  - 進捗バーが表示され、進捗率が更新される
  - ログビューに処理ログが表示される
  - エクスポート完了後、完了ダイアログが表示される
  - 指定した Output Path に JSON ファイルが生成される
- **スタブ**: ASP.NET Core Minimal API で疑似 Azure DevOps API を実装

#### 1.4. エラーハンドリング（認証エラー）

- **観点**: スタブ API が HTTP 401 を返した場合、エラーダイアログが表示される
- **検証内容**:
  - スタブ API が常に HTTP 401 を返すように設定
  - GUI でエクスポートを開始
  - エラーダイアログが表示され、「PAT が無効」というメッセージが表示される
  - エクスポートが中断される
- **スタブ**: HTTP 401 を返すスタブ API

#### 1.5. エラーハンドリング（ネットワークエラー・リトライ）

- **観点**: スタブ API が一時的にタイムアウトを返し、リトライで成功する
- **検証内容**:
  - スタブ API が最初の2回はタイムアウト、3回目で成功するように設定
  - GUI でエクスポートを開始
  - ログビューに「Retrying...」というログが表示される
  - 最終的にエクスポートが成功する
- **スタブ**: 回数カウント付きスタブ API（1〜2回目はタイムアウト、3回目で成功）

#### 1.6. キャンセル機能

- **観点**: エクスポート実行中にキャンセルボタンを押すと、処理が中断される
- **検証内容**:
  - スタブ API が意図的に遅延する（各リクエストに1秒かかる）
  - GUI でエクスポートを開始
  - 進捗が表示されている間に「Cancel」ボタンをクリック
  - エクスポートが中断される
  - 不完全な JSON ファイルが残らない（ファイルが削除される、または作成されない）
- **スタブ**: レスポンスが遅いスタブ API

#### 1.7. ファイル選択ダイアログ

- **観点**: ファイル選択ボタンをクリックすると、ファイル保存ダイアログが表示される
- **検証内容**:
  - 「📁」ボタンをクリック
  - Windows のファイル保存ダイアログが表示される
  - ファイルパスを選択すると、Output Path フィールドに反映される
- **スタブ不要**: OS標準のファイルダイアログ

---

### 2. CLI モードの統合テスト

#### 2.1. ヘルプメッセージの表示

- **観点**: `--help` オプションでヘルプメッセージが表示される
- **検証内容**:
  - `AdoReviewExport.exe --help` を実行
  - 標準出力にヘルプメッセージが表示される
  - 終了コード 0 で終了
- **スタブ不要**: ヘルプ表示のみ

#### 2.2. 引数不足エラー

- **観点**: 必須引数が不足している場合、エラーメッセージが表示される
- **検証内容**:
  - `AdoReviewExport.exe export --org contoso` （他の引数が不足）
  - 標準エラー出力にエラーメッセージが表示される
  - ヘルプメッセージが表示される
  - 終了コード 1 で終了
- **スタブ不要**: 引数解析のみ

#### 2.3. エクスポート処理の実行（スタブ API）

- **観点**: スタブ Azure DevOps API に対してエクスポート処理が正常に完了する
- **検証内容**:
  - スタブ API サーバーを起動
  - `AdoReviewExport.exe export --org contoso --project MyProject --repo my-repo --pat abc123 --output review.json` を実行
  - 標準出力に進捗ログが表示される（「Processing PR 1 / 10」など）
  - エクスポート完了後、「Export completed successfully」というメッセージが表示される
  - 終了コード 0 で終了
  - `review.json` ファイルが生成される
- **スタブ**: ASP.NET Core Minimal API で疑似 Azure DevOps API を実装

#### 2.4. 環境変数での PAT 指定

- **観点**: 環境変数 `AZDO_PAT` で PAT を指定できる
- **検証内容**:
  - 環境変数 `AZDO_PAT` に PAT を設定
  - `AdoReviewExport.exe export --org contoso --project MyProject --repo my-repo --output review.json` （`--pat` を省略）
  - エクスポートが正常に完了する
  - 終了コード 0 で終了
- **スタブ**: スタブ API

#### 2.5. 投稿者フィルタの適用

- **観点**: `--authors` オプションで投稿者フィルタが適用される
- **検証内容**:
  - スタブ API が複数の投稿者（alice, bob, charlie）のコメントを返す
  - `AdoReviewExport.exe export --org contoso --project MyProject --repo my-repo --pat abc123 --output review.json --authors "alice,bob"` を実行
  - 出力 JSON には alice と bob のコメントのみが含まれる（charlie のコメントは除外）
- **スタブ**: 複数の投稿者データを返すスタブ API

#### 2.6. エラー時の終了コード

- **観点**: エラー発生時に適切な終了コードが返される
- **検証内容**:
  - **認証エラー**: スタブ API が HTTP 401 を返す → 終了コード 2
  - **API エラー**: スタブ API が HTTP 500 を返す → 終了コード 3
  - **出力エラー**: 書き込み権限のないディレクトリに出力を指定 → 終了コード 4
- **スタブ**: 各種エラーを返すスタブ API

#### 2.7. Ctrl+C による中断

- **観点**: CLI 実行中に Ctrl+C を押すと、処理が中断される
- **検証内容**:
  - スタブ API が意図的に遅延する
  - CLI でエクスポートを開始
  - 進捗ログが表示されている間に Ctrl+C を押す
  - 処理が中断され、終了コード 130 で終了
  - 不完全な JSON ファイルが残らない
- **スタブ**: レスポンスが遅いスタブ API

---

### 3. データ出力の統合テスト

#### 3.1. JSON スキーマの検証

- **観点**: 出力された JSON がスキーマに準拠している
- **検証内容**:
  - スタブ API から疑似データを取得してエクスポート
  - 出力 JSON を読み込み、以下を検証:
    - `meta` セクションが存在し、必須フィールド（schemaVersion, exportedAt, organization, project, repository, summary）が含まれる
    - `items` 配列が存在し、各レコードに `pr`, `thread`, `comment` が含まれる
    - 各フィールドの型が正しい（例: `pr.id` は整数、`comment.content` は文字列）
  - JSON スキーマバリデーターでスキーマ準拠を確認
- **スタブ**: 標準的なデータを返すスタブ API

#### 3.2. UTF-8 エンコーディング・マルチバイト文字

- **観点**: マルチバイト文字（日本語など）が正しく出力される
- **検証内容**:
  - スタブ API が日本語を含むコメント（「このメソッドは null チェックが必要です」）を返す
  - エクスポートを実行
  - 出力 JSON を UTF-8 として読み込み、日本語が正しく表示される
- **スタブ**: 日本語コメントを含むスタブ API

#### 3.3. 改行・特殊文字のエスケープ

- **観点**: コメント本文に改行・特殊文字が含まれる場合、JSON として正しくエスケープされる
- **検証内容**:
  - スタブ API が改行（`\n`）、タブ（`\t`）、引用符（`"`）を含むコメントを返す
  - エクスポートを実行
  - 出力 JSON を読み込み、コメント本文が正しくパース可能
  - 改行・タブ・引用符が元の形式で復元される
- **スタブ**: 特殊文字を含むスタブ API

#### 3.4. 大量データの処理（メモリ効率）

- **観点**: 大量の PR・コメント（1000 PRs, 10万コメント）をエクスポートしてもメモリ使用量が許容範囲内
- **検証内容**:
  - スタブ API が 1000 件の PR と各 PR に平均 100 件のコメントを返す
  - エクスポートを実行
  - プロセスのメモリ使用量をモニタリング
  - メモリ使用量が 2GB 以下であることを確認
  - エクスポートが完了し、JSON ファイルが正しく生成される
- **スタブ**: 大量データを返すスタブ API
- **注意**: CI では小規模データ（100 PRs）でテスト、ローカルで大規模テスト

---

### 4. エラーハンドリングの統合テスト

#### 4.1. リトライ・バックオフの動作確認

- **観点**: API が一時的なエラー（HTTP 503）を返した場合、指数バックオフでリトライする
- **検証内容**:
  - スタブ API が最初の2回は HTTP 503 を返し、3回目で成功するように設定
  - エクスポートを実行
  - ログに「Retrying after 1 second...」「Retrying after 2 seconds...」というメッセージが表示される
  - 最終的にエクスポートが成功する
- **スタブ**: 回数カウント付きスタブ API

#### 4.2. レート制限（HTTP 429）の処理

- **観点**: API が HTTP 429 (Too Many Requests) を返した場合、`Retry-After` ヘッダーを尊重する
- **検証内容**:
  - スタブ API が HTTP 429 と `Retry-After: 5` ヘッダーを返す
  - エクスポートを実行
  - ログに「Rate limit exceeded. Retrying after 5 seconds...」というメッセージが表示される
  - 5秒後にリトライが実行される
- **スタブ**: HTTP 429 を返すスタブ API

#### 4.3. 最大リトライ回数の超過

- **観点**: 最大リトライ回数（3回）を超えた場合、エラーを表示して中断する
- **検証内容**:
  - スタブ API が常に HTTP 503 を返す（リトライしても成功しない）
  - エクスポートを実行
  - 3回のリトライ後、エラーメッセージが表示される
  - GUI: エラーダイアログ、CLI: 終了コード 3
- **スタブ**: 常に HTTP 503 を返すスタブ API

---

### 5. セキュリティの統合テスト

#### 5.1. PAT のログマスキング

- **観点**: PAT がログファイルに出力されない
- **検証内容**:
  - GUI または CLI でエクスポートを実行（PAT: `abc123def456xyz789`）
  - エクスポート完了後、ログファイル（`%TEMP%\AdoReviewExport\logs\app-{date}.log`）を開く
  - ログファイル内を検索し、PAT が含まれていないことを確認
  - 「***REDACTED***」というマスクが表示されている
- **スタブ**: スタブ API

#### 5.2. PAT の出力 JSON への非含有

- **観点**: 出力 JSON に PAT が含まれない
- **検証内容**:
  - エクスポートを実行
  - 出力 JSON を開き、PAT が含まれていないことを確認
- **スタブ**: スタブ API

#### 5.3. HTTPS 通信の確認

- **観点**: Azure DevOps API との通信が HTTPS で行われる
- **検証内容**:
  - スタブ API を HTTPS で起動
  - エクスポートを実行
  - ネットワークトレース（Fiddler など）で通信内容を確認
  - すべてのリクエストが HTTPS で送信される
- **スタブ**: HTTPS 対応のスタブ API

---

### 6. パフォーマンスの統合テスト

#### 6.1. 通常規模データの処理時間

- **観点**: 100 PRs, 1000 コメントのエクスポートが妥当な時間で完了する
- **検証内容**:
  - スタブ API が 100 PRs, 各 PR に平均 10 コメントを返す
  - エクスポートを実行
  - 処理時間を計測（開始〜完了まで）
  - 処理時間が 5分以内であることを確認
- **スタブ**: 標準データを返すスタブ API

#### 6.2. ページング処理の検証

- **観点**: API ページング（50件ごと）が正しく動作する
- **検証内容**:
  - スタブ API が 150 件の PR を返す（ページングが必要）
  - エクスポートを実行
  - スタブ API のアクセスログを確認
  - `$skip=0`, `$skip=50`, `$skip=100` の3回のリクエストが送信される
  - 出力 JSON に 150 件すべての PR が含まれる
- **スタブ**: ページング対応のスタブ API

---

## スタブ API の実装方針

### スタブ API の技術スタック

- **フレームワーク**: ASP.NET Core Minimal API (.NET 10)
- **起動方式**: テスト実行前に `dotnet run` でスタブサーバーを起動、テスト完了後に終了
- **ポート**: `http://localhost:5000` または `https://localhost:5001`

### スタブ API のエンドポイント

```csharp
// Pull Request 一覧取得
app.MapGet("/api/{org}/{project}/_apis/git/repositories/{repoId}/pullrequests", 
    (string org, string project, string repoId, int? $top, int? $skip) => 
{
    // 疑似 PR データを返す
});

// スレッド一覧取得
app.MapGet("/api/{org}/{project}/_apis/git/repositories/{repoId}/pullrequests/{prId}/threads",
    (string org, string project, string repoId, int prId) => 
{
    // 疑似スレッドデータを返す
});
```

### スタブ API の動作モード

スタブ API は環境変数で動作モードを切り替える:

- `STUB_MODE=success`: 正常なデータを返す
- `STUB_MODE=auth_error`: HTTP 401 を返す
- `STUB_MODE=timeout`: 意図的に遅延してタイムアウトをシミュレート
- `STUB_MODE=rate_limit`: HTTP 429 を返す
- `STUB_MODE=retry_then_success`: 最初の2回はエラー、3回目で成功

---

## CI/CD パイプラインでの実行

### CI 環境

- **プラットフォーム**: GitHub Actions（Windows Runner）
- **実行トリガー**: Pull Request 作成・更新時

### CI ワークフロー

```yaml
name: Integration Tests

on:
  pull_request:
    branches: [main]

jobs:
  integration-tests:
    runs-on: windows-latest
    steps:
      - name: Checkout code
        uses: actions/checkout@v4

      - name: Setup .NET 10
        uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '10.0.x'

      - name: Build application
        run: dotnet build -c Release

      - name: Start stub API
        run: |
          cd tests/StubApi
          dotnet run &
        env:
          STUB_MODE: success

      - name: Run integration tests
        run: dotnet test tests/Integration --logger "trx"

      - name: Stop stub API
        run: taskkill /IM dotnet.exe /F
        if: always()

      - name: Upload test results
        uses: actions/upload-artifact@v4
        with:
          name: test-results
          path: tests/Integration/TestResults/*.trx
        if: always()
```

---

## テスト実行手順（ローカル）

### 1. スタブ API の起動

```powershell
cd tests/StubApi
$env:STUB_MODE = "success"
dotnet run
```

### 2. GUI モードのテスト

```powershell
cd AdoReviewExport.UI\bin\Release\net10.0-windows10.0.19041.0\win-x64
.\AdoReviewExport.exe
```

手動で入力フィールドに以下を入力:
- Organization: `localhost:5000`
- Project: `TestProject`
- Repository: `test-repo`
- PAT: `test-pat-123`
- Output Path: `C:\temp\test-output.json`

### 3. CLI モードのテスト

```powershell
.\AdoReviewExport.exe export --org localhost:5000 --project TestProject --repo test-repo --pat test-pat-123 --output C:\temp\test-output.json
```

### 4. スタブ API の停止

```powershell
# Ctrl+C でスタブ API を停止
```

---

## テスト結果の検証

### 成功基準

- すべての統合テストがエラーなく完了する
- 出力 JSON が正しいスキーマに準拠している
- ログに PAT が含まれていない
- メモリ使用量が許容範囲内（2GB 以下）
- 処理時間が妥当な範囲内（100 PRs で 5分以内）

### 失敗時の対応

- テストが失敗した場合、ログファイル（`%TEMP%\AdoReviewExport\logs\app-{date}.log`）を確認
- スタブ API のアクセスログを確認
- エラーメッセージ・スタックトレースから原因を特定

---

**文書バージョン**: 1.0.0  
**最終更新日**: 2026-01-07
