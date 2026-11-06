# Toccato Desktop Manager v1.0.0

Primeira entrega: gerenciador desktop Windows em português, com clientes e pastas, credenciais vinculadas em cofre local criptografado, RDP integrado, lançadores externos, busca, backup e auditoria local.

## Downloads

- **Setup.exe**: instalador por usuário, requer .NET Framework 4.8+.
- **Portable.zip**: extraia todo o conteúdo antes de executar.
- **Completo.zip**: instalador, portátil, código-fonte, guia, imagem e relatório de validação.
- **SHA256SUMS.txt**: integridade dos arquivos.

O pacote completo é idêntico ao entregue originalmente. Os binários foram compilados com Mono/NSIS. Os testes iniciais foram executados em Linux/Mono (25 de segurança e 14 de interface); os resultados posteriores de Windows estão na aba Actions. Login real nos destinos e instalação no ambiente final precisam de homologação.

Não estão implementados PAM/JIT, RBAC de equipe, MFA/SSO, gravação de sessões, banco compartilhado, cofres externos ou 150 integrações nativas. Consulte a matriz no guia.
