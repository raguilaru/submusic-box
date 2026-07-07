@echo off
rem Builds the x64 Release Native AOT sideload package for Xbox.
rem Run from the Windows build VM (paths are VM-specific):
rem   ssh winvm "cmd /c \\\\Mac\\Repositories\\xbox-subsonic-app\\tools\\build-xbox-package.cmd"
rem
rem Why this shape (see CLAUDE.local.md):
rem - Xbox forbids JIT: only Release + Native AOT packages activate on console.
rem - arm64-native MSBuild is required for correct AOT host detection.
rem - No arm64-hosted MSVC linker exists here, and this VS install has no
rem   working vcvarsall; we put the x64-hosted (emulated) linker and the x64
rem   libs on the environment ourselves and tell ILC to use environmental tools.
rem - Xbox requires signed packages: signs with the self-signed dev cert
rem   (CN=Rafa) from the VM user cert store, created 2026-07-07.
setlocal enabledelayedexpansion
set "VSROOT=C:\Program Files\Microsoft Visual Studio\18\Community"
for /d %%i in ("%VSROOT%\VC\Tools\MSVC\*") do set "VCTOOLS=%%i"
set "WINSDKLIB=C:\Program Files (x86)\Windows Kits\10\Lib\10.0.26100.0"
set "PATH=!VCTOOLS!\bin\Hostx64\x64;%PATH%"
set "LIB=!VCTOOLS!\lib\onecore\x64;%WINSDKLIB%\ucrt\x64;%WINSDKLIB%\um\x64"
"%VSROOT%\MSBuild\Current\Bin\arm64\MSBuild.exe" ^
  "\\Mac\Repositories\xbox-subsonic-app\src\BoxSubMusic.App\BoxSubMusic.App.csproj" ^
  /restore /p:Configuration=Release /p:Platform=x64 ^
  /p:GenerateAppxPackageOnBuild=true /p:AppxBundle=Never ^
  /p:UapAppxPackageBuildMode=SideloadOnly /p:AppxPackageSigningEnabled=true ^
  /p:PackageCertificateThumbprint=6ED8E735DA1D80A7FA379AD218BEB29D03FE84A9 ^
  /p:IlcUseEnvironmentalTools=true /v:m /nologo
exit /b %ERRORLEVEL%
