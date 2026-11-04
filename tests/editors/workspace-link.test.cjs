const { test } = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs/promises');
const os = require('node:os');
const path = require('node:path');
const { WorkspacePublisher } = require('../../extensions/vscode/lease-writer.cjs');

test('workspace updates replace roots, preserve boundaries and clean up the lease', async () => {
  const directory = await fs.mkdtemp(path.join(os.tmpdir(), 'PrimEx-Editor-'));
  const publisher = new WorkspacePublisher(directory, { processId: 123, now: () => new Date('2026-11-04T12:00:00Z') });
  try {
    await publisher.publish(['C:\\Projects\\One', 'c:/projects/one', 'C:\\Projects\\Two', 'relative', 'C:\\', '\\\\server\\share']);
    let lease = JSON.parse(await fs.readFile(publisher.file, 'utf8'));
    assert.equal(lease.version, 1); assert.equal(lease.editor, 'VSCode'); assert.equal(lease.processId, 123);
    assert.deepEqual(lease.folders, ['c:\\projects\\one', 'C:\\Projects\\Two']);
    await Promise.all([publisher.publish(['C:\\Projects\\Old']), publisher.publish(['C:\\Projects\\New'])]);
    lease = JSON.parse(await fs.readFile(publisher.file, 'utf8'));
    assert.deepEqual(lease.folders, ['C:\\Projects\\New']);
    assert.deepEqual((await fs.readdir(directory)).map(name => path.extname(name)), ['.json']);
    await publisher.publish([]); assert.deepEqual(await fs.readdir(directory), []);
    await publisher.publish(['C:\\Projects\\Final']); await publisher.dispose();
    await publisher.publish(['C:\\Projects\\AfterClose']); assert.deepEqual(await fs.readdir(directory), []);
  } finally { await publisher.dispose(); await fs.rm(directory, { recursive: true, force: true }); }
});

test('multiple editor windows have independent leases and recover after a failed write', async () => {
  const directory = await fs.mkdtemp(path.join(os.tmpdir(), 'PrimEx-Editor-Windows-'));
  const first = new WorkspacePublisher(directory), second = new WorkspacePublisher(directory);
  try {
    await Promise.all([first.publish(['C:\\One']), second.publish(['C:\\Two'])]);
    assert.equal((await fs.readdir(directory)).length, 2);
    await first.dispose(); assert.equal((await fs.readdir(directory)).length, 1);
    await second.dispose(); await fs.rmdir(directory); await fs.writeFile(directory, 'blocks directory');
    const retry = new WorkspacePublisher(directory);
    await assert.rejects(retry.publish(['C:\\One']));
    await fs.rm(directory); await retry.publish(['C:\\One']);
    assert.equal((await fs.readdir(directory)).length, 1); await retry.dispose();
  } finally { await first.dispose(); await second.dispose(); await fs.rm(directory, { recursive: true, force: true }); }
});
