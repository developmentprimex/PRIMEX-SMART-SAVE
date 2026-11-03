"""Generate explicit per-user WiX components with stable IDs and HKCU key paths."""
from pathlib import Path
import argparse
import hashlib
import uuid
import xml.etree.ElementTree as ET

NS = "http://wixtoolset.org/schemas/v4/wxs"
ET.register_namespace("", NS)
NAMESPACE = uuid.UUID("221f1dc7-44df-47f8-9f80-a79483f0b0b1")

def identifier(prefix, value):
    return prefix + hashlib.sha256(value.lower().encode()).hexdigest()[:24]

def generate(payload, output):
    root = ET.Element(f"{{{NS}}}Wix")
    fragment = ET.SubElement(root, "Fragment")
    group = ET.SubElement(fragment, "ComponentGroup", Id="PayloadFiles")
    count = 0
    for top in ("Desktop", "NativeHost"):
        top_path = payload / top
        if not top_path.exists():
            continue
        directories = {top_path: top + "Dir"}
        for folder in sorted((p for p in top_path.rglob("*") if p.is_dir()), key=lambda x: (len(x.parts), str(x))):
            relative = folder.relative_to(payload).as_posix()
            directory_id = identifier("D", relative)
            reference = ET.SubElement(fragment, "DirectoryRef", Id=directories[folder.parent])
            ET.SubElement(reference, "Directory", Id=directory_id, Name=folder.name)
            directories[folder] = directory_id
        for folder, directory_id in directories.items():
            relative = folder.relative_to(payload).as_posix()
            component_id = identifier("R", relative)
            component = ET.SubElement(group, "Component", Id=component_id, Directory=directory_id, Guid=str(uuid.uuid5(NAMESPACE, "dir:" + relative.lower())).upper())
            ET.SubElement(component, "RegistryValue", Root="HKCU", Key=r"Software\PrimEx\SmartSave\InstallerFiles", Name=component_id, Type="integer", Value="1", KeyPath="yes")
            ET.SubElement(component, "RemoveFolder", Id=component_id + "Remove", On="uninstall")
        for file in sorted(p for p in top_path.rglob("*") if p.is_file()):
            if file.is_symlink() or any(x.lower() in ("data", "operations", "logs", "remembered-locations", ".git") for x in file.relative_to(top_path).parts[:-1]) or file.name in ("portable.mode", "agent.lock") or file.suffix.lower() == ".pdb":
                raise ValueError(f"Unexpected file in release payload: {file.name}")
            relative = file.relative_to(payload).as_posix()
            component_id = identifier("C", relative)
            component = ET.SubElement(group, "Component", Id=component_id, Directory=directories[file.parent], Guid=str(uuid.uuid5(NAMESPACE, "file:" + relative.lower())).upper())
            file_id = "DesktopExecutable" if relative == "Desktop/PrimEx.SmartSave.Desktop.exe" else identifier("F", relative)
            ET.SubElement(component, "File", Id=file_id, Source=str(file.resolve()), Name=file.name)
            ET.SubElement(component, "RegistryValue", Root="HKCU", Key=r"Software\PrimEx\SmartSave\InstallerFiles", Name=component_id, Value="1", Type="integer", KeyPath="yes")
            count += 1
    if not (payload / "Desktop" / "PrimEx.SmartSave.Desktop.exe").exists():
        raise ValueError("Desktop executable missing")
    ET.indent(root)
    ET.ElementTree(root).write(output, encoding="utf-8", xml_declaration=True)
    return count

if __name__ == "__main__":
    parser = argparse.ArgumentParser()
    parser.add_argument("payload", type=Path)
    parser.add_argument("output", type=Path)
    args = parser.parse_args()
    print(f"Generated components for {generate(args.payload, args.output)} payload files")
