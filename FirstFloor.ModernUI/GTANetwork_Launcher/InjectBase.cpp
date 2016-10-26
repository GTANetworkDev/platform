#pragma once
#include "stdafx.h"

char cinProcessName[128];

//Globals
clock_t t;
clock_t actualclock;
bool timerRunning;
float assignatedMemory;
bool processDown;

//Defines
#define GUI_TITLE 1
#define GUI_NORMAL 2

// Prototype(s)...
typedef BOOL(__stdcall * pfnGetSystemTimes)(LPFILETIME lpIdleTime, LPFILETIME lpKernelTime, LPFILETIME lpUserTime);
static pfnGetSystemTimes s_pfnGetSystemTimes = NULL;

static HMODULE s_hKernel = NULL;

void GetSystemTimesAddress() {
	if (s_hKernel == NULL) {
		s_hKernel = LoadLibrary("Kernel32.dll");
		if (s_hKernel != NULL) {
			s_pfnGetSystemTimes = (pfnGetSystemTimes)GetProcAddress(s_hKernel, "GetSystemTimes");
			if (s_pfnGetSystemTimes == NULL) {
				FreeLibrary(s_hKernel); s_hKernel = NULL;
			}
		}
	}
}

int GetPID(char *pProcessName) {
	HANDLE hSnap = INVALID_HANDLE_VALUE;
	HANDLE hProcess = INVALID_HANDLE_VALUE;
	PROCESSENTRY32 ProcessStruct;
	ProcessStruct.dwSize = sizeof(PROCESSENTRY32);
	hSnap = CreateToolhelp32Snapshot(TH32CS_SNAPPROCESS, 0);
	if (hSnap == INVALID_HANDLE_VALUE)
		return -1;
	if (Process32First(hSnap, &ProcessStruct) == FALSE)
		return -1;
	do {
		if (strcmp(ProcessStruct.szExeFile, pProcessName) == 0) {
			CloseHandle(hSnap);
			return ProcessStruct.th32ProcessID;
			break;
		}
	} while (Process32Next(hSnap, &ProcessStruct));
	CloseHandle(hSnap);
	return -1;
}
bool foundProcess(DWORD processID) {
	HANDLE hProcess;
	PROCESS_MEMORY_COUNTERS pmc;
	// Print information about the memory usage of the process.

	hProcess = OpenProcess(PROCESS_QUERY_INFORMATION |
		PROCESS_VM_READ,
		FALSE, processID);
	if (NULL == hProcess) {
		return 0;
	}
	CloseHandle(hProcess);
	return 1;
}
bool PrintMemoryInfo(DWORD processID) {
	HANDLE hProcess;
	PROCESS_MEMORY_COUNTERS pmc;

	// Print the process identifier.

	printf("\nMemory Information on Process ID: %u\n", processID);
	printf("\n");
	// Print information about the memory usage of the process.

	hProcess = OpenProcess(PROCESS_QUERY_INFORMATION |
		PROCESS_VM_READ,
		FALSE, processID);
	if (NULL == hProcess) {
		return 0;
	}
	if (GetProcessMemoryInfo(hProcess, &pmc, sizeof(pmc))) {
		printf("\tPageFaultCount: %.3f\n", floor(float(pmc.PageFaultCount)));
		printf("\tCurrent memory usage (Approximate): %.3f\n", floor(float(pmc.WorkingSetSize / 1024000)));
		printf("\tQuotaPeakPagedPoolUsage: %.3f\n",
			floor(float(pmc.QuotaPeakPagedPoolUsage / 1024000)));
		printf("\tQuotaPagedPoolUsage: %.3f\n",
			floor(float(pmc.QuotaPagedPoolUsage / 1024000)));
		printf("\tQuotaPeakNonPagedPoolUsage: %.3f\n",
			floor(float(pmc.QuotaPeakNonPagedPoolUsage / 1024000)));
		printf("\tQuotaNonPagedPoolUsage: %.3f\n",
			floor(float(pmc.QuotaNonPagedPoolUsage / 1024000)));
		printf("\tPagefileUsage: %.3f\n",
			floor(float(pmc.PagefileUsage / 1024000)));
		printf("\tPeakPagefileUsage: %.3f\n",
			floor(float(pmc.PeakPagefileUsage / 1024000)));
	}

	CloseHandle(hProcess);
	return 1;
}
float returnProcessMemory(DWORD processID) {
	HANDLE hProcess;
	PROCESS_MEMORY_COUNTERS pmc;

	hProcess = OpenProcess(PROCESS_QUERY_INFORMATION |
		PROCESS_VM_READ,
		FALSE, processID);
	if (NULL == hProcess) {
		return 0;
	}
	if (GetProcessMemoryInfo(hProcess, &pmc, sizeof(pmc))) {
		return floor(float(pmc.WorkingSetSize / 1024000));
	}

	CloseHandle(hProcess);
	return 0;
}
int createMenuItem(char *charName, int times, int guitype, char *titleName) {
	int preCalcSize;
	char buffer[128];
	for (int i = 0; i<times; i++) {
		printf("%s", charName);
		switch (guitype) {
		case GUI_TITLE: {
			if (i == 0)
				continue;
			if (times / 2 == i) {
				sprintf_s(buffer, " %s ", titleName);
				printf(buffer);
				preCalcSize = strlen(buffer) + times;
			}
		}
		}
	}
	printf("\n");
	return preCalcSize;
}
BOOL CallRemoteFunction(HANDLE hProcess, DWORD dwInjModuleBase, LPCSTR szDllName, LPCSTR szFuncName, LPVOID pParams, SIZE_T dwParamsSize, DWORD dwAllocType, DWORD dwMemType, PVOID *pReturn)
{
	LPVOID pRemoteParams = NULL;
	LPVOID pFunctionAddress = NULL;

	if (GetModuleHandle(szDllName))pFunctionAddress = GetProcAddress(GetModuleHandle(szDllName), szFuncName);
	else pFunctionAddress = GetProcAddress(LoadLibrary(szDllName), szFuncName);

	if (!pFunctionAddress)return false;

	pFunctionAddress = dwInjModuleBase + ((LPVOID*)pFunctionAddress - (DWORD)GetModuleHandle(szDllName));

	if (pParams)
	{
		pRemoteParams = VirtualAllocEx(hProcess, NULL, dwParamsSize, dwAllocType, dwMemType);
		if (!pRemoteParams) return false;

		SIZE_T dwBytesWritten = 0;
		if (!WriteProcessMemory(hProcess, pRemoteParams, pParams, dwParamsSize, &dwBytesWritten))
		{
			VirtualFreeEx(hProcess, pRemoteParams, dwParamsSize, MEM_RELEASE);
			return false;
		}
	}

	HANDLE hThread = CreateRemoteThread(hProcess, NULL, 0, (LPTHREAD_START_ROUTINE)pFunctionAddress, pRemoteParams, NULL, NULL);
	if (!hThread)
	{
		VirtualFreeEx(hProcess, pRemoteParams, dwParamsSize, MEM_RELEASE);
		return false;
	}

	DWORD dwExitCode = NULL;
	while (GetExitCodeThread(hThread, &dwExitCode))
	{
		if (dwExitCode != STILL_ACTIVE)
		{
			*pReturn = (PVOID)dwExitCode; break;
		}
	}

	if (pRemoteParams)VirtualFreeEx(hProcess, pRemoteParams, dwParamsSize, MEM_RELEASE);
	return TRUE;
}
DWORD GetModuleBase(DWORD dwProcessIdentifier, TCHAR *lpszModuleName)
{
	HANDLE hSnapshot = CreateToolhelp32Snapshot(TH32CS_SNAPMODULE, dwProcessIdentifier);
	DWORD dwModuleBaseAddress = 0;
	if (hSnapshot != INVALID_HANDLE_VALUE)
	{
		MODULEENTRY32 ModuleEntry32 = { 0 };
		ModuleEntry32.dwSize = sizeof(MODULEENTRY32);
		if (Module32First(hSnapshot, &ModuleEntry32))
		{
			do
			{
				if (strcmp(ModuleEntry32.szModule, lpszModuleName) == 0)
				{
					//MessageBox(NULL, (LPCSTR)ModuleEntry32.szModule, "jeb", NULL);
					dwModuleBaseAddress = (DWORD)ModuleEntry32.modBaseAddr;
					break;
				}
			} while (Module32Next(hSnapshot, &ModuleEntry32));
		}
		CloseHandle(hSnapshot);
	}
	return dwModuleBaseAddress;
}
bool DoesFileExist(const char *fileName)
{
	std::ifstream infile(fileName);
	return infile.good();
}
DWORD FindProcessId(char* processName)
{
	// strip path

	char* p = strrchr(processName, '\\');
	if (p)
		processName = p + 1;

	PROCESSENTRY32 processInfo;
	processInfo.dwSize = sizeof(processInfo);

	HANDLE processesSnapshot = CreateToolhelp32Snapshot(TH32CS_SNAPPROCESS, NULL);
	if (processesSnapshot == INVALID_HANDLE_VALUE)
		return 0;

	Process32First(processesSnapshot, &processInfo);
	if (!strcmp(processName, processInfo.szExeFile))
	{
		CloseHandle(processesSnapshot);
		return processInfo.th32ProcessID;
	}

	while (Process32Next(processesSnapshot, &processInfo))
	{
		if (!strcmp(processName, processInfo.szExeFile))
		{
			CloseHandle(processesSnapshot);
			return processInfo.th32ProcessID;
		}
	}

	CloseHandle(processesSnapshot);
	return 0;
}
bool InjectDLL(char *processName, const char *dllname) {
	char buffer[128];

	printf("INJECT: Attempting to inject %s into %s.\n", dllname, processName);

	if (!foundProcess(GetPID(processName))) {
		return false;
	}

	if (returnProcessMemory(GetPID(processName)) > 32) { //128
		TCHAR NPath[MAX_PATH];
		GetCurrentDirectory(MAX_PATH, NPath);
		sprintf_s(buffer, "%s\\%s", NPath, dllname);

		printf("INJECT: Current DLL location %s \n", buffer);

		if (Inject(GetPID(processName), buffer))
		{
			return true;
		}
	}
	return false;
}
bool Inject(DWORD pId, char *dllName)
{
	HANDLE h = OpenProcess(PROCESS_ALL_ACCESS, false, pId);
	if (h)
	{
		LPVOID LoadLibAddr = (LPVOID)GetProcAddress(GetModuleHandleA("kernel32.dll"), "LoadLibraryA");
		LPVOID dereercomp = VirtualAllocEx(h, NULL, strlen(dllName), MEM_COMMIT | MEM_RESERVE, PAGE_READWRITE);
		WriteProcessMemory(h, dereercomp, dllName, strlen(dllName), NULL);
		HANDLE asdc = CreateRemoteThread(h, NULL, NULL, (LPTHREAD_START_ROUTINE)LoadLibAddr, dereercomp, 0, NULL);
		WaitForSingleObject(asdc, INFINITE);
		VirtualFreeEx(h, dereercomp, strlen(dllName), MEM_RELEASE);
		CloseHandle(asdc);
		CloseHandle(h);
		return true;
	}
	return false;
}
char* returnProcessPathInfo(DWORD processID) {
	char buffer[MAX_PATH];
	HANDLE processHandle = NULL;
	processHandle = OpenProcess(PROCESS_QUERY_INFORMATION | PROCESS_VM_READ, FALSE, processID);
	if (processHandle != NULL) {
		if (GetModuleFileNameEx(processHandle, NULL, buffer, MAX_PATH) == 0) {
			return 0;
		}
		else {
			return buffer;
		}
		CloseHandle(processHandle);
	}
	else {
		return 0;
	}
}
void ClearScreen(void) {
	HANDLE hndl = GetStdHandle(STD_OUTPUT_HANDLE);
	CONSOLE_SCREEN_BUFFER_INFO csbi;
	GetConsoleScreenBufferInfo(hndl, &csbi);
	DWORD written;
	DWORD n = csbi.dwSize.X * csbi.dwCursorPosition.Y + csbi.dwCursorPosition.X + 1;
	COORD curhome = { 0,0 };
	FillConsoleOutputCharacter(hndl, ' ', n, curhome, &written);
	csbi.srWindow.Bottom -= csbi.srWindow.Top;
	csbi.srWindow.Top = 0;
	SetConsoleWindowInfo(hndl, TRUE, &csbi.srWindow);
	SetConsoleCursorPosition(hndl, curhome);
}
void TerminateProcessEx(DWORD processID) {
	HANDLE pHandle;
	pHandle = OpenProcess(PROCESS_ALL_ACCESS, false, processID);
	TerminateProcess(pHandle, 1);
}
void startProgram(char *lpApplicationName) {
	/* Create the process */
	SHELLEXECUTEINFO sei = { 0 };
	sei.cbSize = sizeof(sei);
	sei.nShow = SW_SHOWNORMAL;
	sei.lpFile = TEXT(lpApplicationName);
	sei.fMask = SEE_MASK_CLASSNAME;
	sei.lpVerb = TEXT("open");
	sei.lpClass = TEXT("exefile");
	if (!ShellExecuteEx(&sei)) {
		printf("ShellExecute() failed to start program %s\n", lpApplicationName);
		pause();
		return;
		//exit(1);
	}
	processDown = false;
}
void pause() {
	std::cin.sync(); // Flush The Input Buffer Just In Case
	std::cin.ignore(); // There's No Need To Actually Store The Users Input
}
int PrintModules(DWORD processID)
{
	HMODULE hMods[1024];
	HANDLE hProcess;
	DWORD cbNeeded;
	unsigned int i;

	// Print the process identifier.

	printf("\nProcess ID: %u\n", processID);

	// Get a handle to the process.

	hProcess = OpenProcess(PROCESS_QUERY_INFORMATION |
		PROCESS_VM_READ,
		FALSE, processID);
	if (NULL == hProcess)
		return 1;

	// Get a list of all the modules in this process.

	if (EnumProcessModules(hProcess, hMods, sizeof(hMods), &cbNeeded))
	{
		for (i = 0; i < (cbNeeded / sizeof(HMODULE)); i++)
		{
			TCHAR szModName[MAX_PATH];

			// Get the full path to the module's file.

			if (GetModuleFileNameEx(hProcess, hMods[i], szModName,
				sizeof(szModName) / sizeof(TCHAR)))
			{
				// Print the module name and handle value.

				printf(TEXT("\t%s (0x%08X)\n"), szModName, hMods[i]);
			}
		}
	}

	// Release the handle to the process.

	CloseHandle(hProcess);

	return 0;
}
HMODULE retModuleHandle(DWORD processID, const char* moduleName)
{
	HMODULE hMods[1024];
	HANDLE hProcess;
	DWORD cbNeeded;
	unsigned int i;

	// Get a handle to the process.

	hProcess = OpenProcess(PROCESS_QUERY_INFORMATION |
		PROCESS_VM_READ,
		FALSE, processID);
	if (NULL == hProcess)
		return 0;

	// Get a list of all the modules in this process.

	if (EnumProcessModules(hProcess, hMods, sizeof(hMods), &cbNeeded))
	{
		for (i = 0; i < (cbNeeded / sizeof(HMODULE)); i++)
		{
			TCHAR szModName[MAX_PATH];

			// Get the full path to the module's file.
			if (GetModuleFileNameEx(hProcess, hMods[i], szModName,
				sizeof(szModName) / sizeof(TCHAR)))
			{
				// Print the module name and handle value.
				if (strstr(szModName, moduleName)) {
					return hMods[i];
				}
			}
		}
	}

	// Release the handle to the process.

	CloseHandle(hProcess);

	return 0;
}
