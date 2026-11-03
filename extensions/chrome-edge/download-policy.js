export const STAGING = "PrimEx Smart Save Staging";
export function leafName(path) {
  let name = String(path).split(/[\\/]/).pop() || "download";
  name = name.replace(/[<>:"/\\|?*\x00-\x1f]/g, "_").replace(/[. ]+$/g, "").slice(0, 180) || "download";
  if (/^(CON|PRN|AUX|NUL|COM[1-9]|LPT[1-9])(\.|$)/i.test(name)) name = "_" + name;
  if (name.startsWith(".")) name = "_" + name;
  return name;
}
export function stagePath(id, filename) {
  if (!/^[a-f0-9]{32}$/.test(id)) throw new Error("Invalid operation ID");
  return `${STAGING}/${id}/${leafName(filename)}`;
}
export function eligible(item, extensionId) {
  // DownloadItem does not expose a reliable general-purpose 'manual user Save' flag.
  // Only this extension's explicit context-menu command owns this workflow.
  return item.byExtensionId === extensionId && !item.incognito;
}
export function completeMessage(job, item) {
  if (item.state !== "complete" || item.danger !== "safe" || !item.filename || !Number.isSafeInteger(item.fileSize) || item.fileSize < 0) return null;
  return { version: 1, command: "complete", id: job.operationId, sourcePath: item.filename, complete: true, bytes: item.fileSize, createdUtc: job.createdUtc };
}
