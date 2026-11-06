# Toccato Desktop Manager 1.0.0

Aplicativo desktop Windows em português para organizar clientes, conexões e credenciais em um cofre **local individual**. Repositório do projeto ToccatoRemoteDesktop (nome existente do repositório: `ToccatoRomoteDesktop`).

## O que está implementado

- Clientes, pastas/subpastas, favoritos, recentes, tags e busca por `Ctrl+Space`.
- Cofre criptografado com AES-256-CBC + HMAC-SHA256, PBKDF2-HMAC-SHA256 (600.000 iterações), senha-mestra e backup.
- Credenciais separadas, reutilizáveis e vinculadas às conexões.
- RDP integrado em abas pelo componente do Windows, NLA, alerta de identidade, janela destacável e injeção da senha em memória.
- RDP externo com `/multimon`, sem injeção de senha do cofre.
- Lançadores de OpenSSH, PuTTY (Telnet/Serial), WinSCP (SFTP/SCP/FTP), TigerVNC, AnyDesk, TeamViewer e navegador.
- VPN RAS do Windows antes da conexão, sem senha em argumentos.
- Diagnósticos TCP/ping/DNS e PowerShell local/macros com revisão e confirmação.
- Auditoria local com cadeia SHA-256 dentro do cofre; bloqueio por inatividade e bloqueio da sessão do Windows.
- Instalador por usuário e pacote portátil. Nenhum cliente externo é redistribuído.

## Limites da v1.0

Esta entrega **não implementa** 150 integrações nativas, PAM/JIT, RBAC de equipe, MFA/SSO, gravação de vídeo, APIs de nuvem/virtualização, cofres externos ou bancos compartilhados SQL Server/PostgreSQL/MySQL. O acesso a um portal Web não é uma integração nativa com sua API. SSH e outros clientes externos solicitam autenticação em suas próprias janelas.

Não há certificação de conformidade, auditoria independente de segurança ou homologação de logins reais em ambientes de clientes. O guia detalha o comportamento, as limitações e o roteiro de homologação.

## Instalação e primeiro uso

Execute `ToccatoDesktopManager-1.0.0-Setup.exe` ou extraia o pacote portátil e execute `ToccatoDesktopManager.exe`. Requer .NET Framework 4.8+. Crie uma senha-mestra (mínimo 12 caracteres), um cliente, uma credencial e uma conexão. Configure executáveis externos em Configurações. Não há recuperação da senha-mestra.

Para visualizar dados fictícios sem conectar: `ToccatoDesktopManager.exe --demo`.

Alvo de homologação: Windows 10/11 e Windows Server 2016+ com interface gráfica, x86/x64. Sistemas legados que aceitam .NET 4.8 não são automaticamente homologados. Não há garantia de suporte a qualquer versão do Windows.

## Compilar no Windows

Instale .NET Framework 4.8 e NSIS 3. Use Windows PowerShell 5.1:

```powershell
.\build.ps1
# Somente portátil, sem NSIS:
.\build.ps1 -SkipInstaller
```

O build usa o compilador C# do .NET Framework e não precisa de NuGet ou Visual Studio. Há também um projeto MSBuild em `src/Toccato.Desktop`.

O workflow `windows-build.yml` compila, executa testes do cofre e da interface, configura o ActiveX RDP sem conectar, renderiza a interface e testa instalação/desinstalação silenciosa com preservação de um arquivo de usuário sintético. Os binários de distribuição são mantidos nos artifacts do build; segredos e arquivos de cofre não pertencem ao Git.

## Estrutura

| Caminho | Conteúdo |
| --- | --- |
| `src/Toccato.Desktop/` | Aplicativo C# WinForms, persistência e conectores |
| `installer/` | Instalador NSIS por usuário |
| `tests/` | Testes do cofre, argumentos de processo e interface |
| `docs/Guia_TDM_1_0.html` | Guia completo, matriz de escopo e homologação |
| `docs/ARQUITETURA.md` | Modelo de proteção e evolução para equipe |
| `build.ps1` | Build e empacotamento reproduzíveis no Windows |
| `Publicar_GitHub.ps1` | Envio inicial de fontes e tag para o repositório ainda vazio, usando a autenticação local do Git |
| `docs/VALIDACAO_1_0_0.md` | Resultados efetivamente executados e homologações pendentes |

Licenciamento e distribuição institucional: a definir pelo responsável pelo projeto.
