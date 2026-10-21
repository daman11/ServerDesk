Unicode true
RequestExecutionLevel user
SetCompressor /SOLID zlib
!include "MUI2.nsh"
!include "LogicLib.nsh"
!include "FileFunc.nsh"
!include "x64.nsh"
!include "WinVer.nsh"
!include "nsDialogs.nsh"

!ifndef VERSION
  !error "VERSION is required"
!endif
!ifndef PAYLOAD
  !error "PAYLOAD is required"
!endif
!define PRODUCT_KEY "Software\Microsoft\Windows\CurrentVersion\Uninstall\ServerDesk"
Name "ServerDesk ${VERSION} Public Beta"
OutFile "${OUTPUT}"
InstallDir "$LOCALAPPDATA\Programs\ServerDesk"
InstallDirRegKey HKCU "${PRODUCT_KEY}" "InstallLocation"
Icon "..\..\src\ServerDesk.App\Assets\Brand\serverdesk.ico"
UninstallIcon "..\..\src\ServerDesk.App\Assets\Brand\serverdesk.ico"
VIProductVersion "${VERSION}.0"
VIAddVersionKey /LANG=1033 "ProductName" "ServerDesk Public Beta"
VIAddVersionKey /LANG=1033 "FileDescription" "ServerDesk per-user installer"
VIAddVersionKey /LANG=1033 "FileVersion" "${VERSION}"
VIAddVersionKey /LANG=1033 "LegalCopyright" "Copyright (c) 2026 ServerDesk contributors"
BrandingText "ServerDesk · MIT · Public Beta"
ShowInstDetails show
ShowUninstDetails show
AutoCloseWindow false
Var NoRegister
Var DesktopShortcut
Var DesktopShortcutOverride
Var DesktopShortcutControl
Var ShortcutOptionsDirectory
Var PreviousDesktopShortcut

!define MUI_ABORTWARNING
!define MUI_WELCOMEPAGE_TITLE "ServerDesk ${VERSION} Public Beta"
!define MUI_WELCOMEPAGE_TEXT "SSH · SFTP · RDP · VNC · Serial$\r$\n$\r$\n$(WelcomeText)"
!insertmacro MUI_PAGE_WELCOME
!insertmacro MUI_PAGE_LICENSE "..\..\LICENSE"
!insertmacro MUI_PAGE_DIRECTORY
Page custom DesktopShortcutPage DesktopShortcutPageLeave
!define MUI_PAGE_CUSTOMFUNCTION_PRE CheckNotRunning
!insertmacro MUI_PAGE_INSTFILES
!define MUI_FINISHPAGE_RUN "$INSTDIR\ServerDesk.App.exe"
!define MUI_FINISHPAGE_RUN_NOTCHECKED
!insertmacro MUI_PAGE_FINISH
!insertmacro MUI_UNPAGE_CONFIRM
!define MUI_PAGE_CUSTOMFUNCTION_PRE un.CheckNotRunning
!insertmacro MUI_UNPAGE_INSTFILES
!insertmacro MUI_LANGUAGE "English"
!insertmacro MUI_LANGUAGE "Polish"
LangString WelcomeText ${LANG_ENGLISH} "Installs for your Windows account. Close ServerDesk and finish active sessions before updating. User data and remembered credentials are preserved. This beta has no trusted publisher signature."
LangString WelcomeText ${LANG_POLISH} "Instalacja dla bieżącego konta Windows. Przed aktualizacją zakończ sesje i zamknij ServerDesk. Dane i zapamiętane hasła zostaną zachowane. Ta beta nie ma podpisu zaufanego wydawcy."
LangString RunningText ${LANG_ENGLISH} "Close ServerDesk in the selected directory and finish active sessions, then run Setup again. No files have been replaced."
LangString RunningText ${LANG_POLISH} "Zakończ sesje i zamknij ServerDesk z wybranego katalogu, a następnie uruchom instalator ponownie. Pliki nie zostały zastąpione."
LangString RequirementsText ${LANG_ENGLISH} "ServerDesk requires Windows 10 or newer on an x64 system."
LangString RequirementsText ${LANG_POLISH} "ServerDesk wymaga Windows 10 lub nowszego na komputerze x64."
LangString ShortcutTitle ${LANG_ENGLISH} "Shortcuts"
LangString ShortcutTitle ${LANG_POLISH} "Skróty"
LangString ShortcutDescription ${LANG_ENGLISH} "Choose how to open ServerDesk. The Start menu shortcut is created automatically."
LangString ShortcutDescription ${LANG_POLISH} "Wybierz sposób uruchamiania ServerDesk. Skrót w menu Start powstaje automatycznie."
LangString DesktopShortcutText ${LANG_ENGLISH} "Create a desktop shortcut"
LangString DesktopShortcutText ${LANG_POLISH} "Utwórz skrót na pulpicie"

Function .onInit
  ${IfNot} ${RunningX64}
  ${OrIfNot} ${AtLeastWin10}
    MessageBox MB_ICONSTOP "$(RequirementsText)"
    SetErrorLevel 2
    Quit
  ${EndIf}
  SetShellVarContext current
  StrCpy $NoRegister 0
  ${GetParameters} $0
  ClearErrors
  ${GetOptions} $0 "/NOREGISTER" $1
  ${IfNot} ${Errors}
    StrCpy $NoRegister 1
  ${EndIf}
  StrCpy $DesktopShortcutOverride ""
  ClearErrors
  ${GetOptions} $0 "/DESKTOPSHORTCUT=" $DesktopShortcutOverride
  ${IfNot} ${Errors}
    ${If} $DesktopShortcutOverride != "0"
    ${AndIf} $DesktopShortcutOverride != "1"
      SetErrorLevel 2
      Quit
    ${EndIf}
  ${EndIf}
  Call LoadDesktopShortcutChoice
