"""Run the VS Code link in an isolated actual extension host, never in the user's profile."""
from pathlib import Path
import json
import os
import subprocess
import sys

ROOT = Path(__file__).resolve().parents[2]
output = ROOT / "artifacts/verification" / (sys.argv[1] if len(sys.argv) > 1 else "vscode-host-0.2.11")
if output.exists():
    raise SystemExit("Use a fresh test output directory")
output.mkdir(parents=True)
for name in ("workspace-one", "workspace-two", "workspace-three", "profile/User", "extensions"):
    (output / name).mkdir(parents=True)
settings = {"primexSmartSave.dataDirectory": str(output / "data"), "update.mode": "none",
            "extensions.autoUpdate": False, "extensions.autoCheckUpdates": False, "telemetry.telemetryLevel": "off"}
(output / "profile/User/settings.json").write_text(json.dumps(settings))
workspace = output / "test.code-workspace"
workspace.write_text(json.dumps({"folders": [{"path": str(output / name)} for name in ("workspace-one", "workspace-two")]}))
environment = os.environ.copy()
environment["PRIMEX_EDITOR_HOST_TEST"] = str(output)
startup = subprocess.STARTUPINFO()
startup.dwFlags |= subprocess.STARTF_USESHOWWINDOW
startup.wShowWindow = 0
command = [str(Path(os.environ["LOCALAPPDATA"]) / "Programs/Microsoft VS Code/bin/code.cmd"),
           "--user-data-dir", str(output / "profile"), "--extensions-dir", str(output / "extensions"),
           "--extensionDevelopmentPath=" + str(ROOT / "extensions/vscode"),
           "--extensionTestsPath=" + str(ROOT / "tests/editors/vscode-host.cjs"),
           "--disable-workspace-trust", "--skip-welcome", "--skip-release-notes", "--disable-updates", "--disable-telemetry", "--new-window", "--wait", "--verbose", str(workspace)]
with (output / "host.log").open("w") as log:
    process = subprocess.Popen(command, env=environment, startupinfo=startup, stdout=log, stderr=subprocess.STDOUT)
    (output / "launcher-pid.txt").write_text(str(process.pid))
    try:
        code = process.wait(timeout=120)
    except subprocess.TimeoutExpired:
        process.terminate()
        raise
if code != 0 or not (output / "result.json").exists():
    print((output / "host.log").read_text())
    raise SystemExit(code or 1)
print((output / "result.json").read_text())
