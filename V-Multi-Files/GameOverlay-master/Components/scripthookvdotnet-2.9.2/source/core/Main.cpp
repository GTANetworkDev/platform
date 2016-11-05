
/**
* Copyright (C) 2015 crosire
*
* This software is  provided 'as-is', without any express  or implied  warranty. In no event will the
* authors be held liable for any damages arising from the use of this software.
* Permission  is granted  to anyone  to use  this software  for  any  purpose,  including  commercial
* applications, and to alter it and redistribute it freely, subject to the following restrictions:
*
*   1. The origin of this software must not be misrepresented; you must not claim that you  wrote the
*      original  software. If you use this  software  in a product, an  acknowledgment in the product
*      documentation would be appreciated but is not required.
*   2. Altered source versions must  be plainly  marked as such, and  must not be  misrepresented  as
*      being the original software.
*   3. This notice may not be removed or altered from any source distribution.
*/

#include "ScriptDomain.hpp"
#include "Settings.hpp"

#include "Native.hpp"
#include "NativeMemory.hpp"
#include <STDInclude.h>

#include <atlstr.h>
using namespace System;
using namespace System::Runtime::InteropServices;
using namespace System::Reflection;
namespace WinForms = System::Windows::Forms;

ref struct ScriptHook
{
	static GTA::ScriptDomain ^Domain = nullptr;
	static WinForms::Keys ReloadKey = WinForms::Keys::None;
};


void Start()
{

	HCURSOR Cursor = LoadCursor(NULL, IDC_HAND);
	SetCursor(Cursor);
	ShowCursor(true);
	auto location = Assembly::GetExecutingAssembly()->Location;
	CString s2(location);

	IntPtr ptr = Marshal::StringToHGlobalUni(location);

	AddDllDirectory((LPCWSTR)s2);
	AddDllDirectory((LPCWSTR)ptr.ToPointer());

	Marshal::FreeHGlobal(ptr);

	//CEFAPI::LoadURL("http://v-multi.com/");
}
bool ManagedInit()
{
	if (!Object::ReferenceEquals(ScriptHook::Domain, nullptr))
	{
		GTA::ScriptDomain::Unload(ScriptHook::Domain);
	}

	auto location = Assembly::GetExecutingAssembly()->Location;


	CString s2(location);

	IntPtr ptr = Marshal::StringToHGlobalUni(location);

	AddDllDirectory((LPCWSTR)ptr.ToPointer());

	Marshal::FreeHGlobal(ptr);

	auto settings = GTA::ScriptSettings::Load(IO::Path::ChangeExtension(location, ".ini"));

	//ScriptHook::Domain = GTA::ScriptDomain::Load(IO::Path::Combine(IO::Path::GetDirectoryName(location), settings->GetValue(String::Empty, "ScriptsLocation", "scripts")));



	//ScriptHook::Domain = GTA::ScriptDomain::Load(IO::Path::Combine(IO::Path::GetDirectoryName(Reflection::Assembly::GetExecutingAssembly()->Location), "vmp"));

	ScriptHook::Domain = GTA::ScriptDomain::Load(IO::Path::Combine(IO::Path::GetDirectoryName(Reflection::Assembly::GetExecutingAssembly()->Location), "vmp"));

	ScriptHook::ReloadKey = settings->GetValue<WinForms::Keys>(String::Empty, "ReloadKey", WinForms::Keys::Insert);

	if (Object::ReferenceEquals(ScriptHook::Domain, nullptr))
	{
		return false;
	}

	ScriptHook::Domain->Start();

	return true;
}
bool ManagedTick()
{

	//CEFAPI::ShowCursor(true);
	if (ScriptHook::Domain->IsKeyPressed(ScriptHook::ReloadKey) && ScriptHook::Domain->IsKeyPressed(WinForms::Keys::F8))
	{
		CEFAPI::LoadURL("http://v-multi.com/cef2.html");
		return false;
	}

	ScriptHook::Domain->DoTick();

	return true;
}
void ManagedKeyboardMessage(int key, bool status, bool statusCtrl, bool statusShift, bool statusAlt)
{
	if (Object::ReferenceEquals(ScriptHook::Domain, nullptr))
	{
		return;
	}

	ScriptHook::Domain->DoKeyboardMessage(static_cast<WinForms::Keys>(key), status, statusCtrl, statusShift, statusAlt);
}

#pragma unmanaged

#include <Main.h>
#include <Windows.h>

bool sGameReloaded = false;
PVOID sMainFib = nullptr, sScriptFib = nullptr;
#pragma once

#define _CRT_SECURE_NO_WARNINGS
#define WIN32_LEAN_AND_MEAN
#include <windows.h>
#include <stdio.h>
#include <stdint.h>
#include <vector>
#include <math.h>

#ifdef _WIN64
#define PLATFORM_SHORTNAME "x64"
#elif _WIN32
#define PLATFORM_SHORTNAME "x86"
#else
#define PLATFORM_SHORTNAME "?"
#endif

#define AYRIA_LOADER_LIB "OverlayLoader.dll"

#include <Psapi.h>

#define DBG_CON

