#include "..\STDInclude.h"

namespace Ayria
{
	ITexture2D10::ITexture2D10() : Callback(0)
	{
		this->ResetResources();
	}

	ITexture2D10::ITexture2D10(ID3D10Device* pDevice) : ITexture2D10()
	{
		this->Initialize(pDevice);
	}

	ITexture2D10::~ITexture2D10()
	{
		this->ReleaseResources();

		if (this->Callback)
		{
			this->Callback(this);
		}
	}

	void ITexture2D10::ReleaseResources()
	{
		ReleaseResource(this->Effect);
		ReleaseResource(this->Texture);
		ReleaseResource(this->ShaderResourceView);
		ReleaseResource(this->IndexBuffer);
		ReleaseResource(this->VertexBuffer);
		ReleaseResource(this->InputLayout);
		ReleaseResource(this->ShaderBuffer);
		ReleaseResource(this->BlendState);
		ReleaseResource(this->Device);
	}

	void ITexture2D10::ResetResources()
	{
		ResetResource(this->Effect);
		ResetResource(this->Texture);
		ResetResource(this->ShaderResourceView);
		ResetResource(this->IndexBuffer);
		ResetResource(this->VertexBuffer);
		ResetResource(this->InputLayout);
		ResetResource(this->ShaderBuffer);
		ResetResource(this->BlendState);
		ResetResource(this->Device);

		this->Format = DXGI_FORMAT_UNKNOWN;
	}

	bool ITexture2D10::Create(std::string file)
	{
		// Release old data
		ReleaseResource(this->Texture);
		ReleaseResource(this->ShaderResourceView);

		// Create ShaderResourceView from file
		if (FAILED(D3DX10CreateShaderResourceViewFromFileA(this->Device, file.c_str(), NULL, NULL, &this->ShaderResourceView, NULL)) || !this->ShaderResourceView)
		{
			ReleaseResource(this->ShaderResourceView);
			return false;
		}


		// Get ITexture2D10 from ShaderResourceView
		this->ShaderResourceView->GetResource(reinterpret_cast<ID3D10Resource**>(&this->Texture));
		if (!this->Texture)
		{
			return false;
		}

		// Set local texture dimension
		D3D10_TEXTURE2D_DESC desc;
		this->Texture->GetDesc(&desc);

		this->Width = desc.Width;
		this->Height = desc.Height;

		return true;
	}

	bool ITexture2D10::Create(uint32_t width, uint32_t height, DXGI_FORMAT format, const void* buffer)
	{
		// Release old data
		ReleaseResource(this->Texture);
		ReleaseResource(this->ShaderResourceView);

		this->Format = format;
		if (!this->Device) return false;

		// Create ITexture2D10
		D3D10_TEXTURE2D_DESC desc = { 0 };
		desc.Width = width;
		desc.Height = height;
		desc.MipLevels = desc.ArraySize = 1;
		desc.Format = format;
		desc.SampleDesc.Count = 1;
		desc.Usage = D3D10_USAGE_DYNAMIC;
		desc.BindFlags = D3D10_BIND_SHADER_RESOURCE;
		desc.CPUAccessFlags = D3D10_CPU_ACCESS_WRITE;
		desc.MiscFlags = 0;


		if (FAILED(this->Device->CreateTexture2D(&desc, NULL, &this->Texture)) || !this->Texture)
		{
			ReleaseResource(this->Texture);
			return false;
		}

		D3D10_SHADER_RESOURCE_VIEW_DESC srvDesc;
		srvDesc.Format = format;
		srvDesc.ViewDimension = D3D10_SRV_DIMENSION_TEXTURE2D;
		srvDesc.Texture2D.MipLevels = 1;
		srvDesc.Texture2D.MostDetailedMip = 0;

		if (FAILED(this->Device->CreateShaderResourceView(this->Texture, &srvDesc, &this->ShaderResourceView)) || !this->ShaderResourceView)
		{
			ReleaseResource(this->ShaderResourceView);
			return false;
		}

		this->Width = width;
		this->Height = height;

		return (!buffer || this->Update(buffer));
	}

	bool ITexture2D10::Resize(uint32_t width, uint32_t height)
	{
		bool success = false;

		if (this->Width != width || this->Height != height)
		{
			if (this->Texture)
			{
				D3D10_TEXTURE2D_DESC desc;
				this->Texture->GetDesc(&desc);

				if (desc.Usage == D3D10_USAGE_DYNAMIC)
				{
					success = this->Create(width, height, desc.Format);
				}
			}
		}

		return success;
	}

