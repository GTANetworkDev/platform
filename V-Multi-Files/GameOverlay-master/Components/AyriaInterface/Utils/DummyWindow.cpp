#include "..\STDInclude.h"

DummyWindow::DummyWindow()
{
	DummyWindow::Class = va("%d_%d", rand(), time(NULL));

	WNDCLASS wc = {};
	wc.lpfnWndProc = DefWindowProc;
	wc.hInstance = GetModuleHandle(NULL);
	wc.lpszClassName = DummyWindow::Class.data();

	RegisterClass(&wc);

	DummyWindow::Window = CreateWindowEx(0, DummyWindow::Class.data(), "", WS_OVERLAPPED, CW_USEDEFAULT, CW_USEDEFAULT, CW_USEDEFAULT, CW_USEDEFAULT, NULL, NULL, wc.hInstance, NULL);
}

DummyWindow::~DummyWindow()
{
	DestroyWindow(DummyWindow::Window);
	UnregisterClassA(DummyWindow::Class.data(), GetModuleHandle(NULL));
}

HWND DummyWindow::Get()
{
	return DummyWindow::Window;
}
