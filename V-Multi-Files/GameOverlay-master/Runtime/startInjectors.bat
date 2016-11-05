@echo off
taskkill /IM "AyriaOverlayInjector.exe" /T /F

if exist "x64\AyriaOverlayInjector.exe" (
	pushd "x64"
	start AyriaOverlayInjector.exe
	popd
)

if exist "x86\AyriaOverlayInjector.exe" (
	pushd "x86"
	start AyriaOverlayInjector.exe
	popd
)