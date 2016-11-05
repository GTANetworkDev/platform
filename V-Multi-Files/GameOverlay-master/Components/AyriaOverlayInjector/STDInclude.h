#pragma once

#define _CRT_SECURE_NO_WARNINGS
#define WIN32_LEAN_AND_MEAN
#include <windows.h>
#include <stdio.h>
#include <stdint.h>
#include <vector>
#include <math.h>

#ifdef _WIN64
#define PLATFORM_SHORTNAME "x64"
#elif _WIN32
#define PLATFORM_SHORTNAME "x86"
#else
#define PLATFORM_SHORTNAME "?"
#endif

#include "Utility\VariadicString.h"

