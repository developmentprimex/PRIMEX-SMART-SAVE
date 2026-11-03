const all = (await chrome.storage.local.get("jobs")).jobs ?? {};
for (const [id, job] of Object.entries(all).slice(-30).reverse()) {
  const row = document.createElement("div"); row.className = "job";
  const label = document.createElement("div"); label.textContent = `Download ${id}: ${job.state}`; row.append(label);
  if (["needsAgent", "complete", "interrupted", "browserSafetyReview"].includes(job.state)) {
    const retry = document.createElement("button"); retry.textContent = "Retry delivery to agent";
    retry.addEventListener("click", async () => { retry.disabled = true; await chrome.runtime.sendMessage({ command: "retry", id }); location.reload(); }); row.append(retry);
  }
  document.getElementById("jobs").append(row);
}
if (!Object.keys(all).length) document.getElementById("jobs").textContent = "No guided downloads yet.";
document.getElementById("downloads").addEventListener("click", () => chrome.downloads.showDefaultFolder());
