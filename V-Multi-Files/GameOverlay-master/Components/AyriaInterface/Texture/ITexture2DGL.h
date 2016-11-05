#define glSet(cap, val) (val ? glEnable(cap) : glDisable(cap));

namespace Ayria
{
	class ITexture2DGL : public ITexture2D
	{
	private:
		GLuint      Texture;
		GLuint      Program;
		HDC         Hdc;
		DXGI_FORMAT Format;

		OnDestroyCallback  Callback;

		void ResetResources();

		bool Create(uint32_t width, uint32_t height, GLenum format, const void* buffer = NULL);
		void RecreateTextureIfLost(); // TODO: Is that safe? If our texture is broken, doesn't that cause a memory leak?

		static GLuint GetInternalPixelFormat(GLenum format);

	public:
		uint32_t Width;
		uint32_t Height;

		ITexture2DGL();
		ITexture2DGL(HDC hdc);
		virtual ~ITexture2DGL() override;

		virtual Type GetType() override { return TEXTURE_GL; };

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
		bool Initialize(HDC hdc);
	};
}
