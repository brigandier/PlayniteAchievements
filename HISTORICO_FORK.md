# Histórico da fork

## 30/10/2026 — God of War III Remastered / SpartanPort

### Implementação
- Adicionamos o provedor `SpartanPort` diretamente ao Playnite Achievements, mantendo o identificador original da extensão para preservar configurações existentes.
- Integramos leitura nativa do progresso em `user/trophies.dat`: quatro palavras de 32 bits little-endian, com 35 troféus reconhecidos.
- Associamos troféus pelos identificadores estáveis `gow3_spartanport_000` a `gow3_spartanport_034`.
- Incluímos nomes e descrições em português, com prioridade para `user/trophy_texts/pt.txt` gerado pelo próprio port.
- Adicionamos extração de PNGs do pacote `sce_sys/trophy/trophy00.trp` e geração de ícones cinza em cache da extensão.
- Corrigimos a renovação dos ícones bloqueados quando os PNGs originais são atualizados e isolamos imagens inválidas sem interromper a leitura dos troféus.
- Usamos os caminhos das imagens extraídas como ícones do provedor, sem adicionar campos fora do modelo de conquistas.
- Registramos o arquivo de progresso no monitoramento nativo do Playnite Achievements, com observação de alterações e releitura de segurança.
- Criamos configuração para indicar a pasta do SpartanPort e a pasta de salvamentos, mantendo a detecção pelo diretório de instalação do jogo.
- Inicialmente agrupamos o provedor na seção de emuladores; posteriormente, criamos uma seção própria para recompilações.
- Incluímos SpartanPort na ordem de exibição e atualização dos provedores da extensão.
- Incluímos testes de integridade dos flags, leitura de TRP, 35 definições e textos locais.
- Identificamos esta compilação como Playnite Achievements (SpartanPort), versão 4.1.1.1, mantendo o mesmo ID de extensão para atualização sem duplicação.
- Adicionamos build para Windows pelo GitHub Actions, com geração de pacote `.pext`.

### Compatibilidade e segurança
- Não alteramos o executável, os salvamentos, os arquivos de troféus ou as credenciais da PSN.
- O progresso local não é confundido com as conquistas da conta PSN.
- Um salvamento inexistente ou incompleto não é tratado como confirmação de progresso zerado.
- Preservamos o código e a licença do projeto original.

### Ajustes de diagnóstico e descoberta
- Reforçamos a localização da instalação a partir do caminho configurado, diretório do jogo, variável `SPARTAN_DATA_DIR` e ações de inicialização do Playnite.
- Separamos a escolha da pasta de salvamentos da existência prévia de arquivos, respeitando `user_dir`, `SPARTAN_USER_DIR` e o diretório padrão.
- Adicionamos testes de caminho para instalação independente, diretório personalizado e rejeição de jogos não correspondentes.
- Passamos a preservar os diagnósticos de testes e compilação como artefatos do GitHub Actions, inclusive em caso de falha.

### Validação
- Código integrado na branch `main` da fork.
- Testes automatizados adicionados ao projeto de testes do repositório.
- Compilação no Windows e teste de desbloqueio durante uma partida: ainda não confirmados. Os logs de execução do GitHub Actions precisam ser consultados para identificar possíveis falhas.
- Próxima etapa: confirmar a detecção do diretório real do SpartanPort no Playnite, testar atualizações em tempo real e avaliar leitores genéricos para outros ports.

### Registro
O histórico detalhado permanece nos commits do Git, separados por leitor, caminhos, configurações, integração, testes e empacotamento.

## 30/10/2026 — Compilação local e correções

- Removemos duas propriedades de ícones inexistentes no modelo `AchievementDetail`, que impediam a compilação.
- Adicionamos `COMPILAR_PLAYNITE_SPARTANPORT.bat` para iniciar compilação local no Windows.
- Adicionamos `compilar_spartanport.ps1` para obter a fork, atualizar `main` sem sobrescrever alterações, restaurar NuGet, localizar MSBuild, compilar Release, executar os testes SpartanPort e montar o pacote `.pext`.
- Guardamos logs de execução e arquivos binários do MSBuild em `artefatos` para diagnosticar falhas.
- Não consideramos a compilação validada até obter uma execução bem-sucedida no ambiente Windows.

## 30/10/2026 — Primeira compilação validada

