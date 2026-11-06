# Arquitetura — TDM 1.0

## Aplicativo

WinForms em C#, sem pacotes NuGet, direcionado a .NET Framework 4.8. AnyCPU sem preferência por processo de 32 bits. Executa com a identidade do usuário, sem elevação. O manifesto declara compatibilidade com Windows e DPI de sistema.

`Model.cs` contém entidades e validação. `Vault.cs` concentra criptografia, persistência e exclusão mútua. `Connections.cs` constrói argumentos sem shell, integra RDP e fornece diagnósticos. `Dialogs.cs` e `MainForm.cs` implementam a interface. `Program.cs` oferece inicialização, modo demonstração e captura de dados fictícios para inspeção do layout.

## Formato de arquivo

Todos os inteiros do cabeçalho são little-endian.

| Offset | Tamanho | Conteúdo |
| --- | --- | --- |
| 0 | 8 | Magic ASCII `TDMVLT01` |
| 8 | 4 | Iterações PBKDF2 (600.000 na v1) |
| 12 | 32 | Salt |
| 44 | 16 | IV AES-CBC |
| 60 | 4 | Tamanho do ciphertext |
| 64 | variável, múltiplo de 16 | JSON UTF-8 criptografado com PKCS#7 |
| após ciphertext | 32 | HMAC-SHA256 do cabeçalho e ciphertext |

PBKDF2-HMAC-SHA256 deriva 64 bytes: os primeiros 32 para AES-256 e os demais para HMAC. O MAC é verificado antes da decriptação com comparação de duração constante para valores de tamanho igual. Cabeçalho, limites de tamanho e custo de derivação são validados antes de alocar dados controlados pelo arquivo. Limite total: 32 MiB. Salt é mantido durante a sessão; IV muda a cada gravação. Troca de senha gera novo salt e novas chaves.

Persistência usa arquivo temporário criptografado, flush e `File.Replace`, preservando uma cópia anterior `.bak`. Uma trava em sidecar com `FileShare.None` protege contra concorrência local. Um hash da versão lida em disco é comparado antes de salvar para recusar alterações externas detectadas. Isso não implementa transação multiusuário ou proteção perfeita contra condições de corrida em sistemas de arquivos distribuídos. Alterações da interface usam snapshot e rollback em memória se a gravação falhar.

## Fronteiras de proteção

- A senha-mestra protege o arquivo em repouso. Não há servidor ou administrador do produto capaz de recuperá-la.
- Dados ficam em memória enquanto o cofre está aberto. O processo não resiste a administrador local, debugger, malware, dumps ou leitura de memória privilegiada. Não existe garantia de zerar strings gerenciadas.
- O proprietário do cofre tem acesso a todos os segredos. Ocultar uma coluna não é autorização. RBAC real exige autorização no servidor e separação de custódia.
- O RDP usa o componente nativo com NLA e política de aviso de identidade. Não desabilita verificações de certificado. Usuários ainda podem aceitar avisos do componente; homologar certificados e política do ambiente.
- A senha RDP é entregue em memória ao controle. Credenciais não são passadas por `cmdkey`, arquivos `.rdp`, URI de navegador ou argumentos de clientes externos.
- Clientes externos são executáveis locais explicitamente configurados, com argumentos fixos e quoting Windows. Host e porta são validados. SSH/WinSCP mantêm a validação de chave de host no cliente.
- Scripts são conteúdo explícito revisado pelo usuário, sem substituição textual de credenciais. O contexto é fornecido por variáveis de ambiente. Não há bypass de ExecutionPolicy. EncodedCommand é transporte, não criptografia; o conteúdo do script pode ser observado por processos privilegiados.
- Clipboard é opt-in e temporário; o Windows ou outros programas podem manter histórico.

## Eventos

Solicitação de conexão, despacho, abertura de processo externo e estado do transporte RDP são eventos distintos. Não há atestado de login bem-sucedido ou encerramento remoto quando um processo cliente externo termina. A auditoria não contém senha ou conteúdo/output de scripts. A cadeia local não impede rollback de arquivo inteiro nem reescrita por quem controla o cofre. Log independente e imutável requer serviço central.

O bloqueio por inatividade usa o tempo de entrada do Windows e o bloqueio de estação é observado via WTS. Ao bloquear, o cofre sai de memória e RDP integrado é desconectado. VPNs e aplicações externas não são encerradas. O atalho global pode estar reservado por outro aplicativo; a busca continua disponível na janela.

## Caminho para equipe / banco central

Antes de adicionar SQL Server, PostgreSQL ou MariaDB/MySQL, criar API autenticada e migrar a autorização para esse serviço. O aplicativo não deve receber credenciais administrativas do banco, chaves globais nem acesso direto irrestrito às tabelas.

1. Serviço de autenticação OIDC/SAML, MFA e autorização por usuário/grupo/cofre/ação.
2. Criptografia de envelope por tenant/cofre, com chave raiz em KMS/HSM ou cofre corporativo e política de rotação.
3. Auditoria independente append-only, controle de retenção, detecção de alteração e coleta central de eventos.
4. Contratos de adaptadores para cofres externos, VPNs e APIs de nuvem, com escopos mínimos e testes por fornecedor.
5. Orquestrador PAM/JIT com solicitação, aprovação, expiração e revogação efetivamente aplicadas no servidor.
6. Canal de sessão/gravador dedicado, criptografia dos registros e regras de acesso/retenção, com avaliação de privacidade.
7. Modo offline com subconjunto de dados, expiração, chave local protegida e política de sincronização e revogação.

Esses serviços não estão presentes na v1.0. Não usar um arquivo `.tdmv` em share como substituto de um banco compartilhado.
