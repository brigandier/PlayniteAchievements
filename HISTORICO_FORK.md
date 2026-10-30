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

## 30/10/2026 — Preparação da BETA 04 com auditoria dos três recompilados

- Confrontamos o código dos projetos Hell's Gate, re:Blue e Unleashed Recompiled com a auditoria local de suas instalações.
- Confirmamos Dante's Inferno com o Title ID 454108CF e 13 desbloqueios em userdata/achievements.
- Confirmamos Blue Dragon com o Title ID 4D5307DF e um desbloqueio adicional do re:Blue (ID 65542), no perfil default.
- Confirmamos que Sonic Unleashed utiliza ACH-DATA de 816 bytes, versão 1, com assinatura e checksum válidos; nenhum desbloqueio foi registrado no relatório.
- Corrigimos a descoberta dos diretórios portáteis userdata e dos perfis do re:Blue no provedor ReXGlue, mantendo preferência pelo perfil default e sem selecionar arbitrariamente entre perfis ambíguos.
- Implementamos um leitor binário independente para ACH-DATA, com validação da assinatura, versão, tamanho, checksum, registros e datas.
- Adicionamos testes automatizados para os caminhos reais e para o leitor do Unleashed Recompiled.
- Pendente: obter o catálogo original XDBF dos três jogos com segurança e integrar o novo leitor ao provedor; sem catálogo validado, não publicamos a BETA 04 como concluída.

- Corrigimos a inclusão do novo leitor no projeto isolado de testes após a primeira compilação: o projeto principal já compilava, mas os testes não referenciavam o arquivo-fonte.

## 30/10/2026 — Recomps BETA 04

- Integramos a extração automática de catálogos XDBF e ícones via CLI nativo do ReXGlue SDK 0.10.0, com cache exclusivo da extensão e validação do Title ID, preservando os arquivos do jogo.
- Incluímos os oito IDs adicionais do re:Blue, incluindo Cloaca Maxima (ID 65542, 420G), sem duplicar conquistas nativas.
- Implementamos o provedor Unleashed Recompiled na navegação Recomps → Xbox 360, lendo o ACH-DATA local com horários de desbloqueio e os metadados XDBF originais.
- Adicionamos configurações e textos em inglês e português para Unleashed Recompiled, além do enriquecimento opcional de raridade via Exophase.
- Empacotamos uma CLI de extração nativa licenciada sob BSD-3-Clause, baixada do lançamento oficial e validada por SHA256 no GitHub Actions.
- Atualizamos o manifesto para 4.1.1.4 e o artefato instalável para BETA 04.
- Nenhum arquivo original dos recompilados é modificado. A validação funcional dentro do Playnite e do SDK com os XEX reais continua necessária.

- Corrigimos a publicação do pacote BETA 04 no workflow: o nome do arquivo no upload permaneceu apontando para a BETA 03, embora a primeira compilação e 20 testes tenham passado.
- Atualizamos o nome exibido da extensão para Playnite Achievements (Recomps), mantendo o identificador original e os créditos do upstream.
- Preservamos os créditos dos desenvolvedores nas descrições das oito conquistas adicionais do re:Blue.

- Alinhamos a compilação local em Windows ao pacote do GitHub Actions, incluindo a CLI XDBF oficial com hash SHA256, testes SpartanPort/ReXGlue/Unleashed e identificação BETA 04; adicionamos o atalho COMPILAR_PLAYNITE_RECOMPS.bat.

## 30/10/2026 — Correção de descoberta e diagnóstico dos três jogos

- A BETA 04 instalou sem erro, mas o usuário não visualizou conquistas em Dante's Inferno, Blue Dragon ou Sonic Unleashed.
- A investigação encontrou retornos silenciosos em caso de falha de extração do catálogo XDBF, sem indicação do motivo na interface.
- Implementamos mensagens de diagnóstico por jogo: diretórios, Title ID, existência de saves, sucesso/falha do extrator e contagens de conquistas.
- O extrator agora captura a saída textual e de erro do processo para o log do Playnite, mantendo um limite de tamanho.
- Reforçamos a descoberta de executáveis do Playnite, inclusive ações com argumentos, pastas de instalação vazias e perfis do re:Blue.
- Blue Dragon passa a apresentar suas conquistas adicionais mesmo quando os metadados originais do Xbox 360 não forem extraídos.
- A confirmação funcional dos catálogos originais depende de testes na instalação Windows, não sendo inferida apenas por testes unitários.

- Corrigimos o roteamento de atualização: a ordem anterior selecionava Exophase/Manual antes de ReXGlue e deixava Unleashed Recompiled fora da ordem explícita, impedindo a prioridade dos provedores nativos em jogos que também podem ser reconhecidos pelo agregador.
- Os provedores locais de recompilados agora têm prioridade quando encontram arquivos reais de progresso; overrides explícitos continuam prevalecendo.

## 30/10/2026 — Conquistas Locais e BETA 06

