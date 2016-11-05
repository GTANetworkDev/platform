#include "..\STDInclude.h"

namespace Hook
{
	bool OpenGL::Initialized = false;
	Hook::Jump OpenGL::SwapBufferHook;

	bool OpenGL::Initialize()
	{
		// TODO: Hook wglSwapLayerBuffers as well
		OpenGL::SwapBufferHook.Uninstall();
		OpenGL::SwapBufferHook.Initialize(::SwapBuffers, OpenGL::SwapBuffers);
		OpenGL::SwapBufferHook.Install();

		return true;
	}

	bool OpenGL::Uninitialize()
	{
		OpenGL::Initialized = false;

		OpenGL::SwapBufferHook.Uninstall();

		return true;
	}

	BOOL WINAPI  OpenGL::SwapBuffers(HDC hdc)
	{
		if (!OpenGL::Initialized)
		{
			Ayria::IRenderer::Init_GL(hdc);
			OpenGL::Initialized = true;
		}

		Ayria::IRenderer::Present();

		OpenGL::SwapBufferHook.Uninstall();
		BOOL result = ::SwapBuffers(hdc);
		OpenGL::SwapBufferHook.Install();

		return result;
	}
}
