# Browser workflow and limits

For ordinary downloads into Downloads/Desktop/Documents, the desktop's **Automatic
guidance…** setup now offers folder-based post-save suggestions without this
extension. It applies equally to files downloaded by Teams when they land in a
selected local folder. The extension workflow below remains a separate explicit
link-download integration. Disable folder monitoring when testing extension-only
background-download behavior.

The MV3 extension uses downloads, nativeMessaging, contextMenus, storage and
alarms permissions. It has no content scripts, host permissions, externally
connectable entry points, or web-page-to-native forwarding. The extension's
context-menu command on an HTTP(S) link is its explicit user action. It does not
intercept arbitrary normal browser downloads, embedded viewer Save operations,
POST/form-generated downloads, authenticated application export flows, or browser
page Save As. Incognito downloads are excluded.

1. Right-click a direct download link → **Download link with PrimEx Smart Save**.
2. The extension requests that link with the Downloads API. Filename determination
   checks agent readiness with a short deadline, without waiting for user input.
3. If ready, suggest a browser-relative path:
   `PrimEx Smart Save Staging/<random-operation-guid-without-dashes>/<filename>`.
   GUIDs on the JSON protocol use standard dashed UUID strings.
4. Persist job metadata in extension local storage before making the suggestion.
5. Only a complete download marked safe by the browser, with a known file size,
   may be delivered to the native host. Interrupted or safety-held downloads stay
   under browser control. The host/agent additionally check path containment,
   reparse ancestors, an exclusive open, and exact file length.
6. The agent journals the request before acknowledging it, then serializes dialogs.
   The final destination comes from desktop user input, never from a webpage or
   native command. Complete transfers verify SHA-256, preserve Zone.Identifier
   when present, publish a same-directory temporary file, and remove only a
   revalidated source. Chrome history may show the original staging path as moved.
7. Job state is recovered on browser startup and a periodic alarm. Duplicate
   delivery is harmless after completion. The popup shows status and allows
   retrying delivery to an unavailable agent. Failed or interrupted agent transfers
   require review and retry in the desktop queue.

If readiness times out or native messaging fails before staging, normal browser
filename selection continues. If the agent disappears after staging, the file
remains in the visible staging folder; the extension retries delivery later.
Cancelling the destination dialog cancels the placement operation and retains
the completed staging file. It does not undo the already completed download.
Use the desktop's Retry after review action to reconsider a cancelled placement.

The API does not provide a reliable universal manual-download intent flag. This
candidate deliberately requires its explicit command. It does not claim coverage
of background page downloads. It does not pause arbitrary downloads indefinitely.
Filename suggestions cannot choose an absolute server path, and network transfer
starts only after browser completion. Browser settings, another extension, or a
Save As choice can override the suggestion; if the actual file is outside the
expected staging path, native validation refuses to touch it. Use Move a file
manually in that case. One browser Downloads folder per agent configuration is
currently supported; Chrome and Edge must use that same configured folder.

Install/test: load `extensions/chrome-edge` unpacked in an isolated developer
browser profile or distribute through approved store/enterprise policy; record
its ID; register the host with the developer registration script or MSI; enable
the matching app configuration. Extension installation was not performed by the
development agent. No browser policies were changed.

Protocol: 4-byte little-endian length + UTF-8 JSON, at most 65,536 bytes, depth 8,
unknown fields rejected. Commands are `ping`, `status` (operation UUID), and
`complete` (UUID, completed staging path, completion flag, exact bytes, timestamp).
There is no destination, execute, delete, arbitrary-copy, or arbitrary-write command.

Sources: [Downloads API](https://developer.chrome.com/docs/extensions/reference/api/downloads),
[Chrome native messaging](https://developer.chrome.com/docs/extensions/develop/concepts/native-messaging),
[Edge native messaging](https://learn.microsoft.com/en-us/microsoft-edge/extensions-chromium/developer-guide/native-messaging).
