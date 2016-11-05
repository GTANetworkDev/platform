#include "..\STDInclude.h"

namespace Ayria
{
	ITexture2DGL::ITexture2DGL() : Callback(0)
	{
		this->ResetResources();
	}

	ITexture2DGL::ITexture2DGL(HDC hdc) : ITexture2DGL()
	{
		this->Initialize(hdc);
	}

	ITexture2DGL::~ITexture2DGL()
	{
		this->ReleaseResources();

		if (this->Callback)
		{
			this->Callback(this);
		}
	}

	void ITexture2DGL::ReleaseResources()
	{
		this->Hdc = NULL;

		if (this->Texture /*&& glIsTexture(this->Texture) == GL_TRUE*/)
		{
			glDeleteTextures(1, &this->Texture);
			this->Texture = NULL;
		}

		if (this->Program)
		{
			glDeleteProgram(this->Program);
			this->Program = NULL;
		}
	}

	void ITexture2DGL::ResetResources()
	{
		this->Hdc = NULL;
		this->Texture = NULL;
		this->Program = NULL;
		this->Format = DXGI_FORMAT_UNKNOWN;
	}

	void ITexture2DGL::RecreateTextureIfLost()
	{
		if (glIsTexture(this->Texture) == GL_FALSE)
		{
			this->Create(this->Width, this->Height, this->Format, NULL);
		}
	}

	bool ITexture2DGL::Create(std::string file)
	{
		// Not supported
		return false;
	}

	bool ITexture2DGL::Create(uint32_t width, uint32_t height, DXGI_FORMAT format, const void* buffer)
	{
		this->Format = format;
		return this->Create(width, height, ITexture2D::GetGLFormat(format), buffer);
	}

	bool ITexture2DGL::Create(uint32_t width, uint32_t height, GLenum format, const void* buffer)
	{
		if (this->Texture /*&& glIsTexture(this->Texture) == GL_TRUE*/)
		{
			glDeleteTextures(1, &this->Texture);
			this->Texture = NULL;
		}

		if (this->Program)
		{
			glDeleteProgram(this->Program);
			this->Program = NULL;
		}

		if (!format) return false;

		this->Width = width;
		this->Height = height;

		GLint texture2d;
		glGetIntegerv(GL_TEXTURE_BINDING_2D, &texture2d);

		int alignment = 0;
		glGetIntegerv(GL_UNPACK_ALIGNMENT, &alignment);
		glPixelStorei(GL_UNPACK_ALIGNMENT, 1);

		glGenTextures(1, &this->Texture);
		glBindTexture(GL_TEXTURE_2D, this->Texture);

		glTexParameteri(GL_TEXTURE_2D, GL_TEXTURE_MIN_FILTER, GL_LINEAR);
		glTexParameteri(GL_TEXTURE_2D, GL_TEXTURE_MAG_FILTER, GL_LINEAR);

		glTexParameteri(GL_TEXTURE_2D, GL_TEXTURE_WRAP_S, GL_CLAMP);
		glTexParameteri(GL_TEXTURE_2D, GL_TEXTURE_WRAP_T, GL_CLAMP);

		glTexImage2D(GL_TEXTURE_2D, 0, ITexture2DGL::GetInternalPixelFormat(format), this->Width, this->Height, 0, format, GL_UNSIGNED_BYTE, buffer); 
		glBindTexture(GL_TEXTURE_2D, texture2d);

		glPixelStorei(GL_UNPACK_ALIGNMENT, alignment);

		// Create shaders
		GLint v_shader = glCreateShader(GL_VERTEX_SHADER);
		GLint f_shader = glCreateShader(GL_FRAGMENT_SHADER);

		GLchar* v_shader_src = 
			"void main(void)"
			"{"
			"	gl_TexCoord[0] = gl_MultiTexCoord0;"
			"	gl_Position = gl_ModelViewProjectionMatrix * gl_Vertex;"
			"}";

		GLchar* f_shader_src = 
			"uniform sampler2D tex_sampler;"
			"void main(void)"
			"{"
			"	gl_FragColor = texture2D(tex_sampler,gl_TexCoord[0].st);"
			"}";

		glShaderSource(v_shader, 1, &v_shader_src, 0);
		glShaderSource(f_shader, 1, &f_shader_src, 0);
		glCompileShader(v_shader);
		glCompileShader(f_shader);

		this->Program = glCreateProgram();
		glAttachShader(this->Program, v_shader);
		glAttachShader(this->Program, f_shader);
		glLinkProgram(this->Program);

		glDeleteShader(v_shader);
		glDeleteShader(f_shader);

		return (!buffer || this->Update(buffer));
	}

