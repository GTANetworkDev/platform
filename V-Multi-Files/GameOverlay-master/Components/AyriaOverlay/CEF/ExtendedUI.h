namespace egl
{
	__declspec(dllimport) unsigned int __stdcall GetMainWindowSharedHandle(HANDLE* shared_handle);
	__declspec(dllimport) unsigned int __stdcall SetSwapFrameHandler(std::function<void(void*)> handler);
}

#include "CEFUIV8ExtensionHandler.h"
#include "CEFUIContextMenuHandler.h"

namespace Ayria
{
	class ExtendedUIHandler : public CefClient, public CefLifeSpanHandler, public CefRequestHandler, public CefRenderHandler, public CefDisplayHandler
	{
	public:
		ExtendedUIHandler();

		CefRefPtr<CefBrowser> GetBrowser() { return m_Browser; }
		CefWindowHandle GetBrowserHwnd() { return m_BrowserHwnd; }

		// CefClient methods
		virtual CefRefPtr<CefLifeSpanHandler> GetLifeSpanHandler() OVERRIDE
		{
			return this;
		}

		virtual CefRefPtr<CefRequestHandler> GetRequestHandler() OVERRIDE
		{
			return this;
		}

		virtual CefRefPtr<CefRenderHandler> GetRenderHandler() OVERRIDE
		{
			return this;
		}

		virtual CefRefPtr<CefDisplayHandler> GetDisplayHandler() OVERRIDE
		{
			return this;
		}

		virtual bool GetViewRect(CefRefPtr<CefBrowser> browser, CefRect& rect) OVERRIDE;

		// Virutal on CefLifeSpanHandler
		virtual bool DoClose(CefRefPtr<CefBrowser> browser) OVERRIDE;
		virtual void OnAfterCreated(CefRefPtr<CefBrowser> browser) OVERRIDE;
		virtual void OnBeforeClose(CefRefPtr<CefBrowser> browser) OVERRIDE;

		virtual void OnPaint(CefRefPtr<CefBrowser> browser, PaintElementType type, const RectList& dirtyRects, const void* buffer, int width, int height) OVERRIDE {};

		virtual CefRefPtr<CefContextMenuHandler> GetContextMenuHandler() OVERRIDE;

		// virtual on CefRequestHandler
		virtual bool OnBeforeBrowse(CefRefPtr<CefBrowser> browser, CefRefPtr<CefFrame> frame, CefRefPtr<CefRequest> request, bool is_redirect) OVERRIDE;

	protected:
		// The child browser window
		CefRefPtr<CefBrowser> m_Browser;

		// The child browser window handle
		CefWindowHandle m_BrowserHwnd;

		///
		// Macro that provides a reference counting implementation for classes extending
		// CefBase.
		///
		IMPLEMENT_REFCOUNTING(ExtendedUIHandler);
	};

	class ClientApp : public CefApp, public CefRenderProcessHandler
	{
	public:
		ClientApp() {};

		CefRefPtr<CefRenderProcessHandler> GetRenderProcessHandler() OVERRIDE
		{
			return this;
		}

		virtual void OnBeforeCommandLineProcessing(const CefString& process_type, CefRefPtr<CefCommandLine> command_line) override;
		virtual void OnRegisterCustomSchemes(CefRefPtr<CefSchemeRegistrar> registrar) override;
		virtual void OnContextCreated(CefRefPtr<CefBrowser> browser, CefRefPtr<CefFrame> frame, CefRefPtr<CefV8Context> context) override;


	protected:
		IMPLEMENT_REFCOUNTING(ClientApp);
	};

	class ClientSchemeHandlerFactory : public CefSchemeHandlerFactory
	{
	public:
		virtual CefRefPtr<CefResourceHandler> Create(CefRefPtr<CefBrowser> browser, CefRefPtr<CefFrame> frame, const CefString& scheme_name, CefRefPtr<CefRequest> request);

		IMPLEMENT_REFCOUNTING(ClientSchemeHandlerFactory);
	};

	class UI
	{
	public:
		static bool IsCtrlPressed;
		static bool IsShiftPressed;

		static CefString URL;

		static CefBrowser* Browser;
		static ExtendedUIHandler* UIHandler;

		static ITexture2D* Texture;

		static int Initialized;

		static void Present();
		static void CreateInstance();
		static void Initialize();
		static void Uninitialize();

		// 
		static void Reload();

		static void ReloadIgnoreCache();

		static bool IsLoading();

		static bool IsPopup();

		static void Focus(bool value);

		static void LoadURL(std::string url);

		static void ExecuteJavaScript(std::string code);

		static void GoBack();

		static void GoForward();
		//


		static void WatchViewport();
		static void UpdateTexture();
		static void PushTextureUpdate(const void* buffer, int width, int height, int rowBytes, bool flipVertical);

		static void KeyHandler(IInput::KeyState key);

		static void StartCEF();

		static void OnLost();
		static void OnReset();

		static void CreateTexture();

		static void SwapFrameHandler(void*);

	private:
		static char* Buffer;

		static ID3D11Texture2D* EGLTexture;
		static ID3D11Texture2D* TempTexture;
		static ID3D11Device* Device;
		static ID3D11DeviceContext* DeviceContext;
	};

	class ClientResourceHandler : public CefResourceHandler
	{
	public:
		virtual ~ClientResourceHandler() override;
		virtual bool ProcessRequest(CefRefPtr<CefRequest> request, CefRefPtr<CefCallback> callback) override;
		virtual void GetResponseHeaders(CefRefPtr<CefResponse> response, int64& response_length, CefString& redirectUrl) override;
		virtual void Cancel() override;
		virtual bool ReadResponse(void* data_out, int bytes_to_read, int& bytes_read, CefRefPtr<CefCallback> callback) override;

		IMPLEMENT_REFCOUNTING(ClientResourceHandler);
	};
}