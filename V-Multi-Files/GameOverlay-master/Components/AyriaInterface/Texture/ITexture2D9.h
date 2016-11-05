namespace Ayria
{
	class ITexture2D9 : public ITexture2D
	{
	private:
		ID3DXSprite*       Sprite;
		IDirect3DDevice9*  Device;
		IDirect3DTexture9* Texture;

		DXGI_FORMAT        Format;

		OnDestroyCallback  Callback;

		void ResetResources();
		bool CreateResources();

		bool Create(uint32_t width, uint32_t height, D3DFORMAT format, const void* buffer = NULL);

	public:
		uint32_t Width;
		uint32_t Height;

		ITexture2D9();
		ITexture2D9(IDirect3DDevice9* pDevice);
		virtual ~ITexture2D9() override;

		virtual Type GetType() override { return TEXTURE_D3D9; };

		virtual bool Create(std::string file) override;
		virtual bool Create(uint32_t width, uint32_t height, DXGI_FORMAT format, const void* buffer = NULL) override;
		virtual bool Update(const void* buffer) override;

		virtual bool Resize(uint32_t width, uint32_t height) override;

		virtual void Draw(int32_t x, int32_t y, uint32_t width, uint32_t height, COLORREF color = -1) override;
		virtual void Draw(int32_t x, int32_t y, COLORREF color = -1) override;

		virtual uint32_t GetWidth() override { return this->Width; };
		virtual uint32_t GetHeight() override { return this->Height; };

		virtual bool IsInitialized() override;
		virtual bool IsLoaded() override;

		virtual void OnDestroy(OnDestroyCallback callback) override;

		virtual DXGI_FORMAT GetFormat() override { return this->Format; };

		void ReleaseResources();
		bool Initialize(IDirect3DDevice9* pDevice);
		void Reinitialize(IDirect3DDevice9* pDevice);
	};
}
