# Toccato Desktop Manager 1.0.1

Aplicativo desktop Windows em português para organizar clientes, conexões e credenciais em um cofre **local individual**. Repositório do projeto ToccatoRemoteDesktop (nome existente do repositório: `ToccatoRomoteDesktop`).

## Downloads da v1.0.1

- [Instalador Windows](releases/v1.0.1/ToccatoDesktopManager-1.0.1-Setup.exe)
- [Pacote completo: instalador, portátil, fontes e guia](releases/v1.0.1/ToccatoDesktopManager_1_0_1_Completo.zip)
- [Versão portátil](releases/v1.0.1/ToccatoDesktopManager-1.0.1-Portable.zip)
- [Checksums SHA-256](releases/v1.0.1/SHA256SUMS.txt)

As versões anteriores permanecem em [releases/v1.0.0](releases/v1.0.0). A v1.0.1 conserva o formato dos cofres e adiciona identidade oficial, temas e diagnóstico seguro de erros.

## Novidades da v1.0.1

- Logo oficial Toccato e ícone do aplicativo/instalador a partir do brasão fornecido.
- Paletas **Azul institucional**, **Azul e ciano** e **Azul e grafite**.
- Modos **Claro**, **Escuro** e **Automático (Windows)**, com aplicação imediata e preferências por usuário.
- Diálogos, tabelas, árvore e menus com cores consistentes e texto de botões legível.
- Validação de seleções no cadastro, incluindo conexão sem credencial, e diagnóstico copiável sem senhas ou conteúdo do cadastro.
- O erro anteriormente reportado não foi reproduzido; o diagnóstico passa a identificar o tipo e a origem de eventuais falhas.

Acesse **Exibir → Temas e aparência**. As preferências não modificam o formato do cofre.


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

Execute `ToccatoDesktopManager-1.0.1-Setup.exe` ou extraia o pacote portátil e execute `ToccatoDesktopManager.exe`. Requer .NET Framework 4.8+. Crie uma senha-mestra (mínimo 12 caracteres), um cliente, uma credencial e uma conexão. Configure executáveis externos em Configurações. Não há recuperação da senha-mestra.

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
| `docs/Guia_TDM_1_0_1.html` | Guia completo, matriz de escopo e homologação |
| `docs/ARQUITETURA.md` | Modelo de proteção e evolução para equipe |
| `build.ps1` | Build e empacotamento reproduzíveis no Windows |
| `Publicar_GitHub.ps1` | Script histórico de publicação inicial; o repositório atual já está integrado |
| `docs/VALIDACAO_1_0_1.md` | Resultados efetivamente executados e homologações pendentes |

Licenciamento e distribuição institucional: a definir pelo responsável pelo projeto.
