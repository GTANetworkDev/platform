#include "..\STDInclude.h"

namespace Ayria
{
	IRenderer10::IRenderer10(IDXGISwapChain* pSwapChain) : SwapChain(pSwapChain)
	{
		SwapChain->GetDevice(__uuidof(ID3D10Device), (void**)&this->Device);
	}

	ITexture2D* IRenderer10::CreateTexture()
	{
		ITexture2D* texture = new ITexture2D10(this->Device);
		IRenderer::StoreTexture(texture);
		return texture;
	}

	POINT IRenderer10::Dimension()
	{
		UINT num = 1;
		D3D10_VIEWPORT viewport;
		this->Device->RSGetViewports(&num, &viewport);

		return{ (long)viewport.Width, (long)viewport.Height };
	}

	HWND IRenderer10::GetWindow()
	{
		DXGI_SWAP_CHAIN_DESC desc;
		this->SwapChain->GetDesc(&desc);

		return desc.OutputWindow;
	}

	bool IRenderer10::RequiresGammaCorrection()
	{
		DXGI_SWAP_CHAIN_DESC desc;
		this->SwapChain->GetDesc(&desc);

		// Might return true, even if no correction is required, as I'm not sure if I listed all the gamma formats
		return (ITexture2D::GetGammaCorrection(desc.BufferDesc.Format) == desc.BufferDesc.Format);
	}
}