#ifdef DBG_CON
#define DBGPrint(...) printf(__VA_ARGS__);
#else
#define DBGPrint(...)
#endif

char* libs[] =
{
	"dinput8.dll",
	"xinput1_3.dll",
	"xinput9_1_0.dll",
	"gameoverlayrenderer.dll",
	"gameoverlayrenderer64.dll",
	"igo32.dll",
	"igo64.dll",

	// Might cause injection into browsers and other apps!
	"d3d9.dll",
	"d3d10.dll",
	"d3d11.dll",

	"OpenGL32.dll",
};

char* exceptions[] =
{
	"clover.exe",
	"devenv.exe",
	"firefox.exe",
	"chrome.exe",
	"steam.exe",
	"Netflix.exe",
	"GitHub.exe",
	"atom.exe",
	"dllhost.exe",
	"idaq.exe",
	"explorer.exe",
	"idaq64.exe",
	"notepad++.exe",
	"SearchUI.exe",
	"Discord.exe",
	"Skype.exe",
	"RzSynapse.exe",
	"Dropbox.exe",
	"PDapp.exe",
	"Telegram.exe",
	"CEPHtmlEngine.exe",
	"plugin-container.exe",
	"ApplicationFrameHost.exe",
	"SystemSettings.exe",
	"FlashPlayerPlugin_19_0_0_245.exe",
	"backgroundTaskHost.exe",
	"ScriptedSandbox64.exe",
	"GameOverlayUI.exe",
	"steamwebhelper.exe",
	"Twitter.Windows.exe",
	"AyriaOverlayProc.exe",
	"ShellExperienceHost.exe",
	"CefSharp.BrowserSubprocess.exe",
	"WWAHost.exe",
	"Origin.exe",
	"SkypeHost.exe",
};

std::string GetModuleDir()
{
	HMODULE hModule;
	char    cPath[MAX_PATH] = { 0 };
	GetModuleHandleExA(GET_MODULE_HANDLE_EX_FLAG_FROM_ADDRESS, (LPCSTR)GetModuleDir, &hModule);

	GetModuleFileNameA(hModule, cPath, MAX_PATH);
	std::string path = cPath;
	return path.substr(0, path.find_last_of("\\/"));
}

bool InjectLibrary(DWORD process, std::string library)
{
	bool res = false;
	HMODULE hLocKernel32 = GetModuleHandleA("Kernel32");
	FARPROC hLocLoadLibrary = GetProcAddress(hLocKernel32, "LoadLibraryA");

	HANDLE hToken;
	TOKEN_PRIVILEGES tkp;
	if (OpenProcessToken(GetCurrentProcess(), TOKEN_ADJUST_PRIVILEGES | TOKEN_QUERY, &hToken))
	{
		LookupPrivilegeValue(NULL, SE_DEBUG_NAME, &tkp.Privileges[0].Luid);
		tkp.PrivilegeCount = 1;
		tkp.Privileges[0].Attributes = SE_PRIVILEGE_ENABLED;
		AdjustTokenPrivileges(hToken, 0, &tkp, sizeof(tkp), NULL, NULL);
	}

	HANDLE hProc = OpenProcess(PROCESS_ALL_ACCESS, FALSE, process);

	if (hProc)
	{
		library += '\0';
		LPVOID hRemoteMem = VirtualAllocEx(hProc, NULL, library.size(), MEM_COMMIT, PAGE_READWRITE);

		SIZE_T numBytesWritten;
		WriteProcessMemory(hProc, hRemoteMem, library.c_str(), library.size(), &numBytesWritten);
		HANDLE hRemoteThread = CreateRemoteThread(hProc, NULL, 0, (LPTHREAD_START_ROUTINE)hLocLoadLibrary, hRemoteMem, 0, NULL);

		if (hRemoteThread) res = WaitForSingleObject(hRemoteThread, 5000) != WAIT_TIMEOUT;

		VirtualFreeEx(hProc, hRemoteMem, library.size(), MEM_RELEASE);
		CloseHandle(hProc);
	}

	return res;
}

std::vector<DWORD> GetProcessList()
{
	DWORD numProcesses = 0;
	DWORD processes[1024] = { 0 };

	EnumProcesses(processes, sizeof(processes), &numProcesses);

	std::vector<DWORD> result;

	for (DWORD i = 0; i < numProcesses; i++)
	{
		result.push_back(processes[i]);
	}

	return result;
}

std::string GetProcessName(DWORD process)
{
	std::string name;
	HANDLE Handle = OpenProcess(PROCESS_QUERY_INFORMATION | PROCESS_VM_READ, FALSE, process);

	if (Handle)
	{
		char Buffer[MAX_PATH] = { 0 };
		GetModuleFileNameExA(Handle, 0, Buffer, sizeof(Buffer));
		name.append(Buffer);

		CloseHandle(Handle);
	}

	name = name.substr(name.find_last_of("/\\") + 1);

	return name;
}

