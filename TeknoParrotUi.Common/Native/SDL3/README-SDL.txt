TeknoParrotUI SDL3 native runtime
================================

Windows DLLs in win-x64, win-x86, and win-arm64 are from the official SDL
3.4.16 release, published 2026-09-02:
https://github.com/libsdl-org/SDL/releases/tag/release-3.4.16

Official archive SHA-256 values (verified before extraction):
  SDL3-3.4.16-win32-x64.zip
    4217944b4e51457af4a59c82d883f8443b3e65964b2acd8943484c492756c4b6
  SDL3-3.4.16-win32-x86.zip
    beb4e86e4a101f66556162c3efff9fd1ad88f1761b63be35867a3336a72f8c41
  SDL3-3.4.16-win32-arm64.zip
    dbd381378164447ce7985e40876e619eda0ec8c14b7694080029ee79aa2a494d

The vendored Linux x64 and arm64 binaries were extracted from the pinned
SDL3-CS.Linux 3.4.16 native package. The vendored macOS x64 and arm64 binaries
were extracted from SDL3-CS.MacOS 3.4.16. The desktop and audit projects copy
only the binary matching their publish RID via SDL3.Native.targets. Android
does not import these runtime assets. Linux package source:
https://www.nuget.org/packages/SDL3-CS.Linux/3.4.16
Downloaded package SHA-256:
  847a3b01e3bf02b98a21e9738492677fc80cbb2d32f8961573818d9bbda96039
macOS package source:
https://www.nuget.org/packages/SDL3-CS.MacOS/3.4.16
Restored package SHA-256:
  7d344456e9a48b059b3b80f30f3a32f3546495a03f6ceb975a4564cd342b413a

SDL is distributed under the zlib license. The official SDL license is in
LICENSE.txt. The Linux/macOS runtime package license is in
LICENSE-Native-Packages.txt.

The Android app uses Android's native InputDevice and input event APIs for
controller discovery and capture. SDL3's Android activity and native library
are not packaged into the Avalonia Android app.
