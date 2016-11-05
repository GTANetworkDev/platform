#include "..\STDInclude.h"

namespace Hook
{
	bool DXGI::Initialized = false;
	Hook::Jump DXGI::SwapChainPresentHook;

	HRESULT WINAPI DXGI::Present(IDXGISwapChain* This, UINT SyncInterval, UINT Flags)
	{
		if (!This)
		{
			OutputDebugStringA("[DXGI::Present] SwapChain is NULL!");
			return DXGI_ERROR_INVALID_CALL;
		}

		if (!DXGI::Initialized)
		{
			DXGI::Initialized = true;
			Ayria::IRenderer::Init_DXGI(This);
		}

		Ayria::IRenderer::Present();

		DXGI::SwapChainPresentHook.Uninstall();
		HRESULT result = This->Present(SyncInterval, Flags);
		DXGI::SwapChainPresentHook.Install();

		return result;
	}

	// TODO: Add support for IDXGISwapChain1
	bool DXGI::Initialize()
	{
		IDXGISwapChain* swapChain = NULL;
		DXGI_SWAP_CHAIN_DESC swapChainDesc;
		D3D_FEATURE_LEVEL featureLevel = D3D_FEATURE_LEVEL_11_0;

		ZeroMemory(&swapChainDesc, sizeof(swapChainDesc));
		swapChainDesc.OutputWindow = DummyWindow().Get();
		swapChainDesc.BufferCount = 1;
		swapChainDesc.BufferDesc.Width = 1;
		swapChainDesc.BufferDesc.Height = 1;
		swapChainDesc.BufferDesc.Format = DXGI_FORMAT_R8G8B8A8_UNORM;
		swapChainDesc.SampleDesc.Count = 1;

		D3D11CreateDeviceAndSwapChain(NULL, D3D_DRIVER_TYPE_NULL, NULL, 0, &featureLevel, 1, D3D11_SDK_VERSION, &swapChainDesc, &swapChain, NULL, NULL, NULL);

		if (!swapChain)
		{
			OutputDebugStringA("[DXGI::Initialize] SwapChain is NULL!");
			return false;
		}

		// Statically hook Present
		DXGI::SwapChainPresentHook.Uninstall();
		DXGI::SwapChainPresentHook.Initialize(((IDXGISwapChainC*)swapChain)->lpVtbl->Present, DXGI::Present);
		DXGI::SwapChainPresentHook.Install();

		swapChain->Release();
		return true;
	}

	bool DXGI::Uninitialize()
	{
		DXGI::Initialized = false;
		DXGI::SwapChainPresentHook.Uninstall();
		return true;
	}
}
