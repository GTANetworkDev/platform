#include "..\STDInclude.h"

CefBrowser* JSBridge::cefBrowser = 0;
std::vector<jsFunctionEntry_s> JSBridge::entries;
std::vector<eventTrigger> JSBridge::eventParams;


void JSBridge::Uninitialize()
{
	JSBridge::entries.clear();
	JSBridge::cefBrowser = 0;
}

bool JSBridge::RunFunction(const char* function, const CefV8ValueList &arguments, CefRefPtr<CefV8Value> &retval)
{
	for (auto entry : entries)
	{
		if(!strcmp(function, entry.function))
		{
			retval = entry.callback(arguments);
			return true;
		}
	}

	return false;
}

bool JSBridge::AddFunction(const char* function, callback_t callback)
{
	for (auto entry : entries)
	{
		if (!strcmp(function, entry.function))
		{
			return false;
		}
	}

	jsFunctionEntry_s entry;

	entry.function = function;
	entry.callback = callback;

	entries.push_back(entry);

	return true;
}

void JSBridge::SetFunctions(CefRefPtr<CefV8Value> globalContext, CefRefPtr<CefV8Handler> handler)
{
	for (auto entry : entries)
	{
		CefRefPtr<CefV8Value> jsFunctionElement = CefV8Value::CreateFunction(entry.function, handler);
		globalContext->SetValue(entry.function, jsFunctionElement, V8_PROPERTY_ATTRIBUTE_NONE);
	}
}

void JSBridge::SetBrowser(CefBrowser* browser)
{
	JSBridge::cefBrowser = browser;
}

CefBrowser* JSBridge::GetBrowser()
{
	return JSBridge::cefBrowser;
}