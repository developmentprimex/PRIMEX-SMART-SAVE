"""Build a self-contained, per-user staff MSI. Requires .NET 10, Python and WiX 6."""
from pathlib import Path
import argparse
import json
import subprocess
from generate_payload import generate

ROOT = Path(__file__).resolve().parents[1]

def run(*args):
    subprocess.run([str(x) for x in args], cwd=ROOT, check=True)

def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--wix", default="wix", help="WiX executable or path to wix.dll")
    parser.add_argument("--wix-ui", required=True, help="WiX 6 UI extension DLL")
    parser.add_argument("--offline-feed", type=Path)
    parser.add_argument("--runtime-version", default="10.0.8")
    parser.add_argument("--version", default="0.2.4")
    parser.add_argument("--output", type=Path, default=ROOT / "artifacts" / "staff-release")
    parser.add_argument("--extension-id", action="append", default=[], help="Optional exact approved Chrome/Edge extension ID")
    args = parser.parse_args()
    import re
    if not re.fullmatch(r"\d+\.\d+\.\d+", args.version):
        parser.error("Version must be a three-part numeric MSI version")
    if any(not re.fullmatch(r"[a-p]{32}", value) for value in args.extension_id):
        parser.error("Each extension ID must contain exactly 32 letters a-p")
    output = args.output.resolve()
    if output.exists():
        parser.error("Use a new output folder; existing release payloads are never recursively overwritten")
    output.mkdir(parents=True)
    payload = output / "payload"
    for name in (["Desktop", "NativeHost"] if args.extension_id else ["Desktop"]):
        project = ROOT / "src" / f"PrimEx.SmartSave.{name}" / f"PrimEx.SmartSave.{name}.csproj"
        command = ["dotnet", "publish", project, "-c", "Release", "-r", "win-x64", "--self-contained", "true", f"-p:RuntimeFrameworkVersion={args.runtime_version}", f"-p:Version={args.version}", "-p:DebugType=None", "-o", payload / name]
        if args.offline_feed:
            command += ["--source", args.offline_feed.resolve(), "-p:NuGetAudit=false"]
        run(*command)
    (payload / "Desktop" / "installed.mode").write_text("Installed per-user staff build\n", encoding="utf-8")
    if args.extension_id:
        manifest = dict(name="com.primex.smartsave", description="PrimEx Smart Save completed-download bridge", path=r"NativeHost\PrimEx.SmartSave.NativeHost.exe", type="stdio", allowed_origins=[f"chrome-extension://{x}/" for x in sorted(set(args.extension_id))])
        (payload / "native-host.json").write_text(json.dumps(manifest, indent=2), encoding="utf-8")
    fragment = output / "Payload.wxs"
    count = generate(payload, fragment)
    wix = ["dotnet", "--roll-forward", "Major", args.wix] if args.wix.lower().endswith(".dll") else [args.wix]
    msi = output / f"PrimEx.SmartSave-{args.version}-win-x64.msi"
    command = wix + ["build", ROOT / "installer" / "PrimEx.SmartSave.wxs", fragment, "-arch", "x64", "-ext", args.wix_ui, "-d", f"ProductVersion={args.version}", "-d", f"PayloadRoot={payload}", "-d", f"DeploymentNotice={ROOT / 'installer' / 'DeploymentNotice.rtf'}", "-o", msi]
    if args.extension_id:
        command += ["-d", "BrowserIntegration=1"]
    run(*command)
    import hashlib
    digest = hashlib.sha256(msi.read_bytes()).hexdigest()
    (output / (msi.name + ".sha256")).write_text(f"{digest}  {msi.name}\n", encoding="ascii")
    print(f"Built {msi}\nPayload files: {count}\nSHA256: {digest}")

if __name__ == "__main__":
    main()
