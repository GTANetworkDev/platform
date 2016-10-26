#include "stdafx.h"

namespace SharedUtils
{
	const char * GetAbsolutePath(const char *szFile)
	{
		// Create varibles to save data
		char *szRealPath = new char[MAX_PATH];
		char szPath[MAX_PATH] = { 0 };

		// Get current file/module path
#ifdef _WIN32
		static unsigned int nDummy;
		HMODULE hModuleHandle;
		GetModuleHandleEx(GET_MODULE_HANDLE_EX_FLAG_FROM_ADDRESS | GET_MODULE_HANDLE_EX_FLAG_UNCHANGED_REFCOUNT,
			(LPCSTR)&nDummy, &hModuleHandle);
		GetModuleFileName(hModuleHandle, szPath, MAX_PATH);
#else
		readlink("/proc/self/exe", szPath, MAX_PATH);
#endif

		// Strip current module name
		for (size_t i = strlen(szPath); i > 0; --i)
		{
#ifdef _WIN32
			if (szPath[i] == '\\')
#else
			if (szPath[i] == '/')
#endif
			{
				szPath[i + 1] = '\0';
				break;
			}
		}

		// Set the path for real path
		strcpy(szRealPath, szPath);

		// Add absolute file name
		sprintf(szRealPath, "%s%s", szRealPath, szFile);

		// Return absolute real position
		return szRealPath;
	}

	namespace Registry
	{
		bool Read(HKEY hKeyLocation, const char * szLocation, const char * szRow, const char *szBuffer, DWORD dwSize)
		{
			HKEY hKey = NULL;

			if (RegOpenKeyEx(hKeyLocation, szLocation, NULL, KEY_READ, &hKey) == ERROR_SUCCESS)
			{
				DWORD dwType = REG_SZ;
				LONG getStatus = RegQueryValueEx(hKey, szRow, NULL, &dwType, (BYTE *)szBuffer, &dwSize);
				RegCloseKey(hKey);
				return (getStatus == ERROR_SUCCESS);
			}
			return false;
		}

		bool Write(HKEY hKeyLocation, const char * szSubKey, const char * szKey, const char * szData, DWORD dwSize)
		{
			HKEY hKey = NULL;
			RegOpenKeyEx(hKeyLocation, szSubKey, NULL, KEY_ALL_ACCESS, &hKey);

			if (!hKey)
			{
				RegCreateKeyEx(hKeyLocation, szSubKey, 0, NULL, REG_OPTION_NON_VOLATILE, KEY_ALL_ACCESS, NULL, &hKey, NULL);
			}

			if (hKey)
			{
				RegSetValueEx(hKey, szKey, NULL, REG_SZ, (BYTE *)szData, dwSize);
				RegCloseKey(hKey);
				return true;
			}
			return false;
		}
	};
};