# Histórico da fork

## 30/10/2026 — God of War III Remastered / SpartanPort

### Implementação
- Adicionamos o provedor `SpartanPort` diretamente ao Playnite Achievements, mantendo o identificador original da extensão para preservar configurações existentes.
- Integramos leitura nativa do progresso em `user/trophies.dat`: quatro palavras de 32 bits little-endian, com 35 troféus reconhecidos.
- Associamos troféus pelos identificadores estáveis `gow3_spartanport_000` a `gow3_spartanport_034`.
- Incluímos nomes e descrições em português, com prioridade para `user/trophy_texts/pt.txt` gerado pelo próprio port.
- Adicionamos extração de PNGs do pacote `sce_sys/trophy/trophy00.trp` e geração de ícones cinza em cache da extensão.
- Corrigimos a renovação dos ícones bloqueados quando os PNGs originais são atualizados e isolamos imagens inválidas sem interromper a leitura dos troféus.
- Marcamos as imagens extraídas como ícones próprios da fonte local para preservar a arte original na apresentação.
- Registramos o arquivo de progresso no monitoramento nativo do Playnite Achievements, com observação de alterações e releitura de segurança.
- Criamos configuração para indicar a pasta do SpartanPort e a pasta de salvamentos, mantendo a detecção pelo diretório de instalação do jogo.
- Agrupamos o provedor na seção de emuladores e recompilados da interface.
- Incluímos SpartanPort na ordem de exibição e atualização dos provedores da extensão.
- Incluímos testes de integridade dos flags, leitura de TRP, 35 definições e textos locais.
- Identificamos esta compilação como Playnite Achievements (SpartanPort), versão 4.1.1.1, mantendo o mesmo ID de extensão para atualização sem duplicação.
- Adicionamos build para Windows pelo GitHub Actions, com geração de pacote `.pext`.

### Compatibilidade e segurança
- Não alteramos o executável, os salvamentos, os arquivos de troféus ou as credenciais da PSN.
- Nenhum serviço Python ou processo externo é necessário para detectar desbloqueios.
- O progresso local não é confundido com as conquistas da conta PSN.
- Um salvamento inexistente ou incompleto não é tratado como confirmação de progresso zerado.
- Preservamos o código e a licença do projeto original.

### Validação
- Código integrado na branch `main` da fork.
- Testes automatizados adicionados ao projeto de testes do repositório.
- Compilação no Windows e teste de desbloqueio durante uma partida: aguardam resultado do pipeline e validação em jogo.
- Próxima etapa: confirmar a detecção do diretório real do SpartanPort no Playnite, testar atualizações em tempo real e avaliar leitores genéricos para outros ports.

### Registro
O histórico detalhado permanece nos commits do Git, separados por leitor, caminhos, configurações, integração, testes e empacotamento.