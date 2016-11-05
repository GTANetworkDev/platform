#pragma once

#define _CRT_SECURE_NO_WARNINGS
#define WIN32_LEAN_AND_MEAN
#include <windows.h>

// C/C++ headers
#include <map>
#include <vector>
#include <mutex>
#include <queue>
#include <functional>
#include <direct.h>
#include <process.h>
#include <Tlhelp32.h>
#include <shellapi.h>
#include <shlwapi.h>
#include <shlobj.h>
#include <winbase.h>
#include <conio.h>
#include <time.h>

// custom headers
#include "Utils\Utils.h"
#include "Utils\Hooking.h"

#include "include/cef_app.h"
#include "include/cef_base.h"
#include "include/cef_browser.h"
#include "include/cef_client.h"
#include "include/cef_command_line.h"
#include "include/cef_frame.h"
#include "include/cef_runnable.h"
#include "include/cef_web_plugin.h"

#pragma comment(lib, "shlwapi.lib")
#pragma comment(lib, "d3d11.lib")
#pragma comment(lib, "libGLESv2.dll.lib")
#pragma comment(lib, "libcef.dll.lib")
#pragma comment(lib, "libcef_dll_wrapper.lib")

#include <AyriaInterface\Main.h>

#include "APIClass.h"

#include "Javascript\JSBridge.h"

#include "CEF\ExtendedUI.h"
#include "API.h"
