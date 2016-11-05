#include "..\STDInclude.h"



std::vector<CefRefPtr<CefV8Value> > test;

namespace Ayria
{
	ClientV8ExtensionHandler::ClientV8ExtensionHandler(CefRefPtr<CefApp> app)
	{
		this->app = app;
	}

	bool ClientV8ExtensionHandler::Execute(const CefString &name, CefRefPtr<CefV8Value> object, const CefV8ValueList &arguments, CefRefPtr<CefV8Value> &retval, CefString &exception)
	{
		test = arguments;
		return JSBridge::RunFunction(name.ToString().c_str(), arguments, retval);
	}
}
