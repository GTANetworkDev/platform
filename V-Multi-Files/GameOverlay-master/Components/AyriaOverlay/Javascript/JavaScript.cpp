#include "..\STDInclude.h"
#include "JSBridge.h"


CefRefPtr<CefV8Value> EnableInterception(const CefV8ValueList &arguments)
{
	Ayria::IInput::EnableInterception();
	return CefV8Value::CreateUndefined();
}
CefRefPtr<CefV8Value> showCursor(const CefV8ValueList &arguments)
{
	bool value;
	if (value = arguments[0]->GetBoolValue())
	{
		Ayria::IInput::ShowCursorCEF(value);
	}
	return CefV8Value::CreateUndefined();
}

CefRefPtr<CefV8Value> keyHandler(const CefV8ValueList &arguments)
{
	bool value;
	if (value = arguments[0]->GetBoolValue())
	{
		Ayria::IInput::KeyHandlerCEF(value);
	}
	return CefV8Value::CreateUndefined();
}

CefRefPtr<CefV8Value> triggerEvent(const CefV8ValueList &arguments)
{
	if (arguments.size() >= 1)
	{
		for each (CefRefPtr<CefV8Value> c in arguments)
		{
			try
			{
				eventTrigger test;
				test.String = c->GetStringValue();
				JSBridge::eventParams.push_back(test);
			}
			catch(int e)
			{

			}

			/*
			if (c->IsString())
			{
				eventTrigger test;
				test.String = c->GetStringValue();
				JSBridge::eventParams.push_back(test);
			}		
			if (c->IsBool())
			{
				eventTrigger test;
				test.Bool = c->GetBoolValue();
				JSBridge::eventParams.push_back(test);
			}
			if (c->IsInt())
			{
				eventTrigger test;
				test.Int = c->GetIntValue();
				JSBridge::eventParams.push_back(test);
			}*/
		}
		return CefV8Value::CreateUndefined();
	}
}

CefRefPtr<CefV8Value> DisableInterception(const CefV8ValueList &arguments)
{

	Ayria::IInput::DisableInterception();
	return CefV8Value::CreateUndefined();
}

// --------------------------------------------------------------------------------------+

void JSBridge::Inititalize()
{
	JSBridge::AddFunction("enableInterception", EnableInterception);
	JSBridge::AddFunction("showCursor", showCursor);
	JSBridge::AddFunction("keyHandler", keyHandler);
	JSBridge::AddFunction("triggerEvent", triggerEvent);
	JSBridge::AddFunction("disableInterception", DisableInterception);
}