	bool ITexture2D10::Update(const void* buffer)
	{
		bool success = false;

		// Check texture and buffer validity
		if (buffer && this->Texture)
		{
			D3D10_TEXTURE2D_DESC desc;
			this->Texture->GetDesc(&desc);

			// Check if updating is allowed
			if (desc.Usage == D3D10_USAGE_DYNAMIC && (desc.CPUAccessFlags & D3D10_CPU_ACCESS_WRITE) == D3D10_CPU_ACCESS_WRITE)
			{
				// Map texture buffer
				D3D10_MAPPED_TEXTURE2D texmap;
				if (SUCCEEDED(this->Texture->Map(0, D3D10_MAP_WRITE_DISCARD, 0, &texmap)))
				{
					// Copy new data into the buffer
					int bbp = (int)(texmap.RowPitch / this->Width);
					int bpr = this->Width * bbp;

					for (uint32_t i = 0; i < this->Height; i++)
					{
						memcpy((char*)texmap.pData + (i * texmap.RowPitch), (char*)buffer + i * bpr, bpr);
					}

					// Unmap texture
					this->Texture->Unmap(0);

					success = true;
				}
			}
		}

		return success;
	}

	bool ITexture2D10::Initialize(ID3D10Device* pDevice)
	{
		this->ReleaseResources();

		if (!pDevice) return false;

		this->Device = pDevice;
		this->Device->AddRef();

		return this->CreateResources();
	}

