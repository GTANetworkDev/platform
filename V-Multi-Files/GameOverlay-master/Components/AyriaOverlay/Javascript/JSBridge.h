#pragma once


typedef CefRefPtr<CefV8Value> (_cdecl * callback_t)(const CefV8ValueList &arguments);

typedef struct jsFunctionEntry_s 
{
	const char*            function;
	callback_t             callback;
} jsFunctionEntry_s;


class JSBridge
{
public:
	static std::vector<eventTrigger> eventParams;
	static void Inititalize();
	static void Uninitialize();

	static bool RunFunction(const char* function, const CefV8ValueList &arguments, CefRefPtr<CefV8Value> &retval);
	static bool AddFunction(const char * function, callback_t callback);
	static bool AddAsyncFunction(const char * function, callback_t callback, CefRefPtr<CefV8Value> globalContext, CefRefPtr<CefV8Handler> handler);
	static void SetFunctions(CefRefPtr<CefV8Value> globalContext, CefRefPtr<CefV8Handler> handler);

	static void SetBrowser(CefBrowser* browser);
	static CefBrowser* GetBrowser();

private:
	static CefBrowser* cefBrowser;
	static std::vector<jsFunctionEntry_s> entries;
};

