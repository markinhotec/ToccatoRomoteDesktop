Unicode true
!include "MUI2.nsh"
!include "LogicLib.nsh"
!include "x64.nsh"
!ifndef SOURCE
  !define SOURCE "..\artifacts\1.0.1\portable"
!endif
!ifndef OUTPUT
  !define OUTPUT "..\artifacts\1.0.1\ToccatoDesktopManager-1.0.1-Setup.exe"
!endif
Name "Toccato Desktop Manager"
OutFile "${OUTPUT}"
InstallDir "$LOCALAPPDATA\Programs\Toccato Desktop Manager"
InstallDirRegKey HKCU "Software\Toccato\DesktopManager" "InstallDir"
RequestExecutionLevel user
SetCompressor /SOLID lzma
BrandingText "Toccato Desktop Manager 1.0.1"
VIProductVersion "1.0.1.0"
VIAddVersionKey "ProductName" "Toccato Desktop Manager"
VIAddVersionKey "FileDescription" "Instalador Toccato Desktop Manager"
VIAddVersionKey "FileVersion" "1.0.1.0"
VIAddVersionKey "ProductVersion" "1.0.1.0"
VIAddVersionKey "LegalCopyright" "Toccato"
!define MUI_ABORTWARNING
!define MUI_ICON "..\assets\tdm.ico"
!define MUI_UNICON "..\assets\tdm.ico"
!define MUI_WELCOMEPAGE_TITLE "Toccato Desktop Manager 1.0.1"
!define MUI_WELCOMEPAGE_TEXT "Organize clientes, conexões remotas e credenciais em um workspace local.$\r$\n$\r$\nO TDM será instalado apenas para o seu usuário. Seus cofres não são removidos na desinstalação.$\r$\n$\r$\nRequer .NET Framework 4.8 ou superior."
!insertmacro MUI_PAGE_WELCOME
!define MUI_LICENSEPAGE_HEADER_TEXT "Informações da versão 1.0.1"
!define MUI_LICENSEPAGE_HEADER_SUBTEXT "Leia os requisitos e o escopo desta entrega."
!define MUI_LICENSEPAGE_TEXT_TOP "Informações de instalação e uso do Toccato Desktop Manager:"
!define MUI_LICENSEPAGE_TEXT_BOTTOM "Clique em Continuar para prosseguir com a instalação."
!define MUI_LICENSEPAGE_BUTTON "Continuar"
!insertmacro MUI_PAGE_LICENSE "..\docs\LEIA-ME.txt"
!insertmacro MUI_PAGE_COMPONENTS
!insertmacro MUI_PAGE_DIRECTORY
!insertmacro MUI_PAGE_INSTFILES
!define MUI_FINISHPAGE_RUN "$INSTDIR\ToccatoDesktopManager.exe"
!define MUI_FINISHPAGE_RUN_TEXT "Abrir o Toccato Desktop Manager"
!insertmacro MUI_PAGE_FINISH
!insertmacro MUI_UNPAGE_CONFIRM
!insertmacro MUI_UNPAGE_INSTFILES
!insertmacro MUI_LANGUAGE "PortugueseBR"

!macro RunningCheck
  System::Call 'kernel32::OpenMutexW(i 0x00100000, i 0, w "Local\ToccatoDesktopManager.Running") p.r0'
  ${If} $0 != 0
    System::Call 'kernel32::CloseHandle(p r0)'
    MessageBox MB_ICONEXCLAMATION|MB_OK "Feche o Toccato Desktop Manager e suas sessões antes de continuar."
    Abort
  ${EndIf}
!macroend

Function .onInit
  !insertmacro RunningCheck
  SetShellVarContext current
  ${If} ${RunningX64}
    SetRegView 64
  ${EndIf}
  ReadRegDWORD $0 HKLM "SOFTWARE\Microsoft\NET Framework Setup\NDP\v4\Full" "Release"
  ${If} $0 < 528040
    SetRegView 32
    ReadRegDWORD $0 HKLM "SOFTWARE\Microsoft\NET Framework Setup\NDP\v4\Full" "Release"
  ${EndIf}
  SetRegView 32
  ${If} $0 < 528040
    MessageBox MB_YESNO|MB_ICONINFORMATION "É necessário instalar o Microsoft .NET Framework 4.8 antes do TDM.$\r$\n$\r$\nAbrir a página oficial da Microsoft? Após instalar o .NET, execute este instalador novamente." IDNO no_dotnet
    ExecShell "open" "https://dotnet.microsoft.com/pt-br/download/dotnet-framework/net48"
    no_dotnet:
    SetErrorLevel 2
    Abort
  ${EndIf}
