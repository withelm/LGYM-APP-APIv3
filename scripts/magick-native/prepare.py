"""Fail-closed edits to the pinned upstream native build (no library loading)."""
from pathlib import Path


def replace_once(path, old, new):
    text = path.read_text()
    if text.count(old) != 1:
        raise RuntimeError(f"Unexpected upstream content: {path}: {old}")
    with path.open("w") as output:
        output.write(text.replace(old, new))


def prepare(root):
    replace_once(
        root / "build/linux-x64/settings.sh",
        'export IMAGEMAGICK_OPTIONS=""',
        'export IMAGEMAGICK_OPTIONS="--without-heic"',
    )
    cmake = root / "src/Magick.Native/CMakeLists.txt"
    replace_once(cmake, "set(LIBHEIF libheif.a ${LIBAOM} libde265.a)", "set(LIBHEIF)")
    replace_once(cmake, "set(LIBOPENH264 libopenh264.a)", "set(LIBOPENH264)")
    replace_once(
        cmake,
        "set_target_properties(${LIBRARY_NAME} PROPERTIES LINKER_LANGUAGE CXX)",
        'set_target_properties(${LIBRARY_NAME} PROPERTIES LINKER_LANGUAGE CXX '
        'LINK_FLAGS "-Wl,-z,defs,-Map=/out/native-link.map")',
    )
    # Keep symbols for a meaningful static audit; stripping may happen only after it.
    replace_once(cmake, 'set(CMAKE_C_FLAGS_RELEASE "${CMAKE_C_FLAGS_RELEASE} -s")', "")
    replace_once(cmake, 'set(CMAKE_CXX_FLAGS_RELEASE "${CMAKE_CXX_FLAGS_RELEASE} -s")', "")
    # Make accidental linkage impossible even if a future configure step changes.
    for path in (root / "dependencies").rglob("*"):
        if path.is_file() and path.name.startswith(
            ("libheif.", "libde265.", "libopenh264.", "libx264.", "libx265.")
        ):
            path.unlink()


if __name__ == "__main__":
    prepare(Path("/src/native"))
