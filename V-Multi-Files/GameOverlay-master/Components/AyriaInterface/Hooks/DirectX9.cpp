#include "..\STDInclude.h"

namespace Hook
{
	Hook::Jump DirectX9::EndSceneHook;
	Hook::Jump DirectX9::PresentHook;
	Hook::Jump DirectX9::PresentSCHook;
	Hook::Jump DirectX9::ResetHook;

	bool DirectX9::PresentUsed = false;

	HRESULT WINAPI DirectX9::Present(IDirect3DDevice9C* pInterface, CONST RECT* pSourceRect, CONST RECT* pDestRect, HWND hDestWindowOverride, CONST RGNDATA* pDirtyRegion)
	{
		DirectX9::PresentUsed = true;

 		pInterface->lpVtbl->BeginScene(pInterface);
 		Ayria::IRenderer::Present();
 		pInterface->lpVtbl->EndScene(pInterface);

		DirectX9::PresentHook.Uninstall();
		HRESULT retVal = ((IDirect3DDevice9*)pInterface)->Present(pSourceRect, pDestRect, hDestWindowOverride, pDirtyRegion);
		DirectX9::PresentHook.Install();

		return retVal;
	}

	HRESULT WINAPI DirectX9::PresentSC(IDirect3DSwapChain9C* pChain, CONST RECT* pSourceRect, CONST RECT* pDestRect, HWND hDestWindowOverride, CONST RGNDATA* pDirtyRegion, DWORD dwFlags)
	{
		static bool isInLoop = false;
		IDirect3DDevice9* device;
		pChain->lpVtbl->GetDevice(pChain, &device);

		device->BeginScene();
		Ayria::IRenderer::Present();
		device->EndScene();

		DirectX9::PresentSCHook.Uninstall();
		HRESULT retVal = ((IDirect3DSwapChain9*)pChain)->Present(pSourceRect, pDestRect, hDestWindowOverride, pDirtyRegion, dwFlags);
		DirectX9::PresentSCHook.Install();


		if (DirectX9::PresentUsed)
		{
			DirectX9::PresentSCHook.Uninstall();
			OutputDebugStringA("Game using device's PRESENT, unloading swapchain hook!");
		}

		return retVal;
	}

	HRESULT WINAPI DirectX9::Reset(IDirect3DDevice9C* pInterface, D3DPRESENT_PARAMETERS* pPresentationParameters)
	{
		OutputDebugStringA("Device lost!");

		Ayria::IRenderer9* renderer = Ayria::IRenderer::GetSingleton<Ayria::IRenderer9>();

		if(renderer) renderer->DeviceLost();

		DirectX9::ResetHook.Uninstall();
		HRESULT result = ((IDirect3DDevice9*)pInterface)->Reset(pPresentationParameters);
		DirectX9::ResetHook.Install();

		OutputDebugStringA("Device reset!");

		if (renderer) renderer->DeviceReset();

		return result;
	}

	HRESULT WINAPI DirectX9::EndScene(IDirect3DDevice9C* pInterface)
	{
		// Bridge and unload hook
		DirectX9::EndSceneHook.Uninstall();
		HRESULT retVal = pInterface->lpVtbl->EndScene(pInterface);

		IDirect3DSwapChain9* chain = 0;
		pInterface->lpVtbl->GetSwapChain(pInterface, 0, &chain);

		if (chain)
		{
			// IDirect3DSwapChain9::Present
			IDirect3DSwapChain9C* chainC = (IDirect3DSwapChain9C*)chain;

			DirectX9::PresentSCHook.Uninstall();
			DirectX9::PresentSCHook.Initialize(chainC->lpVtbl->Present, DirectX9::PresentSC);
			DirectX9::PresentSCHook.Install();
		}

		// IDirect3DDevice9::Present
		DirectX9::PresentHook.Uninstall();
		DirectX9::PresentHook.Initialize(pInterface->lpVtbl->Present, DirectX9::Present);
		DirectX9::PresentHook.Install();

		// IDirect3DDevice9::Reset
		DirectX9::ResetHook.Uninstall();
		DirectX9::ResetHook.Initialize(pInterface->lpVtbl->Reset, DirectX9::Reset);
		DirectX9::ResetHook.Install();

		Ayria::IRenderer::Init_D3D9((IDirect3DDevice9*)pInterface);

		return retVal;
	}

	bool DirectX9::Initialize()
	{
		IDirect3D9* direct3d = Direct3DCreate9(32);

		if (!direct3d)
		{
			OutputDebugStringA("Error, direct3d is NULL!");
			return false;
		}

		D3DPRESENT_PARAMETERS presParams;

		ZeroMemory(&presParams, sizeof(presParams));
		presParams.Windowed = TRUE;
		presParams.SwapEffect = D3DSWAPEFFECT_DISCARD;
		presParams.BackBufferFormat = D3DFMT_UNKNOWN;

		IDirect3DDevice9* device;
		direct3d->CreateDevice(D3DADAPTER_DEFAULT, D3DDEVTYPE_NULLREF, DummyWindow().Get(), D3DCREATE_SOFTWARE_VERTEXPROCESSING | D3DCREATE_MULTITHREADED, &presParams, &device);

		if (!device)
		{
			direct3d->Release();
			OutputDebugStringA("[DirectX9::Initialize] Error, device is NULL!");
			return false;
		}

		// Hook Present
		DirectX9::EndSceneHook.Uninstall();
		DirectX9::EndSceneHook.Initialize(((IDirect3DDevice9C*)device)->lpVtbl->EndScene, DirectX9::EndScene);
		DirectX9::EndSceneHook.Install();

		device->ShowCursor(true);
		device->Release();
		direct3d->Release();

		// TODO: Move into a separate source
// 		IDirectDraw* ddraw;
// 		DirectDrawCreate(NULL, &ddraw, NULL);
// 
// 		if (ddraw)
// 		{
// 			ddraw->FlipToGDISurface();
// 			ddraw->Release();
// 		}
// 		else
// 		{
// 			OutputDebugStringA("DirectDraw creation failed!");
// 		}

		return true;
	}

	bool DirectX9::Uninitialize()
	{
		DirectX9::ResetHook.Uninstall();
		DirectX9::PresentHook.Uninstall();
		DirectX9::EndSceneHook.Uninstall();
		DirectX9::PresentSCHook.Uninstall();
		return true;
	}
}
