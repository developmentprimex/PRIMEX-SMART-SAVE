"""Read-only MSI/package checks; never installs the product or changes startup."""
from pathlib import Path
import ctypes
from ctypes import wintypes
import importlib.util
import json
import sys
import tempfile
import unittest
import xml.etree.ElementTree as ET

ROOT = Path(__file__).resolve().parents[2]
spec = importlib.util.spec_from_file_location("payload", ROOT / "installer" / "generate_payload.py")
payload_generator = importlib.util.module_from_spec(spec)
spec.loader.exec_module(payload_generator)
MSI = Path(sys.argv.pop(1)).resolve() if len(sys.argv) > 1 and sys.argv[1].endswith(".msi") else None
EXTRACTED = None
if "--extracted" in sys.argv:
    index = sys.argv.index("--extracted")
    sys.argv.pop(index)
    EXTRACTED = Path(sys.argv.pop(index)).resolve()

class PayloadTests(unittest.TestCase):
    def test_stable_components_and_user_registry_keypaths(self):
        with tempfile.TemporaryDirectory(prefix="PrimEx-Package-Test-") as temporary:
            root = Path(temporary)
            desktop = root / "Desktop"
            (desktop / "en-US").mkdir(parents=True)
            (desktop / "PrimEx.SmartSave.Desktop.exe").write_bytes(b"test")
            (desktop / "en-US" / "resource.dll").write_bytes(b"test")
            first, second = root / "first.wxs", root / "second.wxs"
            self.assertEqual(2, payload_generator.generate(root, first))
            payload_generator.generate(root, second)
            self.assertEqual(first.read_bytes(), second.read_bytes())
            xml = ET.parse(first)
            ns = {"w": payload_generator.NS}
            for component in xml.findall(".//w:Component", ns):
                registry = component.find("w:RegistryValue", ns)
                self.assertIsNotNone(registry)
                self.assertEqual("HKCU", registry.get("Root"))
                self.assertEqual("yes", registry.get("KeyPath"))

    def test_developer_payload_rejected(self):
        with tempfile.TemporaryDirectory(prefix="PrimEx-Package-Test-") as temporary:
            root = Path(temporary)
            (root / "Desktop").mkdir()
            (root / "Desktop" / "PrimEx.SmartSave.Desktop.exe").write_bytes(b"test")
            (root / "Desktop" / "portable.mode").write_text("private developer state")
            with self.assertRaises(ValueError):
                payload_generator.generate(root, root / "payload.wxs")

    def test_default_configuration_remains_opt_in(self):
        options = json.loads((ROOT / "config" / "appsettings.example.json").read_text(encoding="utf-8"))
        self.assertFalse(options["Monitoring"]["Enabled"])
        self.assertEqual([], options["CompanyServer"]["ApprovedRoots"])
        self.assertTrue(options["SmartSave"]["AllowOutsideServer"])
        self.assertTrue(options["StartWithWindows"])
        self.assertEqual(["*"], options["Monitoring"]["Extensions"])
        self.assertTrue(options["Monitoring"]["IgnoreDevelopmentProjects"])
        self.assertEqual([], options["Monitoring"]["ExcludedFolders"])
        self.assertEqual(1, options["Monitoring"]["QuietSeconds"])
        self.assertEqual(1, options["Monitoring"]["PromptTimingVersion"])

    def test_private_state_directory_rejected(self):
        for name in ("data", "remembered-locations", "editor-workspaces"):
            with self.subTest(name=name), tempfile.TemporaryDirectory(prefix="PrimEx-Package-Test-") as temporary:
                root = Path(temporary)
                (root / "Desktop" / name).mkdir(parents=True)
                (root / "Desktop" / "PrimEx.SmartSave.Desktop.exe").write_bytes(b"test")
                (root / "Desktop" / name / "operation.json").write_text("private")
                with self.assertRaises(ValueError):
                    payload_generator.generate(root, root / "payload.wxs")

