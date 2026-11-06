# Validação da v1.0.1

A atualização adiciona identidade oficial, três paletas e modos Claro, Escuro e Automático. O formato do cofre não foi alterado: `Model.cs`, `Vault.cs` e `Connections.cs` conservam a implementação da v1.0.0.

## Verificações locais executadas

Foi executada uma bateria com 49 verificações de aparência e regressão em Linux/Mono, aprovada após a normalização do logo e a codificação compatível do ícone. Ela cobre as seis combinações de paleta e modo, contraste do texto, preferência persistente, arquivo de preferências inválido, alternância automática, atualização de uma janela aberta, cadastro sem credencial, validação de seleções vazias e diagnóstico sem conteúdo sensível.

O logo recebido tinha extensão `.png`, mas era codificado como WebP. Foi convertido para PNG estático, mantendo os mesmos pixels RGBA do primeiro quadro, a transparência e a proporção. O brasão foi convertido em ICO de 16, 24, 32, 48, 64, 128 e 256 pixels, com quadros DIB e máscaras de transparência compatíveis com o .NET Framework.

## Verificação no Windows

O workflow `Windows build and security tests` compila usando o compilador do .NET Framework, executa os testes do cofre, da interface e de aparência, gera imagens das seis combinações e verifica instalação/desinstalação com preservação de arquivo sintético de usuário. Os resultados dessa execução serão registrados após a conclusão do workflow. A criação automática de releases com permissão de escrita não é utilizada.

## Limites

O aviso genérico de cadastro reportado na v1.0.0 não teve sua causa exata reproduzida. A v1.0.1 reforça a validação dos campos de seleção e oferece diagnóstico seguro copiável para identificar futuras exceções. Não se afirma uma causa técnica não confirmada.

Nenhum teste inicia conexão em servidores reais de clientes. Login real, VPN, certificados, monitores e permissões de cada ambiente continuam sujeitos à homologação local.
