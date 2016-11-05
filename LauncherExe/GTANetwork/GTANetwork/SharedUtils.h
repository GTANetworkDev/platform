#pragma once

#define SAFE_DELETE(d) if(d) { delete d; d = NULL; }
#define DLL_EXPORT extern "C" __declspec(dllexport)

#define STRING2(x) #x
#define STRING(x) STRING2(x)

namespace SharedUtils
{
	namespace Registry
	{
		bool						Read(HKEY hKeyLocation, const char * szLocation, const char * szRow, const char *szBuffer, DWORD dwSize);
		bool						Write(HKEY hKeyLocation, const char * szSubKey, const char * szKey, const char * szData, DWORD dwSize);
	};
};