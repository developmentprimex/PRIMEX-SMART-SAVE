import test from "node:test";
import assert from "node:assert/strict";
import { leafName, stagePath, eligible, completeMessage } from "../../extensions/chrome-edge/download-policy.js";
test("safe filenames and browser-relative staging", () => {
  const id = "a".repeat(32); const path = stagePath(id, "C:\\fake\\report.pdf");
  assert.equal(path, `PrimEx Smart Save Staging/${id}/report.pdf`);
  assert.equal(leafName("CON.txt"), "_CON.txt"); assert.equal(leafName("../evil<>.docx"), "evil__.docx");
  assert.throws(() => stagePath("../escape", "file"));
});
test("only explicit extension-owned downloads eligible", () => {
  assert.equal(eligible({ byExtensionId: "ours", incognito: false }, "ours"), true);
  assert.equal(eligible({ byExtensionId: "other" }, "ours"), false);
  assert.equal(eligible({ byExtensionId: "ours", incognito: true }, "ours"), false);
  assert.equal(eligible({}, "ours"), false);
});
test("interrupted and unsafe downloads never sent as complete", () => {
  const job = { operationId: "a".repeat(32), createdUtc: new Date().toISOString() };
  for (const state of ["interrupted", "in_progress"]) assert.equal(completeMessage(job, { state }), null);
  assert.equal(completeMessage(job, { state: "complete", danger: "dangerous", filename: "x", fileSize: 1 }), null);
  assert.equal(completeMessage(job, { state: "complete", danger: "safe", filename: "x", fileSize: -1 }), null);
  assert.equal(completeMessage(job, { state: "complete", danger: "safe", filename: "x", fileSize: 10 }).bytes, 10);
});
