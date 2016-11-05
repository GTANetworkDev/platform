#include "..\STDInclude.h"

namespace Ayria
{
	void ContextMenuHandler::OnBeforeContextMenu(CefRefPtr<CefBrowser> browser, CefRefPtr<CefFrame> frame, CefRefPtr<CefContextMenuParams> params, CefRefPtr<CefMenuModel> model)
	{
		// Clear all context menus
		model->Clear();

		// Clear main context menu
		//if (model->GetCount() == 5)
		//{
		//	model->Clear();
		//}
	}
}
