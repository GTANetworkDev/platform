// ==========================================================
// alterIWnet project
// 
// Component: aiw_client
// Sub-component: steam_api
// Purpose: Various generic utility functions.
//
// Initial author: NTAuthority
// Started: 2010-09-10
// ==========================================================

#include "..\STDInclude.h"
#include <ShellAPI.h>
#include <strsafe.h>

// a funny thing is how this va() function could possibly come from leaked IW code.
#define VA_BUFFER_COUNT		4
#define VA_BUFFER_SIZE		4096

static char g_vaBuffer[VA_BUFFER_COUNT][VA_BUFFER_SIZE];
static int g_vaNextBufferIndex = 0;

const char *va( const char *fmt, ... )
{
	va_list ap;
	char *dest = &g_vaBuffer[g_vaNextBufferIndex][0];
	g_vaNextBufferIndex = (g_vaNextBufferIndex + 1) % VA_BUFFER_COUNT;
	va_start(ap, fmt);
	vsprintf( dest, fmt, ap );
	va_end(ap);
	return dest;
}

std::string GetModuleDir()
{
	HMODULE hModule;
	char    cPath[MAX_PATH] = { 0 };
	GetModuleHandleEx(GET_MODULE_HANDLE_EX_FLAG_FROM_ADDRESS, (LPCSTR)GetModuleDir, &hModule);

	GetModuleFileNameA(hModule, cPath, MAX_PATH);
	std::string path = cPath;
	return path.substr(0, path.find_last_of("\\/"));
}

std::string GetLastErrorMessage()
{
	char* lpMsgBuf;
	char* lpDisplayBuf;
	DWORD dw = GetLastError();

	FormatMessageA(FORMAT_MESSAGE_ALLOCATE_BUFFER | FORMAT_MESSAGE_FROM_SYSTEM | FORMAT_MESSAGE_IGNORE_INSERTS, NULL, dw, MAKELANGID(LANG_NEUTRAL, SUBLANG_DEFAULT), (char*)&lpMsgBuf, 0, NULL);

	// Display the error message and exit the process

	lpDisplayBuf = (char*)LocalAlloc(LMEM_ZEROINIT, lstrlen(lpMsgBuf) + 40);
	StringCchPrintf(lpDisplayBuf, LocalSize(lpDisplayBuf), "Error %d: %s", dw, lpMsgBuf);

	std::string _error(lpDisplayBuf);

	LocalFree(lpMsgBuf);
	LocalFree(lpDisplayBuf);

	return _error;
}
