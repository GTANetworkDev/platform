#include "..\STDInclude.h"
#include <shellapi.h>

namespace Ayria {
	ExtendedUIHandler::ExtendedUIHandler()
	{
		m_Browser = 0;
		m_BrowserHwnd = 0;
	}

	bool ExtendedUIHandler::DoClose(CefRefPtr<CefBrowser> browser)
	{
		return false;
	}

	void ExtendedUIHandler::OnAfterCreated(CefRefPtr<CefBrowser> browser)
	{
		if (!m_Browser.get())
		{
			// We need to keep the main child window, but not popup windows
			m_Browser = browser;
			m_BrowserHwnd = browser->GetHost()->GetWindowHandle();
		}

		UI::Browser = browser;
	}

	void ExtendedUIHandler::OnBeforeClose(CefRefPtr<CefBrowser> browser)
	{
		if (m_BrowserHwnd == browser->GetHost()->GetWindowHandle())
		{
			// Free the browser pointer so that the browser can be destroyed
			m_Browser = NULL;
		}

		//ExtendedUI::StopBrowser();
	}

	bool ExtendedUIHandler::OnBeforeBrowse(CefRefPtr<CefBrowser> browser, CefRefPtr<CefFrame> frame, CefRefPtr<CefRequest> request, bool is_redirect)
	{
		return false;
	}

	bool ExtendedUIHandler::GetViewRect(CefRefPtr<CefBrowser> browser, CefRect& rect)
	{
		Ayria::IRenderer* renderer = Ayria::IRenderer::GetSingleton();

		if (renderer)
		{
			rect.Set(0, 0, renderer->Width(), renderer->Height());
			return true;
		}

		return false;
	}

	CefRefPtr<CefContextMenuHandler> ExtendedUIHandler::GetContextMenuHandler()
	{
		return new ContextMenuHandler();
	}

	void ClientApp::OnBeforeCommandLineProcessing(const CefString& process_type, CefRefPtr<CefCommandLine> command_line)
	{
		// Kills webgl support, but at least enables 60 fps, instead of ~15 :P
		//command_line->AppendSwitch("disable-gpu");
		command_line->AppendSwitch("off-screen-rendering-enabled");
		command_line->AppendSwitchWithValue("off-screen-frame-rate", "60");
	}

	void ClientApp::OnRegisterCustomSchemes(CefRefPtr<CefSchemeRegistrar> registrar)
	{
		//OutputDebugStringA("Scheme register");
		registrar->AddCustomScheme("ayria", true, false, false);
	}

	void ClientApp::OnContextCreated(CefRefPtr<CefBrowser> browser, CefRefPtr<CefFrame> frame, CefRefPtr<CefV8Context> context)
	{
		CefRefPtr<CefV8Value> object = context->GetGlobal();
		CefRefPtr<CefV8Handler> handler = new ClientV8ExtensionHandler(this);

		CefRefPtr<CefV8Value> isCef = CefV8Value::CreateBool(true);
		object->SetValue("isCef", isCef, V8_PROPERTY_ATTRIBUTE_NONE);

		JSBridge::SetBrowser(browser);
		JSBridge::SetFunctions(object, handler);
	}

	CefRefPtr<CefResourceHandler> ClientSchemeHandlerFactory::Create(CefRefPtr<CefBrowser> browser, CefRefPtr<CefFrame> frame, const CefString& scheme_name, CefRefPtr<CefRequest> request)
	{
		return new ClientResourceHandler();
	}
}