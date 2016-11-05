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

#include "Utility\VariadicString.h"

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
	GetModuleHandleEx(GET_MODULE_HANDLE_EX_FLAG_FROM_ADDRESS, (LPCSTR)GetModuleDir, &hModule);

	GetModuleFileNameA(hModule, cPath, MAX_PATH);
	std::string path = cPath;
	return path.substr(0, path.find_last_of("\\/"));
}

bool InjectLibrary(DWORD process, std::string library)
{
	bool res = false;
	HMODULE hLocKernel32 = GetModuleHandle("Kernel32");
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
		GetModuleFileNameEx(Handle, 0, Buffer, sizeof(Buffer));
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
				GetModuleFileNameEx(Handle, moduleArray[i], Buffer, sizeof(Buffer));

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
	for (int i = 0; i < (sizeof(exceptions) / sizeof(exceptions[0])); i++)
	{
		if (!_stricmp(name.data(), exceptions[i]))
		{
			return false;
		}
	}

	bool result = false;
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

	return result;
}

void DoInjectCheck()
{
	std::vector<DWORD> processes = GetProcessList();

	for (auto process : processes)
	{
		if (IsInjectable(process))
		{
			if (InjectLibrary(process, GetModuleDir() + "/" + AYRIA_LOADER_LIB))
			{
				DBGPrint("Injected: %s\n", GetProcessName(process).data());
			}
		}
	}
}

int WINAPI WinMain(HINSTANCE hInInstance, HINSTANCE hPrevInstance, LPSTR, int nCmdShow)
{
#ifdef DBG_CON
	AllocConsole();
	AttachConsole(GetCurrentProcessId());
	freopen("CON", "w", stdout);
#endif

	DBGPrint("AyriaOverlayInjector running on " PLATFORM_SHORTNAME "\n");

	while (true)
	{
		DoInjectCheck();
		Sleep(1000);
	}

	return 0;
}
