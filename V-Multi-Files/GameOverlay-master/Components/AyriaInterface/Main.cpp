#include "STDInclude.h"

namespace Ayria
{
	void Initialize()
	{
		Hook::DXGI::Initialize();
		Hook::DirectX9::Initialize();

		Hook::OpenGL::Initialize();

		IInput::Initialize();
	}

	void Uninitialize()
	{
		Hook::DXGI::Uninitialize();
		Hook::DirectX9::Uninitialize();

		Hook::OpenGL::Uninitialize();

		IInput::Uninitialize();
	}
}

BOOL APIENTRY DllMain(HMODULE hModule, DWORD ul_reason_for_call, LPVOID lpReserved)
{
	if (ul_reason_for_call == DLL_PROCESS_ATTACH)
	{
		// It's illegal to initialize DXGI from DllMain, so run it in a separate thread.
		// CreateThread is actually illegal as well, but it works :P
		CreateThread(0, 0, static_cast<DWORD(__stdcall *)(void*)>([] (void*) -> DWORD
		{
			Ayria::Initialize();
			return 0;
		}), 0, 0, 0);
	}
	else if (ul_reason_for_call == DLL_PROCESS_DETACH)
	{
		Ayria::Uninitialize();
	}

	return TRUE;
}
