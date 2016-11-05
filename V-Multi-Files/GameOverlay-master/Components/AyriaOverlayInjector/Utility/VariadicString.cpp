/*
	This project is released under the GPL 2.0 license.
	Some parts are based on research by Bas Timmer and the OpenSteamworks project.
	Please do no evil.

	Initial author: (https://github.com/)Convery
	Started: 2014-11-24
	Notes:
		A single function that was copied from Call of Duty 4.
		Probably the most useful function ever.
*/

#include "..\STDInclude.h"

// Incase we ever need to increment the storage.
#define VA_BUFFER_COUNT		4
#define VA_BUFFER_SIZE		32768

static char vaBuffer[VA_BUFFER_COUNT][VA_BUFFER_SIZE];
static int vaNextBufferIndex = 0;
static CRITICAL_SECTION ThreadSafe;
static bool Initialized = false;

const char *va(const char *fmt, ...)
{
	va_list AP;
	int32_t Length = 0;
	char *Destination = nullptr;

	if(!Initialized)
	{
		InitializeCriticalSection(&ThreadSafe);
		Initialized = true;
	}

	EnterCriticalSection(&ThreadSafe);
	Destination = &vaBuffer[vaNextBufferIndex][0];
	vaNextBufferIndex = (vaNextBufferIndex + 1) % VA_BUFFER_COUNT;

	va_start(AP, fmt);
	Length = _vsnprintf_s(Destination, VA_BUFFER_SIZE, _TRUNCATE, fmt, AP);
	Destination[VA_BUFFER_SIZE - 1] = '\0';
	va_end(AP);

	if (Length < 0 || Length >= VA_BUFFER_SIZE)
	{
		// This is pretty bad.
		MessageBoxA(NULL, "Attempted to overrun string in call to va()", "LibUIProc", NULL);
	}

	LeaveCriticalSection(&ThreadSafe);
	return Destination;
}

const char* GetFormattedError()
{
	static char buf[VA_BUFFER_SIZE] = { 0 };
	FormatMessageA(FORMAT_MESSAGE_FROM_SYSTEM, NULL, GetLastError(), MAKELANGID(LANG_NEUTRAL, SUBLANG_DEFAULT), buf, VA_BUFFER_SIZE, NULL);
	return buf;
}
