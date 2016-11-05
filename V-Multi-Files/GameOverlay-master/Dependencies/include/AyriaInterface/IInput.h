#define INPUT_INTERFACE_KEY_COUNT 0x100

namespace Ayria
{
	class IInput
	{
	public:
		struct KeyState
		{
			int virtualKey;
			int scanCode;
			int state;
			bool down;
			bool isChar;
			unsigned short charCode;
		};

		typedef void(*KeyCallback)(KeyState key);

		static AI_DECL void EnableInterception();
		static AI_DECL void ShowCursorCEF(bool value);
		static AI_DECL void KeyHandlerCEF(bool value);
		static AI_DECL void DisableInterception();

		static AI_DECL bool IsIntercepted();

		static AI_DECL POINT CursorPos();

		static AI_DECL void OnKeyPress(KeyCallback callback);

#ifdef AYRIA_INTERFACE_MAIN
		static volatile long InterceptInputState;

		static Hook::Jump GetCursorPosHook;
		static Hook::Jump SetCursorPosHook;
		static Hook::Jump ShowCursorHook;
		static Hook::Jump SetCursorHook;

		static SHORT KeyMap[INPUT_INTERFACE_KEY_COUNT];
		static std::vector<KeyCallback> KeyCallbacks;

		static POINT StoredCursorPos;

		static void Initialize();
		static void Uninitialize();

		static void KeyHandler();
#endif
	};
}