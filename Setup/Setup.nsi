;NSIS Modern User Interface
;GTA NETWORK INSTALLER

  !include "MUI2.nsh"

  Name "GTA Network"
  OutFile "GTANSetup.exe"

  InstallDir "$PROGRAMFILES64\GTA Network"

  RequestExecutionLevel admin

  !define MUI_ABORTWARNING
  
  !insertmacro MUI_PAGE_LICENSE "License.txt"
  !insertmacro MUI_PAGE_DIRECTORY
  !insertmacro MUI_PAGE_INSTFILES
  
  !insertmacro MUI_UNPAGE_CONFIRM
  !insertmacro MUI_UNPAGE_INSTFILES
  !insertmacro MUI_LANGUAGE "English"

Section "Client" SecDummy

  SetOutPath "$INSTDIR"
  File /r "C:\GTA Network\*"
  CreateShortCut "$DESKTOP\GTA Network.lnk" "$INSTDIR\GTANLauncher.exe" ""
  WriteUninstaller "$INSTDIR\Uninstall.exe"

SectionEnd

Section "Uninstall"

  Delete "$INSTDIR\Uninstall.exe"
  Delete "$DESKTOP\GTA Network.lnk"
  RMDir /r /REBOOTOK "$INSTDIR"
  DeleteRegKey /ifempty HKLM "HKEY_LOCAL_MACHINE\SOFTWARE\WOW6432Node\Rockstar Games\Grand Theft Auto V\GTANetworkInstallDir"

SectionEnd