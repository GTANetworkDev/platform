#include "..\STDInclude.h"

namespace Ayria
{
	class IRenderer9 : public IRenderer
	{
	private:
		IDirect3DDevice9* Device;

	public:
		IRenderer9(IDirect3DDevice9* pDevice) : Device(pDevice) {}

		virtual ITexture2D* CreateTexture() override;
		virtual POINT Dimension() override;
		virtual HWND GetWindow() override;

		void DeviceLost();
		void DeviceReset();
	};
}