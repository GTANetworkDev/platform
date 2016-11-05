namespace Ayria
{
	class IRenderer
	{
	public:
		typedef void(__cdecl* Callback)();

		struct Container
		{
			Container() : onInit(0), onPresent(0) {}

			Callback onInit;
			Callback onPresent;
		};

		virtual ITexture2D* CreateTexture() = 0;
		virtual HWND GetWindow() = 0;
		virtual POINT Dimension() = 0;

		virtual uint32_t Width() { return (uint32_t)this->Dimension().x; };
		virtual uint32_t Height() { return (uint32_t)this->Dimension().y; };

		virtual bool IsSupported(DXGI_FORMAT format);

		virtual bool RequiresGammaCorrection() { return false; };
		virtual DXGI_FORMAT GetCorrectedFormat(DXGI_FORMAT format) 
		{
			return (RequiresGammaCorrection() ? ITexture2D::GetGammaCorrection(format) : format);
		};

		// Static methods
		static IRenderer* Singleton;
		static AI_DECL IRenderer* GetSingleton();
		static AI_DECL void Enqueue(Container &container);

#ifdef AYRIA_INTERFACE_MAIN
		struct PrivateContainer : Container
		{
			bool initialized;

			PrivateContainer(Container& container) : initialized(false)
			{
				this->onInit = container.onInit;
				this->onPresent = container.onPresent;
			}
		};

		static std::mutex Mutex;
		static std::vector<ITexture2D*> Textures;
		static std::vector<PrivateContainer> Queue;

		static void Init_D3D9(IDirect3DDevice9* pDevice);
		static void Init_DXGI(IDXGISwapChain* pSwapChain);
		static void Init_GL(HDC handle);
		static void Initialize(IRenderer* instance);
		static void StoreTexture(ITexture2D* pTexture);
		static void RemoveTexture(ITexture2D* pTexture);
		static void Present();

		static void EnumTextures(std::function<void(ITexture2D*)> callback);

		template <typename T> static T* GetSingleton() { return reinterpret_cast<T*>(GetSingleton()); };
#endif
	};
}