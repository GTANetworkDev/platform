#pragma once

namespace Hook
{
	class Jump
	{
	public:
		Jump() : Place(nullptr), Stub(nullptr), Initialized(false), Installed(false) { ZeroMemory(Jump::Buffer, sizeof(Jump::Buffer)); }
		Jump(void* place, void* stub) : Jump() { Jump::Initialize(place, stub); }

		~Jump();

		Jump* Initialize(void* place, void* stub);
		void Install();
		void Uninstall();

		// Static 
		static Hook::Jump* FindHook(void* address);

	private:
		bool Initialized;
		bool Installed;

		void* Place;
		void* Stub;
		char Buffer[sizeof(INT_PTR) + 5];

		std::mutex StateMutex;

		void InstallRelative();
		void InstallAbsolute();

		bool IsRelativePossible();

		// Static
		static std::vector<Hook::Jump*> HookMap;
		static void StoreHook(Hook::Jump* hook);
		static void RemoveHook(Hook::Jump* hook);
	};
}

#define Hook_CallNative(func, retPtr, ...)         \
{                                                  \
	Hook::Jump* hook = Hook::Jump::FindHook(func); \
                                                   \
	if (hook)                                      \
	{                                              \
		hook->Uninstall();                         \
		*retPtr = func(__VA_ARGS__);               \
		hook->Install();                           \
	}                                              \
	else                                           \
	{                                              \
		*retPtr = func(__VA_ARGS__);               \
	}                                              \
}

#define Hook_CallNativeVoid(func, ...)             \
{                                                  \
	Hook::Jump* hook = Hook::Jump::FindHook(func); \
                                                   \
	if (hook)                                      \
	{                                              \
		hook->Uninstall();                         \
		func(__VA_ARGS__);                         \
		hook->Install();                           \
	}                                              \
	else                                           \
	{                                              \
		func(__VA_ARGS__);                         \
	}                                              \
}