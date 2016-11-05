#include "..\STDInclude.h"

namespace Ayria
{
	class IRenderer10 : public IRenderer
	{
	private:
		IDXGISwapChain* SwapChain;
		ID3D10Device* Device;

	public:
		IRenderer10(IDXGISwapChain* pSwapChain);

		virtual ITexture2D* CreateTexture() override;
		virtual POINT Dimension() override;
		virtual HWND GetWindow() override;

		virtual bool RequiresGammaCorrection() override;
	};
}