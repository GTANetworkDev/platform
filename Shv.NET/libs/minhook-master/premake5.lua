project "libMinHook.x64"
	language "C++"
	kind "StaticLib"
	targetname "libMinHook.x64"
	
	includedirs { 
	}
	
	libdirs {
	}
	
	links { }	
	
	defines { "WIN32", "_LIB", "_CRT_NONSTDC_NO_DEPRECATE" }
	
	--[[pchheader "StdInc.h"
	pchsource "StdInc.cpp"]]
	
	vpaths { 
		["Headers/*"] = "*.h",
		["Sources/*"] = "*.cpp",
		["*"] = "premake5.lua"
	}
	
	files {
		"premake5.lua",
		"src/**.h",
		"src/**.c"
	}
	