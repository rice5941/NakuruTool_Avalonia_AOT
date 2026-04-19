---
name: _sub_DiffVerify_codex
description: 差分検証を行うサブエージェント(Codex)
target: vscode
user-invocable: false
model: GPT-5.4 (copilot)
tools: [vscode/runCommand, execute/getTerminalOutput, execute/killTerminal, execute/createAndRunTask, execute/runInTerminal, read/problems, read/readFile, read/viewImage, read/terminalSelection, read/terminalLastCommand, edit/createDirectory, edit/createFile, edit/editFiles, search, web, avalonia-docs/get_avalonia_expert_rules, avalonia-docs/lookup_avalonia_api, avalonia-docs/migrate_diagnostics, avalonia-docs/search_avalonia_docs]
---