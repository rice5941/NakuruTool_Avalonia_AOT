---
name: _sub_Architect_codex
description: 設計を行うサブエージェント(Codex)
target: vscode
user-invokable: false
model: GPT-5.3-Codex (copilot)
tools: ['execute/getTerminalOutput', 'execute/awaitTerminal', 'execute/killTerminal', 'execute/createAndRunTask', 'execute/runInTerminal', 'read/problems', 'read/readFile', 'read/terminalSelection', 'read/terminalLastCommand', 'edit/createDirectory', 'edit/createFile', 'edit/editFiles', 'search', 'web', io.github.upstash/context7/*]
agents: []
---