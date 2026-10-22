#!/bin/zsh
set -euo pipefail
cd "${0:A:h}"
mkdir -p .build/module-cache
export CLANG_MODULE_CACHE_PATH="$PWD/.build/module-cache"
export SWIFTPM_MODULECACHE_OVERRIDE="$PWD/.build/module-cache"
swift build -c release --product FuturesBar --disable-sandbox
bin_dir="$(swift build -c release --show-bin-path --disable-sandbox)"
app_path="$PWD/output/合约行情.app"
mkdir -p "$app_path/Contents/MacOS"
# Replace the inode so a running copy never sees a truncated executable.
cp "$bin_dir/FuturesBar" "$app_path/Contents/MacOS/.FuturesBar.next"
mv -f "$app_path/Contents/MacOS/.FuturesBar.next" "$app_path/Contents/MacOS/FuturesBar"
cp Info.plist "$app_path/Contents/Info.plist"
codesign --force --sign - "$app_path"
printf '已生成：%s\n' "$app_path"
