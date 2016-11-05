#include "..\STDInclude.h"

namespace Ayria 
{
	bool UI::IsCtrlPressed = false;
	bool UI::IsShiftPressed = false;

	CefString UI::URL;

	CefBrowser* UI::Browser;
	ExtendedUIHandler* UI::UIHandler;

	Ayria::ITexture2D* UI::Texture = 0;

	int UI::Initialized = 0;

	char* UI::Buffer = NULL;

	ID3D11Texture2D* UI::EGLTexture = 0;
	ID3D11Texture2D* UI::TempTexture = 0;
	ID3D11Device* UI::Device = 0;
	ID3D11DeviceContext* UI::DeviceContext = 0;

	void UI::StartCEF()
	{
		if (UI::Initialized) return;

		// Initialize CEF
		CefMainArgs args(GetModuleHandle(NULL));

		CefRefPtr<ClientApp> cefApplication(new ClientApp);

		CefSettings cSettings;
		//cSettings.multi_threaded_message_loop = TRUE;
		cSettings.windowless_rendering_enabled = TRUE;
		cSettings.no_sandbox = TRUE;
		cSettings.single_process = TRUE;
		cSettings.remote_debugging_port = 13370;
		cSettings.log_severity = LOGSEVERITY_DISABLE; // Disable logging for now
		cSettings.persist_user_preferences = TRUE;


		std::string path = GetModuleDir();

		CefString(&cSettings.browser_subprocess_path) = path + "\\AyriaOverlayProc.exe";
		CefString(&cSettings.locales_dir_path) = path;
		CefString(&cSettings.resources_dir_path) = path;
		CefString(&cSettings.locale) = "en-US";

		char szPath[MAX_PATH] = { 0 };
		SHGetFolderPath(NULL, CSIDL_LOCAL_APPDATA, NULL, 0, szPath);
		PathAppend(szPath, "\\Ayria\\"); _mkdir(szPath);
		PathAppend(szPath, "Overlay\\"); _mkdir(szPath);

		CefString(&cSettings.cache_path) = std::string(szPath) + "Cache\\";
		CefString(&cSettings.user_data_path) = std::string(szPath) + "User\\";

		CefInitialize(args, cSettings, cefApplication, 0);
		CefRegisterSchemeHandlerFactory("ayria", "", new ClientSchemeHandlerFactory());

		UI::UIHandler = new ExtendedUIHandler();
		CefRefPtr<CefClient> client(UI::UIHandler);

		HWND window = GetForegroundWindow();
		Ayria::IRenderer* renderer = Ayria::IRenderer::GetSingleton();

		if (renderer)
		{
			window = renderer->GetWindow();
		}


		CefWindowInfo info;
		
		info.SetAsWindowless(window, true);
		info.transparent_painting_enabled = true;
		info.windowless_rendering_enabled = TRUE;


		CefBrowserSettings settings;
		settings.windowless_frame_rate = 60;


		CefBrowserHost::CreateBrowser(info, client, UI::URL, settings, 0);
		//SetLayeredWindowAttributes(window, RGB(0xff, 0xff, 0xff), 0xff, LWA_COLORKEY);
		UI::Initialized = GetCurrentThreadId();
	}

	void UI::Reload()
	{
		if (UI::Browser)
		{
			UI::Browser->Reload();
		}
	}


	void UI::Focus(bool value)
	{
		if (UI::Browser)
		{
			UI::Browser->GetHost()->SetFocus(value);
		}
	}

	void UI::LoadURL(std::string url)
	{
		if (UI::Browser)
		{
			UI::Browser->GetMainFrame()->LoadURL(url);
		}
	}
	void UI::ExecuteJavaScript(std::string code)
	{
		if (UI::Browser)
		{
			UI::Browser->GetMainFrame()->ExecuteJavaScript(code, UI::Browser->GetMainFrame()->GetURL(), 0);
		}
	}
	void UI::GoBack()
	{
		if (UI::Browser)
		{
			UI::Browser->GoBack();
		}
	}
	void UI::GoForward()
	{
		if (UI::Browser)
		{
			UI::Browser->GoForward();
		}
	}
	void UI::ReloadIgnoreCache()
	{
		if (UI::Browser)
		{
			UI::Browser->ReloadIgnoreCache();
		}
	}
	bool UI::IsLoading()
	{
		if (UI::Browser)
		{
			return UI::Browser->IsLoading();
		}
	}
	bool UI::IsPopup()
	{
		if (UI::Browser)
		{
			return UI::Browser->IsPopup();
		}
	}

