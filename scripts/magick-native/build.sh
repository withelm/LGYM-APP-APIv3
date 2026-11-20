#!/usr/bin/env bash
set -euo pipefail

# Pinned sources matching Magick.NET-Q8-AnyCPU 15.1.0. Never execute the library
# during this build: verification below uses ELF metadata and the linker inputs.
[[ "$(uname -m)" = x86_64 ]]
git init /src/native
cd /src/native
git remote add origin https://github.com/dlemstra/Magick.Native.git
git fetch --depth 1 origin ce236bfba506817317517c3d6113b5566ecd03cd
git checkout --detach FETCH_HEAD
git init src/ImageMagick/ImageMagick
cd src/ImageMagick/ImageMagick
git remote add origin https://github.com/ImageMagick/ImageMagick.git
git fetch --depth 1 origin 5d1bcc55f2c83bc2ff802118b7369756610f3ad4
git checkout --detach FETCH_HEAD
[[ "$(cat ../ImageMagick.commit)" = 5d1bcc55f2c83bc2ff802118b7369756610f3ad4 ]]
curl --proto '=https' --proto-redir '=https' --fail --location --retry 3 \
    https://github.com/ImageMagick/Dependencies/releases/download/2026.10.31.2057/linux-x64-static.zip \
    --output /tmp/dependencies.zip
echo 'fe5f2e5f820cc01374dd6a406ccbe48a05c56a62983af40b381242942df5708e  /tmp/dependencies.zip' | sha256sum --check
mkdir /src/native/dependencies
ln -s /src/native/dependencies /tmp/dependencies
unzip -q /tmp/dependencies.zip -d /src/native/dependencies
python3 /scripts/prepare.py
bash /src/native/build/shared/build.imagemagick.sh linux x64 Q8 no
cd /src/native/src/Magick.Native
mkdir -p /out
bash /src/native/build/shared/build.native.sh linux x64 Q8 no
cp Q8/libMagick.Native-Q8-x64.dll.so /out/Magick.Native-Q8-x64.dll.so
cp Q8/CMakeFiles/Magick.Native-Q8-x64.dll.dir/link.txt /out/link.txt
nm --defined-only /out/Magick.Native-Q8-x64.dll.so > /out/defined-symbols.txt
readelf --dynamic /out/Magick.Native-Q8-x64.dll.so > /out/dynamic.txt
if grep -Ei 'heif_|de265_|Wels(Create|Destroy)|x26[45]_|lib(heif|de265|openh264|x264|x265)' \
    /out/defined-symbols.txt /out/dynamic.txt /out/link.txt /out/native-link.map; then
    echo 'Forbidden HEVC/AVC dependency detected' >&2
    exit 1
fi
# Nonempty expected symbols ensure the audit did not silently inspect a stripped file.
grep -q 'WebPDecode' /out/defined-symbols.txt
grep -q 'jpeg_read_header' /out/defined-symbols.txt
grep -q 'png_read_info' /out/defined-symbols.txt
sha256sum /out/Magick.Native-Q8-x64.dll.so > /out/SHA256SUMS
cp /src/native/src/ImageMagick/ImageMagick/config.log /out/config.log
