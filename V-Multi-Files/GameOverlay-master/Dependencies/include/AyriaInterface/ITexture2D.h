// DirectX headers emit tons of warnings
// I guess we don't have to care about them, or do we?
#pragma warning( push )
#pragma warning(disable : 4005 ) // Macro redefinition
#pragma warning(disable : 4838 ) // Conversion requires a narrowing conversion

// DirectX stuff
#define INITGUID
#include <DXGI1_2.h>
#include <d3d9.h>
#include <d3d11.h>
#include <xnamath.h>

#include <GL\GL.h>
#pragma warning( pop ) 

#define ResetResource(resource) resource = NULL
#define ReleaseResource(resource) if(resource) { resource->Release(); ResetResource(resource); }

namespace Ayria
{
	class ITexture2D
	{
	protected:
		struct Vertex
		{
			Vertex() {}
			Vertex(float x, float y, float z, float u, float v, COLORREF col) : pos(x, y, z), texCoord(u, v), color(col) {}

			XMFLOAT3 pos;
			XMFLOAT2 texCoord;
			COLORREF color;
		};

		typedef void(*OnDestroyCallback)(ITexture2D*);

	public:
		enum Type
		{
			TEXTURE_UNDEFINED = 0,
			TEXTURE_D3D9,
			TEXTURE_D3D10,
			TEXTURE_D3D11,
			TEXTURE_GL,
		};

		virtual ~ITexture2D() {};

		template <typename TextureInterface>
		TextureInterface* As() { return reinterpret_cast<TextureInterface*>(this); };

		virtual Type GetType() { return TEXTURE_UNDEFINED; };

		virtual bool Create(std::string file) = 0;
		virtual bool Create(uint32_t width, uint32_t height, DXGI_FORMAT format, const void* buffer = NULL) = 0;
		virtual bool Update(const void* buffer) = 0;

		virtual bool Resize(uint32_t width, uint32_t height) = 0;

		virtual DXGI_FORMAT GetFormat() = 0;

		virtual void Draw(int32_t x, int32_t y, uint32_t width, uint32_t height, COLORREF color = -1) = 0;
		virtual void Draw(int32_t x, int32_t y, COLORREF color = -1) = 0;

		virtual uint32_t GetWidth() = 0;
		virtual uint32_t GetHeight() = 0;

		virtual bool IsInitialized() = 0;
		virtual bool IsLoaded() = 0;

		virtual bool IsReady()
		{
			return (this->IsInitialized() && this->IsLoaded());
		}

		virtual void OnDestroy(OnDestroyCallback callback) = 0;

		static COLORREF Color(uint8_t r = -1, uint8_t g = -1, uint8_t b = -1, uint8_t a = -1)
		{
			return RGB(r, g, b) | (a << 24);
		}

		static D3DFORMAT GetD3DFormat(DXGI_FORMAT format)
		{
			switch (format)
			{
			case DXGI_FORMAT_B8G8R8A8_UNORM:      return D3DFMT_A8R8G8B8;
			case DXGI_FORMAT_B8G8R8A8_UNORM_SRGB: return D3DFMT_A8R8G8B8;
			case DXGI_FORMAT_B8G8R8X8_UNORM:      return D3DFMT_X8R8G8B8;
			case DXGI_FORMAT_B8G8R8X8_UNORM_SRGB: return D3DFMT_X8R8G8B8;
			case DXGI_FORMAT_R8G8B8A8_UNORM:      return D3DFMT_A8B8G8R8;
			case DXGI_FORMAT_R8G8B8A8_UNORM_SRGB: return D3DFMT_A8B8G8R8;
			}

			return D3DFMT_UNKNOWN;
		}

		static DXGI_FORMAT GetGammaCorrection(DXGI_FORMAT format)
		{
			switch (format)
			{
			case DXGI_FORMAT_B8G8R8A8_UNORM:      return DXGI_FORMAT_B8G8R8A8_UNORM_SRGB;
			case DXGI_FORMAT_B8G8R8X8_UNORM:      return DXGI_FORMAT_B8G8R8X8_UNORM_SRGB;
			case DXGI_FORMAT_R8G8B8A8_UNORM:      return DXGI_FORMAT_R8G8B8A8_UNORM_SRGB;

			case DXGI_FORMAT_BC1_UNORM:           return DXGI_FORMAT_BC1_UNORM_SRGB;
			case DXGI_FORMAT_BC2_UNORM:           return DXGI_FORMAT_BC2_UNORM_SRGB;
			case DXGI_FORMAT_BC3_UNORM:           return DXGI_FORMAT_BC3_UNORM_SRGB;
			case DXGI_FORMAT_BC7_UNORM:           return DXGI_FORMAT_BC7_UNORM_SRGB;
			}

			return format;
		}

		static GLenum GetGLFormat(DXGI_FORMAT format)
		{
			switch (format)
			{
			case DXGI_FORMAT_R8G8B8A8_UNORM:      return GL_RGBA;
			case DXGI_FORMAT_R8G8B8A8_UNORM_SRGB: return GL_RGBA;
			}

			return 0;
		}
	};
}
