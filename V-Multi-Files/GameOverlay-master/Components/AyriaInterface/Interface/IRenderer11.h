#include "..\STDInclude.h"

namespace Ayria
{
	class IRenderer11 : public IRenderer
	{
	private:
		IDXGISwapChain* SwapChain;
		ID3D11Device* Device;

	public:
		IRenderer11(IDXGISwapChain* pSwapChain);

		virtual ITexture2D* CreateTexture() override;
		virtual POINT Dimension() override;
		virtual HWND GetWindow() override;

		virtual bool RequiresGammaCorrection() override;
	};
}