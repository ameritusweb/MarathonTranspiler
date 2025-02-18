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
Object.defineProperty(exports, "__esModule", { value: true });
exports.activate = activate;
exports.deactivate = deactivate;
const vscode = __importStar(require("vscode"));
const path = __importStar(require("path"));
const node_1 = require("vscode-languageclient/node");
let client;
function activate(context) {
    const serverPath = context.asAbsolutePath(path.join('server', 'MarathonTranspiler.LSP.dll'));
    const outputChannel = vscode.window.createOutputChannel('Marathon LSP');
    outputChannel.appendLine(`Extension activated. Working directory: ${serverPath}`);
    context.subscriptions.push(outputChannel);
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
}
function deactivate() {
    return client ? client.stop() : undefined;
}
