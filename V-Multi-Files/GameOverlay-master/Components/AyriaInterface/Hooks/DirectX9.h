namespace Hook
{
	class DirectX9
	{
	public:
		static bool Initialize();
		static bool Uninitialize();

	private:
		static Hook::Jump EndSceneHook;
		static Hook::Jump PresentHook;
		static Hook::Jump PresentSCHook;
		static Hook::Jump ResetHook;

		static bool PresentUsed;

		static HRESULT WINAPI PresentSC(IDirect3DSwapChain9C* chain, CONST RECT* pSourceRect, CONST RECT* pDestRect, HWND hDestWindowOverride, CONST RGNDATA* pDirtyRegion, DWORD dwFlags);
		static HRESULT WINAPI Present(IDirect3DDevice9C* pInterface, CONST RECT* pSourceRect, CONST RECT* pDestRect, HWND hDestWindowOverride, CONST RGNDATA* pDirtyRegion);
		static HRESULT WINAPI Reset(IDirect3DDevice9C* pInterface, D3DPRESENT_PARAMETERS* pPresentationParameters);
		static HRESULT WINAPI EndScene(IDirect3DDevice9C* pInterface);
	};
}