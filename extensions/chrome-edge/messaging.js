export const HOST = "com.primex.smartsave";
export function nativeRequest(message, timeout = 10000) {
  return new Promise((resolve) => {
    let settled = false;
    const finish = (reply) => { if (!settled) { settled = true; clearTimeout(timer); resolve(reply); } };
    const timer = setTimeout(() => finish({ ok: false, code: "AgentUnavailable" }), timeout);
    try {
      chrome.runtime.sendNativeMessage(HOST, message, (reply) => {
        const error = chrome.runtime.lastError;
        finish(error ? { ok: false, code: "AgentUnavailable" } : reply ?? { ok: false, code: "InvalidResponse" });
      });
    } catch { finish({ ok: false, code: "AgentUnavailable" }); }
  });
}
