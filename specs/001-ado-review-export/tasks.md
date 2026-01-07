---
description: "Task list for Azure DevOps PR Review Comment Exporter implementation"
---

# Tasks: Azure DevOps PR Review Comment Exporter

**Feature Branch**: `001-ado-review-export`  
**Date**: January 7, 2026  
**Input**: Design documents from `/specs/001-ado-review-export/`

**Prerequisites**: plan.md, spec.md, research.md, functional-design.md, integration-test.md, quickstart.md

**Organization**: Tasks are grouped by user story to enable independent implementation and testing of each story.

**Tests**: 統合テストを省略してはいけません。統合テスト (integration-test.md) は、与えられた敬虔な統合テスト計画を接後で粛行し、実 OS 環境を修正せずの範囲で操作検証を実施する。（仕様要求としてを指定しないが、統一機査程度の品質施として実施する）

---

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story this task belongs to (e.g., US1, US2, US3)
- Include exact file paths in descriptions

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Project initialization and basic structure

- [X] T001 Create solution file at AdoReviewExport/AdoReviewExport.sln
- [X] T002 Initialize AdoReviewExport.UI project (WinUI 3, .NET 10, Unpackaged) at AdoReviewExport/AdoReviewExport.UI/AdoReviewExport.UI.csproj
- [X] T003 [P] Initialize AdoReviewExport.Application class library (.NET 10) at AdoReviewExport/AdoReviewExport.Application/AdoReviewExport.Application.csproj
- [X] T004 [P] Initialize AdoReviewExport.Infrastructure class library (.NET 10) at AdoReviewExport/AdoReviewExport.Infrastructure/AdoReviewExport.Infrastructure.csproj
- [X] T005 Configure project references (UI → Application → Infrastructure)
- [X] T006 [P] Add NuGet packages to UI project: Microsoft.WindowsAppSDK 1.7+, Microsoft.Extensions.DependencyInjection, Microsoft.Extensions.Logging
- [X] T007 [P] Add NuGet packages to Infrastructure project: Polly 8.x, System.Net.Http, System.Text.Json, NLog 5.x
- [X] T008 Configure Unpackaged WinUI 3 settings in AdoReviewExport.UI/AdoReviewExport.UI.csproj (EnableMsixTooling=false, WindowsAppSDKSelfContained=false; dotnet build向けにAppxMSBuildToolsPathも設定)
- [X] T009 [P] Create xUnit test project at AdoReviewExport/tests/Unit/Unit.csproj
- [X] T010 [P] Create xUnit integration test project at AdoReviewExport/tests/Integration/Integration.csproj
- [X] T011 [P] Add Moq package to Unit test project
- [X] T012 Create stub API project at AdoReviewExport/tests/StubApi/Program.cs using ASP.NET Core Minimal API with /pullrequests and /threads endpoints

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Core infrastructure that MUST be complete before ANY user story can be implemented

**⚠️ CRITICAL**: No user story work can begin until this phase is complete

