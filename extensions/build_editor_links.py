"""Build offline VSIX files for VS Code and Visual Studio from local source/SDK cache."""
from pathlib import Path
from xml.sax.saxutils import escape
import argparse
import json
import subprocess
import zipfile
import xml.etree.ElementTree as ET

ROOT = Path(__file__).resolve().parents[1]
NS = "http://schemas.microsoft.com/developer/vsx-schema/2011"


def content_types():
    return '''<?xml version="1.0" encoding="utf-8"?>
<Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types">
<Default Extension="json" ContentType="application/json"/>
<Default Extension="vsixmanifest" ContentType="text/xml"/>
<Default Extension="cjs" ContentType="application/javascript"/>
<Default Extension="md" ContentType="text/plain"/>
<Default Extension="dll" ContentType="application/octet-stream"/>
<Default Extension="pkgdef" ContentType="text/plain"/>
</Types>'''


def build(output, skip_build=False):
    output = Path(output).resolve()
    output.mkdir(parents=True, exist_ok=True)
    version = ET.parse(ROOT / "Directory.Build.props").findtext(".//Version")
    package = json.loads((ROOT / "extensions/vscode/package.json").read_text())
    if package["version"] != version:
        raise ValueError("VS Code extension version must match the desktop version")
    if not skip_build:
        subprocess.run(["dotnet", "build", ROOT / "extensions/visual-studio/PrimEx.SmartSave.VisualStudio.csproj", "-c", "Release", "--no-restore", "--nologo"], cwd=ROOT, check=True)
    vscode = output / f"PrimEx.SmartSave-VSCode-{version}.vsix"
    manifest = f'''<?xml version="1.0" encoding="utf-8"?>
<PackageManifest Version="2.0.0" xmlns="{NS}">
<Metadata><Identity Language="en-US" Id="{package['name']}" Version="{version}" Publisher="{package['publisher']}"/>
<DisplayName>{escape(package['displayName'])}</DisplayName><Description xml:space="preserve">{escape(package['description'])}</Description>
<Tags>workspace,smart-save</Tags><Categories>Other</Categories>
<Properties><Property Id="Microsoft.VisualStudio.Code.Engine" Value="{package['engines']['vscode']}"/>
<Property Id="Microsoft.VisualStudio.Code.ExtensionKind" Value="ui"/>
<Property Id="Microsoft.VisualStudio.Code.ExecutesCode" Value="true"/></Properties></Metadata>
<Installation><InstallationTarget Id="Microsoft.VisualStudio.Code"/></Installation>
<Dependencies/><Assets><Asset Type="Microsoft.VisualStudio.Code.Manifest" Path="extension/package.json" Addressable="true"/>
<Asset Type="Microsoft.VisualStudio.Services.Content.Details" Path="extension/README.md" Addressable="true"/></Assets>
</PackageManifest>'''
    with zipfile.ZipFile(vscode, "w", zipfile.ZIP_DEFLATED) as archive:
        archive.writestr("[Content_Types].xml", content_types())
        archive.writestr("extension.vsixmanifest", manifest)
        for name in ("package.json", "extension.cjs", "lease-writer.cjs", "README.md"):
            archive.write(ROOT / "extensions/vscode" / name, "extension/" + name)
    studio = output / f"PrimEx.SmartSave-VisualStudio-{version}.vsix"
    targets = "".join(f'<InstallationTarget Id="Microsoft.VisualStudio.{sku}" Version="[17.14,19.0)"><ProductArchitecture>amd64</ProductArchitecture></InstallationTarget>' for sku in ("Community", "Pro", "Enterprise"))
    manifest = f'''<?xml version="1.0" encoding="utf-8"?>
<PackageManifest Version="2.0.0" xmlns="{NS}"><Metadata>
<Identity Language="en-US" Id="PrimEx.SmartSave.WorkspaceLink.E7DBA7A8-70C5-44C8-83AC-E435452BD52A" Version="{version}" Publisher="PrimEx"/>
<DisplayName>PrimEx Smart Save Workspace Link</DisplayName><Description xml:space="preserve">Automatically keep files in open solutions and folders without placement prompts.</Description></Metadata>
<Installation AllUsers="false">{targets}</Installation><Dependencies/>
<Prerequisites><Prerequisite Id="Microsoft.VisualStudio.Component.CoreEditor" Version="[17.14,19.0)" DisplayName="Visual Studio core editor"/></Prerequisites>
<Assets><Asset Type="Microsoft.VisualStudio.VsPackage" Path="PrimEx.SmartSave.VisualStudio.pkgdef"/></Assets></PackageManifest>'''
    with zipfile.ZipFile(studio, "w", zipfile.ZIP_DEFLATED) as archive:
        archive.writestr("[Content_Types].xml", content_types())
        archive.writestr("extension.vsixmanifest", manifest)
        archive.write(ROOT / "extensions/visual-studio/bin/Release/net472/PrimEx.SmartSave.VisualStudio.dll", "PrimEx.SmartSave.VisualStudio.dll")
        pkgdef = (ROOT / "extensions/visual-studio/PrimEx.SmartSave.VisualStudio.pkgdef").read_text()
        archive.writestr("PrimEx.SmartSave.VisualStudio.pkgdef", pkgdef.replace("@VERSION@", version + ".0"))
    return [vscode, studio]


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--output", type=Path, required=True)
    parser.add_argument("--skip-build", action="store_true")
    args = parser.parse_args()
    for path in build(args.output, args.skip_build):
        print(path)
