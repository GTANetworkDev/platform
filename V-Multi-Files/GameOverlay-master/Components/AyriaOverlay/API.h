#pragma once

#include "APIClass.h"

namespace CEFAPI
{
	extern "C" __declspec(dllexport) void Crash();
	extern "C" __declspec(dllexport) void StartCEF();
	extern "C" __declspec(dllexport) void LoadURL(std::string url);
	extern "C" __declspec(dllexport) void ReloadPageIgnoreCache();
	extern "C" __declspec(dllexport) void GoBack();
	extern "C" __declspec(dllexport) void GoForward();
	extern "C" __declspec(dllexport) void ExecuteJavaScript(std::string  code);
	extern  __declspec(dllexport) std::vector<eventTrigger> GetTriggerEvent();
	extern  __declspec(dllexport) void ResetTriggerEvent();
	extern "C" __declspec(dllexport) void ShowCursor(bool value);
	extern "C" __declspec(dllexport) void KeyHandler(bool value);
	extern "C" __declspec(dllexport) void Focus(bool value);
	extern "C" __declspec(dllexport) void Reload();
}