- [X] T013 Create domain exception hierarchy base class at AdoReviewExport/AdoReviewExport.Application/Exceptions/AdoExportException.cs
- [X] T014 [P] Create InputValidationException at AdoReviewExport/AdoReviewExport.Application/Exceptions/InputValidationException.cs
- [X] T015 [P] Create AuthenticationException at AdoReviewExport/AdoReviewExport.Application/Exceptions/AuthenticationException.cs
- [X] T016 [P] Create ApiException at AdoReviewExport/AdoReviewExport.Application/Exceptions/ApiException.cs
- [X] T017 [P] Create OutputException at AdoReviewExport/AdoReviewExport.Application/Exceptions/OutputException.cs
- [X] T018 Create ExportRequest model at AdoReviewExport/AdoReviewExport.Application/Models/ExportRequest.cs
- [X] T019 [P] Create ExportProgress model at AdoReviewExport/AdoReviewExport.Application/Models/ExportProgress.cs
- [X] T020 [P] Create ExportResult model at AdoReviewExport/AdoReviewExport.Application/Models/ExportResult.cs
- [X] T021 Create DTO classes: IdentityDto at AdoReviewExport/AdoReviewExport.Infrastructure/AzureDevOps/Dtos/IdentityDto.cs
- [X] T022 [P] Create FilePositionDto at AdoReviewExport/AdoReviewExport.Infrastructure/AzureDevOps/Dtos/FilePositionDto.cs
- [X] T023 [P] Create ThreadContextDto at AdoReviewExport/AdoReviewExport.Infrastructure/AzureDevOps/Dtos/ThreadContextDto.cs
- [X] T024 [P] Create CommentDto at AdoReviewExport/AdoReviewExport.Infrastructure/AzureDevOps/Dtos/CommentDto.cs
- [X] T025 [P] Create ThreadDto at AdoReviewExport/AdoReviewExport.Infrastructure/AzureDevOps/Dtos/ThreadDto.cs
- [X] T026 [P] Create PullRequestDto at AdoReviewExport/AdoReviewExport.Infrastructure/AzureDevOps/Dtos/PullRequestDto.cs
- [X] T027 Create IAdoApiClient interface at AdoReviewExport/AdoReviewExport.Infrastructure/AzureDevOps/IAdoApiClient.cs with GetPullRequestsAsync and GetThreadsAsync methods
- [X] T028 Implement AdoApiClient at AdoReviewExport/AdoReviewExport.Infrastructure/AzureDevOps/AdoApiClient.cs with HTTP Basic Auth for PAT
- [X] T029 Implement paging support using Azure DevOps API continuation token in AdoApiClient for PR retrieval
- [X] T030 Create RetryPolicyFactory at AdoReviewExport/AdoReviewExport.Infrastructure/Http/RetryPolicyFactory.cs using Polly (exponential backoff, max 3 retries)
- [X] T031 Add HTTP 429 rate limit handling with Retry-After header respect in AdoApiClient
- [X] T032_Scope Create PrimaryAuthenticationScope validation: On first API connection, call _apis/profile/profiles/me to verify PAT has 'vso.code' Read scope; throw AuthenticationException with required scope message if missing (FR-004)
- [X] T032_Masking Add PAT masking in NLog output (replace PAT with ***REDACTED*** in file/console output)
- [X] T033 Create IJsonExporter interface at AdoReviewExport/AdoReviewExport.Infrastructure/Json/IJsonExporter.cs
- [X] T034_Base Implement JsonExporter with Utf8JsonWriter for streaming output at AdoReviewExport/AdoReviewExport.Infrastructure/Json/JsonExporter.cs
- [X] T034_Meta Implement JsonExporter to include root.meta section with exportedAt (ISO 8601), repository (org/project/repo), counts (totalPRs, totalComments, totalThreads), appVersion, filters (FR-014)
- [X] T034_Compat Add root.schemaVersion = "1.0" to JSON output; document that new fields will be added-only, never modifying existing structure (FR-015)
- [X] T035 Create IExportService interface at AdoReviewExport/AdoReviewExport.Application/Services/IExportService.cs with ExportAsync method
- [X] T036 Implement ExportService at AdoReviewExport/AdoReviewExport.Application/Services/ExportService.cs orchestrating PR/Thread/Comment retrieval
- [X] T037 Configure dependency injection container in AdoReviewExport/AdoReviewExport.UI/App.xaml.cs for all services and interfaces
- [X] T038_NLog Setup NLog with file logging to %TEMP%\AdoReviewExport\logs\ with daily rotation and JSON format output

**Checkpoint**: Foundation ready - user story implementation can now begin in parallel

---

## Phase 3: User Story 1 - GUI 操作による単一リポジトリのコメントエクスポート (Priority: P1) 🎯 MVP

**Goal**: GUI アプリケーションを使って特定リポジトリの全 PR レビューコメントを JSON ファイルとしてエクスポートする

