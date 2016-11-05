#include "STDInclude.h"
#include "API.h"
#include <string>

namespace CEFAPI
{
	void Crash()
	{
		Ayria::UI::Uninitialize();
	}
	void StartCEF()
	{
		Ayria::UI::CreateInstance();
	}
	void LoadURL(std::string url)
	{
		Ayria::UI::LoadURL(url);
	}
	void ReloadPage()
	{
		Ayria::UI::Reload();
	}
	void ReloadPageIgnoreCache()
	{
		Ayria::UI::ReloadIgnoreCache();
	}
	void GoBack()
	{
		Ayria::UI::GoBack();
	}
	void GoForward()
	{
		Ayria::UI::GoForward();
	}
	void ExecuteJavaScript(std::string code)
	{
		Ayria::UI::ExecuteJavaScript(code);
	}
	std::vector<eventTrigger> GetTriggerEvent()
	{
		return JSBridge::eventParams;
	}
	void ResetTriggerEvent()
	{
		JSBridge::eventParams.clear();
	}
	void ShowCursor(bool value)
	{
		Ayria::IInput::ShowCursorCEF(value);
	}
	void KeyHandler(bool value)
	{
		Ayria::IInput::KeyHandlerCEF(value);
	}
	void Focus(bool value)
	{
		Ayria::UI::Focus(value);
	}
}

