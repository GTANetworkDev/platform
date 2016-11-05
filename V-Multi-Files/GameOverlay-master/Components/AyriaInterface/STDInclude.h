#pragma once

#define _CRT_SECURE_NO_WARNINGS
#define WIN32_LEAN_AND_MEAN
#include <windows.h>

// C/C++ headers
#include <thread>
#include <vector>
#include <mutex>
#include <functional>

// custom headers
#include "Utils\Utils.h"
#include "Utils\Hooking.h"
#include "Utils\DummyWindow.h"

// DirectX headers emit tons of warnings
// I guess we don't have to care about them, or do we?
#pragma warning( push )
#pragma warning(disable : 4005 ) // Macro redefinition
#pragma warning(disable : 4838 ) // Conversion requires a narrowing conversion

// DirectX stuff
#define INITGUID
#define DIRECTINPUT_HEADER_VERSION  0x0800
#include <d3d9.h>
#include <ddraw.h>
#include <dinput.h>
#include <d3dx9core.h>
#include <DXGI1_2.h>
#include <d3d11.h>
#include <D3DX11async.h>
#include <d3dx11Effect.h>
#include <d3d10.h>
#include <d3dx10.h>
#include <Gdiplus.h>
#include <xnamath.h>

#define GLEW_STATIC
#include <GL\glew.h>
#pragma warning( pop ) 

#pragma comment(lib, "d3d9.lib")
#pragma comment(lib, "ddraw.lib")
#pragma comment(lib, "d3dx9.lib")
#pragma comment(lib, "d3d10.lib")
#pragma comment(lib, "d3d11.lib")
#pragma comment(lib, "D3DX10.lib")
#pragma comment(lib, "D3DX11.lib")
#pragma comment(lib, "Effects11.lib")
#pragma comment(lib, "d3dcompiler.lib")
#pragma comment(lib, "OpenGL32.lib")
#pragma comment(lib, "glew32s.lib")

#define AYRIA_INTERFACE_MAIN
#include <AyriaInterface\Main.h>

#include "Texture\ITexture2D9.h"
#include "Texture\ITexture2D10.h"
#include "Texture\ITexture2D11.h"
#include "Texture\ITexture2DGL.h"

#include "Interface\IRenderer9.h"
#include "Interface\IRenderer10.h"
#include "Interface\IRenderer11.h"
#include "Interface\IRendererGL.h"

#include "Hooks\DirectXC.h"
#include "Hooks\DirectX9.h"
#include "Hooks\DXGI2.h"
#include "Hooks\OpenGL.h"