**Independent Test**: GUI アプリを起動し、Azure DevOps の認証情報とリポジトリ情報を入力して実行すると、指定した場所に JSON ファイルが出力される。LLM にそのファイルを渡すと、過去のレビュー指摘の傾向を抽出できる。

### GUI Implementation for User Story 1

- [X] T039 Create Program.cs at AdoReviewExport/AdoReviewExport.UI/Program.cs with Main method for GUI/CLI mode branching
- [X] T040 Implement GUI mode detection and WinUI Application.Start in Program.Main
- [X] T041 Create App.xaml at AdoReviewExport/AdoReviewExport.UI/App.xaml with WinUI 3 application markup
- [X] T042 Implement App.xaml.cs with DI container initialization at AdoReviewExport/AdoReviewExport.UI/App.xaml.cs
- [X] T043 Create MainWindow.xaml at AdoReviewExport/AdoReviewExport.UI/Views/MainWindow.xaml with input fields (Organization, Project, Repository, PAT, Output Path, Authors)
- [X] T044 Add progress bar, status text, and log view UI controls to MainWindow.xaml
- [X] T045 Add Start Export and Cancel buttons to MainWindow.xaml
- [X] T046 Create MainViewModel at AdoReviewExport/AdoReviewExport.UI/ViewModels/MainViewModel.cs with INotifyPropertyChanged
- [X] T047 Implement input validation in MainViewModel (required fields check, MSG-001 to MSG-005)
- [X] T048 Implement StartExportAsync command in MainViewModel calling IExportService.ExportAsync
- [X] T049 Implement progress reporting via IProgress<ExportProgress> in MainViewModel updating UI
- [X] T050 Implement log view updates in MainViewModel (ObservableCollection<string> for logs)
- [X] T051 Implement Cancel button functionality with CancellationTokenSource in MainViewModel
- [X] T052 Create file selection dialog handler (📁 button) in MainWindow.xaml.cs
- [X] T053 Create completion dialog at AdoReviewExport/AdoReviewExport.UI/Views/CompletionDialog.xaml with Open File/Open Folder buttons
- [X] T054 Create error dialog at AdoReviewExport/AdoReviewExport.UI/Views/ErrorDialog.xaml with error message display
- [X] T055 Implement error handling in MainViewModel mapping exceptions to user-friendly messages (ERR-001 to ERR-005)
- [X] T056 Add PAT masking in PasswordBox (display as ****) in MainWindow.xaml
- [X] T057 Implement default output path generation (Current directory + ado-review-export-{timestamp}.json) in MainViewModel

### Integration Tests for User Story 1

- [X] T058 [P] Implement stub API endpoint for PR list via Minimal API at /pullrequests returning mock PR data
- [X] T059 [P] Implement stub API endpoint for threads list via Minimal API at /threads returning mock thread/comment data
- [X] T060 [P] Implement stub API modes via environment variable (success, auth_error, timeout, rate_limit) in StubApi/Program.cs
- [X] T061 Create integration test for GUI application startup at AdoReviewExport/tests/Integration/GuiModeTests.cs verifying MainWindow displays
- [X] T062 [P] Create integration test for GUI export success at AdoReviewExport/tests/Integration/GuiModeTests.cs using stub API
- [X] T063 [P] Create integration test for GUI authentication error at AdoReviewExport/tests/Integration/GuiModeTests.cs verifying error dialog
- [X] T064 [P] Create integration test for GUI network error with retry at AdoReviewExport/tests/Integration/GuiModeTests.cs
- [X] T065 [P] Create integration test for GUI cancel functionality at AdoReviewExport/tests/Integration/GuiModeTests.cs verifying incomplete file not saved
- [X] T066 [P] Create integration test for JSON schema validation at AdoReviewExport/tests/Integration/DataOutputTests.cs verifying meta and items structure
- [X] T067 [P] Create integration test for UTF-8 encoding and multibyte characters at AdoReviewExport/tests/Integration/DataOutputTests.cs with Japanese text
- [X] T068 [P] Create integration test for special character escaping (newlines, quotes) at AdoReviewExport/tests/Integration/DataOutputTests.cs
- [X] T069 [P] Create integration test for PAT masking in logs at AdoReviewExport/tests/Integration/SecurityTests.cs verifying PAT not in log file
- [X] T070 [P] Create integration test for PAT not in output JSON at AdoReviewExport/tests/Integration/SecurityTests.cs

