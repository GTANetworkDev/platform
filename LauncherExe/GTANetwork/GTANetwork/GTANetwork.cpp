#include "stdafx.h"

using namespace std;

bool ProcessRunning(const char* name)
{
	HANDLE SnapShot = CreateToolhelp32Snapshot(TH32CS_SNAPPROCESS, 0);

	if (SnapShot == INVALID_HANDLE_VALUE)
		return false;

	PROCESSENTRY32 procEntry;
	procEntry.dwSize = sizeof(PROCESSENTRY32);

	if (!Process32First(SnapShot, &procEntry))
		return false;

	do
	{
		if (strcmp(procEntry.szExeFile, name) == 0)
			return true;
	} while (Process32Next(SnapShot, &procEntry));

	return false;
}

int main(int argc, char * argv[])
{


	SetConsoleTitle("GTANetwork - Console");

	const char *dllname = "bin\\scripthookv.dll";
	const char *dllname2 = "bin\\ScriptHookVDotNet.dll";

	bool GameThread = false;
	bool Steam = false;
	char GameFullPath[MAX_PATH] = { 0 };
	char Params[] = "-scOfflineOnly";
	FreeConsole();

	if (argc > 3)
	{
		if (std::string(argv[1]) != "startasgtan")
		{
			return 0;
		}
		if (std::string(argv[2]) == "true")
		{
			Steam = true;
		}
		sprintf_s(GameFullPath, "%s\\GTAVLauncher.exe", argv[3]);
	}
	else
	{
		return 0;
	}

	if (!DoesFileExist(dllname)) {
		MessageBox(NULL, "Could not find the ScriptHookVDotNet DLL", "Fatal Error", MB_ICONERROR);
		return 0;
	}
	if (!DoesFileExist(dllname2)) {
		MessageBox(NULL, "Could not find the ScriptHookV DLL", "Fatal Error", MB_ICONERROR);
		return 0;
	}

	// Predefine startup and process infos
	STARTUPINFO siStartupInfo;
	PROCESS_INFORMATION piProcessInfo;
	memset(&siStartupInfo, 0, sizeof(siStartupInfo));
	memset(&piProcessInfo, 0, sizeof(piProcessInfo));
	siStartupInfo.cb = sizeof(siStartupInfo);


	printf("SCAN: Waiting for GTA5.exe to start.\n");

	// Resume game main thread
	ResumeThread(piProcessInfo.hThread);

	bool GameStarted = false;
	bool Injected_ScriptHook = false;
	bool Injected_GtaN = false;
	int InjectTry = 0;

	printf("PATH: %s \n", GameFullPath);
	if (Steam)
	{
		printf("START: Attempting to start Grand Theft Auto V Steam Version.\n");
		if (!ShellExecute(0, 0, "steam://run/271590", Params, 0, SW_SHOW)) {
			MessageBox(NULL, "Grand Theft Auto V was not able to start.", "Fatal Error", MB_ICONERROR);
			return 0;
		}
	}
	else
	{
		if (CreateProcess(GameFullPath, Params,NULL, NULL, FALSE, 0, NULL,NULL, &siStartupInfo, &piProcessInfo))
		{
			WaitForSingleObject(piProcessInfo.hProcess, 5000);
		}
		else
		{
			return 0;
		}
	}


	while (GameThread == false)
	{
		InjectTry++;
		HWND hWnds = FindWindowA(NULL, "Grand Theft Auto V");
		SetWindowText(hWnds, "GTA:Network");
		bool yes = ProcessRunning("GTA5.exe");

		if (yes != NULL)
		{
			Sleep(7500);
			if (GameStarted == false) {
				printf("SCAN: GTA5.exe has successfully started!\n\n");
				GameStarted = true;
			}
			else
			{
				if (InjectDLL("GTA5.exe", dllname) == true)
				{
					printf("INJECT: Successfully injected %s into Grand Theft Auto V!\n\n", dllname);
					Injected_ScriptHook = true;
				}
				if (InjectDLL("GTA5.exe", dllname2) && Injected_ScriptHook)
				{
					printf("INJECT: Successfully injected %s into Grand Theft Auto V!\n\n", dllname2);
					Injected_GtaN = true;
					GameThread = true;
				}
				Sleep(2500);
			}
		}
		if (InjectTry > 125) // if the process bug --- Close it after 200 *  100 = 20 secondes.
			return 0;
		Sleep(200);
	}
	Sleep(500);
}