	void UI::Uninitialize()
	{
		if (!UI::Initialized) return;

		if (UI::Texture)
		{
			delete UI::Texture;
			UI::Texture = 0;
		}

		if (UI::TempTexture)
		{
			UI::TempTexture->Release();
			UI::TempTexture = 0;
		}

		if (UI::EGLTexture)
		{
			UI::EGLTexture->Release();
			UI::EGLTexture = 0;
		}

		if (UI::DeviceContext)
		{
			UI::DeviceContext->Release();
			UI::DeviceContext = 0;
		}

		if (UI::Device)
		{
			UI::Device->Release();
			UI::Device = 0;
		}

		if (UI::Buffer)
		{
			delete[] UI::Buffer;
			UI::Buffer = 0;
		}

		if (UI::Browser)
		{
			DestroyWindow(UI::Browser->GetHost()->GetWindowHandle());
		}

		// So this is the uglies way to trick CEF into shutting down.
		// I bet there is a way more elegant one, but I don't know it, yet.
		// When you initialize CEF, it stores the current thread id (using GetCurrentThreadId).
		// When you shut it down, it checks if you are on the same thread again, if not, CefShutdown won't work.
		// Now the situation here is pretty tricky, as CEF's initialization happens in some random thread.
		// Due to hooking somewhere in DirectX (or any other engine), we can assume that we are in the render thread.
		// The problem though is, when shutting down CEF, we'll most likely be inside the ExitProcess procedure.
		// That means the render thread will most likely be terminated already, therefore we can't shutdown CEF.
		// Now what I do here is, I store the thread ID when initializing CEF in ExtendedUI::Initialized.
		// When shutting down, I store the thread we are running in inside _thread.
		// Then, I hook GetCurrentThreadId. The hook then checks if we are in the shutdown thread or not.
		// If we are, it fakes the ID by returning the one from the initialization thread.
		// That means that CEF thinks it's in the initialization thread and terminates gracefully, even if it's not.
		// This is totally ugly, but it's the best way I've come up with so far.
		static DWORD _thread = GetCurrentThreadId();

		Hook::Jump _hook;
		_hook.Initialize(GetCurrentThreadId, static_cast<DWORD(__stdcall*)()>([] () -> DWORD
		{
			DWORD realThread = 0;
			Hook_CallNative(GetCurrentThreadId, &realThread);

			if (realThread == _thread)
			{
				return UI::Initialized;
			}

			return realThread;
		}))->Install();

		CefShutdown();

		_hook.Uninstall();

		UI::Browser = 0;
		UI::Initialized = 0;
	}