**Checkpoint**: At this point, User Story 1 should be fully functional and testable independently. MVP is complete.

---

## Phase 4: User Story 2 - CLI によるバッチエクスポート (Priority: P2)

**Goal**: CLI コマンドを実行してエクスポートを行い、自動化や定期実行を可能にする

**Independent Test**: PowerShell やバッチファイルから、コマンドライン引数（Organization、Project、Repository、PAT、出力先）を渡してアプリを実行すると、GUI を表示せずに JSON ファイルが出力される。終了コードで成功/失敗を判定できる。

### CLI Implementation for User Story 2

- [X] T071 [US2] Implement CLI mode detection in Program.Main (args starting with --)
- [X] T072 [US2] Create command line argument parser at AdoReviewExport/AdoReviewExport.UI/Helpers/CliArgumentParser.cs for --org, --project, --repo, --pat, --output
- [X] T073 [US2] Implement environment variable support for AZDO_PAT in CliArgumentParser
- [X] T074 [US2] Create ConsoleHelper at AdoReviewExport/AdoReviewExport.UI/Helpers/ConsoleHelper.cs with AttachConsole for WinExe output
- [X] T075 [US2] Implement CLI mode entry point RunCliMode in Program.cs calling ExportService
- [X] T076 [US2] Implement progress logging to Console.WriteLine in CLI mode
- [X] T077 [US2] Implement exit code mapping: 0=success, 1=error (any error type) per FR-024
- [X] T078 [US2] Implement --help option handler displaying usage message in ConsoleHelper
- [X] T079 [US2] Implement Ctrl+C handling with CancellationToken in CLI mode
- [X] T080 [US2] Add validation for missing required arguments with error message and exit code 1

### Integration Tests for User Story 2

- [X] T081 [P] [US2] Create integration test for --help display at AdoReviewExport/tests/Integration/CliModeTests.cs verifying usage message
- [X] T082 [P] [US2] Create integration test for missing arguments at AdoReviewExport/tests/Integration/CliModeTests.cs verifying exit code 1
- [X] T083 [P] [US2] Create integration test for successful CLI export at AdoReviewExport/tests/Integration/CliModeTests.cs using stub API verifying exit code 0
- [X] T084 [P] [US2] Create integration test for AZDO_PAT environment variable at AdoReviewExport/tests/Integration/CliModeTests.cs
- [X] T085 [P] [US2] Create integration test for CLI authentication error at AdoReviewExport/tests/Integration/CliModeTests.cs verifying exit code 2
- [X] T086 [P] [US2] Create integration test for CLI API error at AdoReviewExport/tests/Integration/CliModeTests.cs verifying exit code 3
- [X] T087 [P] [US2] Create integration test for CLI output error at AdoReviewExport/tests/Integration/CliModeTests.cs verifying exit code 4
- [X] T088 [P] [US2] Create integration test for Ctrl+C cancellation at AdoReviewExport/tests/Integration/CliModeTests.cs verifying exit code 130

**Checkpoint**: At this point, User Stories 1 AND 2 should both work independently. Both GUI and CLI modes are functional.

---

## Phase 5: User Story 3 - 投稿者フィルタによる特定ユーザーのコメント抽出 (Priority: P3)

**Goal**: 投稿者名でフィルタしてエクスポートし、特定のレビュアーのコメントのみを抽出する

**Independent Test**: GUI または CLI で投稿者フィルタ（例：`--authors "user1,user2"`）を指定してエクスポートすると、出力 JSON には指定ユーザーのコメントのみが含まれる。

### Author Filter Implementation for User Story 3

