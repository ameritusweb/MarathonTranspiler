import * as vscode from 'vscode';
import * as path from 'path';
import { LanguageClient, LanguageClientOptions, ServerOptions, TransportKind } from 'vscode-languageclient/node';

let client: LanguageClient;
let statusBarItem: vscode.StatusBarItem;
let previewButton: vscode.StatusBarItem;

export async function activate(context: vscode.ExtensionContext) {
    const serverPath = context.asAbsolutePath(
        path.join('server', 'MarathonTranspiler.LSP.dll')
    );

    statusBarItem = vscode.window.createStatusBarItem(vscode.StatusBarAlignment.Right, 100);
    statusBarItem.command = 'marathon.forceCompile';
    statusBarItem.tooltip = 'Force compile Marathon code';
    context.subscriptions.push(statusBarItem);

    previewButton = vscode.window.createStatusBarItem(vscode.StatusBarAlignment.Right, 99);
    previewButton.text = "$(preview) Preview";
    previewButton.command = 'marathon.showTranspiled';
    previewButton.tooltip = 'Show transpiled code';
    context.subscriptions.push(previewButton);

    // Register force compile command
    context.subscriptions.push(
        vscode.commands.registerCommand('marathon.forceCompile', async () => {
            const editor = vscode.window.activeTextEditor;
            if (editor && editor.document.languageId === 'mrt') {
                statusBarItem.text = "$(sync~spin) Compiling...";
                statusBarItem.show();

                // Send command to LSP
                try {
                    await client.sendRequest('workspace/executeCommand', {
                        command: 'marathon.forceCompile',
                        arguments: [editor.document.uri.toString()]
                    });

                    statusBarItem.text = "$(check) Ready";
                } catch (error) {
                    vscode.window.showErrorMessage(`Error during compilation: ${error}`);
                    statusBarItem.text = "$(error) Compilation Failed";
                }
            }
        })
    );

    const outputChannel = vscode.window.createOutputChannel('Marathon LSP');
    outputChannel.appendLine(`Extension activated. Working directory: ${serverPath}`);
    
    checkActiveEditor(vscode.window.activeTextEditor);

    // Show status bar when active editor changes
    context.subscriptions.push(
        vscode.window.onDidChangeActiveTextEditor(editor => {
            checkActiveEditor(editor);
        })
    );

    // Ensure semantic highlighting is enabled
    await checkSemanticHighlightingSetting(context, outputChannel);

    // Log available languages
    vscode.languages.getLanguages().then(langs => {
        outputChannel.appendLine(`Available Languages: ${langs}`);
    });

    context.subscriptions.push(outputChannel);

    const showTranspiledCommand = vscode.commands.registerCommand('marathon.showTranspiled', async () => {
        const editor = vscode.window.activeTextEditor;
        if (editor && editor.document.languageId === 'mrt') {
          try {
            statusBarItem.text = "$(sync~spin) Generating preview...";
            statusBarItem.show();
            
            // Request transpiled code from the LSP
            const result: any = await client.sendRequest('marathon/getTranspiledCode', {
              uri: editor.document.uri.toString()
            });
            
            if (result && result.code) {
              // Create a new untitled document with the transpiled code
              const languageId = getLanguageIdFromTarget(result.target);
              const doc = await vscode.workspace.openTextDocument({
                content: result.code,
                language: languageId
              });
              
              // Show the document in a new editor group (side by side)
              await vscode.window.showTextDocument(doc, { viewColumn: vscode.ViewColumn.Beside });
              statusBarItem.text = "$(check) Ready";
            } else {
              vscode.window.showErrorMessage('No transpiled code available');
              statusBarItem.text = "$(error) Preview Failed";
            }
          } catch (error) {
            vscode.window.showErrorMessage(`Error generating preview: ${error}`);
            statusBarItem.text = "$(error) Preview Failed";
          }
        }
      });
      
      context.subscriptions.push(showTranspiledCommand);

    const serverOptions: ServerOptions = {
        run: { command: "dotnet", args: [serverPath], transport: TransportKind.stdio },
        debug: { command: "dotnet", args: [serverPath], transport: TransportKind.stdio }
    };

    const clientOptions: LanguageClientOptions = {
        documentSelector: [{ scheme: "file", language: "mrt" }],
        synchronize: {
            configurationSection: "mrtLanguageServer"
        }
    };

    client = new LanguageClient("mrtLanguageServer", "MRT Language Server", serverOptions, clientOptions);

    client.start();
}

function getLanguageIdFromTarget(target: string): string {
    switch (target.toLowerCase()) {
      case 'csharp': return 'csharp';
      case 'react': 
      case 'react-redux': return 'javascript';
      case 'python': return 'python';
      case 'unity': return 'csharp';
      case 'orleans': return 'csharp';
      case 'wpf': return 'csharp';
      default: return 'plaintext';
    }
  }

export function deactivate(): Thenable<void> | undefined {
    return client ? client.stop() : undefined;
}

function checkActiveEditor(editor: vscode.TextEditor | undefined) {
    if (editor && editor.document.languageId === 'mrt') {
      statusBarItem.text = "$(check) Ready";
      statusBarItem.show();
      previewButton.show();
    } else {
      statusBarItem.hide();
      previewButton.hide();
    }
  }

/**
 * Ensures that semantic highlighting is enabled
 */
async function checkSemanticHighlightingSetting(context: vscode.ExtensionContext, outputChannel: vscode.OutputChannel) {
    const config = vscode.workspace.getConfiguration('editor');
    const semanticHighlightingSetting = config.get('semanticHighlighting.enabled');

    outputChannel.appendLine(`[DEBUG] Current semanticHighlighting setting: ${semanticHighlightingSetting}`);

    if (semanticHighlightingSetting !== true) {
        const message = 'Semantic highlighting is disabled. Marathon syntax highlighting will be limited.';
        const enableButton = 'Enable Now';

        const selection = await vscode.window.showWarningMessage(message, enableButton);
        if (selection === enableButton) {
            await config.update('semanticHighlighting.enabled', true, vscode.ConfigurationTarget.Global);
            vscode.window.showInformationMessage('Semantic highlighting has been enabled.');
            outputChannel.appendLine("[INFO] Enabled semantic highlighting.");
        }
    }
}