	void UI::KeyHandler(Ayria::IInput::KeyState key)
	{
		if (!UI::Browser) return;

		CefRefPtr<CefBrowserHost> host = UI::Browser->GetHost();

		if (!host.get()) return;

		POINT mousePos = Ayria::IInput::CursorPos();

		if (key.virtualKey >= VK_LBUTTON && key.virtualKey <= VK_MBUTTON && key.virtualKey != VK_CANCEL)
		{
			// mouse click event
			CefMouseEvent event;

			event.x = mousePos.x;
			event.y = mousePos.y;

			CefBrowserHost::MouseButtonType type;

			if (key.virtualKey == VK_LBUTTON) type = CefBrowserHost::MouseButtonType::MBT_LEFT;
			if (key.virtualKey == VK_RBUTTON) type = CefBrowserHost::MouseButtonType::MBT_RIGHT;
			if (key.virtualKey == VK_MBUTTON) type = CefBrowserHost::MouseButtonType::MBT_MIDDLE;

			host->SendMouseClickEvent(event, type, !key.down, 1);

			if (key.virtualKey == VK_LBUTTON && key.down) host->SendFocusEvent(1);
		}
		 	/*else if (key.virtualKey == WM_MOUSEHWHEEL || key.virtualKey == MOUSE)
		 	{
		 		// mouse wheel event
		 		CefMouseEvent event;
		 		event.x = mousePos.x;
		 		event.y = mousePos.y;
		 
		 		int scrollDelta = 50;
		 		host->SendMouseWheelEvent(event, 0, scrollDelta * ((key == WM_MOUSEHWHEEL) ? -1 : 1));
		 	}*/
		else
		{
			CefKeyEvent event;

			event.windows_key_code = key.virtualKey;
			event.type = (key.down ? KEYEVENT_KEYDOWN : KEYEVENT_KEYUP);

			// Control
			if (key.virtualKey == VK_CONTROL) UI::IsCtrlPressed = key.down;
			if (key.virtualKey == VK_SHIFT) UI::IsShiftPressed = key.down;

			if (UI::IsShiftPressed)
			{
				event.modifiers |= EVENTFLAG_SHIFT_DOWN;
			}

			if (UI::IsCtrlPressed && key.down)
			{
				if (UI::Browser->GetFocusedFrame())
				{
					if (key.virtualKey == 'V') UI::Browser->GetFocusedFrame()->Paste();
					if (key.virtualKey == 'C') UI::Browser->GetFocusedFrame()->Copy();
					if (key.virtualKey == 'X') UI::Browser->GetFocusedFrame()->Cut();
					if (key.virtualKey == 'A') UI::Browser->GetFocusedFrame()->SelectAll();
					if (key.virtualKey == 'Z') UI::Browser->GetFocusedFrame()->Undo();
					if (key.virtualKey == 'Y') UI::Browser->GetFocusedFrame()->Redo();
				}
			}

			// Chars
			if (key.isChar && key.down)
			{
				host->SendKeyEvent(event);

				event.windows_key_code = key.charCode;
				event.type = KEYEVENT_CHAR;
			}

			host->SendKeyEvent(event);
		}
	}

	void UI::PushTextureUpdate(const void* buffer, int width, int height, int rowBytes, bool flipVertical)
	{
		if (UI::Texture && UI::Texture->IsReady() && UI::Buffer)
		{
			Ayria::IRenderer* renderer = Ayria::IRenderer::GetSingleton();
			if (!renderer) return;

			bool requiresRBSwap = (UI::Texture->GetFormat() == renderer->GetCorrectedFormat(DXGI_FORMAT_R8G8B8A8_UNORM));

			if (UI::Texture->GetFormat() != renderer->GetCorrectedFormat(DXGI_FORMAT_B8G8R8A8_UNORM) && !requiresRBSwap)
			{
				OutputDebugStringA("Error, invalid texture format!");
				return;
			}

			//memcpy(UI::Buffer, buffer, width * height * 4);
			for (int i = 0; i < height; i++)
			{
				char* source = (char*)buffer + rowBytes * (flipVertical ? (height - (i + 1)) : i);
				char* dest = UI::Buffer + width * 4 * i;

				memcpy(dest, source, width * 4);
			}

			// Swap red and blue channel
			for (int i = 0; i < width * height && requiresRBSwap; i++)
			{
				char _byte = UI::Buffer[i * 4];
				UI::Buffer[i * 4 + 0] = UI::Buffer[i * 4 + 2];
				UI::Buffer[i * 4 + 2] = _byte;
			}
		}
	}

	void UI::WatchViewport()
	{
		Ayria::IRenderer* renderer = Ayria::IRenderer::GetSingleton();

		if (renderer && UI::Texture && (renderer->Width() != UI::Texture->GetWidth() || renderer->Height() != UI::Texture->GetHeight()))
		{
			UI::Texture->Resize(renderer->Width(), renderer->Height());

			if (UI::Browser)
			{
				UI::Browser->GetHost()->WasResized();
			}
		}
	}

