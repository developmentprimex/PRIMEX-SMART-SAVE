const { test } = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs/promises');
const path = require('node:path');
const os = require('node:os');
const vm = require('node:vm');
const { createRequire } = require('node:module');

test('activation and workspace events register local roots without touching projects', async () => {
  const data = await fs.mkdtemp(path.join(os.tmpdir(), 'PrimEx-Extension-'));
  const file = path.resolve('extensions/vscode/extension.cjs');
  const realRequire = createRequire(file), hooks = {}, context = { subscriptions: [] };
  const status = { show() {}, hide() {}, dispose() {} };
  const api = {
    workspace: {
      workspaceFolders: [{ uri: { scheme: 'file', fsPath: 'C:\\Projects\\New' } }, { uri: { scheme: 'vscode-remote', fsPath: '/remote/project' } }],
      getConfiguration: () => ({ get: () => data }),
      onDidChangeWorkspaceFolders: fn => { hooks.workspace = fn; return { dispose() {} }; },
      onDidChangeConfiguration: fn => { hooks.configuration = fn; return { dispose() {} }; }
    },
    window: { createOutputChannel: () => ({ appendLine() {}, dispose() {} }), createStatusBarItem: () => status },
    commands: { registerCommand: (name, fn) => { hooks.command = fn; return { dispose() {} }; } }, StatusBarAlignment: { Right: 2 }
  };
  const exports = {};
  vm.runInNewContext(await fs.readFile(file, 'utf8'), { exports, require: name => name === 'vscode' ? api : realRequire(name),
    process, setInterval: fn => { hooks.heartbeat = fn; return { unref() {} }; }, clearInterval() {} }, { filename: file });
  async function records() {
    const directory = path.join(data, 'editor-workspaces');
    return Promise.all((await fs.readdir(directory)).filter(name => name.endsWith('.json')).map(async name => JSON.parse(await fs.readFile(path.join(directory, name), 'utf8'))));
  }
  try {
    await exports.activate(context);
    assert.deepEqual((await records())[0].folders, ['C:\\Projects\\New']);
    api.workspace.workspaceFolders = [{ uri: { scheme: 'file', fsPath: 'C:\\Projects\\Other' } }];
    await hooks.workspace(); assert.deepEqual((await records())[0].folders, ['C:\\Projects\\Other']);
    api.workspace.workspaceFolders = [];
    await hooks.command(); assert.deepEqual(await records(), []);
    api.workspace.workspaceFolders = [{ uri: { scheme: 'file', fsPath: 'C:\\Projects\\Final' } }];
    await hooks.workspace(); await exports.deactivate(); assert.deepEqual(await records(), []);
  } finally { await exports.deactivate(); await fs.rm(data, { recursive: true, force: true }); }
});
