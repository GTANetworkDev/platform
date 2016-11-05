#include "STDInclude.h"

#define OVERLAY_LIB "Overlay.dll"

bool InjectGameOverlay()
{
	if (GetModuleHandleA(OVERLAY_LIB))
	{
		OutputDebugStringA("AyriaOverlay already loaded, aborting...");
		return false;
	}

	std::string path = GetModuleDir();
	std::wstring wPath(path.begin(), path.end());

	OutputDebugStringA(va("Adding new dll directory for overlay: %s", path.data()));
	SetDllDirectoryA(path.data());
	AddDllDirectory(wPath.data());

	if (!LoadLibraryA(OVERLAY_LIB))
	{
		OutputDebugStringA("Failed to load AyriaOverlay");
		OutputDebugStringA(GetLastErrorMessage().data());
		return false;
	}

	OutputDebugStringA("AyriaOverlay loaded successfully");
	
	return true;
}

BOOL APIENTRY DllMain(HMODULE hModule, DWORD ul_reason_for_call, LPVOID lpReserved)
{
	if (ul_reason_for_call == DLL_PROCESS_ATTACH)
	{
		InjectGameOverlay();
	}

	return TRUE;
}
