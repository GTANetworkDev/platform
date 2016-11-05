namespace Ayria
{
	class ContextMenuHandler : public CefContextMenuHandler
	{
	public:
		virtual void OnBeforeContextMenu(CefRefPtr<CefBrowser> browser, CefRefPtr<CefFrame> frame, CefRefPtr<CefContextMenuParams> params, CefRefPtr<CefMenuModel> model) OVERRIDE;

	protected:
		IMPLEMENT_REFCOUNTING(ContextMenuHandler);
	};
}
