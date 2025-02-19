import * as vscode from 'vscode';
import * as path from 'path';
import { LanguageClient, LanguageClientOptions, ServerOptions, TransportKind } from 'vscode-languageclient/node';

let client: LanguageClient;

export async function activate(context: vscode.ExtensionContext) {
    const serverPath = context.asAbsolutePath(
        path.join('server', 'MarathonTranspiler.LSP.dll')
    );

    const outputChannel = vscode.window.createOutputChannel('Marathon LSP');
    outputChannel.appendLine(`Extension activated. Working directory: ${serverPath}`);

    // Ensure semantic highlighting is enabled
    await checkSemanticHighlightingSetting(context, outputChannel);

    // Log available languages
    vscode.languages.getLanguages().then(langs => {
        outputChannel.appendLine(`Available Languages: ${langs}`);
    });

    context.subscriptions.push(outputChannel);

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

export function deactivate(): Thenable<void> | undefined {
    return client ? client.stop() : undefined;
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