	bool ITexture2D10::CreateResources()
	{
		// Release used resources
		ReleaseResource(this->Effect);
		ReleaseResource(this->ShaderBuffer);
		ReleaseResource(this->VertexBuffer);
		ReleaseResource(this->InputLayout);
		ReleaseResource(this->IndexBuffer);

		// Effect data
		const char EffectSrc[] =
			"Texture2D SpriteTex;"
			"SamplerState samLinear {"
			"     Filter = MIN_MAG_MIP_LINEAR;"
			"     AddressU = Wrap;"
			"     AddressV = Wrap;"
			"};"
			"struct VertexIn {"
			"     float3 PosNdc : POSITION;"
			"     float2 Tex    : TEXCOORD;"
			"     float4 Color  : COLOR;"
			"};"
			"struct VertexOut {"
			"     float4 PosNdc : SV_POSITION;"
			"     float2 Tex    : TEXCOORD;"
			"     float4 Color  : COLOR;"
			"};"
			"VertexOut VS(VertexIn vin) {"
			"     VertexOut vout;"
			"     vout.PosNdc = float4(vin.PosNdc, 1.0f);"
			"     vout.Tex    = vin.Tex;"
			"     vout.Color  = vin.Color;"
			"     return vout;"
			"};"
			"float4 PS(VertexOut pin) : SV_Target {"
			"     return pin.Color*SpriteTex.Sample(samLinear, pin.Tex);"
			"};"
			"technique10 SpriteTech {"
			"     pass P0 {"
			"         SetVertexShader( CompileShader( vs_4_0, VS() ) );"
			"         SetGeometryShader( NULL );"
			"         SetPixelShader( CompileShader( ps_4_0, PS() ) );"
			"     }"
			"}";

		// Input layout data
		D3D10_INPUT_ELEMENT_DESC layout[] =
		{
			{ "POSITION", 0, DXGI_FORMAT_R32G32B32_FLOAT, 0, 0, D3D10_INPUT_PER_VERTEX_DATA, 0 },
			{ "TEXCOORD", 0, DXGI_FORMAT_R32G32_FLOAT, 0, 12, D3D10_INPUT_PER_VERTEX_DATA, 0 },
			{ "COLOR", 0, DXGI_FORMAT_R8G8B8A8_UNORM, 0, 20, D3D10_INPUT_PER_VERTEX_DATA, 0 }
		};

		// Compile Effect
		if (FAILED(D3DX10CompileFromMemory(EffectSrc, sizeof(EffectSrc), 0, 0, 0, "SpriteTech", "fx_4_0", 0, 0, 0, &this->ShaderBuffer, 0, 0)) || !this->ShaderBuffer)
		{
			OutputDebugStringA("Penis1");
			ReleaseResource(this->ShaderBuffer);
			return false;
		}

		// Create ShaderBuffer from compiled Effect
		if (FAILED(D3D10CreateEffectFromMemory(this->ShaderBuffer->GetBufferPointer(), this->ShaderBuffer->GetBufferSize(), 0, this->Device, NULL, &this->Effect)) || !this->Effect)
		{
			OutputDebugStringA("Penis2");
			ReleaseResource(this->Effect);
			return false;
		}

		ReleaseResource(this->ShaderBuffer);

		D3D10_PASS_DESC passDesc;
		this->EffectTechnique = this->Effect->GetTechniqueByName("SpriteTech");
		this->EffectShaderResourceVariable = this->Effect->GetVariableByName("SpriteTex")->AsShaderResource();
		this->EffectTechnique->GetPassByIndex(0)->GetDesc(&passDesc);

		// Create InputLayout
		if (FAILED(this->Device->CreateInputLayout(layout, ARRAYSIZE(layout), passDesc.pIAInputSignature, passDesc.IAInputSignatureSize, &this->InputLayout)) || !this->InputLayout)
		{
			ReleaseResource(this->InputLayout);
			return false;
		}

		// Create IndexBuffer
		DWORD indices[] =
		{
			0, 1, 2,
			0, 2, 3,
		};

		D3D10_BUFFER_DESC indexBufferDesc;
		ZeroMemory(&indexBufferDesc, sizeof(indexBufferDesc));
		indexBufferDesc.Usage = D3D10_USAGE_DEFAULT;
		indexBufferDesc.ByteWidth = sizeof(DWORD) * 2 * 3;
		indexBufferDesc.BindFlags = D3D10_BIND_INDEX_BUFFER;
		indexBufferDesc.CPUAccessFlags = 0;
		indexBufferDesc.MiscFlags = 0;

		D3D10_SUBRESOURCE_DATA iinitData;
		iinitData.pSysMem = indices;

		if (FAILED(this->Device->CreateBuffer(&indexBufferDesc, &iinitData, &this->IndexBuffer)) || !this->IndexBuffer)
		{
			ReleaseResource(this->IndexBuffer);
			return false;
		}

		// Initialize vertex buffer
		D3D10_BUFFER_DESC vertexBufferDesc;
		ZeroMemory(&vertexBufferDesc, sizeof(vertexBufferDesc));
		vertexBufferDesc.Usage = D3D10_USAGE_DYNAMIC;
		vertexBufferDesc.ByteWidth = sizeof(ITexture2D::Vertex) * 4;
		vertexBufferDesc.BindFlags = D3D10_BIND_VERTEX_BUFFER;
		vertexBufferDesc.CPUAccessFlags = D3D10_CPU_ACCESS_WRITE;

		if (FAILED(this->Device->CreateBuffer(&vertexBufferDesc, NULL, &this->VertexBuffer)) || !this->VertexBuffer)
		{
			ReleaseResource(this->VertexBuffer);
			return false;
		}

		// Initialize blend state
		D3D10_BLEND_DESC blendStateDesc;
		ZeroMemory(&blendStateDesc, sizeof(blendStateDesc));
		blendStateDesc.BlendEnable[0] = TRUE;
		blendStateDesc.BlendOp = D3D10_BLEND_OP_ADD;
		blendStateDesc.SrcBlend = D3D10_BLEND_SRC_ALPHA;
		blendStateDesc.DestBlend = D3D10_BLEND_INV_SRC_ALPHA;
		blendStateDesc.BlendOpAlpha = D3D10_BLEND_OP_ADD;
		blendStateDesc.SrcBlendAlpha = D3D10_BLEND_ONE;
		blendStateDesc.DestBlendAlpha = D3D10_BLEND_ZERO;
		blendStateDesc.RenderTargetWriteMask[0] = D3D10_COLOR_WRITE_ENABLE_ALL;

		if (FAILED(this->Device->CreateBlendState(&blendStateDesc, &this->BlendState)) || !this->BlendState)
		{
			ReleaseResource(this->BlendState);
			return false;
		}

		return true;
	}

