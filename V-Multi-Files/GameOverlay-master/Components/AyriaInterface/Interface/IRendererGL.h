#include "..\STDInclude.h"

namespace Ayria
{
	class IRendererGL : public IRenderer
	{
	private:
		HDC Handle;

	public:
		IRendererGL(HDC handle) : Handle(handle) { glewInit(); }

		virtual ITexture2D* CreateTexture() override;
		virtual POINT Dimension() override;
		virtual HWND GetWindow() override;
	};
}
