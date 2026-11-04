const fs = require('node:fs/promises');
const path = require('node:path');
const { randomUUID } = require('node:crypto');

class WorkspacePublisher {
  constructor(directory, { editor = 'VSCode', processId = process.pid, now = () => new Date() } = {}) {
    this.directory = directory; this.editor = editor; this.processId = processId; this.now = now;
    this.file = path.join(directory, `${editor.toLowerCase()}-${randomUUID()}.json`);
    this.pending = Promise.resolve(); this.disposed = false;
  }
  publish(folders) {
    if (this.disposed) return this.pending;
    const roots = [...new Map(folders.filter(folder => typeof folder === 'string' && path.win32.isAbsolute(folder) &&
      /^[a-z]:[\\/]/i.test(folder) && path.win32.parse(folder).root.toLowerCase() !== path.win32.normalize(folder).toLowerCase())
      .map(folder => path.win32.normalize(folder).replace(/[\\/]+$/, '')).map(folder => [folder.toLowerCase(), folder])).values()].slice(0, 128);
    const write = async () => {
      if (!roots.length) { await fs.rm(this.file, { force: true }); return; }
      await fs.mkdir(this.directory, { recursive: true });
      const temporary = `${this.file}.${randomUUID()}.tmp`;
      try {
        await fs.writeFile(temporary, JSON.stringify({ version: 1, editor: this.editor, processId: this.processId,
          updatedUtc: this.now().toISOString(), folders: roots }), { flag: 'wx' });
        await fs.rename(temporary, this.file);
      } finally { await fs.rm(temporary, { force: true }); }
    };
    this.pending = this.pending.catch(() => {}).then(write);
    return this.pending;
  }
  async dispose() {
    this.disposed = true;
    await this.pending.catch(() => {});
    await fs.rm(this.file, { force: true });
  }
}
module.exports = { WorkspacePublisher };
