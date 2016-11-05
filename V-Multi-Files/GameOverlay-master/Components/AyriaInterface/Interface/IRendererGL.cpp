#include "..\STDInclude.h"

namespace Ayria
{
	ITexture2D* IRendererGL::CreateTexture()
	{
		ITexture2D* texture = new ITexture2DGL(this->Handle);
		return texture;
	}

	POINT IRendererGL::Dimension()
	{
		RECT rect;
		GetClientRect(this->GetWindow(), &rect);
		return { rect.right - rect.left, rect.bottom - rect.top };
	}

	HWND IRendererGL::GetWindow()
	{
		return WindowFromDC(this->Handle);
	}
}