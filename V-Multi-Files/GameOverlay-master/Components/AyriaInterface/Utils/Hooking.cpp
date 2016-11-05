#include "..\STDInclude.h"

namespace Hook
{
	std::vector<Hook::Jump*> Jump::HookMap;

	Jump::~Jump()
	{
		if (Jump::Initialized)
		{
			Jump::Uninstall();
		}

		Jump::RemoveHook(this);
	}

	Jump* Jump::Initialize(void* place, void* stub)
	{
		if (Jump::Initialized) return this;
		Jump::Initialized = true;

		Jump::Place = place;
		Jump::Stub = stub;

		Jump::StoreHook(this);

		return this;
	}

	void Jump::Install()
	{
		Jump::StateMutex.lock();

		if (!Jump::Initialized || Jump::Installed)
		{
			Jump::StateMutex.unlock();
			return;
		}

		Jump::Installed = true;

		DWORD d;
		VirtualProtect(Jump::Place, sizeof(Jump::Buffer), PAGE_EXECUTE_READWRITE, &d);
		memcpy(Jump::Buffer, Jump::Place, sizeof(Jump::Buffer));

		if (Jump::IsRelativePossible())
		{
			Jump::InstallRelative();
		}
		else
		{
			Jump::InstallAbsolute();
		}

		VirtualProtect(Jump::Place, sizeof(Jump::Buffer), d, &d);

		FlushInstructionCache(GetCurrentProcess(), Jump::Place, sizeof(Jump::Buffer));

		Jump::StateMutex.unlock();
	}

	bool Jump::IsRelativePossible()
	{
		// Maybe multiply the relative address by 2 to ensure signed range is correct?
		uint64_t relAddress = (uint64_t)Jump::Stub - ((uint64_t)Jump::Place + 5);
		int32_t highBits = ((relAddress >> 32) & 0xFFFFFFFF);
		return (highBits == 0 || highBits == -1);
	}

	void Jump::InstallRelative()
	{
		char* _Code = (char*)Jump::Place;
		size_t relAddress = (size_t)Jump::Stub - ((size_t)Jump::Place + 5);

		// jmp
		_Code[0] = (char)0xE9;

		// Set target operand
		*(int*)&_Code[1] = (int)relAddress;
	}

	void Jump::InstallAbsolute()
	{
		char* _Code = (char*)Jump::Place;
		int pos = 0;

		// mov rax/eax
#if _WIN64
		_Code[pos++] = (char)0x48;
#endif
		_Code[pos++] = (char)0xB8;

		// Set target operand
		memcpy(&_Code[pos], &Stub, sizeof(Jump::Stub));

		// jmp rax/eax
#if _WIN64
		_Code[pos++ + sizeof(Jump::Stub)] = (char)0x48;
#endif
		_Code[pos++ + sizeof(Jump::Stub)] = (char)0xFF;
		_Code[pos++ + sizeof(Jump::Stub)] = (char)0xE0;
	}

	void Jump::Uninstall()
	{
		Jump::StateMutex.lock();

		if (!Jump::Initialized || !Jump::Installed)
		{
			Jump::StateMutex.unlock();
			return;
		}

		Jump::Installed = false;

		DWORD d;
		VirtualProtect(Jump::Place, sizeof(Jump::Buffer), PAGE_EXECUTE_READWRITE, &d);
		
		memcpy(Jump::Place, Jump::Buffer, sizeof(Jump::Buffer));

		VirtualProtect(Jump::Place, sizeof(Jump::Buffer), d, &d);

		FlushInstructionCache(GetCurrentProcess(), Jump::Place, sizeof(Jump::Buffer));

		Jump::StateMutex.unlock();
	}

	void* Jump::GetAddress()
	{
		return Jump::Place;
	}

	void Jump::StoreHook(Hook::Jump* hook)
	{
		Jump::HookMap.push_back(hook);
	}

	void Jump::RemoveHook(Hook::Jump* hook)
	{
		for (auto iter = Jump::HookMap.begin(); iter != Jump::HookMap.end(); iter++)
		{
			if (*iter == hook)
			{
				Jump::HookMap.erase(iter);
				break;
			}
		}
	}

	Jump* Jump::FindHook(void* address)
	{
		for (auto iter = Jump::HookMap.begin(); iter != Jump::HookMap.end(); iter++)
		{
			if ((*iter)->Place == address)
			{
				return *iter;
			}
		}

		return NULL;
	}
}