std::vector<std::string> GetProcessModules(DWORD process)
{
	std::vector<std::string> modules;

	HANDLE Handle = OpenProcess(PROCESS_QUERY_INFORMATION | PROCESS_VM_READ, FALSE, process);

	if (Handle)
	{
		DWORD numModules = 0;
		HMODULE moduleArray[1024] = { 0 };
		EnumProcessModules(Handle, moduleArray, sizeof(moduleArray), &numModules);

		for (DWORD i = 0; i < numModules; i++)
		{
			std::string name;
			char Buffer[MAX_PATH] = { 0 };

			if (moduleArray[i])
			{
				GetModuleFileNameExA(Handle, moduleArray[i], Buffer, sizeof(Buffer));

				name.append(Buffer);
				name = name.substr(name.find_last_of("/\\") + 1);
				modules.push_back(name);
			}
		}

		CloseHandle(Handle);
	}

	return modules;
}

bool IsInjectable(DWORD process)
{
	std::string name = GetProcessName(process);

	bool result = false;
	if (name == "GTA5.exe")
	{
		/*for (int i = 0; i < (sizeof(exceptions) / sizeof(exceptions[0])); i++)
		{
			if (!_stricmp(name.data(), exceptions[i])
			{
				return false;
			}
		}
		*/
		
		HANDLE Handle = OpenProcess(PROCESS_ALL_ACCESS | PROCESS_QUERY_INFORMATION | PROCESS_VM_READ, FALSE, process);

		if (Handle)
		{
			std::vector<std::string> modules = GetProcessModules(process);

			for (auto module : modules)
			{
				// Library already injected
				if (!_stricmp(module.data(), AYRIA_LOADER_LIB)) return false;

				// Check if directx lib
				for (int i = 0; i < (sizeof(libs) / sizeof(libs[0])); i++)
				{
					if (!_stricmp(module.data(), libs[i]))
					{
						result = true;
					}
				}
			}

			CloseHandle(Handle);
		}
	}

	return result;
}

bool DoInjectCheck()
{

	std::vector<DWORD> processes = GetProcessList();

	for (auto process : processes)
	{
		if (IsInjectable(process))
		{
			if (InjectLibrary(process, GetModuleDir() + "/" + AYRIA_LOADER_LIB))
			{
				return true;
				//DBGPrint("Injected: %s\n", GetProcessName(process).data());
			}
		}
	}
}

void ScriptMain()
{
	const auto version = getGameVersion();

	HWND hGTA = FindWindowA(NULL, "Grand Theft Auto V");
	SetWindowTextA(hGTA, "V-Multiplayer");

	if (version >= 24)
	{
		const auto global2566708 = getGlobalPtr(2566708);

		if (global2566708 != nullptr)
		{
			*global2566708 = 1;
		}
	}
	else if (version >= 20)
	{
		// Disable mpexecutive and mplowrider2 car removing
		const auto global2562051 = getGlobalPtr(2562051);

		if (global2562051 != nullptr)
		{
			*global2562051 = 1;
		}
	}
	else if (version >= 18)
	{
		// Disable mplowrider2 car removing
		const auto global2558120 = getGlobalPtr(2558120);

		if (global2558120 != nullptr)
		{

			*global2558120 = 1;
		}
	}

	// Set up fibers
	sGameReloaded = true;
	sMainFib = GetCurrentFiber();

	if (sScriptFib == nullptr)
	{
		const auto callback = [](LPVOID)
		{
			while (ManagedInit())
			{
				sGameReloaded = false;

				// Run main loop
				while (!sGameReloaded && ManagedTick())
				{
					// Switch back to main script fiber used by Script Hook
					SwitchToFiber(sMainFib);
				}
			}
		};

		// Create our own fiber for the common language runtime once
		sScriptFib = CreateFiber(0, callback, nullptr);
	}

	while (true)
	{
		// Yield execution
		scriptWait(0);

		// Switch to our own fiber and wait for it to switch back
		SwitchToFiber(sScriptFib);
	}
}
void ScriptKeyboardMessage(DWORD key, WORD repeats, BYTE scanCode, BOOL isExtended, BOOL isWithAlt, BOOL wasDownBefore, BOOL isUpNow)
{
	ManagedKeyboardMessage(static_cast<int>(key), isUpNow == FALSE, (GetAsyncKeyState(VK_CONTROL) & 0x8000) != 0, (GetAsyncKeyState(VK_SHIFT) & 0x8000) != 0, isWithAlt != FALSE);
}


#include <API.h>
BOOL WINAPI DllMain(HMODULE hModule, DWORD fdwReason, LPVOID lpvReserved)
{
	switch (fdwReason)
	{
	case DLL_PROCESS_ATTACH:
		AddDllDirectory((LPCWSTR)"I:\\Script\\V-Multi\\FiveMP-pre-0.1a-41\\bin\\Debug");
		//Start();
		//DisableThreadLibraryCalls(hModule);
		scriptRegister(hModule, &ScriptMain);
		keyboardHandlerRegister(&ScriptKeyboardMessage);
		break;
	case DLL_PROCESS_DETACH:
		DeleteFiber(sScriptFib);
		scriptUnregister(hModule);
		keyboardHandlerUnregister(&ScriptKeyboardMessage);
		//CEFAPI::Crash();
		break;
	}

	return TRUE;
}

