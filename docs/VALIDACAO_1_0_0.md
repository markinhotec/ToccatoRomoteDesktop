# Validação da entrega 1.0.0

Data: 06/11/2026. Esta entrega foi compilada e testada em Linux com Mono 6.12.0.199 e NSIS 3.11. Não houve acesso a uma máquina Windows ou servidor de cliente para homologação de ponta a ponta.

## Executado

| Verificação | Resultado |
| --- | --- |
| Compilação C# em modo WinExe, AnyCPU, com manifesto e ícone | Concluída, sem avisos na compilação final |
| 25 testes automatizados de segurança, modelo e conectores | 25 aprovados, 0 falhas |
| Verificação independente do formato criptográfico com Python/cryptography | Aprovada: PBKDF2-SHA256, HMAC-SHA256, AES-256-CBC e conteúdo sintético decriptado |
| 14 verificações da interface com Mono/Xvfb | Aprovadas: formulários, busca por nome/IP/credencial, resultado vazio, máscara de senha, árvore e layout mínimo |
| Captura da janela real com dados fictícios | Gerada e inspecionada visualmente |
| Compilação NSIS do instalador Unicode por usuário | Concluída, sem avisos |
| Formato dos executáveis | Aplicativo PE32 .NET AnyCPU e instalador PE32 NSIS |
| XML do projeto, configuração e manifesto | Sintaxe validada |
| YAML do workflow Windows | Sintaxe validada |
| Escrita no repositório pelo conector GitHub | Recusada: HTTP 403, Resource not accessible by integration |

Os 25 testes cobrem política de senha, serialização e cadeia de auditoria, alteração de registros, IDs duplicados, referências ausentes, validação de destinos, esquemas de URL, quoting de argumentos, ausência de senhas nos argumentos de lançadores, criptografia de metadados, abertura por senha correta, rejeição de senha incorreta, adulteração, truncamento, custo KDF inválido, exclusão mútua, rollback, backup, IV aleatório, troca de senha, conflito externo e conectividade TCP em loopback.

## Ainda não executado

- Instalação, reinstalação e desinstalação no Windows x86/x64.
- Ativação e configuração do controle RDP ActiveX no Windows. O teste condicional está incluído no workflow, mas não foi executado nesta sessão.
- Login RDP/NLA real, certificados, vários monitores e interação de sessão.
- Instalação e autenticação dos clientes externos, VPN RAS real, MFA dos destinos e transferência de arquivos.
- Validação visual em escalas de DPI e versões de Windows usadas pela equipe.
- Revisão independente de segurança, testes de intrusão e avaliação de conformidade.

O arquivo `.github/workflows/windows-build.yml` contém um processo de build nativo Windows, testes do cofre e da interface, configuração do componente RDP sem conectar e teste de instalação/desinstalação silenciosa com preservação de um arquivo sintético. Não há execução ou resultado de CI a atribuir a esta entrega: a integração GitHub recusou a primeira gravação e nenhum arquivo foi enviado.

## Distribuição

O pacote contém binários .NET gerados em Linux/Mono, instalador NSIS, modo portátil, fontes, guia, imagem de demonstração e checksums SHA-256. A aplicação não depende de Mono no Windows; depende do .NET Framework 4.8+. O instalador não possui assinatura digital de editor. Os certificados de assinatura e a política de distribuição institucional não foram fornecidos.

Use contas e destinos de homologação antes de cadastrar credenciais de produção. Consulte o roteiro no guia. O produto é uma base inicial local; os recursos corporativos futuros da matriz não são apresentados como implementados.
