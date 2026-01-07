---
description: Execute the implementation planning workflow using the plan template to generate design artifacts.
handoffs: 
  - label: Create Tasks
    agent: speckit.tasks
    prompt: Break the plan into tasks
    send: true
  - label: Create Checklist
    agent: speckit.checklist
    prompt: Create a checklist for the following domain...
---

## User Input

```text
$ARGUMENTS
```

You **MUST** consider the user input before proceeding (if not empty).

## Outline

1. **Setup**: Run `.specify/scripts/powershell/setup-plan.ps1 -Json` from repo root and parse JSON for FEATURE_SPEC, IMPL_PLAN, SPECS_DIR, BRANCH. For single quotes in args like "I'm Groot", use escape syntax: e.g 'I'\''m Groot' (or double-quote if possible: "I'm Groot").

2. **Load context**: Read FEATURE_SPEC and `.specify/memory/constitution.md`. Load IMPL_PLAN template (already copied).

3. **Execute plan workflow**: Follow the structure in IMPL_PLAN template to:
   - Fill Technical Context (mark unknowns as "NEEDS CLARIFICATION")
   - Fill Constitution Check section from constitution
   - Evaluate gates (ERROR if violations unjustified)
   - Phase 0: Generate research.md (resolve all NEEDS CLARIFICATION)
   - Phase 1: Generate functional-design.md, integration-test.md, quickstart.md
   - Phase 1: Update agent context by running the agent script
   - Re-evaluate Constitution Check post-design

4. **Stop and report**: Command ends after Phase 2 planning. Report branch, IMPL_PLAN path, and generated artifacts.

## Phases

### Phase 0: Outline & Research

1. **Extract unknowns from Technical Context** above:
   - For each NEEDS CLARIFICATION → research task
   - For each dependency → best practices task
   - For each integration → patterns task

2. **Generate and dispatch research agents**:

   ```text
   For each unknown in Technical Context:
     Task: "Research {unknown} for {feature context}"
   For each technology choice:
     Task: "Find best practices for {tech} in {domain}"
   ```

3. **Consolidate findings** in `research.md` using format:
   - Decision: [what was chosen]
   - Rationale: [why chosen]
   - Alternatives considered: [what else evaluated]

**Output**: research.md with all NEEDS CLARIFICATION resolved

### Phase 1: Design, External Spec, and Integration Test Plan

**Prerequisites:** `research.md` complete

1. **Create external specification** → `functional-design.md`:
    - Use the following exact heading structure.
    - **Important**: The sentences written under some headings in the sample below are **explanations of what to write in that section**, not the final document body.
       - Do **NOT** copy those explanation sentences into `functional-design.md` as-is.
       - Replace them with feature-specific content.
       - If you want to preserve guidance, convert it to **HTML comments** (e.g. `<!-- ... -->`) so it won't be mistaken as finished spec text.

     ```markdown
     # 外部仕様書

     ## 概要

     ## 機能

     ソフトウェアが持つ機能の一覧と、それぞれの説明を記載する。

     ## ユーザーインターフェース

     GUIもしくはCLIを含む場合、その定義を記載する。GUIの場合は画面定義・画面の動作など。CLIの場合はコマンドの定義など。

     GUI/CLIどちらのケースでも、ユーザーへ表示するメッセージが有る場合は、メッセージの文字列とメッセージを表示する条件を一覧表で記載すること。

     ## ソフトウェアインターフェース

     外部システムとのインターフェースを定義する。APIエンドポイントやデータフォーマットなどを記載する。APIエンドポイントは、WebAPIであればOpenAPI形式、.NETクラスライブラリについてはC#のXMLドキュメントコメント形式で、ネイティブC++についてはVisual C++のXMLドキュメントコメント形式で記載すること。

     ## 実現方式

     ソフトウェアの構成図、アーキテクチャや使用する技術スタックを記載する。また、機能を実現する上で方式の指定や重要なポイントがあれば記載する（Phase 0で判明した内容など）。

     ## 保守機能

     ソフトウェアを保守するための機能について記載する。ログの取得方法、設定変更方法、監視ポイントなど。

     ## 開発環境

     ソフトウェアをビルド・デプロイするために必要な環境を記載する。
     ```

   - Ensure the "ソフトウェアインターフェース" section contains the concrete external interfaces for this feature (e.g., OpenAPI snippet, XML doc comments, message formats), as applicable.

2. **Create integration test plan** → `integration-test.md`:
   - Write the integration test perspectives as bullet points.
   - Do NOT include unit-test-only perspectives; focus on tests performed in a real operating environment.

3. **Agent context update**:
   - Run `.specify/scripts/powershell/update-agent-context.ps1 -AgentType copilot`
   - These scripts detect which AI agent is in use
   - Update the appropriate agent-specific context file
   - Add only new technology from current plan
   - Preserve manual additions between markers

**Output**: functional-design.md, integration-test.md, quickstart.md, agent-specific file

## Key rules

- Use absolute paths
- ERROR on gate failures or unresolved clarifications
