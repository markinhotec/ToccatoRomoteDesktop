# Validação da v1.0.1

A atualização adiciona identidade oficial, três paletas e modos Claro, Escuro e Automático. O formato do cofre não foi alterado: `Model.cs`, `Vault.cs` e `Connections.cs` conservam a implementação da v1.0.0.

## Verificações locais executadas

Foi executada uma bateria com 49 verificações de aparência e regressão em Linux/Mono, aprovada após a normalização do logo e a codificação compatível do ícone. Ela cobre as seis combinações de paleta e modo, contraste do texto, preferência persistente, arquivo de preferências inválido, alternância automática, atualização de uma janela aberta, cadastro sem credencial, validação de seleções vazias e diagnóstico sem conteúdo sensível.

O logo recebido tinha extensão `.png`, mas era codificado como WebP. Foi convertido para PNG estático, mantendo os mesmos pixels RGBA do primeiro quadro, a transparência e a proporção. O brasão foi convertido em ICO de 16, 24, 32, 48, 64, 128 e 256 pixels, com quadros DIB e máscaras de transparência compatíveis com o .NET Framework.

## Verificação no Windows: aprovada

Execução concluída com sucesso em 6 de novembro de 2026 no Windows Server 2022, usando o compilador Microsoft do .NET Framework e o NSIS. Os pacotes de distribuição desta versão são os binários compilados nesse Windows, sem recompilação em Mono.

- [Execução 42273906791 no GitHub Actions](https://github.com/markinhotec/ToccatoRomoteDesktop/actions/runs/42273906791).
- Código compilado e testado: `734ee4d2da6d0addec8b67b485aeb7be09276d91`.
- 25 testes de segurança aprovados.
- Testes da interface aprovados, incluindo configuração do componente RDP nativo sem conexão com servidor.
- 57 verificações de aparência e regressão aprovadas, incluindo seis combinações de cores, contraste, atualização de janelas abertas, preferências inválidas/nulas, cadastro sem credencial e porta personalizada.
- Oito capturas geradas pelo próprio aplicativo: demonstração, seis temas e diálogo de aparência.
- Instalação e desinstalação silenciosas aprovadas, com preservação de um arquivo `.tdmv` sintético na pasta de instalação.

O ZIP do artifact Windows foi conferido com o SHA-256 `9063c10a16a16e33f12670e2b31bd9a0f6853eb03c43a3ba68139f696e392d91`. O instalador e o portátil também foram conferidos com o arquivo de hashes gerado no build. O pacote completo inclui fontes, guia, identidade visual e capturas dessa execução.

O workflow tem somente permissão de leitura do repositório. A distribuição é armazenada em `releases/v1.0.1`, junto ao histórico da v1.0.0.

## Limites

O aviso genérico de cadastro reportado na v1.0.0 não teve sua causa exata reproduzida. A v1.0.1 reforça a validação dos campos de seleção e oferece diagnóstico seguro copiável para identificar futuras exceções. Não se afirma uma causa técnica não confirmada.

Nenhum teste inicia conexão em servidores reais de clientes. Login real, VPN, certificados, monitores e permissões de cada ambiente continuam sujeitos à homologação local.
