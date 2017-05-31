#include "NativeMemory.hpp"

// workaround for an unmanaged code
unsigned long long GetOfflinePatchAddr()
{
	return GTA::Native::MemoryAccess::FindPattern("\x48\x83\x3D\x00\x00\x00\x00\x00\x88\x05\x00\x00\x00\x00\x75\x0B",
		"xxx????xxx????xx");
}

unsigned long long GetGameTextHookAddr()
{
	return GTA::Native::MemoryAccess::FindPattern("\xE8\x00\x00\x00\x00\x8B\x0D\x8C\x68\xF4\x01\x65\x48\x8B\x04\x25\x58\x00\x00\x00\xBA\xB4\x00\x00\x00\x48\x8B\x04\xC8\x8B\x0C\x02\xD1\xE9\x80\xE1\x01\x0F\xB6\xC1\x48\x8D",
		"x????xx????xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx");
}

#pragma unmanaged

#include <Windows.h>
#include <cstdint>

void ForceOffline()
{
	uintptr_t address = GetOfflinePatchAddr();

	if (address)
	{
		address += 8;

		unsigned long dwProtect{};
		unsigned long dwProtect2{};

		VirtualProtect((void*)address, 0x6ui64, 0x40u, &dwProtect);
		memset((void*)address, 0x90, 6);
		VirtualProtect((void*)address, 0x6ui64, dwProtect, &dwProtect2);
	}
}


#include "../../libs/minhook-master/include/MinHook.h"

char *(__fastcall *o_GetGameText)(__int64 a1, BYTE *a2, __int64 a3);

char *__fastcall GetGameText(__int64 a1, BYTE *a2, __int64 a3)
{
	if (strcmp((const char*)a2, "LOADING_SPLAYER_L") == 0)
		return (char*)"Loading GTA Network";

	return o_GetGameText(a1, a2, a3);
}


void HookGameText()
{
	UINT64 addr = GetGameTextHookAddr();

	if (addr != 0)
	{
		MH_Initialize();

		addr += 0x5 - 0x1A;


		MH_CreateHook((void*)addr, &GetGameText, (void**)&o_GetGameText);
		MH_EnableHook((void*)addr);
	}
}