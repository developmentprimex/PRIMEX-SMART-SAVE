import test from "node:test";
import assert from "node:assert/strict";
const event = () => ({ listeners: [], addListener(fn) { this.listeners.push(fn); } });
let importId = 0;
async function setup(saved = {}, available = true) {
  const storage = structuredClone(saved), sent = [], suggested = [], files = new Map();
  globalThis.chrome = {
    runtime: { id: "ours", onInstalled: event(), onStartup: event(), onMessage: event(), getURL: (p) => `chrome-extension://ours/${p}`, sendNativeMessage(_host, message, callback) {
      sent.push(message); callback({ ok: available, code: available ? "Ready" : "AgentUnavailable", state: "Pending" });
    } },
    storage: { local: { async get(key) { return { [key]: structuredClone(storage[key]) }; }, async set(values) { Object.assign(storage, structuredClone(values)); } } },
    downloads: { onDeterminingFilename: event(), onChanged: event(), onErased: event(), async search({id}) { return files.has(id) ? [files.get(id)] : []; }, async download() {} },
    action: { async setBadgeText() {} }, contextMenus: { onClicked: event(), removeAll(fn) { fn(); }, create() {} },
    alarms: { onAlarm: event(), create() {} }
  };
  await import(`../../extensions/chrome-edge/background.js?test=${++importId}`);
  const api = chrome;
  async function determine(id, extra = {}) {
    const item = { id, filename: "report.pdf", byExtensionId: "ours", incognito: false, ...extra };
    await new Promise((resolve) => api.downloads.onDeterminingFilename.listeners[0](item, (value) => { suggested.push(value); resolve(); }));
    return suggested.at(-1);
  }
  async function until(predicate) { for (let i = 0; i < 500; i++) { if (predicate()) return; await new Promise(r => setTimeout(r, 2)); } assert.fail("Worker did not settle"); }
  return { api, storage, sent, files, determine, until };
}
test("concurrent guided downloads have independent staging and completion", async () => {
  const s = await setup(); const [a, b] = await Promise.all([s.determine(1), s.determine(2)]);
  assert.notEqual(a.filename, b.filename); assert.equal(Object.keys(s.storage.jobs).length, 2);
  for (const job of Object.values(s.storage.jobs)) assert.match(job.operationId, /^[a-f0-9]{8}-[a-f0-9]{4}-[a-f0-9]{4}-[a-f0-9]{4}-[a-f0-9]{12}$/);
  assert.equal(s.sent.filter(x => x.command === "complete").length, 0);
  for (const id of [1, 2]) {
    s.files.set(id, { id, state: "complete", danger: "safe", filename: `C:/Downloads/${s.storage.jobs[id].relativePath}`, fileSize: 10 });
    s.api.downloads.onChanged.listeners[0]({ id, state: { current: "complete" } });
  }
  await s.until(() => Object.values(s.storage.jobs).every(x => x.state === "queued"));
  assert.equal(s.sent.filter(x => x.command === "complete").length, 2);
  s.api.downloads.onChanged.listeners[0]({ id: 1, state: { current: "complete" } });
  await s.until(() => s.sent.some(x => x.command === "status"));
  assert.equal(s.sent.filter(x => x.command === "complete").length, 2);
});
test("agent unavailable falls back without staging", async () => {
  const s = await setup({}, false); assert.equal(await s.determine(3), undefined); assert.equal(s.storage.jobs, undefined);
});
test("ordinary browser downloads are untouched", async () => {
  const s = await setup(); assert.equal(await s.determine(3, { byExtensionId: undefined }), undefined); assert.equal(s.sent.length, 0);
});
test("worker restart recovers completed staged download", async () => {
  const first = await setup(); await first.determine(4);
  const second = await setup(first.storage);
  second.files.set(4, { id: 4, state: "complete", danger: "safe", filename: `C:/Downloads/${second.storage.jobs[4].relativePath}`, fileSize: 5 });
  second.api.runtime.onStartup.listeners[0](); await second.until(() => second.storage.jobs[4].state === "queued");
  assert.equal(second.sent.filter(x => x.command === "complete").length, 1);
});
test("interrupted download retained without a transfer", async () => {
  const s = await setup(); await s.determine(5); s.files.set(5, { id: 5, state: "interrupted" });
  s.api.downloads.onChanged.listeners[0]({ id: 5, state: { current: "interrupted" } });
  await s.until(() => s.storage.jobs[5].state === "interrupted"); assert.equal(s.sent.filter(x => x.command === "complete").length, 0);
});
