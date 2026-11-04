const vscode = require('vscode');
const path = require('node:path');
const os = require('node:os');
const { WorkspacePublisher } = require('./lease-writer.cjs');

let publisher, timer, output, status;
let updating = Promise.resolve(), stopping = false;
function dataDirectory() {
  const configured = vscode.workspace.getConfiguration('primexSmartSave').get('dataDirectory', '').trim();
  if (configured && !path.isAbsolute(configured)) throw new Error('DataDirectoryMustBeAbsolute');
  return configured || path.join(process.env.LOCALAPPDATA || path.join(os.homedir(), 'AppData', 'Local'), 'PrimEx', 'SmartSave');
}
function roots() { return (vscode.workspace.workspaceFolders || []).filter(folder => folder.uri.scheme === 'file').map(folder => folder.uri.fsPath); }
function update() {
  if (stopping) return updating;
  updating = updating.catch(() => {}).then(updateCore);
  return updating;
}
async function updateCore() {
  if (process.platform !== 'win32') return;
  try {
    const directory = path.join(dataDirectory(), 'editor-workspaces');
    if (!publisher || publisher.directory !== directory) {
      if (publisher) await publisher.dispose();
      publisher = new WorkspacePublisher(directory);
    }
    const folders = roots();
    await publisher.publish(folders);
    status.text = '$(shield) Smart Save';
    status.tooltip = `Linked ${folders.length} local workspace folder(s). Smart Save 0.2.11+ automatically keeps files in open workspaces. No per-project exclusion is needed.`;
    if (folders.length) status.show(); else status.hide();
  } catch (error) {
    status.hide(); output.appendLine(`Workspace link could not be updated (${error.code || error.message}).`);
  }
}
exports.activate = async function(context) {
  output = vscode.window.createOutputChannel('PrimEx Smart Save');
  status = vscode.window.createStatusBarItem(vscode.StatusBarAlignment.Right, 90);
  status.command = 'primexSmartSave.refreshWorkspaces';
  context.subscriptions.push(output, status,
    vscode.workspace.onDidChangeWorkspaceFolders(update),
    vscode.workspace.onDidChangeConfiguration(event => { if (event.affectsConfiguration('primexSmartSave.dataDirectory')) void update(); }),
    vscode.commands.registerCommand('primexSmartSave.refreshWorkspaces', update));
  await update();
  timer = setInterval(() => void update(), 30000); timer.unref();
  context.subscriptions.push({ dispose() { clearInterval(timer); } });
};
exports.deactivate = async function() { stopping = true; clearInterval(timer); await updating.catch(() => {}); if (publisher) await publisher.dispose(); };
