#ifdef AYRIA_INTERFACE_MAIN
#define AI_DECL __declspec(dllexport)
#else
#define AI_DECL __declspec(dllimport)
#pragma comment(lib, "Interface.lib")
#endif

#include "ITexture2D.h"
#include "IRenderer.h"
#include "IInput.h"

void CreateCEFInstance();
void InitializeCEF();
