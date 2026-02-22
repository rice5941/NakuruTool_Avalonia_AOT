---
name: _sub_DiffVerify_opus
description: 差分検証を行うサブエージェント(Claude Opus)
target: vscode
user-invokable: false
model: Claude Opus 4.6 (copilot)
tools: ['execute/getTerminalOutput', 'execute/awaitTerminal', 'execute/killTerminal', 'execute/createAndRunTask', 'execute/runInTerminal', 'read/problems', 'read/readFile', 'read/terminalSelection', 'read/terminalLastCommand', 'edit/createDirectory', 'edit/createFile', 'edit/editFiles', 'search', 'web', io.github.upstash/context7/*]
agents: []
---