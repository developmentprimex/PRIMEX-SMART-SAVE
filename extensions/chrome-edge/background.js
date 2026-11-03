import { nativeRequest } from "./messaging.js";
import { stagePath, eligible, completeMessage } from "./download-policy.js";

const MENU = "primex-download-link";
// Serialize storage mutations, including filename callbacks for concurrent downloads.
let serial = Promise.resolve();
function locked(action) { const task = serial.then(action); serial = task.catch(() => {}); return task; }
async function jobs() { return (await chrome.storage.local.get("jobs")).jobs ?? {}; }
async function saveJob(id, job) { const all = await jobs(); all[id] = job; await chrome.storage.local.set({ jobs: all }); }
async function badge(text) { await chrome.action.setBadgeText({ text }); }
chrome.runtime.onInstalled.addListener(() => {
  chrome.contextMenus.removeAll(() => chrome.contextMenus.create({ id: MENU, title: "Download link with PrimEx Smart Save", contexts: ["link"], targetUrlPatterns: ["http://*/*", "https://*/*"] }));
  chrome.alarms.create("recover", { periodInMinutes: 1 });
});
chrome.contextMenus.onClicked.addListener((info) => {
  if (info.menuItemId !== MENU || !/^https?:\/\//i.test(info.linkUrl ?? "")) return;
  chrome.downloads.download({ url: info.linkUrl, saveAs: false, conflictAction: "uniquify" }).catch(() => badge("!"));
});
chrome.downloads.onDeterminingFilename.addListener((item, suggest) => {
  if (!eligible(item, chrome.runtime.id)) { suggest(); return false; }
  let suggested = false;
  const finish = (value) => { if (!suggested) { suggested = true; clearTimeout(deadline); suggest(value); } };
  // No desktop dialog is awaited here; allow normal browser fallback quickly.
  const deadline = setTimeout(() => finish(), 2000);
  locked(async () => {
    const health = await nativeRequest({ version: 1, command: "ping" }, 1000);
    if (!health.ok || suggested) { finish(); await badge("!"); return; }
    const operationId = crypto.randomUUID();
    const filename = stagePath(operationId.replaceAll("-", ""), item.filename);
    const job = { operationId, createdUtc: new Date().toISOString(), state: "downloading", relativePath: filename };
    await saveJob(item.id, job);
    if (suggested) { await saveJob(item.id, { ...job, state: "browserFallback" }); return; }
    finish({ filename, conflictAction: "uniquify" });
  }).catch(() => { finish(); badge("!"); });
  return true;
});
async function reconcile(id, retry = false) {
  const all = await jobs(), job = all[id];
  if (!job || ["Completed", "Cancelled", "NeedsReview", "browserFallback", "missing"].includes(job.state) && !retry) return;
  if (job.state === "queued") {
    const response = await nativeRequest({ version: 1, command: "status", id: job.operationId });
    if (response.ok && response.state !== "Unknown") await saveJob(id, { ...job, state: response.state === "Pending" || response.state === "Transferring" ? "queued" : response.state });
    return;
  }
  const items = await chrome.downloads.search({ id: Number(id) });
  const item = items[0];
  if (!item) { await saveJob(id, { ...job, state: "missing" }); return; }
  if (item.state !== "complete") { await saveJob(id, { ...job, state: item.state }); return; }
  const message = completeMessage(job, item);
  if (!message) { await saveJob(id, { ...job, state: "browserSafetyReview" }); await badge("!"); return; }
  const response = await nativeRequest(message);
  await saveJob(id, { ...job, state: response.ok ? "queued" : "needsAgent", code: response.code });
  await badge(response.ok ? "" : "!");
}
chrome.downloads.onChanged.addListener((delta) => {
  if (delta.state || delta.danger) locked(() => reconcile(delta.id)).catch(() => badge("!"));
});
chrome.downloads.onErased.addListener((id) => locked(async () => { const job = (await jobs())[id]; if (job) await saveJob(id, { ...job, state: "missing" }); }).catch(() => badge("!")));
async function recover() { for (const id of Object.keys(await jobs())) await reconcile(id); }
chrome.runtime.onStartup.addListener(() => locked(recover).catch(() => badge("!")));
chrome.alarms.onAlarm.addListener((alarm) => { if (alarm.name === "recover") locked(recover).catch(() => badge("!")); });
chrome.runtime.onMessage.addListener((message, sender, respond) => {
  if (sender.id !== chrome.runtime.id || sender.tab || sender.url !== chrome.runtime.getURL("popup.html")) return false;
  if (message.command === "retry" && /^\d+$/.test(String(message.id))) {
    locked(() => reconcile(message.id, true)).then(() => respond({ ok: true }), () => respond({ ok: false })); return true;
  }
  return false;
});
