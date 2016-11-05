#include "..\STDInclude.h"

namespace Ayria
{
	IRenderer11::IRenderer11(IDXGISwapChain* pSwapChain) : SwapChain(pSwapChain)
	{
		SwapChain->GetDevice(__uuidof(ID3D11Device), (void**)&this->Device);


	}

	ITexture2D* IRenderer11::CreateTexture()
	{
		ITexture2D* texture = new ITexture2D11(this->Device);
		IRenderer::StoreTexture(texture);
		return texture;
	}


	POINT IRenderer11::Dimension()
	{
		UINT num = 1;
		D3D11_VIEWPORT viewport;
		ID3D11DeviceContext* context;
		this->Device->GetImmediateContext(&context);
		context->RSGetViewports(&num, &viewport);

		return{ (long)viewport.Width, (long)viewport.Height };
	}

	HWND IRenderer11::GetWindow()
	{
		DXGI_SWAP_CHAIN_DESC desc;
		this->SwapChain->GetDesc(&desc);

		return desc.OutputWindow;
	}

	bool IRenderer11::RequiresGammaCorrection()
	{
		DXGI_SWAP_CHAIN_DESC desc;
		this->SwapChain->GetDesc(&desc);

		// Might return true, even if no correction is required, when a format without gamma complement is passed
		return (ITexture2D::GetGammaCorrection(desc.BufferDesc.Format) == desc.BufferDesc.Format);
	}
}