- [X] T089 [US3] Add AuthorFilters property to ExportRequest model in AdoReviewExport/AdoReviewExport.Application/Models/ExportRequest.cs
- [X] T090 [US3] Implement author filter logic in ExportService.ExportAsync filtering comments by author.displayName or author.uniqueName (case-insensitive partial match)
- [X] T091 [US3] Update JsonExporter to include applied filters in meta.filters section of JSON output
- [X] T092 [US3] Add Authors optional input field to MainWindow.xaml (comma-separated text box)
- [X] T093 [US3] Update MainViewModel to pass AuthorFilters from UI input to ExportRequest
- [X] T094 [US3] Add --authors optional argument to CLI argument parser in CliArgumentParser
- [X] T095 [US3] Implement warning message when author filter results in zero comments (display in GUI log, CLI stdout)

### Integration Tests for User Story 3

- [X] T096 [P] [US3] Update stub API to return comments from multiple authors (alice, bob, charlie) via Minimal API /threads endpoint
- [X] T097 [P] [US3] Create integration test for GUI author filter at AdoReviewExport/tests/Integration/GuiModeTests.cs verifying only filtered authors in output
- [X] T098 [P] [US3] Create integration test for CLI author filter at AdoReviewExport/tests/Integration/CliModeTests.cs verifying only alice and bob comments exported
- [X] T099 [P] [US3] Create integration test for non-existent author filter at AdoReviewExport/tests/Integration/DataOutputTests.cs verifying warning message and zero comments

**Checkpoint**: All user stories should now be independently functional. Full feature set is complete.

---

## Phase 6: Polish & Cross-Cutting Concerns

**Purpose**: Improvements that affect multiple user stories

- [ ] T100 [P] Create CI workflow at .github/workflows/ci.yml with build, unit tests, stub API startup, integration tests
- [ ] T101 [P] Add memory efficiency test (1000 PRs, 10,000 comments) at AdoReviewExport/tests/Integration/PerformanceTests.cs verifying <2GB memory usage
- [ ] T102 [P] Create integration test for paging (150 PRs requiring multiple pages) at AdoReviewExport/tests/Integration/DataOutputTests.cs
- [ ] T103 [P] Create integration test for retry with backoff at AdoReviewExport/tests/Integration/ErrorHandlingTests.cs
- [ ] T104 [P] Create integration test for rate limit (HTTP 429) handling at AdoReviewExport/tests/Integration/ErrorHandlingTests.cs
- [ ] T105 [P] Create integration test for max retry exhaustion at AdoReviewExport/tests/Integration/ErrorHandlingTests.cs
- [ ] T106 Add XML documentation comments (Japanese) to all public interfaces and classes
- [ ] T107 Update README.md with project overview, build instructions, and quickstart link
- [ ] T108 Validate quickstart.md steps by following GUI and CLI scenarios
- [ ] T109 Create release build script at scripts/build-release.ps1 for Release mode build and zip packaging
- [ ] T110 Review copilot-instructions.md compliance (Japanese docs, English code, exception handling, no OS modification in tests)

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies - can start immediately
- **Foundational (Phase 2)**: Depends on Setup completion - BLOCKS all user stories
- **User Stories (Phase 3, 4, 5)**: All depend on Foundational phase completion
  - User Story 1 (P1 - GUI): Can start after Foundational - No dependencies on other stories
  - User Story 2 (P2 - CLI): Can start after Foundational - No dependencies on other stories (shares same ExportService)
  - User Story 3 (P3 - Filter): Can start after Foundational - Extends US1 and US2 but is independently testable
- **Polish (Phase 6)**: Depends on all desired user stories being complete

### User Story Dependencies

- **User Story 1 (P1 - GUI Export)**: Can start after Foundational (Phase 2) - No dependencies on other stories
- **User Story 2 (P2 - CLI Export)**: Can start after Foundational (Phase 2) - Reuses ExportService from US1 but can be implemented independently
- **User Story 3 (P3 - Author Filter)**: Can start after Foundational (Phase 2) - Extends both US1 and US2 by adding filter parameter, but independently testable

