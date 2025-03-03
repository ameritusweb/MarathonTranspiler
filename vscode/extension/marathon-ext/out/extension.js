"use strict";
var __createBinding = (this && this.__createBinding) || (Object.create ? (function(o, m, k, k2) {
    if (k2 === undefined) k2 = k;
    var desc = Object.getOwnPropertyDescriptor(m, k);
    if (!desc || ("get" in desc ? !m.__esModule : desc.writable || desc.configurable)) {
      desc = { enumerable: true, get: function() { return m[k]; } };
    }
    Object.defineProperty(o, k2, desc);
}) : (function(o, m, k, k2) {
    if (k2 === undefined) k2 = k;
    o[k2] = m[k];
}));
var __setModuleDefault = (this && this.__setModuleDefault) || (Object.create ? (function(o, v) {
    Object.defineProperty(o, "default", { enumerable: true, value: v });
}) : function(o, v) {
    o["default"] = v;
});
var __importStar = (this && this.__importStar) || (function () {
    var ownKeys = function(o) {
        ownKeys = Object.getOwnPropertyNames || function (o) {
            var ar = [];
            for (var k in o) if (Object.prototype.hasOwnProperty.call(o, k)) ar[ar.length] = k;
            return ar;
        };
        return ownKeys(o);
    };
    return function (mod) {
        if (mod && mod.__esModule) return mod;
        var result = {};
        if (mod != null) for (var k = ownKeys(mod), i = 0; i < k.length; i++) if (k[i] !== "default") __createBinding(result, mod, k[i]);
        __setModuleDefault(result, mod);
        return result;
    };
})();
var __awaiter = (this && this.__awaiter) || function (thisArg, _arguments, P, generator) {
    function adopt(value) { return value instanceof P ? value : new P(function (resolve) { resolve(value); }); }
    return new (P || (P = Promise))(function (resolve, reject) {
        function fulfilled(value) { try { step(generator.next(value)); } catch (e) { reject(e); } }
        function rejected(value) { try { step(generator["throw"](value)); } catch (e) { reject(e); } }
        function step(result) { result.done ? resolve(result.value) : adopt(result.value).then(fulfilled, rejected); }
        step((generator = generator.apply(thisArg, _arguments || [])).next());
    });
};
Object.defineProperty(exports, "__esModule", { value: true });
exports.activate = activate;
exports.deactivate = deactivate;
const vscode = __importStar(require("vscode"));
const path = __importStar(require("path"));
const node_1 = require("vscode-languageclient/node");
let client;
let statusBarItem;
let previewButton;
function activate(context) {
    return __awaiter(this, void 0, void 0, function* () {
        const serverPath = context.asAbsolutePath(path.join('server', 'MarathonTranspiler.LSP.dll'));
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
        context.subscriptions.push(vscode.commands.registerCommand('marathon.forceCompile', () => __awaiter(this, void 0, void 0, function* () {
            const editor = vscode.window.activeTextEditor;
            if (editor && editor.document.languageId === 'mrt') {
                statusBarItem.text = "$(sync~spin) Compiling...";
                statusBarItem.show();
                // Send command to LSP
                try {
                    yield client.sendRequest('workspace/executeCommand', {
                        command: 'marathon.forceCompile',
                        arguments: [editor.document.uri.toString()]
                    });
                    statusBarItem.text = "$(check) Ready";
                }
                catch (error) {
                    vscode.window.showErrorMessage(`Error during compilation: ${error}`);
                    statusBarItem.text = "$(error) Compilation Failed";
                }
            }
        })));
        const outputChannel = vscode.window.createOutputChannel('Marathon LSP');
        outputChannel.appendLine(`Extension activated. Working directory: ${serverPath}`);
        checkActiveEditor(vscode.window.activeTextEditor);
        // Show status bar when active editor changes
        context.subscriptions.push(vscode.window.onDidChangeActiveTextEditor(editor => {
            checkActiveEditor(editor);
        }));
        // Ensure semantic highlighting is enabled
        yield checkSemanticHighlightingSetting(context, outputChannel);
        // Log available languages
        vscode.languages.getLanguages().then(langs => {
            outputChannel.appendLine(`Available Languages: ${langs}`);
        });
        context.subscriptions.push(outputChannel);
        const showTranspiledCommand = vscode.commands.registerCommand('marathon.showTranspiled', () => __awaiter(this, void 0, void 0, function* () {
            const editor = vscode.window.activeTextEditor;
            if (editor && editor.document.languageId === 'mrt') {
                try {
                    statusBarItem.text = "$(sync~spin) Generating preview...";
                    statusBarItem.show();
                    // Request transpiled code from the LSP
                    const result = yield client.sendRequest('marathon/getTranspiledCode', {
                        uri: editor.document.uri.toString()
                    });
                    if (result && result.code) {
                        // Create a new untitled document with the transpiled code
                        const languageId = getLanguageIdFromTarget(result.target);
                        const doc = yield vscode.workspace.openTextDocument({
                            content: result.code,
                            language: languageId
                        });
                        // Show the document in a new editor group (side by side)
                        yield vscode.window.showTextDocument(doc, { viewColumn: vscode.ViewColumn.Beside });
                        statusBarItem.text = "$(check) Ready";
                    }
                    else {
                        vscode.window.showErrorMessage('No transpiled code available');
                        statusBarItem.text = "$(error) Preview Failed";
                    }
                }
                catch (error) {
                    vscode.window.showErrorMessage(`Error generating preview: ${error}`);
                    statusBarItem.text = "$(error) Preview Failed";
                }
            }
        }));
        context.subscriptions.push(showTranspiledCommand);
        const serverOptions = {
            run: { command: "dotnet", args: [serverPath], transport: node_1.TransportKind.stdio },
            debug: { command: "dotnet", args: [serverPath], transport: node_1.TransportKind.stdio }
        };
        const clientOptions = {
            documentSelector: [{ scheme: "file", language: "mrt" }],
            synchronize: {
                configurationSection: "mrtLanguageServer"
            }
        };
        client = new node_1.LanguageClient("mrtLanguageServer", "MRT Language Server", serverOptions, clientOptions);
        client.start();
    });
}
function getLanguageIdFromTarget(target) {
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
function deactivate() {
    return client ? client.stop() : undefined;
}
function checkActiveEditor(editor) {
    if (editor && editor.document.languageId === 'mrt') {
        statusBarItem.text = "$(check) Ready";
        statusBarItem.show();
        previewButton.show();
    }
    else {
        statusBarItem.hide();
        previewButton.hide();
    }
}
/**
 * Ensures that semantic highlighting is enabled
 */
function checkSemanticHighlightingSetting(context, outputChannel) {
    return __awaiter(this, void 0, void 0, function* () {
        const config = vscode.workspace.getConfiguration('editor');
        const semanticHighlightingSetting = config.get('semanticHighlighting.enabled');
        outputChannel.appendLine(`[DEBUG] Current semanticHighlighting setting: ${semanticHighlightingSetting}`);
        if (semanticHighlightingSetting !== true) {
            const message = 'Semantic highlighting is disabled. Marathon syntax highlighting will be limited.';
            const enableButton = 'Enable Now';
            const selection = yield vscode.window.showWarningMessage(message, enableButton);
            if (selection === enableButton) {
                yield config.update('semanticHighlighting.enabled', true, vscode.ConfigurationTarget.Global);
                vscode.window.showInformationMessage('Semantic highlighting has been enabled.');
                outputChannel.appendLine("[INFO] Enabled semantic highlighting.");
            }
        }
    });
}