FunctionEnd

Function LoadDesktopShortcutChoice
  ${If} $ShortcutOptionsDirectory != $INSTDIR
    StrCpy $ShortcutOptionsDirectory $INSTDIR
    ReadINIStr $PreviousDesktopShortcut "$INSTDIR\install-state.ini" "Setup" "DesktopShortcut"
    StrCpy $DesktopShortcut 0
    ${If} $PreviousDesktopShortcut == "1"
      StrCpy $DesktopShortcut 1
    ${EndIf}
    ${If} $DesktopShortcutOverride != ""
      StrCpy $DesktopShortcut $DesktopShortcutOverride
    ${EndIf}
  ${EndIf}
FunctionEnd

Function DesktopShortcutPage
  Call LoadDesktopShortcutChoice
  !insertmacro MUI_HEADER_TEXT "$(ShortcutTitle)" "$(ShortcutDescription)"
  nsDialogs::Create 1018
  Pop $0
  ${If} $0 == error
    Abort
  ${EndIf}
  ${NSD_CreateCheckbox} 0 12u 100% 24u "$(DesktopShortcutText)"
  Pop $DesktopShortcutControl
  ${NSD_SetState} $DesktopShortcutControl $DesktopShortcut
  nsDialogs::Show
FunctionEnd

Function DesktopShortcutPageLeave
  ${NSD_GetState} $DesktopShortcutControl $DesktopShortcut
FunctionEnd

!macro CHECK_IDLE Prefix
Function ${Prefix}CheckNotRunning
  ; Refuse an upgrade/uninstall while any application binary is in use.
  ; Never terminate a session or schedule partial replacement on reboot.
  !include "${BUSY_INCLUDE}"
FunctionEnd
!macroend
!insertmacro CHECK_IDLE ""
!insertmacro CHECK_IDLE "un."

Section "ServerDesk" Main
  Call CheckNotRunning
  Call LoadDesktopShortcutChoice
  SetOutPath "$INSTDIR"
  SetOverwrite on
  !include "${CLEAN_INCLUDE}"
  ClearErrors
  File /r "${PAYLOAD}\*"
  ${If} ${Errors}
    SetErrorLevel 3
    Abort
  ${EndIf}
  WriteINIStr "$INSTDIR\install-state.ini" "Setup" "NoRegister" "$NoRegister"
  WriteINIStr "$INSTDIR\install-state.ini" "Setup" "DesktopShortcut" "$DesktopShortcut"
  WriteUninstaller "$INSTDIR\Uninstall.exe"
  ${If} $NoRegister == 0
    CreateShortcut "$SMPROGRAMS\ServerDesk.lnk" "$INSTDIR\ServerDesk.App.exe" "" "$INSTDIR\ServerDesk.App.exe"
    ${If} $DesktopShortcut == 1
      CreateShortcut "$DESKTOP\ServerDesk.lnk" "$INSTDIR\ServerDesk.App.exe" "" "$INSTDIR\ServerDesk.App.exe"
    ${ElseIf} $PreviousDesktopShortcut == 1
      ReadRegStr $0 HKCU "${PRODUCT_KEY}" "InstallLocation"
      ${If} $0 == $INSTDIR
        Delete "$DESKTOP\ServerDesk.lnk"
      ${EndIf}
    ${EndIf}
    WriteRegStr HKCU "${PRODUCT_KEY}" "DisplayName" "ServerDesk Public Beta"
    WriteRegStr HKCU "${PRODUCT_KEY}" "DisplayVersion" "${VERSION}"
    WriteRegStr HKCU "${PRODUCT_KEY}" "InstallLocation" "$INSTDIR"
    WriteRegStr HKCU "${PRODUCT_KEY}" "DisplayIcon" "$INSTDIR\ServerDesk.App.exe"
    WriteRegStr HKCU "${PRODUCT_KEY}" "Publisher" "ServerDesk contributors"
    WriteRegStr HKCU "${PRODUCT_KEY}" "UninstallString" '$\"$INSTDIR\Uninstall.exe$\"'
    WriteRegStr HKCU "${PRODUCT_KEY}" "QuietUninstallString" '$\"$INSTDIR\Uninstall.exe$\" /S'
    WriteRegDWORD HKCU "${PRODUCT_KEY}" "NoModify" 1
    WriteRegDWORD HKCU "${PRODUCT_KEY}" "NoRepair" 1
    WriteRegDWORD HKCU "${PRODUCT_KEY}" "EstimatedSize" ${SIZE_KB}
  ${EndIf}
SectionEnd

Section "Uninstall"
  SetShellVarContext current
  Call un.CheckNotRunning
  ReadRegStr $0 HKCU "${PRODUCT_KEY}" "InstallLocation"
  ${If} $0 == $INSTDIR
    Delete "$SMPROGRAMS\ServerDesk.lnk"
    ReadINIStr $1 "$INSTDIR\install-state.ini" "Setup" "DesktopShortcut"
    ${If} $1 == 1
      Delete "$DESKTOP\ServerDesk.lnk"
    ${EndIf}
    DeleteRegKey HKCU "${PRODUCT_KEY}"
  ${EndIf}
  ; Generated exact owned files. No recursive directory deletion and no AppData operations.
  !include "${UNINSTALL_INCLUDE}"
  Delete "$INSTDIR\install-state.ini"
  Delete "$INSTDIR\Uninstall.exe"
  RMDir "$INSTDIR"
SectionEnd
