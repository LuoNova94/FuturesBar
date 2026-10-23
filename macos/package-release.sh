#!/bin/zsh
set -euo pipefail
cd "${0:A:h}"
project_root="${PWD:h}"
output_dir="$project_root/output"

if [[ ! -f "$project_root/LICENSE" ]]; then
    print -u2 '请先填写 LICENSE 中的版权署名。'
    exit 1
fi

zsh build.sh

version="$(plutil -extract CFBundleShortVersionString raw Info.plist)"
binary_arch="$(lipo -archs "$output_dir/合约行情.app/Contents/MacOS/FuturesBar")"
case "$binary_arch" in
    arm64) arch_label=arm64 ;;
    x86_64) arch_label=x86_64 ;;
    *arm64*x86_64*|*x86_64*arm64*) arch_label=universal ;;
    *) print -u2 "无法识别架构：$binary_arch"; exit 1 ;;
esac

release_dir="$output_dir/releases/v$version"
mkdir -p "$release_dir"
stage="$(mktemp -d "$output_dir/release-stage.XXXXXX")"
trap 'rm -rf "$stage"' EXIT

mkdir -p "$stage/dmg"
ditto --norsrc --noextattr --noqtn "$output_dir/合约行情.app" "$stage/dmg/合约行情.app"
ln -s /Applications "$stage/dmg/Applications"
cp INSTALL.txt "$stage/dmg/安装说明.txt"
cp "$project_root/LICENSE" "$stage/dmg/LICENSE.txt"
codesign --verify --strict "$stage/dmg/合约行情.app"

dmg_name="FuturesBar-$version-$arch_label-unnotarized.dmg"
hdiutil create -volname "FuturesBar $version" -srcfolder "$stage/dmg" \
    -fs HFS+ -format UDZO -ov "$release_dir/$dmg_name"
hdiutil verify "$release_dir/$dmg_name"

source_name="FuturesBar-$version-source"
mkdir -p "$stage/$source_name"
# Export both peer platform projects; never include local build caches or binaries.
(
    cd "$project_root"
    tar --exclude='*/.build' --exclude='*/.swiftpm' --exclude='*/bin' \
        --exclude='*/obj' --exclude='.DS_Store' -cf - macos windows docs
) | tar -xf - -C "$stage/$source_name"
for item in README.md LICENSE .gitignore; do
    cp "$project_root/$item" "$stage/$source_name/"
done
ditto -c -k --keepParent --norsrc --noextattr --noqtn "$stage/$source_name" "$release_dir/$source_name.zip"
cp RELEASE.md "$release_dir/RELEASE.md"
(
    cd "$release_dir"
    shasum -a 256 "$dmg_name" "$source_name.zip" > SHA256SUMS.txt
)
printf '发布文件已生成：%s\n' "$release_dir"