	bool ITexture2D10::TranslateVertices(int32_t x, int32_t y, uint32_t width, uint32_t height, COLORREF color)
	{
		UINT numViewports = 1;
		D3D10_VIEWPORT viewport;
		void* mappedData;

		// Calculate VertexBuffer
		if (!this->VertexBuffer || FAILED(this->VertexBuffer->Map(D3D10_MAP_WRITE_DISCARD, 0, &mappedData))) return false;

		this->Device->RSGetViewports(&numViewports, &viewport);
		ITexture2D::Vertex* v = reinterpret_cast<ITexture2D::Vertex*>(mappedData);

		v[0] = ITexture2D::Vertex((2.0f * (float)x / viewport.Width - 1.0f), (1.0f - 2.0f * (float)y / viewport.Height), 0.5f, 0.0f, 0.0f, color); // Vertex 1
		v[1] = ITexture2D::Vertex((2.0f * (float)(x + width) / viewport.Width - 1.0f), (1.0f - 2.0f * (float)y / viewport.Height), 0.5f, 1.0f, 0.0f, color); // Vertex 2
		v[2] = ITexture2D::Vertex((2.0f * (float)(x + width) / viewport.Width - 1.0f), (1.0f - 2.0f * (float)(y + height) / viewport.Height), 0.5f, 1.0f, 1.0f, color); // Vertex 3
		v[3] = ITexture2D::Vertex((2.0f * (float)x / viewport.Width - 1.0f), (1.0f - 2.0f * (float)(y + height) / viewport.Height), 0.5f, 0.0f, 1.0f, color); // Vertex 4

		this->VertexBuffer->Unmap();

		return true;
	}

	void ITexture2D10::Draw(int32_t x, int32_t y, COLORREF color)
	{
		this->Draw(x, y, this->Width, this->Height, color);
	}

	void ITexture2D10::Draw(int32_t x, int32_t y, uint32_t width, uint32_t height, COLORREF color)
	{
		UINT offset = 0;
		UINT stride = sizeof(ITexture2D::Vertex);

		// Backup resources
		// Giving a variable name is necessary, so that the destructor is called properly
		ITexture2D10::BackupContainer 👻(this->Device);

		// Translate to screen
		this->TranslateVertices(x, y, width, height, color);

		// Draw texture
		this->Device->OMSetBlendState(this->BlendState, NULL, 0xffffffff);
		this->Device->IASetPrimitiveTopology(D3D10_PRIMITIVE_TOPOLOGY_TRIANGLELIST);
		this->EffectShaderResourceVariable->SetResource(this->ShaderResourceView);
		this->EffectTechnique->GetPassByIndex(0)->Apply(0);
		this->Device->IASetIndexBuffer(this->IndexBuffer, DXGI_FORMAT_R32_UINT, 0);
		this->Device->IASetVertexBuffers(0, 1, &this->VertexBuffer, &stride, &offset);
		this->Device->IASetInputLayout(this->InputLayout);
		this->Device->DrawIndexed(6, 0, 0);
	}

	bool ITexture2D10::IsInitialized()
	{
		return (this->Device != NULL);
	}

	bool ITexture2D10::IsLoaded()
	{
		return (this->Texture && this->ShaderResourceView);
	}

	void ITexture2D10::OnDestroy(ITexture2D::OnDestroyCallback callback)
	{
		this->Callback = callback;
	}

	ITexture2D10::BackupContainer::BackupContainer(ID3D10Device* device) : Device(device)
	{
		this->Device->AddRef();
		this->Device->PSGetShader(&PixelShader);
		this->Device->VSGetShader(&VertexShader);
		this->Device->IAGetInputLayout(&InputLayout);
		this->Device->IAGetPrimitiveTopology(&Topology);
		this->Device->PSGetShaderResources(0, 1, &ResourceView);
		this->Device->VSGetShaderResources(0, 1, &VertexResource);
		this->Device->IAGetVertexBuffers(0, 1, &VertexBuffer, &Stride, &Offset);
		this->Device->OMGetBlendState(&BlendState, BlendFactor, &SampleMask);
	}

	ITexture2D10::BackupContainer::~BackupContainer()
	{
		this->Device->OMSetBlendState(BlendState, BlendFactor, SampleMask);
		this->Device->IASetVertexBuffers(0, 1, &VertexBuffer, &Stride, &Offset);
		this->Device->VSSetShaderResources(0, 1, &VertexResource);
		this->Device->PSSetShaderResources(0, 1, &ResourceView);
		this->Device->IASetPrimitiveTopology(Topology);
		this->Device->IASetInputLayout(InputLayout);
		this->Device->VSSetShader(VertexShader);
		this->Device->PSSetShader(PixelShader);
		this->Device->Release();
	}
}