	void UI::UpdateTexture()
	{
		if (UI::EGLTexture)
		{
			if (!UI::DeviceContext)
			{
				if (UI::Device)
				{
					UI::Device->GetImmediateContext(&UI::DeviceContext);
				}
				else
				{
					OutputDebugStringA("D3D11 device is NULL, unable to retrieve its context!");
					return;
				}
			}

			if (UI::DeviceContext)
			{
				IDXGIKeyedMutex* keyedMutex = nullptr;
				UI::EGLTexture->QueryInterface(__uuidof(IDXGIKeyedMutex), (void**)&keyedMutex);
				if (keyedMutex)
				{
					if (keyedMutex->AcquireSync(1, 5) == S_OK && UI::Device)
					{
						if (UI::TempTexture)
						{
							D3D11_TEXTURE2D_DESC desc;
							UI::TempTexture->GetDesc(&desc);

							UI::DeviceContext->CopyResource(UI::TempTexture, UI::EGLTexture);

							D3D11_MAPPED_SUBRESOURCE texmap;
							if (SUCCEEDED(UI::DeviceContext->Map(UI::TempTexture, 0, D3D11_MAP_READ, 0, &texmap)))
							{
								UI::PushTextureUpdate(texmap.pData, desc.Width, desc.Height, texmap.RowPitch, true);

								UI::Texture->Update(UI::Buffer);
								UI::DeviceContext->Unmap(UI::TempTexture, 0);
							}
						}
						else
						{
							OutputDebugStringA("Temporary texture is NULL!");
						}
					}

					keyedMutex->ReleaseSync(0);
					keyedMutex->Release();
				}
				else
				{
					OutputDebugStringA("Unable to access libEGL's keyed mutex!\n");
				}
			}
			else
			{
				OutputDebugStringA("D3D11 context is NULL.\n");
			}
		}
		else
		{
			//OutputDebugStringA("LibEGL's shared texture is NULL, unable to update CEF texture\n");
		}
	}

	void UI::Present()
	{
		if (!UI::Initialized) return;

		CefDoMessageLoopWork();

		if (UI::Browser)
		{
			static bool loaded = false;
			if (!loaded)
			{
				loaded = true;
				//UI::Browser->GetFocusedFrame()->LoadURL("http://home.dekart811.net");
				UI::Browser->GetFocusedFrame()->LoadURL("ayria://index");
			}

			POINT pos = Ayria::IInput::CursorPos();

			CefMouseEvent event;
			event.x = pos.x;
			event.y = pos.y;

			event.modifiers |= EVENTFLAG_LEFT_MOUSE_BUTTON;
			event.modifiers |= EVENTFLAG_RIGHT_MOUSE_BUTTON;
			event.modifiers |= EVENTFLAG_MIDDLE_MOUSE_BUTTON;

			// Is that even needed?
			if (UI::IsCtrlPressed) event.modifiers |= EVENTFLAG_CONTROL_DOWN;

			CefRefPtr<CefBrowserHost> host = UI::Browser->GetHost();
			host->SendMouseMoveEvent(event, false);

			if (UI::Texture)
			{
				UI::UpdateTexture();
				UI::Texture->Draw(0, 0/*, Ayria::ITexture2D::Color(255, 255, 255, 200)*/);

			}

			UI::WatchViewport();
		}
	}

	void UI::OnLost()
	{
		// 	if (UI::Texture)
		// 	{
		// 		UI::Texture->OnLostDevice();
		// 	}
		if (UI::Texture)
		{
			delete UI::Texture;
			UI::Texture = NULL;
		}
	}

	void UI::OnReset()
	{
		// 	if (UI::Texture)
		// 	{
		// 		UI::Texture->OnResetDevice();
		// 	}
		UI::CreateTexture();
	}

