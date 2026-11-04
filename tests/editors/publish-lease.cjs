const { WorkspacePublisher } = require('../../extensions/vscode/lease-writer.cjs');
const fs = require('node:fs/promises');
const path = require('node:path');
(async () => {
  const directory = process.argv[2], folder = process.argv[3];
  const publisher = new WorkspacePublisher(directory);
  try {
    await publisher.publish([folder]);
    for (let i = 0; i < 100; i++) {
      try { await fs.access(path.join(directory, 'stop')); break; } catch {}
      await new Promise(resolve => setTimeout(resolve, 50));
    }
  } finally { await publisher.dispose(); }
})().catch(error => { console.error(error); process.exitCode = 1; });
