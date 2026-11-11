"""Check the library assets and dependency metadata before publishing."""

from pathlib import Path
from zipfile import ZipFile
import xml.etree.ElementTree as ET
import sys


def require(condition, message):
    if not condition:
        raise SystemExit(message)


packages = Path(sys.argv[1]) if len(sys.argv) > 1 else Path("artifacts/packages")
expected = {"LexiDiff": "LexiDiff.dll", "LexiDiff.Snowball": "Snowball.dll"}
found = {}
for path in packages.glob("*.nupkg"):
    with ZipFile(path) as archive:
        names = archive.namelist()
        nuspec = next(name for name in names if name.endswith(".nuspec"))
        root = ET.fromstring(archive.read(nuspec))
        ns = {"n": root.tag.split("}")[0].removeprefix("{")}
        metadata = root.find("n:metadata", ns)
        package_id = metadata.findtext("n:id", namespaces=ns)
        version = metadata.findtext("n:version", namespaces=ns)
        require(package_id in expected, f"Unexpected package: {path}")
        require(package_id not in found, f"Multiple versions of {package_id}")
        found[package_id] = version
        assets = [name for name in names if name.startswith("lib/")]
        require(f"lib/netstandard2.0/{expected[package_id]}" in assets,
                f"Missing .NET Standard assembly: {path}")
        require(all(name.startswith("lib/netstandard2.0/") for name in assets),
                f"Unexpected framework assets: {path}")
        require("README.md" in names and "LICENSE" in names,
                f"Missing package documentation: {path}")
        require(path.with_suffix(".snupkg").is_file(), f"Missing symbols: {path}")
        groups = metadata.findall("n:dependencies/n:group", ns)
        require(len(groups) == 1 and groups[0].get("targetFramework") == ".NETStandard2.0",
                f"Unexpected dependency frameworks: {path}")
        deps = {dep.get("id"): dep.get("version") for dep in groups[0]}
        wanted = {"LexiDiff.Snowball": version, "ICU4N": "60.1.0-alpha.440"} if package_id == "LexiDiff" else {}
        require(deps == wanted, f"Unexpected dependencies in {path}: {deps}")
        print(f"Verified {package_id} {version}: netstandard2.0 assets and dependencies")

require(set(found) == set(expected), "Expected both LexiDiff and LexiDiff.Snowball packages")
require(len(set(found.values())) == 1, "Package versions must match")
