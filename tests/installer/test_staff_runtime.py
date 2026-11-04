"""Launch only disposable self-contained agent processes; no install or registry writes."""
from pathlib import Path
import json
import os
import subprocess
import sys
import tempfile
import time
import unittest
import ctypes
from ctypes import wintypes

EXE = Path(sys.argv.pop(1)).resolve() if len(sys.argv) > 1 and sys.argv[1].endswith(".exe") else None

@unittest.skipUnless(EXE and EXE.exists() and sys.platform == "win32", "Supply a published staff executable")
class StaffRuntimeTests(unittest.TestCase):
    def launch(self, data, *arguments):
        environment = os.environ.copy()
        environment["DOTNET_ROOT"] = str(data / "no-global-runtime")
        environment["DOTNET_MULTILEVEL_LOOKUP"] = "0"
        startup = subprocess.STARTUPINFO()
        startup.dwFlags |= subprocess.STARTF_USESHOWWINDOW
        startup.wShowWindow = 0
        return subprocess.Popen([str(EXE), "--data-dir", str(data), "--background", *arguments], env=environment, startupinfo=startup)

    def wait_for_event(self, process, data, event):
        path = data / "logs" / "events.jsonl"
        for _ in range(150):
            if path.exists() and event in path.read_text(encoding="utf-8"):
                return path.read_text(encoding="utf-8")
            self.assertIsNone(process.poll(), "Agent exited before startup completed")
            time.sleep(0.1)
        self.fail(f"Missing agent event: {event}")

    def stop(self, process):
        if process.poll() is None:
            process.terminate()
        process.wait(timeout=10)

    def write_settings(self, data, completed=True, startup=True):
        options = json.loads((EXE.parent / "appsettings.example.json").read_text(encoding="utf-8"))
        options["GuidanceSetupCompleted"] = completed
        options["StartWithWindows"] = startup
        (data / "appsettings.json").write_text(json.dumps(options), encoding="utf-8")

    def test_self_contained_payload_has_local_runtime(self):
        options = json.loads(EXE.with_suffix(".runtimeconfig.json").read_text())["runtimeOptions"]
        self.assertIn("includedFrameworks", options)
        self.assertNotIn("frameworks", options)
        for name in ("coreclr.dll", "hostfxr.dll", "hostpolicy.dll", "installed.mode"):
            self.assertTrue((EXE.parent / name).exists(), name)

    def test_first_signin_requests_setup_and_keeps_monitoring_disabled(self):
        with tempfile.TemporaryDirectory(prefix="PrimEx-Staff-FirstRun-") as temporary:
            data = Path(temporary)
            process = self.launch(data, "--startup")
            try:
                self.wait_for_event(process, data, "FirstRunSetupRequested")
                options = json.loads((data / "appsettings.json").read_text())
                self.assertFalse(options["Monitoring"]["Enabled"])
                self.assertEqual([], options["CompanyServer"]["ApprovedRoots"])
                self.assertFalse(options.get("GuidanceSetupCompleted", False))
                self.assertEqual(["*"], options["Monitoring"]["Extensions"])
                self.assertTrue(options["Monitoring"]["IgnoreDevelopmentProjects"])
                self.assertEqual([], options["Monitoring"]["ExcludedFolders"])
                self.assertEqual([], list((data / "operations").glob("*.json")))
            finally:
                self.stop(process)

    def test_later_signin_keeps_setup_closed(self):
        with tempfile.TemporaryDirectory(prefix="PrimEx-Staff-Signin-") as temporary:
            data = Path(temporary)
            self.write_settings(data)
            process = self.launch(data, "--startup")
            try:
                self.wait_for_event(process, data, "ApplicationStarted")
                time.sleep(0.5)
                self.assertNotIn("FirstRunSetupRequested", (data / "logs" / "events.jsonl").read_text())
                self.assertIsNone(process.poll())
            finally:
                self.stop(process)

    def test_startup_opt_out_exits_without_starting_agent(self):
        with tempfile.TemporaryDirectory(prefix="PrimEx-Staff-OptOut-") as temporary:
            data = Path(temporary)
            self.write_settings(data, startup=False)
            process = self.launch(data, "--startup")
            try:
                self.assertEqual(0, process.wait(timeout=10))
                self.assertFalse((data / "logs").exists())
            finally:
                self.stop(process)

    def test_manual_open_still_works_after_startup_opt_out(self):
        with tempfile.TemporaryDirectory(prefix="PrimEx-Staff-Manual-") as temporary:
            data = Path(temporary)
            self.write_settings(data, startup=False)
            process = self.launch(data)
            try:
                self.wait_for_event(process, data, "ApplicationStarted")
            finally:
                self.stop(process)

    def test_previous_default_config_automatically_detects_new_images(self):
        with tempfile.TemporaryDirectory(prefix="PrimEx-Staff-FileTypes-") as temporary:
            data = Path(temporary)
            watched = data / "downloads"
            watched.mkdir()
            self.write_settings(data)
            settings = data / "appsettings.json"
            options = json.loads(settings.read_text())
            options["Monitoring"].update(Enabled=True, Folders=[str(watched)], QuietSeconds=1, Extensions=[".pdf", ".docx", ".xlsx", ".pptx", ".doc", ".xls", ".zip"])
            original = json.dumps(options)
            settings.write_text(original)
            process = self.launch(data, "--startup")
            try:
                self.wait_for_event(process, data, "MonitoringStarted")
                (watched / "image.png.crdownload").write_bytes(b"unfinished")
                (watched / "image.png").write_bytes(b"synthetic completed image")
                (watched / "image.JPG").write_bytes(b"synthetic completed image")
                journal = data / "operations"
                for _ in range(100):
                    self.assertIsNone(process.poll())
                    if len(list(journal.glob("*.json"))) == 2:
                        break
                    time.sleep(0.1)
                requests = [json.loads(path.read_text()) for path in journal.glob("*.json")]
                self.assertEqual({"image.png", "image.JPG"}, {item["Request"]["DisplayName"] for item in requests})
                self.assertTrue(all(item["State"] == 0 for item in requests))
                self.assertEqual(original, settings.read_text())
                self.assertTrue((watched / "image.png.crdownload").exists())
            finally:
                self.stop(process)

    def test_coding_projects_and_explicit_exclusions_never_queue_requests(self):
        with tempfile.TemporaryDirectory(prefix="PrimEx-Staff-Projects-") as temporary:
            data = Path(temporary)
            watched = data / "downloads"
            project, excluded = watched / "coding-project", watched / "unmarked-project"
            (project / ".git").mkdir(parents=True)
            excluded.mkdir()
            self.write_settings(data)
            settings = data / "appsettings.json"
            options = json.loads(settings.read_text())
            options["Monitoring"].update(Enabled=True, Folders=[str(watched)], IncludeSubdirectories=True, QuietSeconds=1, ExcludedFolders=[str(excluded)])
            settings.write_text(json.dumps(options))
            process = self.launch(data)
            try:
                self.wait_for_event(process, data, "MonitoringStarted")
                for folder in (project, excluded):
                    for name in ("README.md", "settings.json", "asset.png", "document.pdf"):
                        (folder / name).write_text("synthetic project asset")
                (watched / "normal.pdf").write_text("ordinary document")
                journal = data / "operations"
                for _ in range(100):
                    self.assertIsNone(process.poll())
                    if list(journal.glob("*.json")):
                        break
                    time.sleep(0.1)
                time.sleep(1.5)
                requests = [json.loads(path.read_text()) for path in journal.glob("*.json")]
                self.assertEqual(["normal.pdf"], [item["Request"]["DisplayName"] for item in requests])
                self.assertTrue((project / "README.md").exists() and (excluded / "README.md").exists())
            finally:
                self.stop(process)

    def test_background_save_shows_only_prompt_and_never_the_main_queue(self):
        user32 = ctypes.WinDLL("user32", use_last_error=True)
        callback_type = ctypes.WINFUNCTYPE(wintypes.BOOL, wintypes.HWND, wintypes.LPARAM)
        user32.EnumWindows.argtypes = [callback_type, wintypes.LPARAM]
        user32.GetWindowThreadProcessId.argtypes = [wintypes.HWND, ctypes.POINTER(wintypes.DWORD)]
        user32.IsWindowVisible.argtypes = [wintypes.HWND]
        user32.GetWindowTextW.argtypes = [wintypes.HWND, wintypes.LPWSTR, ctypes.c_int]
        user32.PostMessageW.argtypes = [wintypes.HWND, wintypes.UINT, wintypes.WPARAM, wintypes.LPARAM]
        def visible_windows(pid):
            windows = []
            @callback_type
            def collect(hwnd, _):
                owner_pid = wintypes.DWORD()
                user32.GetWindowThreadProcessId(hwnd, ctypes.byref(owner_pid))
                if owner_pid.value == pid and user32.IsWindowVisible(hwnd):
                    title = ctypes.create_unicode_buffer(512)
                    user32.GetWindowTextW(hwnd, title, 512)
                    windows.append((hwnd, title.value))
                return True
            user32.EnumWindows(collect, 0)
            return windows
        with tempfile.TemporaryDirectory(prefix="PrimEx-Staff-Standalone-") as temporary:
            data = Path(temporary)
            watched = data / "downloads"
            watched.mkdir()
            self.write_settings(data)
            settings = data / "appsettings.json"
            options = json.loads(settings.read_text())
            options["Monitoring"].update(Enabled=True, Folders=[str(watched)], QuietSeconds=1)
            settings.write_text(json.dumps(options))
            process = self.launch(data)
            try:
                self.wait_for_event(process, data, "MonitoringStarted")
                self.assertEqual([], visible_windows(process.pid))
                document = watched / "normal.pdf"
                document.write_text("synthetic ordinary download")
                prompt = None
                for _ in range(100):
                    windows = visible_windows(process.pid)
                    self.assertFalse(any("Document Storage Manager" in title for _, title in windows))
                    prompt = next((hwnd for hwnd, title in windows if title == "Downloads or Files PX"), None)
                    if prompt:
                        break
                    self.assertIsNone(process.poll())
                    time.sleep(0.1)
                self.assertIsNotNone(prompt)
                user32.PostMessageW(prompt, 0x0010, 0, 0)  # WM_CLOSE: dismiss this isolated test prompt.
                for _ in range(60):
                    requests = [json.loads(path.read_text()) for path in (data / "operations").glob("*.json")]
                    if requests and requests[0]["State"] == 4:
                        break
                    time.sleep(0.1)
                self.assertEqual(4, requests[0]["State"])
                self.assertEqual([], visible_windows(process.pid))
                self.assertTrue(document.exists())
            finally:
                self.stop(process)

    def test_editor_link_suppresses_an_empty_project_and_close_restores_normal_guidance(self):
        from datetime import datetime, timezone
        with tempfile.TemporaryDirectory(prefix="PrimEx-Staff-Editor-Link-") as temporary:
            data = Path(temporary)
            watched, project = data / "downloads", data / "downloads/new-empty-project"
            project.mkdir(parents=True)
            leases = data / "editor-workspaces"
            leases.mkdir()
            (leases / "code.json").write_text(json.dumps(dict(version=1, editor="VSCode", processId=os.getpid(),
                updatedUtc=datetime.now(timezone.utc).isoformat(), folders=[str(project)])))
            self.write_settings(data)
            settings = data / "appsettings.json"
            options = json.loads(settings.read_text())
            options["Monitoring"].update(Enabled=True, Folders=[str(watched)], IncludeSubdirectories=True, QuietSeconds=1)
            settings.write_text(json.dumps(options))
            process = self.launch(data)
            try:
                self.wait_for_event(process, data, "MonitoringStarted")
                for name in ("README.md", "settings.json", "report.pdf", "asset.png"):
                    (project / name).write_text("synthetic editor asset")
                time.sleep(1.7)
                self.assertEqual([], list((data / "operations").glob("*.json")))
                (leases / "code.json").unlink()
                time.sleep(0.3)
                (project / "after-close.pdf").write_text("ordinary file after editor close")
                for _ in range(100):
                    if list((data / "operations").glob("*.json")):
                        break
                    self.assertIsNone(process.poll())
                    time.sleep(0.1)
                requests = [json.loads(path.read_text()) for path in (data / "operations").glob("*.json")]
                self.assertEqual(["after-close.pdf"], [item["Request"]["DisplayName"] for item in requests])
                integrations = EXE.parent / "EditorIntegrations"
                self.assertTrue(any(integrations.glob("*VSCode*.vsix")) and any(integrations.glob("*VisualStudio*.vsix")))
            finally:
                self.stop(process)

if __name__ == "__main__":
    unittest.main(verbosity=2)
