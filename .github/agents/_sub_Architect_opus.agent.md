---
name: _sub_Architect_opus
description: 設計を行うサブエージェント(Claude Opus)
target: vscode
user-invocable: false
model: Claude Opus 4.6 (copilot)
tools: ['execute/getTerminalOutput', 'execute/awaitTerminal', 'execute/killTerminal', 'execute/createAndRunTask', 'execute/runInTerminal', 'read/problems', 'read/readFile', 'read/terminalSelection', 'read/terminalLastCommand', 'edit/createDirectory', 'edit/createFile', 'edit/editFiles', 'search', 'web', 'roslyn-mcp/*']
agents: []
---