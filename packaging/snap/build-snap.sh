#!/bin/bash
# Builds the Ubuntu snap. Stages the sources snapcraft needs (everything except
# build output), so the part does not have to copy the whole workspace.
set -euo pipefail

snap_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
project_dir="$(cd "$snap_dir/../.." && pwd)"
staged="$snap_dir/staged"

crate_version="$(grep -m1 '^version = ' "$project_dir/Cargo.toml" | cut -d'"' -f2)"
snap_version="$(grep -m1 '^version: ' "$snap_dir/snapcraft.yaml" | cut -d' ' -f2 | tr -d '"')"
if [ "$crate_version" != "$snap_version" ]; then
    echo "Version mismatch: Cargo.toml has $crate_version, snapcraft.yaml has $snap_version" >&2
    exit 1
fi

command -v convert >/dev/null || {
    echo "ImageMagick (convert) is required to build the snap icon" >&2
    exit 1
}
command -v snapcraft >/dev/null || {
    echo "snapcraft is required; install it with: sudo snap install snapcraft --classic" >&2
    exit 1
}

rm -rf "$staged"
mkdir -p "$staged/gui"
cp "$project_dir/Cargo.toml" "$project_dir/Cargo.lock" "$project_dir/rust-toolchain.toml" \
    "$project_dir/app.ico" "$staged/"
cp -r "$project_dir/src" "$staged/src"
cp "$snap_dir/gui/random-image-viewer.desktop" "$staged/gui/"
convert "$project_dir/app.ico" -resize 256x256 "$staged/icon.png"

cd "$snap_dir"
snapcraft

echo "Snap built: $snap_dir"/*.snap
echo "Install it with: sudo snap install --dangerous $snap_dir"/*.snap