	bool ITexture2DGL::Resize(uint32_t width, uint32_t height)
	{
		bool success = false;

		if (this->Width != width || this->Height != height)
		{
			if (this->Texture)
			{
				success = this->Create(width, height, this->Format);
			}
		}

		return success;
	}

	bool ITexture2DGL::Update(const void* buffer)
	{
		this->RecreateTextureIfLost();

		GLint texture2d;
		glGetIntegerv(GL_TEXTURE_BINDING_2D, &texture2d);

		glBindTexture(GL_TEXTURE_2D, this->Texture);
		glTexSubImage2D(GL_TEXTURE_2D, 0, 0, 0, this->Width, this->Height, ITexture2D::GetGLFormat(this->Format), GL_UNSIGNED_BYTE, buffer);

		glBindTexture(GL_TEXTURE_2D, texture2d);

		return true;
	}

	bool ITexture2DGL::Initialize(HDC hdc)
	{
		this->ReleaseResources();

		if (!hdc) return false;

		this->Hdc = hdc;

		return true;
	}

	void ITexture2DGL::Draw(int32_t x, int32_t y, COLORREF color)
	{
		this->Draw(x, y, this->Width, this->Height, color);
	}

	void ITexture2DGL::Draw(int32_t x, int32_t y, uint32_t width, uint32_t height, COLORREF color)
	{
		this->RecreateTextureIfLost();

		RECT rect;
		GetClientRect(WindowFromDC(this->Hdc), &rect);

		uint32_t sWidth = rect.right - rect.left;
		uint32_t sHeight = rect.bottom - rect.top;

		glPushClientAttrib(GL_CLIENT_ALL_ATTRIB_BITS);
		glPushAttrib(GL_ALL_ATTRIB_BITS);

		GLint id;
		glGetIntegerv(GL_CURRENT_PROGRAM, &id);

		glUseProgram(this->Program);
		
		glMatrixMode(GL_PROJECTION);
		glPushMatrix();
		glLoadIdentity();
		glOrtho(0.0, (double)sWidth, (double)sHeight, 0.0, -1.0, 1.0);
		glTranslatef(0.0, 0.0, 0.0);
		glMatrixMode(GL_MODELVIEW);
		glPushMatrix();
		glLoadIdentity();

		glEnable(GL_TEXTURE_2D);
 		glDisable(GL_LIGHTING);
		glEnable(GL_BLEND);
		glBlendFunc(GL_SRC_ALPHA, GL_ONE_MINUS_SRC_ALPHA);

 		glColor4d(GetRValue(color) / 255.0, GetGValue(color) / 255.0, GetBValue(color) / 255.0, (LOBYTE((color) >> 24)) / 255.0);

		GLint texture2d;
		glGetIntegerv(GL_TEXTURE_BINDING_2D, &texture2d);
 		glBindTexture(GL_TEXTURE_2D, this->Texture);
 
		glBegin(GL_QUADS);
		glTexCoord2i(0, 0); glVertex3i(x, y, 0);
		glTexCoord2i(0, 1); glVertex3i(x, (height + y), 0);
		glTexCoord2i(1, 1); glVertex3i((width + x), (height + y), 0);
		glTexCoord2i(1, 0); glVertex3i((width + x), y, 0);
		glEnd();

		glBindTexture(GL_TEXTURE_2D, texture2d);

 		glPopMatrix();
		glMatrixMode(GL_PROJECTION);
		glPopMatrix();
		glMatrixMode(GL_MODELVIEW);

		glUseProgram(id);

		glPopAttrib();
		glPopClientAttrib();
	}

	bool ITexture2DGL::IsInitialized()
	{
		return (this->Hdc != NULL);
	}

	bool ITexture2DGL::IsLoaded()
	{
		return (this->Texture != NULL);
	}

	void ITexture2DGL::OnDestroy(ITexture2D::OnDestroyCallback callback)
	{
		this->Callback = callback;
	}

	GLuint ITexture2DGL::GetInternalPixelFormat(GLenum format)
	{
		switch (format)
		{
		case GL_RGBA: return GL_RGBA8;
		case GL_RGB:  return GL_RGB8;
		}

		return 0;
	}
}