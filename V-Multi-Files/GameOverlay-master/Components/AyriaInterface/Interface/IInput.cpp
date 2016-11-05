#include "..\STDInclude.h"

namespace Ayria
{
	volatile long IInput::InterceptInputState = 0;
	bool KeyHandlerValue = true;
	POINT IInput::StoredCursorPos = { 0, 0 };

	Hook::Jump IInput::GetCursorPosHook;
	Hook::Jump IInput::SetCursorPosHook;
	Hook::Jump IInput::ShowCursorHook;
	Hook::Jump IInput::SetCursorHook;

	SHORT IInput::KeyMap[INPUT_INTERFACE_KEY_COUNT] = { 0 };
	std::vector<IInput::KeyCallback> IInput::KeyCallbacks;

	void IInput::EnableInterception()
	{
		// Store cursor position
		POINT p;
		GetCursorPos(&p);

		InterlockedIncrement(&IInput::InterceptInputState);
	}

	void IInput::ShowCursorCEF(bool value)
	{
		ShowCursor(value);
	}

	void IInput::KeyHandlerCEF(bool value)
	{
		KeyHandlerValue = value;
	}

	void IInput::DisableInterception()
	{
		InterlockedDecrement(&IInput::InterceptInputState);

		// Restore cursor position
		SetCursorPos(IInput::StoredCursorPos.x, IInput::StoredCursorPos.y);
	}

	bool IInput::IsIntercepted()
	{
		return (IInput::InterceptInputState > 0);
	}

	POINT IInput::CursorPos()
	{
		POINT point;
		Hook_CallNativeVoid(GetCursorPos, &point);

		IRenderer* renderer = IRenderer::GetSingleton();
		if (renderer)
		{
			ScreenToClient(renderer->GetWindow(), &point);
		}

		return point;
	}

	void IInput::OnKeyPress(IInput::KeyCallback callback)
	{
		IInput::KeyCallbacks.push_back(callback);
	}

	void IInput::Initialize()
	{
		IInput::GetCursorPosHook.Initialize(GetCursorPos, static_cast<BOOL(__stdcall *)(POINT*)>([] (POINT* point)
		{
			if (IInput::IsIntercepted())
			{
				*point = IInput::StoredCursorPos;
				return FALSE;
			}

			BOOL _return;
			Hook_CallNative(GetCursorPos, &_return, point);
			IInput::StoredCursorPos = *point;
			return _return;
		}))->Install();

		IInput::SetCursorPosHook.Initialize(SetCursorPos, static_cast<BOOL(__stdcall *)(int X, int Y)>([] (int X, int Y)
		{
			if (IInput::IsIntercepted())
			{
				return FALSE;
			}

			IInput::StoredCursorPos = { X, Y };

			BOOL _return;
			Hook_CallNative(SetCursorPos, &_return, X, Y);
			return _return;
		}))->Install();

		IInput::ShowCursorHook.Initialize(ShowCursor, static_cast<int(__stdcall *)(BOOL)>([] (BOOL bShow)
		{
			if (IInput::IsIntercepted())
			{
				static int count = 0;

				Hook_CallNativeVoid(ShowCursor, TRUE);
				return (bShow ? ++count : --count);
			}

			int _return = 0;
			Hook_CallNative(ShowCursor, &_return, bShow);
			return _return;
		}))->Install();

		IInput::SetCursorHook.Initialize(SetCursor, static_cast<HCURSOR(__stdcall *)(HCURSOR)>([] (HCURSOR hCursor)
		{
			if (IInput::IsIntercepted())
			{
				Hook_CallNativeVoid(SetCursor, LoadCursor(NULL, IDC_ARROW));
				return hCursor;
			}

			HCURSOR _return = hCursor;
			Hook_CallNative(SetCursor, &_return, hCursor);
			return _return;
		}))->Install();

		// Key handler loop
		IRenderer::Container container;
		container.onPresent = IInput::KeyHandler;
		IRenderer::Enqueue(container);
	}

	void IInput::Uninitialize()
	{
		IInput::GetCursorPosHook.Uninstall();
		IInput::SetCursorPosHook.Uninstall();
		IInput::ShowCursorHook.Uninstall();
		IInput::SetCursorHook.Uninstall();
	}

	void IInput::KeyHandler()
	{

		if (KeyHandlerValue == true)
		{
			// Only dispatch key handlers, if we're focused on our window.
			IRenderer* renderer = IRenderer::GetSingleton();
			if (renderer && renderer->GetWindow() != GetForegroundWindow()) return;

			// Get keyboard state
			BYTE states[INPUT_INTERFACE_KEY_COUNT];
			GetKeyboardState(states);

			// Get keyboard layout 
			HKL layout = GetKeyboardLayout(0);

			for (int i = 0; i < INPUT_INTERFACE_KEY_COUNT; i++)
			{
				SHORT oldState = IInput::KeyMap[i];
				SHORT newState = GetKeyState(i);

				// Emit state change
				if (oldState != newState)
				{
					KeyState key;
					key.virtualKey = i;
					key.state = newState;
					key.down = ((newState & 0x8000) != 0);
					key.isChar = (MapVirtualKey(key.virtualKey, MAPVK_VK_TO_CHAR) != 0);
					key.scanCode = MapVirtualKeyEx(key.virtualKey, MAPVK_VK_TO_VSC_EX, layout);

					// Get correct char code
					key.charCode = 0;
					ToAsciiEx(key.virtualKey, key.scanCode, states, &key.charCode, 0, layout);

					for (auto callback : IInput::KeyCallbacks)
					{
						callback(key);
					}
				}

				IInput::KeyMap[i] = newState;
			}
		}
	}
}
