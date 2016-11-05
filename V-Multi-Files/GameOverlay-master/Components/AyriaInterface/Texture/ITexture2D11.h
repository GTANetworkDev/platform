namespace Ayria
{
	class ITexture2D11 : public ITexture2D
	{
	private:
		ID3D11DeviceContext*                 Context;
		ID3D11Device*                        Device;
		ID3D11Texture2D*                     Texture;
		ID3D11ShaderResourceView*            ShaderResourceView;
		ID3D11Buffer*                        IndexBuffer;
		ID3D11Buffer*                        VertexBuffer;
		ID3D10Blob*                          ShaderBuffer;
		ID3D11InputLayout*                   InputLayout;
		ID3D11BlendState*                    BlendState;
		ID3DX11Effect*                       Effect;
		ID3DX11EffectTechnique*              EffectTechnique;
		ID3DX11EffectShaderResourceVariable* EffectShaderResourceVariable;

		DXGI_FORMAT                          Format;

		OnDestroyCallback                    Callback;

		void ResetResources();
		bool CreateResources();
		bool TranslateVertices(int32_t x, int32_t y, uint32_t width, uint32_t height, COLORREF color);

		class BackupContainer
		{
		private:
			UINT Offset;
			UINT Stride;
			UINT SampleMask;
			UINT VSClassInsts;
			UINT PSClassInsts;
			FLOAT BlendFactor[4];
			ID3D11Buffer* VertexBuffer;
			ID3D11BlendState* BlendState;
			ID3D11PixelShader* PixelShader;
			ID3D11InputLayout* InputLayout;
			D3D_PRIMITIVE_TOPOLOGY Topology;
			ID3D11VertexShader* VertexShader;
			ID3D11ClassInstance* VSClassInstance;
			ID3D11ClassInstance* PSClassInstance;
			ID3D11ShaderResourceView* ResourceView;
			ID3D11ShaderResourceView* VertexResource;

			ID3D11DeviceContext* Context;

		public:
			BackupContainer(ID3D11DeviceContext* context);
			~BackupContainer();
		};

	public:
		uint32_t Width;
		uint32_t Height;

		ITexture2D11();
		ITexture2D11(ID3D11Device* pDevice);
		virtual ~ITexture2D11() override;

		virtual Type GetType() override { return TEXTURE_D3D11; };

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
		bool Initialize(ID3D11Device* pDevice);
	};
}
