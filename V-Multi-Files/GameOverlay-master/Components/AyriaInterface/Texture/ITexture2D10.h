namespace Ayria
{
	class ITexture2D10 : public ITexture2D
	{
	private:
		ID3D10Device*                       Device;
		ID3D10Texture2D*                    Texture;
		ID3D10ShaderResourceView*           ShaderResourceView;
		ID3D10Buffer*                       IndexBuffer;
		ID3D10Buffer*                       VertexBuffer;
		ID3D10Blob*                         ShaderBuffer;
		ID3D10InputLayout*                  InputLayout;
		ID3D10BlendState*                   BlendState;
		ID3D10Effect*                       Effect;
		ID3D10EffectTechnique*              EffectTechnique;
		ID3D10EffectShaderResourceVariable* EffectShaderResourceVariable;

		DXGI_FORMAT                         Format;

		OnDestroyCallback                   Callback;

		void ResetResources();
		bool CreateResources();
		bool TranslateVertices(int32_t x, int32_t y, uint32_t width, uint32_t height, COLORREF color);

		class BackupContainer
		{
		private:
			UINT Offset;
			UINT Stride;
			UINT SampleMask;
			FLOAT BlendFactor[4];
			ID3D10Buffer* VertexBuffer;
			ID3D10BlendState* BlendState;
			ID3D10PixelShader* PixelShader;
			ID3D10InputLayout* InputLayout;
			D3D_PRIMITIVE_TOPOLOGY Topology;
			ID3D10VertexShader* VertexShader;
			ID3D10ShaderResourceView* ResourceView;
			ID3D10ShaderResourceView* VertexResource;

			ID3D10Device* Device;

		public:
			BackupContainer(ID3D10Device* device);
			~BackupContainer();
		};

	public:
		uint32_t Width;
		uint32_t Height;

		ITexture2D10();
		ITexture2D10(ID3D10Device* pDevice);
		virtual ~ITexture2D10() override;

		virtual Type GetType() override { return TEXTURE_D3D10; };

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
		bool Initialize(ID3D10Device* pDevice);
	};
}