	void UI::CreateTexture()
	{
		if (UI::Texture)
		{
			delete UI::Texture;
			UI::Texture = NULL;
		}

		if (UI::Buffer)
		{
			delete[] UI::Buffer;
			UI::Buffer = NULL;
		}

		Ayria::IRenderer* renderer = Ayria::IRenderer::GetSingleton();

		if (renderer)
		{
			UI::Buffer = new char[renderer->Width() * renderer->Height() * 4];

			DXGI_FORMAT format;
			UI::Texture = renderer->CreateTexture();

			if (UI::Texture)
			{
				if (renderer->IsSupported(renderer->GetCorrectedFormat(DXGI_FORMAT_B8G8R8A8_UNORM)))
				{
					OutputDebugStringA("ARGB supported!");
					format = renderer->GetCorrectedFormat(DXGI_FORMAT_B8G8R8A8_UNORM);
				}
				else if (renderer->IsSupported(renderer->GetCorrectedFormat(DXGI_FORMAT_R8G8B8A8_UNORM)))
				{
					OutputDebugStringA("ABGR supported!");
					format = renderer->GetCorrectedFormat(DXGI_FORMAT_R8G8B8A8_UNORM);
				}
				else
				{
					OutputDebugStringA("Nothing supported!");
					return;
				}

				if (!UI::Texture->Create(renderer->Width(), renderer->Height(), format))
				{
					OutputDebugStringA("Creating texture failed!");
				}
			}
			else
			{
				OutputDebugStringA("Allocating texture failed!");
			}
		}
	}

	void UI::SwapFrameHandler(void* surface)
	{
		static HANDLE lastParentHandle = 0;

		HANDLE parentHandle;
		if (egl::GetMainWindowSharedHandle(&parentHandle))
		{
			if (lastParentHandle != parentHandle && parentHandle)
			{
				if (UI::Device)
				{
					lastParentHandle = parentHandle;

					ID3D11Resource* resource = nullptr;
					if (SUCCEEDED(UI::Device->OpenSharedResource(parentHandle, __uuidof(IDXGIResource), (void**)&resource)))
					{
						ID3D11Texture2D* texture = UI::EGLTexture;
						if (SUCCEEDED(resource->QueryInterface(__uuidof(ID3D11Texture2D), (void**)&UI::EGLTexture)))
						{
							if (texture)
							{
								texture->Release();
							}

							if (UI::EGLTexture)
							{
								D3D11_TEXTURE2D_DESC desc = { 0 };
								UI::EGLTexture->GetDesc(&desc);

								desc.Usage = D3D11_USAGE_STAGING;
								desc.CPUAccessFlags = D3D11_CPU_ACCESS_READ;
								desc.BindFlags = 0;
								desc.MiscFlags = 0;

								if (UI::TempTexture)
								{
									UI::TempTexture->Release();
								}

								if (FAILED(UI::Device->CreateTexture2D(&desc, NULL, &UI::TempTexture)))
								{
									OutputDebugStringA("Failed to create temporary texture.\n");
								}
							}
							else
							{
								OutputDebugStringA("LibEGL's shared texture is NULL.\n");
							}
						}
						else
						{
							OutputDebugStringA("Unable to access libEGL's shared texture.\n");
						}
					}
					else
					{
						OutputDebugStringA("Unable to access libEGL's shared resource.\n");
					}
				}
				else
				{
					OutputDebugStringA("Temporary D3D11 device is NULL, unable to retrieve texture!\n");
				}
			}
		}
		else
		{
			OutputDebugStringA("Failed to retrieve libEGL's window handle!\n");
		}
	}

	void UI::CreateInstance()
	{
		CreateCEFInstance();
	}
	void UI::Initialize()
	{
		// Start CEF, if it's not initialized
		UI::StartCEF();

		egl::SetSwapFrameHandler(UI::SwapFrameHandler);

		// Create temporary device
		D3D11CreateDevice(nullptr, D3D_DRIVER_TYPE_HARDWARE, nullptr, 0, nullptr, 0, D3D11_SDK_VERSION, &UI::Device, nullptr, &UI::DeviceContext);

		if (!UI::Device)
		{
			OutputDebugStringA("Device creation failed!");
		}

		UI::CreateTexture();

		if (UI::Browser)
		{
			UI::Browser->GetHost()->WasResized();
			UI::Browser->Reload();
		}
	}
}