- Corrigimos o workflow para compilar `source/PlayniteAchievements.csproj` antes de executar os testes que carregam a DLL principal.
- Diagnosticamos a falha anterior no GitHub Actions: os testes não conseguiam localizar `PlayniteAchievements.dll` porque a compilação ainda não havia sido executada.
- Confirmamos a execução bem-sucedida do GitHub Actions nº 23 (`41112929798`), baseada no commit `2c0f734b`.
- Compilamos a extensão Release no Windows sem erros de compilação e executamos os oito testes SpartanPort com oito aprovações.
- Geramos `PlayniteAchievements_SpartanPort_BETA01.pext` como artefato instalável da execução.
- Validamos a estrutura ZIP do pacote, a presença da DLL, do manifesto, do ícone e do executável auxiliar.
- Mantemos pendente a validação real no Playnite, a identificação da pasta de dados no computador e os desbloqueios durante a partida.

## 30/10/2026 — Organização das plataformas e localização

- Criamos a seção **Recomps** nas configurações de plataformas, com divisão por PlayStation 4, PlayStation 3 e Xbox 360.
- Movemos SpartanPort para PlayStation 4, preservando o identificador e os dados do provedor.
- Adicionamos páginas informativas para PlayStation 3 e Xbox 360, sem criar provedores de conquistas fictícios.
- Implementamos navegação com cabeçalhos de grupo e subgrupo no painel de plataformas.
- Iniciamos a localização brasileira de `source/Localization/pt_BR.xaml`, com tradução de ações, tabelas, menus, edição de conquistas e configurações de plataformas.
- Localizamos as configurações específicas do SpartanPort por meio de recursos em inglês e português, removendo explicações desnecessárias da interface.
- A tradução completa do catálogo de textos e a validação visual permanecem em andamento.


## 30/10/2026 — Padronização das configurações do SpartanPort

- Alinhamos a página do SpartanPort ao layout das páginas ShadPS4, RPCS3 e Xenia: cabeçalho, ativação, status de configuração e seções padronizadas.
- Criamos uma identidade gráfica exclusiva para o provedor, independente dos ícones de emuladores.
- Passamos a validar os caminhos informados e a mostrar o estado da configuração na interface.
- Adicionamos a opção de complementar a raridade dos troféus com metadados do Exophase, somente quando ativada e autenticada.
- Preservamos os nomes, descrições e ícones locais durante a complementação; utilizamos os nomes originais em inglês como alternativa para associar troféus quando a tradução não corresponder à listagem externa.
- Registramos strings específicas em inglês e português nos catálogos de localização existentes.
- Testes de compilação e funcionamento dentro do Playnite permanecem como etapa de validação.

## 30/10/2026 — Localização completa e BETA 0.2

- Concluímos a correspondência das 1.629 chaves do catálogo `en_US.xaml` no `pt_BR.xaml`, com tradução de menus, configurações, filtros, edição, estatísticas, notificações, vitrine e Workshop.
- Mantivemos nomes próprios, siglas, identificadores técnicos e expressões usuais que não exigem adaptação.
- Verificamos a ausência de chaves duplicadas, de recursos ausentes e de diferenças nos marcadores de formatação.
- Atualizamos a versão da extensão para `4.1.1.2` e identificamos o instalador como BETA 0.2.
- A compilação automatizada não substitui a verificação visual e funcional das telas no Playnite.

## 30/10/2026 — Provedor ReXGlue para recompilações de Xbox 360

- Analisamos o armazenamento compartilhado do SDK ReXGlue: catálogos em `achievements.toml` e desbloqueios em `achievements/<TitleID>.toml`, incluindo datas em FILETIME.
- Confirmamos o mesmo formato de progresso em Geometry Wars: Retro Evolved 2 e Army of Two, incluindo o catálogo TSV exportado por este último.
- Adicionamos um leitor reutilizável dos catálogos TOML e TSV e dos salvamentos com seções `[unlocked.<id>]`.
- Implementamos descoberta delimitada pelas pastas do jogo, instalação portátil, dados de usuário e diretório Documents.
- Incluímos compatibilidade inicial com o catálogo em cache do Army of Two, preservando a distinção entre perfis.
- Registramos o novo provedor ReXGlue em Recomps → Xbox 360, com progresso em jogo e opção de complementar raridade pelo Exophase.
- Adicionamos testes para formatos de catálogo, idiomas, datas, ícones, identidade de título e salvamentos ambíguos.
- Atualizamos a seleção de testes Windows para cobrir SpartanPort e ReXGlue e identificamos a versão experimental como 4.1.1.3.
- Os recompilados que não exportam um catálogo legível ainda não são reconhecidos automaticamente; a leitura direta dos recursos XDBF do XEX permanece em estudo.
- A execução real em jogos e a validação visual da interface ainda precisam ser confirmadas.

- Corrigimos a descrição exibida após desbloqueio durante uma partida, preservando o texto original da conquista no cache.
- Incluímos ReXGlue na geração de identificadores para enriquecimento de raridade com Exophase.
