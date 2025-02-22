# marathon-ext README

🏃‍♂️ Marathon Transpiler (marathon-ext)
🚀 Runtime-First Software Development & Transpilation for VS Code

Marathon is a Runtime-First code transpiler that allows you to write code in execution order, automatically generating the necessary class structures and boilerplate. It's ideal for:

Event-driven programming
Distributed systems
Game engines
Scientific computing
🏗️ Write execution flow first, let the structure build itself!

## Features

✅ Runtime-First Syntax
✅ Auto-Generates Boilerplate Code
✅ Semantic Highlighting for mrt Files
✅ Runtime-First Test-Driven Development (TDD) with @assert
✅ Code Annotations for Execution Flow (@varInit, @run, @onEvent)
✅ Transpiles to React, Python, C#, and more!

## Requirements

VS Code >=1.97.0
Node.js >=16.x
npm >=8.x

## Extension Settings

* `mrtconfig.json`: Must be at the same folder level as your MRT file.

{
  "transpilerOptions": {
    "target": "csharp"
  }
}

## Known Issues

Semantic token highlighting is currently limited. Expanding support for React Native is planned.

## Release Notes

1.0.0 - Initial Release
✅ Syntax highlighting for .mrt
✅ Initial support for C#

---

## Working with Markdown

You can author your README using Visual Studio Code. Here are some useful editor keyboard shortcuts:

* Split the editor (`Cmd+\` on macOS or `Ctrl+\` on Windows and Linux).
* Toggle preview (`Shift+Cmd+V` on macOS or `Shift+Ctrl+V` on Windows and Linux).
* Press `Ctrl+Space` (Windows, Linux, macOS) to see a list of Markdown snippets.

## For more information

* [Marathon Transpiler](https://github.com/ameritusweb/MarathonTranspiler)

**Enjoy!**
