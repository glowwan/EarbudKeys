; EarbudKeys installer (NSIS). Build on any machine with makensis:
;   makensis installer.nsi
; Produces Setup-EarbudKeys-1.0.0.exe. Requires EarbudKeys.exe,
; README.md and LICENSE next to this script at compile time.
!include "MUI2.nsh"
!include "LogicLib.nsh"

!define APP_NAME "蓝牙耳机语音键"
!define APP_EXE "EarbudKeys.exe"
!define APP_VERSION "1.0.0"
!define APP_PUBLISHER "glowwan"
!define APP_URL "https://github.com/glowwan/dsh-bluetooth-earbuds"
!define UNINST_KEY "Software\Microsoft\Windows\CurrentVersion\Uninstall\EarbudKeys"

Name "${APP_NAME} ${APP_VERSION}"
OutFile "Setup-EarbudKeys-${APP_VERSION}.exe"
InstallDir "$PROGRAMFILES\EarbudKeys"
InstallDirRegKey HKLM "Software\EarbudKeys" "InstallDir"
RequestExecutionLevel admin
ShowInstDetails show

!define MUI_ABORTWARNING
!define MUI_FINISHPAGE_RUN "$INSTDIR\${APP_EXE}"
!define MUI_FINISHPAGE_RUN_TEXT "运行蓝牙耳机语音键"
!insertmacro MUI_PAGE_WELCOME
!insertmacro MUI_PAGE_COMPONENTS
!insertmacro MUI_PAGE_DIRECTORY
!insertmacro MUI_PAGE_INSTFILES
!insertmacro MUI_PAGE_FINISH
!insertmacro MUI_UNPAGE_CONFIRM
!insertmacro MUI_UNPAGE_INSTFILES
!insertmacro MUI_LANGUAGE "SimpChinese"
!insertmacro MUI_LANGUAGE "English"

Section "${APP_NAME}" SecMain
  SectionIn RO
  SetOutPath "$INSTDIR"
  ; Stop a running copy first (best effort).
  nsExec::ExecToStack 'taskkill /F /IM ${APP_EXE}'
  Pop $0
  Pop $1
  File "${APP_EXE}"
  File "README.md"
  File "LICENSE"
  WriteRegStr HKLM "Software\EarbudKeys" "InstallDir" "$INSTDIR"
  WriteRegStr HKLM "${UNINST_KEY}" "DisplayName" "${APP_NAME} ${APP_VERSION}"
  WriteRegStr HKLM "${UNINST_KEY}" "Publisher" "${APP_PUBLISHER}"
  WriteRegStr HKLM "${UNINST_KEY}" "DisplayVersion" "${APP_VERSION}"
  WriteRegStr HKLM "${UNINST_KEY}" "URLInfoAbout" "${APP_URL}"
  WriteRegStr HKLM "${UNINST_KEY}" "UninstallString" '"$INSTDIR\uninstall.exe"'
  WriteRegStr HKLM "${UNINST_KEY}" "QuietUninstallString" '"$INSTDIR\uninstall.exe" /S'
  WriteRegDWORD HKLM "${UNINST_KEY}" "NoModify" 1
  WriteRegDWORD HKLM "${UNINST_KEY}" "NoRepair" 1
  WriteUninstaller "$INSTDIR\uninstall.exe"
  CreateDirectory "$SMPROGRAMS\${APP_NAME}"
  CreateShortCut "$SMPROGRAMS\${APP_NAME}\${APP_NAME}.lnk" "$INSTDIR\${APP_EXE}" "" "$INSTDIR\${APP_EXE}" 0
  CreateShortCut "$SMPROGRAMS\${APP_NAME}\卸载.lnk" "$INSTDIR\uninstall.exe" "" "$INSTDIR\uninstall.exe" 0
SectionEnd

Section "桌面快捷方式" SecDesktop
  CreateShortCut "$DESKTOP\${APP_NAME}.lnk" "$INSTDIR\${APP_EXE}" "" "$INSTDIR\${APP_EXE}" 0
SectionEnd

Section "开机自启动" SecAutoStart
  WriteRegStr HKCU "Software\Microsoft\Windows\CurrentVersion\Run" "EarbudKeys" '"$INSTDIR\${APP_EXE}"'
SectionEnd

!insertmacro MUI_FUNCTION_DESCRIPTION_BEGIN
  !insertmacro MUI_DESCRIPTION_TEXT ${SecMain} "主程序、说明和许可证（必装）。"
  !insertmacro MUI_DESCRIPTION_TEXT ${SecDesktop} "在桌面上创建一个快捷方式。"
  !insertmacro MUI_DESCRIPTION_TEXT ${SecAutoStart} "Windows 登录后自动在后台运行。"
!insertmacro MUI_FUNCTION_DESCRIPTION_END

Section "Uninstall"
  nsExec::ExecToStack 'taskkill /F /IM ${APP_EXE}'
  Pop $0
  Pop $1
  Delete "$INSTDIR\${APP_EXE}"
  Delete "$INSTDIR\README.md"
  Delete "$INSTDIR\LICENSE"
  Delete "$INSTDIR\uninstall.exe"
  Delete "$SMPROGRAMS\${APP_NAME}\${APP_NAME}.lnk"
  Delete "$SMPROGRAMS\${APP_NAME}\卸载.lnk"
  RMDir "$SMPROGRAMS\${APP_NAME}"
  Delete "$DESKTOP\${APP_NAME}.lnk"
  DeleteRegKey HKLM "Software\EarbudKeys"
  DeleteRegKey HKLM "${UNINST_KEY}"
  DeleteRegValue HKCU "Software\Microsoft\Windows\CurrentVersion\Run" "EarbudKeys"
  MessageBox MB_YESNO "是否同时删除已保存的耳机映射（位于 %APPDATA%\EarbudKeys\settings.json）？" IDYES deleteSettings
  Goto done
  deleteSettings:
    Delete "$APPDATA\EarbudKeys\settings.json"
    RMDir "$APPDATA\EarbudKeys"
  done:
SectionEnd