### Within Each User Story

- **User Story 1**: UI components → ViewModel → Service integration → Integration tests
- **User Story 2**: CLI argument parsing → CLI entry point → Service integration → Integration tests
- **User Story 3**: Model extension → Filter logic in service → UI/CLI updates → Integration tests

### Parallel Opportunities

**Phase 1 (Setup)**:
- T003, T004 can run in parallel (different projects)
- T006, T007 can run in parallel (different projects)
- T009, T010, T011 can run in parallel (test projects)

**Phase 2 (Foundational)**:
- T014-T017 can run in parallel (exception classes, different files)
- T019, T020 can run in parallel (model classes, different files)
- T022-T026 can run in parallel (DTO classes, different files)

**Phase 3 (User Story 1)**:
- T058, T059, T060 can run in parallel (stub API endpoints, different files)
- T062-T068 can run in parallel (integration tests, different files)
- T069, T070 can run in parallel (security tests, different files)

**Phase 4 (User Story 2)**:
- T081-T088 can run in parallel (CLI integration tests, different files)

**Phase 5 (User Story 3)**:
- T096-T099 can run in parallel (filter integration tests, different files)

**Phase 6 (Polish)**:
- T100-T105 can run in parallel (CI, performance tests, error handling tests)

**Cross-Phase Parallelization**:
- Once Foundational (Phase 2) completes, User Story 1, 2, and 3 can all start in parallel if team capacity allows
- Different team members can work on GUI (US1), CLI (US2), and Filter (US3) simultaneously

---

## Parallel Example: Foundational Phase

```bash
# Launch all exception classes together:
Task T014: "Create InputValidationException at AdoReviewExport/AdoReviewExport.Application/Exceptions/InputValidationException.cs"
Task T015: "Create AuthenticationException at AdoReviewExport/AdoReviewExport.Application/Exceptions/AuthenticationException.cs"
Task T016: "Create ApiException at AdoReviewExport/AdoReviewExport.Application/Exceptions/ApiException.cs"
Task T017: "Create OutputException at AdoReviewExport/AdoReviewExport.Application/Exceptions/OutputException.cs"

# Launch all DTO classes together:
Task T022: "Create FilePositionDto at AdoReviewExport/AdoReviewExport.Infrastructure/AzureDevOps/Dtos/FilePositionDto.cs"
Task T023: "Create ThreadContextDto at AdoReviewExport/AdoReviewExport.Infrastructure/AzureDevOps/Dtos/ThreadContextDto.cs"
Task T024: "Create CommentDto at AdoReviewExport/AdoReviewExport.Infrastructure/AzureDevOps/Dtos/CommentDto.cs"
Task T025: "Create ThreadDto at AdoReviewExport/AdoReviewExport.Infrastructure/AzureDevOps/Dtos/ThreadDto.cs"
Task T026: "Create PullRequestDto at AdoReviewExport/AdoReviewExport.Infrastructure/AzureDevOps/Dtos/PullRequestDto.cs"
```

---

## Parallel Example: User Story 1 (GUI)

```bash
# Launch all stub API endpoints together:
Task T058: "Implement stub API endpoint for PR list at AdoReviewExport/tests/StubApi/Controllers/PullRequestsController.cs"
Task T059: "Implement stub API endpoint for threads list at AdoReviewExport/tests/StubApi/Controllers/ThreadsController.cs"
Task T060: "Implement stub API modes via environment variable in StubApi/Program.cs"

# Launch all integration tests together:
Task T062: "Create integration test for GUI export success at AdoReviewExport/tests/Integration/GuiModeTests.cs"
Task T063: "Create integration test for GUI authentication error at AdoReviewExport/tests/Integration/GuiModeTests.cs"
Task T064: "Create integration test for GUI network error with retry at AdoReviewExport/tests/Integration/GuiModeTests.cs"
Task T065: "Create integration test for GUI cancel functionality at AdoReviewExport/tests/Integration/GuiModeTests.cs"
```

