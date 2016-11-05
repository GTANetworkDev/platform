#include "..\STDInclude.h"

namespace Ayria
{
	ITexture2D9::ITexture2D9() : Callback(0)
	{
		this->ResetResources();
	}

	ITexture2D9::ITexture2D9(IDirect3DDevice9* pDevice) : ITexture2D9()
	{
		this->Initialize(pDevice);
	}

	ITexture2D9::~ITexture2D9()
	{
		this->ReleaseResources();

		if (this->Callback)
		{
			this->Callback(this);
		}
	}

	void ITexture2D9::ReleaseResources()
	{
		ReleaseResource(this->Texture);
		ReleaseResource(this->Sprite);
		ReleaseResource(this->Device);
	}

	void ITexture2D9::ResetResources()
	{
		ResetResource(this->Texture);
		ResetResource(this->Sprite);
		ResetResource(this->Device);

		this->Format = DXGI_FORMAT_UNKNOWN;
	}

	bool ITexture2D9::Create(std::string file)
	{
		// Release old data
		ReleaseResource(this->Texture);

		if (FAILED(D3DXCreateTextureFromFileEx(this->Device, file.c_str(), D3DX_DEFAULT_NONPOW2, D3DX_DEFAULT_NONPOW2, D3DX_DEFAULT, 0, D3DFMT_UNKNOWN, D3DPOOL_MANAGED, D3DX_DEFAULT, D3DX_DEFAULT, 0, NULL, NULL, &this->Texture)) || !this->Texture)
		{
			ReleaseResource(this->Texture);
			return false;
		}

		D3DSURFACE_DESC desc;
		this->Texture->GetLevelDesc(0, &desc);

		this->Width = desc.Width;
		this->Height = desc.Height;

		return true;
	}

	bool ITexture2D9::Create(uint32_t width, uint32_t height, DXGI_FORMAT format, const void* buffer)
	{
		this->Format = format;
		return this->Create(width, height, ITexture2D::GetD3DFormat(format), buffer);
	}

	bool ITexture2D9::Create(uint32_t width, uint32_t height, D3DFORMAT format, const void* buffer)
	{
		// Release old data
		ReleaseResource(this->Texture);
		if (!this->Device) return false;

		if (this->Device->CreateTexture(width, height, 1, D3DUSAGE_DYNAMIC, format, D3DPOOL_DEFAULT, &this->Texture, NULL) != D3D_OK || !this->Texture)
		{
			ReleaseResource(this->Texture);
			return false;
		}

		this->Width = width;
		this->Height = height;

		return (!buffer || this->Update(buffer));
	}

	bool ITexture2D9::Resize(uint32_t width, uint32_t height)
	{
		bool success = false;

		if (this->Width != width || this->Height != height)
		{
			if (this->Texture)
			{
				D3DSURFACE_DESC desc;
				this->Texture->GetLevelDesc(0, &desc);

				if ((desc.Usage & D3DUSAGE_DYNAMIC) == D3DUSAGE_DYNAMIC)
				{
					success = this->Create(width, height, desc.Format);
				}
			}
		}

		return success;
	}

	bool ITexture2D9::Update(const void* buffer)
	{
		bool success = false;

		// Check texture and buffer validity
		if (buffer && this->Texture)
		{
			D3DSURFACE_DESC desc;
			this->Texture->GetLevelDesc(0, &desc);

			// Check if updating is allowed
			if ((desc.Usage & D3DUSAGE_DYNAMIC) == D3DUSAGE_DYNAMIC)
			{
				// Map texture buffer
				D3DLOCKED_RECT lockedRect;
				if (SUCCEEDED(this->Texture->LockRect(0, &lockedRect, NULL, 0)))
				{
					// Copy new data into the buffer
					int bbp = (int)(lockedRect.Pitch / this->Width);
					int bpr = this->Width * bbp;

					for (uint32_t i = 0; i < this->Height; i++)
					{
						memcpy((char*)lockedRect.pBits + (i * lockedRect.Pitch), (char*)buffer + i * bpr, bpr);
					}

					// Unmap texture
					this->Texture->UnlockRect(0);

					success = true;
				}
			}
		}
		return success;
	}

	bool ITexture2D9::Initialize(IDirect3DDevice9* pDevice)
	{
		this->ReleaseResources();

		if (!pDevice) return false;

		this->Device = pDevice;
		this->Device->AddRef();

		return this->CreateResources();
	}

	void ITexture2D9::Reinitialize(IDirect3DDevice9* pDevice)
	{
		this->Initialize(pDevice);
		this->Create(this->Width, this->Height, this->Format);
	}

	bool ITexture2D9::CreateResources()
	{
		// Release used resources
		ReleaseResource(this->Sprite);

		if (FAILED(D3DXCreateSprite(this->Device, &this->Sprite)) || !this->Sprite)
		{
			ReleaseResource(this->Sprite);
			return false;
		}

		return true;
	}

	void ITexture2D9::Draw(int32_t x, int32_t y, COLORREF color)
	{
		this->Draw(x, y, this->Width, this->Height, color);
	}

	// Width and height not needed yet
	void ITexture2D9::Draw(int32_t x, int32_t y, uint32_t width, uint32_t height, COLORREF color)
	{
		D3DXVECTOR3 position((float)x, (float)y, 0.0f);

		if (this->Sprite && this->Texture)
		{
			this->Sprite->Begin(D3DXSPRITE_ALPHABLEND);
			this->Sprite->Draw(this->Texture, NULL, NULL, &position, color);
			this->Sprite->End();
		}
	}

	bool ITexture2D9::IsInitialized()
	{
		return (this->Device != NULL);
	}

	bool ITexture2D9::IsLoaded()
	{
		return (this->Texture != NULL);
	}

	void ITexture2D9::OnDestroy(ITexture2D::OnDestroyCallback callback)
	{
		this->Callback = callback;
	}
}