class Database:
    def __init__(self, path):
        self.api = ctypes.WinDLL("msi")
        self.api.MsiOpenDatabaseW.argtypes = [wintypes.LPCWSTR, wintypes.LPCWSTR, ctypes.POINTER(wintypes.UINT)]
        self.api.MsiDatabaseOpenViewW.argtypes = [wintypes.UINT, wintypes.LPCWSTR, ctypes.POINTER(wintypes.UINT)]
        self.api.MsiViewExecute.argtypes = [wintypes.UINT, wintypes.UINT]
        self.api.MsiViewFetch.argtypes = [wintypes.UINT, ctypes.POINTER(wintypes.UINT)]
        self.api.MsiRecordGetStringW.argtypes = [wintypes.UINT, wintypes.UINT, wintypes.LPWSTR, ctypes.POINTER(wintypes.DWORD)]
        self.handle = wintypes.UINT()
        self.check(self.api.MsiOpenDatabaseW(str(path), None, ctypes.byref(self.handle)))

    def check(self, result):
        if result:
            raise OSError(f"Windows Installer read-only API error {result}")

    def rows(self, sql, columns):
        view = wintypes.UINT()
        self.check(self.api.MsiDatabaseOpenViewW(self.handle, sql, ctypes.byref(view)))
        result = []
        try:
            self.check(self.api.MsiViewExecute(view, 0))
            while True:
                record = wintypes.UINT()
                fetched = self.api.MsiViewFetch(view, ctypes.byref(record))
                if fetched == 259:
                    break
                self.check(fetched)
                try:
                    row = []
                    for column in range(1, columns + 1):
                        buffer = ctypes.create_unicode_buffer(32768)
                        length = wintypes.DWORD(len(buffer))
                        self.check(self.api.MsiRecordGetStringW(record, column, buffer, ctypes.byref(length)))
                        row.append(buffer.value)
                    result.append(row)
                finally:
                    self.api.MsiCloseHandle(record)
        finally:
            self.api.MsiCloseHandle(view)
        return result

    def close(self):
        self.api.MsiCloseHandle(self.handle)

@unittest.skipUnless(MSI and MSI.exists() and sys.platform == "win32", "Supply a built Windows staff MSI")
class MsiTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.db = Database(MSI)

    @classmethod
    def tearDownClass(cls):
        cls.db.close()

    def test_identity_and_user_scope(self):
        properties = dict(self.db.rows("SELECT `Property`, `Value` FROM `Property`", 2))
        self.assertEqual("PrimEx Smart Save", properties["ProductName"])
        self.assertEqual("0.2.15", properties["ProductVersion"])
        self.assertNotIn(properties.get("ALLUSERS"), ("1", "2"))
        self.assertEqual("{74FC93D4-971F-4D60-8FA7-226C2AB7E5AE}", properties["UpgradeCode"])

    def test_startup_is_current_user_and_correct_arguments(self):
        registry = self.db.rows("SELECT `Root`, `Key`, `Name`, `Value` FROM `Registry`", 4)
        self.assertTrue(all(row[0] == "1" for row in registry))
        startup = [row for row in registry if row[1] == r"Software\Microsoft\Windows\CurrentVersion\Run"]
        self.assertEqual(1, len(startup))
        self.assertEqual("PrimEx Smart Save", startup[0][2])
        self.assertEqual('"[DesktopDir]PrimEx.SmartSave.Desktop.exe" --startup --background', startup[0][3])
        self.assertFalse(any("NativeMessagingHosts" in row[1] for row in registry))

    def test_desktop_and_start_menu_shortcuts(self):
        shortcuts = self.db.rows("SELECT `Directory_`, `Target` FROM `Shortcut`", 2)
        self.assertEqual({"DesktopFolder", "ProgramMenuDir"}, {row[0] for row in shortcuts})
        self.assertTrue(all(row[1] == "[DesktopDir]PrimEx.SmartSave.Desktop.exe" for row in shortcuts))

    def test_runtime_included_and_private_state_excluded(self):
        files = self.db.rows("SELECT `FileName` FROM `File`", 1)
        names = {row[0].split("|")[-1] for row in files}
        for name in ("coreclr.dll", "hostfxr.dll", "PresentationFramework.dll", "PrimEx.SmartSave.Desktop.exe", "installed.mode", "appsettings.example.json"):
            self.assertIn(name, names)
        for name in ("portable.mode", "agent.lock", "appsettings.json", "events.jsonl"):
            self.assertNotIn(name, names)
        self.assertIn("PrimEx.SmartSave-VSCode-0.2.15.vsix", names)
        self.assertIn("PrimEx.SmartSave-VisualStudio-0.2.15.vsix", names)
        self.assertFalse(any(name.endswith(".pdb") for name in names))

    def test_uninstall_has_no_user_document_delete_rules(self):
        removals = self.db.rows("SELECT `FileName` FROM `RemoveFile`", 1)
        self.assertTrue(all(not row[0] for row in removals))  # Empty-folder cleanup only.

    @unittest.skipUnless(EXTRACTED, "Supply --extracted with the WiX decompile -x directory")
    def test_embedded_payload_matches_published_files(self):
        import hashlib
        fragment = ET.parse(MSI.parent / "Payload.wxs")
        files = fragment.findall(".//w:File", {"w": payload_generator.NS})
        self.assertGreater(len(files), 100)
        for file in files:
            embedded = EXTRACTED / "File" / file.get("Id")
            published = Path(file.get("Source"))
            self.assertTrue(embedded.exists(), file.get("Name"))
            self.assertEqual(hashlib.sha256(published.read_bytes()).digest(), hashlib.sha256(embedded.read_bytes()).digest(), file.get("Name"))

if __name__ == "__main__":
    unittest.main(verbosity=2)
