#include "STDInclude.h"

static bool showOverlay = false;


void CreateCEFInstance()
{
	InitializeCEF();
}
void InitializeCEF()
{
	OutputDebugStringA("Initializing the AyriaOverlay...");
	JSBridge::Inititalize();

	Ayria::IRenderer::Container container;
	container.onInit = Ayria::UI::Initialize;
	container.onPresent = Ayria::UI::Present;
	Ayria::IRenderer::Enqueue(container);

	container.onInit = NULL;
	container.onPresent = [] ()
	{
		Ayria::IRenderer* renderer = Ayria::IRenderer::GetSingleton();

		if (renderer)
		{
			OutputDebugStringA(va("Dim: %dx%d", renderer->Width(), renderer->Height()));
		}
	};

	//Ayria::IRenderer::Enqueue(container);

	Ayria::IInput::OnKeyPress(Ayria::UI::KeyHandler);
}

void Uninitialize()
{
	OutputDebugStringA("Uninitializing the AyriaOverlay...");
	Ayria::UI::Uninitialize();
}

BOOL APIENTRY DllMain(HMODULE hModule, DWORD ul_reason_for_call, LPVOID lpReserved)
{
	if (ul_reason_for_call == DLL_PROCESS_ATTACH)
	{
		InitializeCEF();
	}
	else if (ul_reason_for_call == DLL_PROCESS_DETACH)
	{
		Uninitialize();
	}

	return TRUE;
}
