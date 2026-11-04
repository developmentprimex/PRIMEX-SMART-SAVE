from pathlib import Path
import json
import sys
import unittest
import zipfile
import xml.etree.ElementTree as ET

ROOT = Path(__file__).resolve().parents[2]
VERSION = ET.parse(ROOT / "Directory.Build.props").findtext(".//Version")
PACKAGES = Path(sys.argv.pop(1)).resolve() if len(sys.argv) > 1 else ROOT / f"artifacts/verification/editor-links-{VERSION}"
NS = {"v": "http://schemas.microsoft.com/developer/vsx-schema/2011"}


class EditorPackages(unittest.TestCase):
    def test_vscode_activates_for_every_workspace_locally(self):
        with zipfile.ZipFile(PACKAGES / f"PrimEx.SmartSave-VSCode-{VERSION}.vsix") as archive:
            self.assertIsNone(archive.testzip())
            package = json.loads(archive.read("extension/package.json"))
            self.assertEqual(VERSION, package["version"])
            self.assertEqual(["*"], package["activationEvents"])
            self.assertEqual(["ui"], package["extensionKind"])
            self.assertTrue(package["capabilities"]["untrustedWorkspaces"]["supported"])
            self.assertIn("extension/" + package["main"].removeprefix("./"), archive.namelist())
            ET.fromstring(archive.read("[Content_Types].xml"))
            manifest = ET.fromstring(archive.read("extension.vsixmanifest"))
            self.assertEqual("primex", manifest.find("v:Metadata/v:Identity", NS).get("Publisher"))

    def test_visual_studio_package_registration_and_supported_hosts(self):
        with zipfile.ZipFile(PACKAGES / f"PrimEx.SmartSave-VisualStudio-{VERSION}.vsix") as archive:
            self.assertIsNone(archive.testzip())
            manifest = ET.fromstring(archive.read("extension.vsixmanifest"))
            self.assertEqual(VERSION, manifest.find("v:Metadata/v:Identity", NS).get("Version"))
            targets = manifest.findall("v:Installation/v:InstallationTarget", NS)
            self.assertEqual(3, len(targets))
            self.assertTrue(all(target.get("Version") == "[17.14,19.0)" and target.findtext("v:ProductArchitecture", namespaces=NS) == "amd64" for target in targets))
            asset = manifest.find("v:Assets/v:Asset", NS)
            registration = archive.read(asset.get("Path")).decode()
            self.assertNotIn("@VERSION@", registration)
            self.assertIn("AllowsBackgroundLoading", registration)
            self.assertIn(f"Version={VERSION}.0", registration)
            self.assertIn("4646B819-1AE0-4E79-97F4-8A8176FDD664", registration)
            self.assertTrue(archive.read("PrimEx.SmartSave.VisualStudio.dll").startswith(b"MZ"))


if __name__ == "__main__":
    unittest.main(verbosity=2)