- Confirmamos nos logs das BETAs 04/05 a falha do extrator XDBF: saída 106 com `--project-name is required`. Os arquivos de progresso eram encontrados, mas a ausência do catálogo interrompia a atualização de Dante e Sonic antes da persistência. Confirmamos também a leitura das oito extras do re:Blue no provedor da BETA 05, sem o catálogo original; esse registro não comprova a apresentação na interface.
- Corrigimos a chamada da CLI ReXGlue 0.10.0, incluindo os argumentos obrigatórios do comando pai `init`, e validamos a extração dos três XEX reais. Mantivemos a CLI já distribuída para carregar XEX compactados e aplicar os patches encontrados pelo SDK, com saída restrita ao cache da extensão. Não encontramos um XDBF diretamente legível nos executáveis inspecionados; não substituímos o carregador do SDK por uma busca de assinaturas sem validação.
- Implementamos o provedor compartilhado **Conquistas Locais**, separado do provedor Manual, com associação permanente pelo GUID do jogo no Playnite.
- Separamos a configuração do jogo, o carregamento do catálogo e os leitores de progresso. Adicionamos uma interface comum com identificação do formato, critérios de validação, leitura, monitoramento, datas e limitações.
- Adicionamos leitores compartilhados para ReXGlue TOML, Unleashed Recompiled `ACH-DATA` e SpartanPort `trophies.dat`, reutilizando os leitores existentes e preservando os identificadores estáveis das conquistas.
- Implementamos seleção do jogo, detecção automática, indicação de progresso, catálogo TOML/TSV ou XEX original, diretório opcional de ícones, Title ID, prévia com imagens, pontuações, descrições, estados e datas, validação e configuração do monitoramento.
- Integramos a configuração ao fluxo normal de edição dos provedores: preparamos a associação com **Salvar configuração** e persistimos ao confirmar as configurações com **OK**. Preservamos o cancelamento da sessão de configurações. A atualização normal do jogo carrega o catálogo no cache usado pela interface.
- Priorizamos fontes explicitamente configuradas em Conquistas Locais sobre a descoberta e os overrides anteriores de outros provedores. Preservamos o roteamento anterior para jogos sem essa associação; remover a configuração restaura esse comportamento.
- Integramos o provedor ao monitor e ao sistema de notificações existentes, com observação do arquivo e releitura de segurança a cada 500 ms. Adicionamos uma política por jogo para que desativar o monitoramento também interrompa a atualização periódica durante a partida, preservando a atualização manual.
- Validamos a gravação e a recuperação no SQLite por meio da DLL Release real, a atualização silenciosa do progresso anterior à sessão, a observação de substituições atômicas e a emissão única pela política de notificações com arquivos sintéticos dos três formatos.
- Rejeitamos progresso TOML parcial, IDs duplicados, datas inválidas, listas malformadas e dados sem formato comprovado. Validamos a correspondência de Title IDs e recusamos catálogos incompatíveis ou sem os IDs desbloqueados, sem fabricar estados.
- Preservamos a validação de tamanho, assinatura, versão e checksum de `ACH-DATA`. Confirmamos no código do Unleashed Recompiled que o desbloqueio altera a estrutura em memória e a persistência ocorre separadamente. Não identificamos nesse mecanismo um canal externo de eventos imediatos; registramos na interface que a notificação depende da gravação do arquivo. Não introduzimos leitura de memória nem modificações no recompilado.
- Incluímos os oito ícones públicos adicionais do re:Blue e sua licença BSD-3-Clause, preservando os créditos e as descrições originais. Mantivemos o enriquecimento opcional de raridade pelo Exophase.
- Validamos em modo somente leitura as instalações reais: Dante's Inferno com **44 conquistas e 13 desbloqueadas**; Blue Dragon com **43 originais e oito adicionais**, incluindo **Cloaca Maxima, ID 65542, desbloqueada com 420 pontos**; Sonic Unleashed com **50 conquistas bloqueadas** e `ACH-DATA` íntegro de 816 bytes. Confirmamos todos os ícones e a identidade dos bytes dos arquivos de progresso antes e depois da leitura.
- Validamos também a descoberta automática dos caminhos reais nos três jogos. Mantivemos os catálogos extraídos e os dados pessoais somente nos artefatos locais ignorados pelo Git.
- Executamos a suíte ampla no Windows: **5.015 aprovações, nenhuma falha e um teste previamente ignorado**, sem incluir a inspeção opcional das instalações. Corrigimos expectativas de cultura nos testes de captura, Guild Wars 2 e pontuação, sem alterar esses recursos. Executamos separadamente os cinco cenários finais de Conquistas Locais, incluindo a inspeção real, com cinco aprovações.
- Removemos o evento redundante de cópia pós-compilação que expandia caminhos incorretamente no MSBuild local; confirmamos a distribuição das localizações pela regra `CopyToOutputDirectory` já existente.
- Compilamos em Release, atualizamos a versão para **4.1.1.6** e geramos `PlayniteAchievements_Recomps_BETA06.pext`. Validamos a integridade ZIP, as 168 entradas, o manifesto, os executáveis, as localizações, os ícones e as licenças, sem salvamentos ou arquivos proprietários no pacote.
- Ampliamos o GitHub Actions para executar a suíte de regressão e verificar os componentes obrigatórios antes de disponibilizar o instalador BETA 06.
- Normalizamos os finais de linha do catálogo PT-BR conforme o `.gitattributes` existente, preservando todos os textos e eliminando diferenças locais de conversão CRLF/LF.
- Registramos como pendentes a validação visual da configuração no Playnite, a apresentação após atualizar os jogos e a exibição efetiva do overlay com sons e imagens durante uma partida. Os testes automatizados verificam a cadeia de dados e a política de emissão; não certificam a renderização do overlay nem a latência entre o desbloqueio em memória e o salvamento do Sonic.
