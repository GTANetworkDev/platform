namespace Hook
{
	typedef  HRESULT(__stdcall* PresentDXGI_t)(IDXGISwapChain * This, UINT SyncInterval, UINT Flags);

	class DXGI
	{
	public:
		static bool Initialize();
		static bool Uninitialize();

	private:
		static bool Initialized;
		static Hook::Jump SwapChainPresentHook;
		static HRESULT WINAPI Present(IDXGISwapChain* This, UINT SyncInterval, UINT Flags);
	};
}