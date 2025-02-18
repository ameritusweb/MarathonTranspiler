import * as vscode from 'vscode';
import * as cp from 'child_process';
import * as path from 'path';
import { LanguageClient, LanguageClientOptions, ServerOptions, TransportKind } from 'vscode-languageclient/node';

let client: LanguageClient;

export function activate(context: vscode.ExtensionContext) {
    const serverPath = context.asAbsolutePath(
        path.join('server', 'MarathonTranspiler.LSP.dll')
      );

      const outputChannel = vscode.window.createOutputChannel('Marathon LSP');
      outputChannel.appendLine(`Extension activated. Working directory: ${serverPath}`);
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