---

## Implementation Strategy

### MVP First (User Story 1 Only)

1. ✅ Complete Phase 1: Setup
2. ✅ Complete Phase 2: Foundational (CRITICAL - blocks all stories)
3. ✅ Complete Phase 3: User Story 1 (GUI Export)
4. **STOP and VALIDATE**: Test User Story 1 independently using integration tests
5. **Deploy/Demo MVP**: Users can export review comments via GUI

**MVP Scope**: User Story 1 provides complete value - users can export PR review comments to JSON via GUI and analyze with LLM.

### Incremental Delivery

1. **Foundation**: Complete Setup (Phase 1) + Foundational (Phase 2) → Core infrastructure ready
2. **MVP Release**: Add User Story 1 (Phase 3) → GUI export working → Test independently → Deploy/Demo
3. **Enhancement 1**: Add User Story 2 (Phase 4) → CLI export working → Test independently → Deploy/Demo
4. **Enhancement 2**: Add User Story 3 (Phase 5) → Author filtering working → Test independently → Deploy/Demo
5. **Polish**: Complete Phase 6 → CI/CD, performance optimization, documentation

Each story adds value without breaking previous stories. After MVP (US1), users can already analyze review comments.

### Parallel Team Strategy

With multiple developers:

1. **Team Phase**: Complete Setup (Phase 1) + Foundational (Phase 2) together
2. **Parallel Phase** (Once Foundational is done):
   - Developer A: User Story 1 (GUI) - T039-T070
   - Developer B: User Story 2 (CLI) - T071-T088 (can start immediately after foundational)
   - Developer C: User Story 3 (Filter) - T089-T099 (can start immediately after foundational)
3. **Integration Phase**: Stories merge and integrate independently
4. **Polish Phase**: Team completes Phase 6 together

**Benefit**: With 3 developers, all user stories can be completed in parallel after foundational phase, significantly reducing time to full feature release.

---

## Summary

- **Total Tasks**: 110
- **Phase 1 (Setup)**: 12 tasks
- **Phase 2 (Foundational)**: 26 tasks (BLOCKING - must complete before any user story)
- **Phase 3 (User Story 1 - GUI)**: 32 tasks (MVP)
- **Phase 4 (User Story 2 - CLI)**: 18 tasks
- **Phase 5 (User Story 3 - Filter)**: 11 tasks
- **Phase 6 (Polish)**: 11 tasks

**Parallel Opportunities**:
- Phase 1: 7 tasks can run in parallel
- Phase 2: 12 tasks can run in parallel
- Phase 3: 14 tasks can run in parallel
- Phase 4: 8 tasks can run in parallel
- Phase 5: 4 tasks can run in parallel
- Phase 6: 6 tasks can run in parallel
- **Cross-phase**: All 3 user stories can be developed in parallel after Phase 2

**MVP Recommendation**: Complete Phase 1 + Phase 2 + Phase 3 (User Story 1) = 70 tasks for a fully functional GUI export tool.

**Independent Test Criteria**:
- **User Story 1**: Launch GUI, input credentials, export to JSON, verify file can be read by LLM
- **User Story 2**: Run CLI command, verify JSON output and exit code 0, verify file can be read by LLM
- **User Story 3**: Run with --authors filter, verify only specified authors in output JSON

---

## Notes

- All tasks follow the strict checklist format: `- [ ] [TaskID] [P?] [Story?] Description with file path`
- [P] tasks = different files, no dependencies within the same phase
- [Story] label (US1, US2, US3) maps task to specific user story for traceability
- Tests are included per integration-test.md specifications (operational validation, not unit tests)
- Each user story is independently completable and testable
- Commit after each task or logical group
- Stop at any checkpoint to validate story independently
- Foundation (Phase 2) MUST be complete before any user story work begins

**Implementation Order**: Setup → Foundational → US1 (MVP) → US2 (CLI) → US3 (Filter) → Polish

**文書バージョン**: 1.0.0  
**最終更新日**: 2026-01-07
