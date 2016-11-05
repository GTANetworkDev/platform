#include "..\STDInclude.h"

namespace Ayria
{
	ITexture2D* IRenderer9::CreateTexture() 
	{
		ITexture2D* texture = new ITexture2D9(this->Device);
		IRenderer::StoreTexture(texture);
		return texture;
	}

	POINT IRenderer9::Dimension()
	{
		D3DVIEWPORT9 viewport;
		this->Device->GetViewport(&viewport);

		return{ (long)viewport.Width, (long)viewport.Height };
	}

	HWND IRenderer9::GetWindow()
	{
		D3DDEVICE_CREATION_PARAMETERS params;
		this->Device->GetCreationParameters(&params);

		return params.hFocusWindow;
	}

	void IRenderer9::DeviceLost()
	{
		IRenderer::EnumTextures([] (ITexture2D* texture)
		{
			if (texture->GetType() == ITexture2D::Type::TEXTURE_D3D9)
			{
				ITexture2D9* texture9 = texture->As<ITexture2D9>();
				texture9->ReleaseResources();
			}
		});
	}

	void IRenderer9::DeviceReset()
	{
		IRenderer::EnumTextures([&] (ITexture2D* texture)
		{
			if (texture->GetType() == ITexture2D::Type::TEXTURE_D3D9)
			{
				ITexture2D9* texture9 = texture->As<ITexture2D9>();
				texture9->Reinitialize(this->Device);
			}
		});
	}
}