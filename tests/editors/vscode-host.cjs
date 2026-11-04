const vscode = require('vscode');
const fs = require('node:fs/promises');
const path = require('node:path');
const assert = require('node:assert/strict');
const delay = ms => new Promise(resolve => setTimeout(resolve, ms));

exports.run = async function() {
  const root = process.env.PRIMEX_EDITOR_HOST_TEST;
  const registry = path.join(root, 'data', 'editor-workspaces');
  const extension = vscode.extensions.getExtension('primex.smart-save-workspace-link');
  assert(extension); await extension.activate();
  async function waitForRoots(expected) {
    for (let i = 0; i < 100; i++) {
      let files = []; try { files = await fs.readdir(registry); } catch {}
      const records = [];
      for (const file of files.filter(name => name.endsWith('.json'))) {
        try { records.push(JSON.parse(await fs.readFile(path.join(registry, file), 'utf8'))); } catch {}
      }
      const actual = records.flatMap(record => record.folders).map(value => value.toLowerCase()).sort();
      if (JSON.stringify(actual) === JSON.stringify(expected.map(value => value.toLowerCase()).sort())) return records;
      await delay(100);
    }
    throw new Error('Workspace lease did not match open folders');
  }
  const first = path.join(root, 'workspace-one'), second = path.join(root, 'workspace-two'), third = path.join(root, 'workspace-three');
  await waitForRoots([first, second]);
  assert(vscode.workspace.updateWorkspaceFolders(1, 1, { uri: vscode.Uri.file(third) }));
  await waitForRoots([first, third]);
  assert(vscode.workspace.updateWorkspaceFolders(1, 1));
  await waitForRoots([first]);
  for (const folder of [first, second, third]) assert.deepEqual(await fs.readdir(folder), []);
  await fs.writeFile(path.join(root, 'result.json'), JSON.stringify({ passed: true, editorVersion: vscode.version,
    checks: ['activation', 'multiple unmarked roots', 'workspace switch', 'workspace removal', 'no project files written'] }, null, 2));
};
