"""Exercise fail-closed patching with disposable synthetic upstream sources."""
from pathlib import Path
import runpy
import sys
import tempfile
import unittest
from unittest.mock import patch


class PrepareTests(unittest.TestCase):
    def test_removes_only_forbidden_archives_and_preserves_required_codecs(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            settings = root / "build/linux-x64/settings.sh"
            settings.parent.mkdir(parents=True)
            settings.write_text('export IMAGEMAGICK_OPTIONS=""\n')
            cmake = root / "src/Magick.Native/CMakeLists.txt"
            cmake.parent.mkdir(parents=True)
            cmake.write_text("\n".join([
                "set(LIBHEIF libheif.a ${LIBAOM} libde265.a)",
                "set(LIBOPENH264 libopenh264.a)",
                "set_target_properties(${LIBRARY_NAME} PROPERTIES LINKER_LANGUAGE CXX)",
                'set(CMAKE_C_FLAGS_RELEASE "${CMAKE_C_FLAGS_RELEASE} -s")',
                'set(CMAKE_CXX_FLAGS_RELEASE "${CMAKE_CXX_FLAGS_RELEASE} -s")',
                "set(LIBWEBP libwebp.a)",
            ]))
            libraries = root / "dependencies/lib"
            libraries.mkdir(parents=True)
            forbidden = ["libheif.a", "libde265.a", "libopenh264.a", "libx264.a", "libx265.a"]
            retained = ["libwebp.a", "libpng.a", "libturbojpeg.a"]
            for name in forbidden + retained:
                (libraries / name).write_bytes(b"fixture")
            command = [sys.executable, str(Path(__file__).with_name("prepare.py")), str(root)]
            with patch.object(sys, "argv", command[1:]):
                runpy.run_path(command[1], run_name="__main__")
            self.assertEqual(sorted(p.name for p in libraries.iterdir()), sorted(retained))
            self.assertIn("--without-heic", settings.read_text())
            self.assertNotIn("libheif.a", cmake.read_text())
            self.assertNotIn("libopenh264.a", cmake.read_text())
            self.assertIn("-Wl,-z,defs", cmake.read_text())
            self.assertIn("set(LIBWEBP libwebp.a)", cmake.read_text())
            # Reapplying or upstream drift must fail rather than silently skipping edits.
            with patch.object(sys, "argv", command[1:]):
                with self.assertRaisesRegex(RuntimeError, "Unexpected upstream content"):
                    runpy.run_path(command[1], run_name="__main__")


if __name__ == "__main__":
    unittest.main()