FunctionEnd

Section "Toccato Desktop Manager (obrigatório)" MainSection
  SectionIn RO
  SetOutPath "$INSTDIR"
  SetOverwrite on
  ClearErrors
  File "${SOURCE}\ToccatoDesktopManager.exe"
  File "${SOURCE}\ToccatoDesktopManager.exe.config"
  File "${SOURCE}\tdm.ico"
  File "${SOURCE}\Guia_TDM_1_0_1.html"
  File "${SOURCE}\LEIA-ME.txt"
  ${If} ${Errors}
    MessageBox MB_ICONSTOP "A instalação não conseguiu copiar todos os arquivos. Verifique permissões e espaço livre."
    Abort
  ${EndIf}
  Delete "$INSTDIR\Guia_TDM_1_0.html"
  WriteUninstaller "$INSTDIR\Desinstalar.exe"
  CreateDirectory "$SMPROGRAMS\Toccato Desktop Manager"
  CreateShortCut "$SMPROGRAMS\Toccato Desktop Manager\Toccato Desktop Manager.lnk" "$INSTDIR\ToccatoDesktopManager.exe" "" "$INSTDIR\tdm.ico"
  CreateShortCut "$SMPROGRAMS\Toccato Desktop Manager\Guia de uso.lnk" "$INSTDIR\Guia_TDM_1_0_1.html"
  CreateShortCut "$SMPROGRAMS\Toccato Desktop Manager\Desinstalar.lnk" "$INSTDIR\Desinstalar.exe"
  WriteRegStr HKCU "Software\Toccato\DesktopManager" "InstallDir" "$INSTDIR"
  WriteRegStr HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\ToccatoDesktopManager" "DisplayName" "Toccato Desktop Manager"
  WriteRegStr HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\ToccatoDesktopManager" "DisplayVersion" "1.0.1"
  WriteRegStr HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\ToccatoDesktopManager" "Publisher" "Toccato"
  WriteRegStr HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\ToccatoDesktopManager" "UninstallString" '$\"$INSTDIR\Desinstalar.exe$\"'
  WriteRegStr HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\ToccatoDesktopManager" "QuietUninstallString" '$\"$INSTDIR\Desinstalar.exe$\" /S'
  WriteRegStr HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\ToccatoDesktopManager" "DisplayIcon" "$INSTDIR\ToccatoDesktopManager.exe"
  WriteRegDWORD HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\ToccatoDesktopManager" "NoModify" 1
  WriteRegDWORD HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\ToccatoDesktopManager" "NoRepair" 1
SectionEnd
Section "Atalho na área de trabalho" DesktopSection
  CreateShortCut "$DESKTOP\Toccato Desktop Manager.lnk" "$INSTDIR\ToccatoDesktopManager.exe" "" "$INSTDIR\tdm.ico"
SectionEnd
Function un.onInit
  !insertmacro RunningCheck
  SetShellVarContext current
FunctionEnd
Section "Uninstall"
  Delete "$INSTDIR\ToccatoDesktopManager.exe"
  Delete "$INSTDIR\ToccatoDesktopManager.exe.config"
  Delete "$INSTDIR\tdm.ico"
  Delete "$INSTDIR\Guia_TDM_1_0_1.html"
  Delete "$INSTDIR\Guia_TDM_1_0.html"
  Delete "$INSTDIR\LEIA-ME.txt"
  Delete "$INSTDIR\Desinstalar.exe"
  Delete "$DESKTOP\Toccato Desktop Manager.lnk"
  Delete "$SMPROGRAMS\Toccato Desktop Manager\Toccato Desktop Manager.lnk"
  Delete "$SMPROGRAMS\Toccato Desktop Manager\Guia de uso.lnk"
  Delete "$SMPROGRAMS\Toccato Desktop Manager\Desinstalar.lnk"
  RMDir "$SMPROGRAMS\Toccato Desktop Manager"
  RMDir "$INSTDIR"
  DeleteRegKey HKCU "Software\Toccato\DesktopManager"
  DeleteRegKey HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\ToccatoDesktopManager"
  ; User-created vaults, even if placed in INSTDIR, are deliberately preserved.
SectionEnd
