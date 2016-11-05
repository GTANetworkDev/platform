#include "..\STDInclude.h"

namespace Ayria
{
	std::mutex IRenderer::Mutex;
	IRenderer* IRenderer::Singleton = NULL;
	std::vector<ITexture2D*> IRenderer::Textures;
	std::vector<IRenderer::PrivateContainer> IRenderer::Queue;

	bool IRenderer::IsSupported(DXGI_FORMAT format)
	{
		bool supported = false;
		ITexture2D* texture = this->CreateTexture();

		if (texture)
		{
			supported = texture->Create(1, 1, format);
			delete texture;
		}

		return supported;
	}

	IRenderer* IRenderer::GetSingleton()
	{
		return IRenderer::Singleton;
	}

	void IRenderer::Init_D3D9(IDirect3DDevice9* pDevice)
	{
		IRenderer::Initialize(new IRenderer9(pDevice));
		pDevice->ShowCursor(true);

		OutputDebugStringA("Ayria::IRenderer initialized in DirectX9 mode");
	}

	void IRenderer::Init_DXGI(IDXGISwapChain* pSwapChain)
	{
		ID3D10Device* pDevice10 = NULL;
		ID3D11Device* pDevice11 = NULL;
		pSwapChain->GetDevice(__uuidof(ID3D10Device), (void**)&pDevice10);
		pSwapChain->GetDevice(__uuidof(ID3D11Device), (void**)&pDevice11);

		if (pDevice10)
		{
			IRenderer::Initialize(new IRenderer10(pSwapChain));
			OutputDebugStringA("Ayria::IRenderer initialized in DirectX10 mode");
		}
		else if (pDevice11)
		{
			IRenderer::Initialize(new IRenderer11(pSwapChain));
			OutputDebugStringA("Ayria::IRenderer initialized in DirectX11 mode");

		}
		else
		{
			OutputDebugStringA("Ayria::IRenderer: Unable to determine device from DXGI Swap chain!");
			return;
		}
	}

	void IRenderer::Init_GL(HDC handle)
	{
		IRenderer::Initialize(new IRendererGL(handle));
		OutputDebugStringA("Ayria::IRenderer initialized in OpenGL mode");
	}

	void IRenderer::Initialize(IRenderer* instance)
	{
		if (IRenderer::Singleton)
		{
			delete IRenderer::Singleton;
		}

		IRenderer::Singleton = instance;
	}

	void IRenderer::StoreTexture(ITexture2D* pTexture)
	{
		IRenderer::Mutex.lock();

		pTexture->OnDestroy(IRenderer::RemoveTexture);

		for (auto texture : IRenderer::Textures)
		{
			if (texture == pTexture)
			{
				IRenderer::Mutex.unlock();
				return;
			}
		}

		IRenderer::Textures.push_back(pTexture);

		IRenderer::Mutex.unlock();
	}

	void IRenderer::RemoveTexture(ITexture2D* pTexture)
	{
		IRenderer::Mutex.lock();

		for (auto iter = IRenderer::Textures.begin(); iter != IRenderer::Textures.end();iter++)
		{
			if (*iter == pTexture)
			{
				IRenderer::Textures.erase(iter);
				break;
			}
		}

		IRenderer::Mutex.unlock();
	}

	void IRenderer::Enqueue(Container &container)
	{
		IRenderer::Queue.push_back(container);
	}

	void IRenderer::Present()
	{
		for (auto &container : IRenderer::Queue)
		{
			if (!container.initialized)
			{
				container.initialized = true;
				if (container.onInit) container.onInit();
			}

			if (container.onPresent) container.onPresent();
		}
	}

	void IRenderer::EnumTextures(std::function<void(ITexture2D*)> callback)
	{
		IRenderer::Mutex.lock();

		for (auto &texture : IRenderer::Textures)
		{
			callback(texture);
		}

		IRenderer::Mutex.unlock();
	}
}