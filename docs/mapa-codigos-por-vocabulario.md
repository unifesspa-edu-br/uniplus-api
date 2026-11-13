# Mapa dos códigos por vocabulário

Documento de apoio da [uniplus-api#2382](https://github.com/unifesspa-edu-br/uniplus-api/issues/2382). Não é ADR e não decide nada: levanta os códigos de fase, tipo de decisão, formulário e tipo de ato, todos os lugares que os usam e a ação prevista em cada uso. É a lista de trabalho da [uniplus-api#2322](https://github.com/unifesspa-edu-br/uniplus-api/issues/2322), que executa as renomeações.

## 1. Como ler

Quatro vocabulários distintos usam palavras parecidas. Cada um é fechado e próprio, e nenhum se confunde com outro:

| Vocabulário | Pergunta que responde | Onde vive | Natureza |
|---|---|---|---|
| Fase canônica | Em que momento do ciclo o processo está | `FaseCanonicaCatalogo` (domínio fechado) e cadastro `fase_canonica` | Domínio fechado em código que o cadastro referencia; nome e sinalizadores são editáveis |
| Tipo de decisão | Que tipo de decisão um motivo justifica | `TipoMotivoDeDecisao` e `TipoMotivoDeDecisaoCodigo` | Vocabulário fechado em código |
| Formulário | Para que serve o formulário (finalidade) e de que modelo partiu (código do modelo) | `EstruturaFormulario` (finalidade) e cadastro `modelos_formulario` (código do modelo) | Finalidade fechada em código; modelo é cadastro |
| Tipo de ato | Que ato normativo se publica | Cadastro versionado de Publicações; `seeds/seed-tipos-ato.json` é a referência | Cadastro configurável, sem literal no domínio (ADR-0103) |

Ações usadas na coluna "Ação":

- **renomear**: o uso passa ao código novo na execução da #2322.
- **manter**: o uso continua como está.
- **remover**: o uso deixa de existir.
- **derivado**: não há literal no uso; ele muda sozinho quando o catálogo muda.
- **ponto em aberto para o líder técnico**: depende de decisão que este mapa não toma. Vem sempre com a referência `PA-n` da seção 9.

### 1.1 A palavra "homologação" em cada vocabulário

| Vocabulário | Código atual | Código novo | Definido em | Ação |
|---|---|---|---|---|
| Fase canônica | `HOMOLOGACAO` | `HOMOLOGACAO_INSCRICOES` | `FaseCanonicaCatalogo.Descritos` e `FaseCanonicaSeed` (Configuracao) | renomear |
| Tipo de decisão | `HOMOLOGACAO_DAS_INSCRICOES` | `HOMOLOGACAO_DAS_INSCRICOES` (mantido) | `TipoMotivoDeDecisaoCodigo` (Selecao.Domain) | manter |
| Formulário | nenhum | não se aplica | as finalidades são `INSCRICAO`, `ISENCAO_TAXA` e `HABILITACAO`; não existe finalidade nem modelo de homologação | manter; ver PA-5 |
| Tipo de ato | `HOMOLOGACAO_INSCRICOES` | `HOMOLOGACAO_INSCRICOES` (mantido) | `seeds/seed-tipos-ato.json` (cadastro de Publicações) | manter |

Nenhum código canônico `HOMOLOGACAO` sozinho sobrevive em nenhum vocabulário depois da #2322. O que restar com esse texto em dado de teste de tipo de ato fictício consta nas tabelas com ação "manter". `HOMOLOGACAO_RESULTADO_FINAL` (fase) não muda.

### 1.2 A palavra "habilitação" em cada vocabulário

| Vocabulário | Código atual | Código novo | Definido em | Ação |
|---|---|---|---|---|
| Fase canônica | `HABILITACAO` | mantido neste mapa | `FaseCanonicaCatalogo.Descritos`, `FaseCanonicaSeed` e `PrecedenciaFaseSeed` | manter; ponto em aberto para o líder técnico (PA-1) |
| Tipo de decisão | `ANALISE_DOCUMENTAL` (avaliada na fase `HABILITACAO`) | mantido | `TipoMotivoDeDecisaoCodigo`; `PoliticaDaAnaliseDocumental` cita a fase | manter; PA-3 |
| Formulário | finalidade `HABILITACAO` | mantido neste mapa | `EstruturaFormulario.FinalidadeHabilitacao` | manter; ponto em aberto para o líder técnico (PA-2) |
| Tipo de ato | `HOMOLOGACAO_HABILITACAO` (seed) e `RESULTADO_HABILITACAO` (só no catálogo de HML) | mantidos | `seeds/seed-tipos-ato.json`; mapa de consolidação do HML | manter; PA-4 |

`CodigoFaseHabilitacao` (constante `FormularioProcesso.CodigoFaseHabilitacao = "HABILITACAO"`) é o código da **fase** e consta na seção 3.3 com os 16 usos. Ele é a constante que amarra a finalidade de habilitação à fase com esse código, ao contrário da isenção, que usa a capacidade configurada da fase. A #2322 não pode copiar esse desenho para a homologação: ver PA-3. Outras constantes de fase decidem comportamento por código (`CodigoSolicitacaoIsencao`, `CodigoAvaliacao`, `CodigoInscricao`), na seção 3.3.

## 2. Como a varredura foi feita

Base varrida: `uniplus-api` na `main` (`6de7e94b4`), `uniplus-web` na `main` (`b7f5c43b`) e `uniplus-developers` na `main` (`1fb96f23c`), só leitura. Os scripts de implantação e de backup do HML foram varridos e não citam nenhum dos tokens; o conteúdo do banco e do backup de HML não foi lido (seção 7).

Comandos, a partir da raiz de cada repositório:

```bash
# API: toda ocorrência dos tokens, fora de bin/obj
grep -rIn -E "HOMOLOGACAO|HABILITACAO|CodigoFaseHabilitacao|FinalidadeHabilitacao" \
  src tests scripts seeds contracts docs .github docker infra tools .claude

# API: tokens com sufixo ou prefixo, para achar todas as variantes de código
grep -rIoh -E "[A-Za-z_]*(HOMOLOGACAO|HABILITACAO)[A-Za-z_]*" src tests scripts seeds contracts docs | sort | uniq -c | sort -rn

# API: literais de fase em migrações (só HOMOLOGACAO sozinho)
grep -lE "(^|[^A-Z_])HOMOLOGACAO([^A-Z_]|$)" \
  src/configuracao/*/Persistence/Migrations/*.cs

# API: configuração de permissões (nada encontrado)
grep -rIn -i -E "homolog|habilit" src/*/*/Permissoes src/autorizacao src/shared/*Authorization*

# API: coluna ou JSON que guarda código de fase
grep -rn -i -E "fase.*codigo|ponto_?resolucao|faseCodigo" src --include=*.cs | grep -v Migrations

# Web e portal (developers): na main, por git grep
git grep -n -E "HOMOLOGACAO|HABILITACAO" origin/main
```

Resultado dos comandos de busca por área (contagem de arquivos com o token):

| Área | Arquivos | Onde está no mapa |
|---|---|---|
| `src/` da API, sem migrações | 16 | seções 3 a 6 |
| Migrações e instantâneo de modelo da Configuracao | 110 (6 migrações, 103 `*.Designer.cs`, 1 `ConfiguracaoDbContextModelSnapshot.cs`) | seção 3.4 |
| Testes da API | 75 | seção 3.6 |
| `scripts/` e `seeds/` da API | 3 | seções 3.7 e 6 |
| `docs/adrs/` da API | 3 | seção 3.8 |
| Contratos OpenAPI e coleções Postman da API | 0 códigos (as 7 ocorrências de "Homologação" nos `contracts/openapi.*.json` são a descrição do servidor de homologação) | seção 3.9 |
| Configuração de permissões | 0 | seção 3.9 |
| Seed e backup do HML (scripts de implantação e de backup) | 0 | seção 7 |
| Web (`uniplus-web`) | 62 | seção 3.10 |
| Portal (`uniplus-developers`) | 6 | seção 3.11 |

O conjunto de fases também é citado por constante sem literal (`FaseCanonicaCatalogo.CodigoInscricao`, `CodigoAvaliacao`, `CodigoSolicitacaoIsencao`); essas constantes não mudam.

Limite da classificação por linha nas seções 3.6 e 3.10: ela foi feita por padrão de texto, conferida linha a linha nas ocorrências ambíguas de `HABILITACAO` da API e, no web, deixada como "fase ou finalidade" onde o texto não distingue. A ação nunca depende dessa distinção enquanto PA-1 e PA-2 estiverem abertos (as duas são "manter"). Quem executar a #2322 e a [uniplus-web#1446](https://github.com/unifesspa-edu-br/uniplus-web/issues/1446) repete o `grep` acima antes de abrir o PR: toda linha que restar com `HOMOLOGACAO` sozinho, fora das migrações aplicadas, precisa constar neste mapa com ação "manter" e a justificativa.

## 3. Vocabulário 1: fase canônica

### 3.1 Os dezesseis códigos

Origem única do conjunto: `FaseCanonicaCatalogo.Descritos` (`src/configuracao/Unifesspa.UniPlus.Configuracao.Domain/Enums/FaseCanonicaCatalogo.cs`), que deriva `Codigos`. O cadastro (`configuracao.fase_canonica`, semeado por `FaseCanonicaSeed` e pela migração `SemeiaFasesCanonicas`) referencia cada um deles; nome, descrição e sinalizadores são editáveis, e o código não é.

| Código atual | Código novo ou mantido | Ação |
|---|---|---|
| `INSCRICAO` | mantido | manter |
| `SOLICITACAO_ISENCAO` | mantido | manter |
| `HOMOLOGACAO` | `HOMOLOGACAO_INSCRICOES` | renomear; o código antigo passa a ser recusado (seção 7) |
| `ENSALAMENTO` | mantido | manter |
| `AVALIACAO` | mantido | manter |
| `CLASSIFICACAO` | mantido | manter |
| `RESULTADO_PRELIMINAR` | mantido | manter |
| `RECURSOS` | mantido | manter |
| `RESULTADO_FINAL` | mantido | manter |
| `HABILITACAO` | mantido neste mapa | manter; ponto em aberto para o líder técnico (PA-1) |
| `HETEROIDENTIFICACAO` | mantido | manter |
| `AVALIACAO_BIOPSICOSSOCIAL` | mantido | manter |
| `MATRICULA` | mantido | manter |
| `HOMOLOGACAO_RESULTADO_FINAL` | mantido | manter |
| `LISTA_ESPERA` | mantido | manter |
| `CHAMADA` | mantido | manter |

### 3.2 Vocabulário fechado em código e cadastro configurável

| O que | Natureza | Observação |
|---|---|---|
| Conjunto de códigos de fase (`FaseCanonicaCatalogo.Codigos`) | fechado em código | Guarda do domínio (`FaseCanonica.EhCanonico`), validator e CHECKs do banco derivam dele. Renomear o código é mudar o catálogo e uma migração nova |
| Linha do cadastro (`fase_canonica`: nome, descrição, unidade sugerida, `permite_complementacao`, `coleta_inscricao` e demais sinalizadores) | cadastro configurável | Edita-se sem deploy. O sinalizador `permite_complementacao` só vale para os códigos de `CodigosComComplementacaoPermitida` |
| Aresta de precedência (`precedencia_fase`) | cadastro configurável, com seed de seis arestas | Cita códigos de fase por valor, sem chave estrangeira |
| Fase do cronograma de um processo (`fases_cronograma`) | snapshot congelado na publicação | Copia o código por valor; a decisão de publicação lê o snapshot, nunca o cadastro vivo |
| Capacidade de uma fase (coleta de inscrição, coleta de solicitação de isenção, produtos que declara) | configuração da fase | É por ela, e não pelo código, que o sistema reconhece o que a fase faz. A isenção já funciona assim (`PoliticaDaIsencaoDaTaxa.AvaliaNaFase` usa `ColetaSolicitacaoIsencao`) |

O mapa não cria regra por código de fase. As regras por código que já existem estão listadas na seção 3.3 e em PA-3 como dívida a decidir.

### 3.3 Usos em código de produção (API)

| # | Arquivo | Símbolo | Uso | Ação |
|---|---|---|---|---|
| 1 | `src/configuracao/Unifesspa.UniPlus.Configuracao.Domain/Enums/FaseCanonicaCatalogo.cs` | `Descritos` (linha 43) | `new("HOMOLOGACAO", "Homologação das inscrições")` | renomear para `HOMOLOGACAO_INSCRICOES`, mesmo rótulo |
| 2 | idem | `Descritos` (linhas 50 e 54) | `HABILITACAO`, `HOMOLOGACAO_RESULTADO_FINAL` | manter (PA-1 para o primeiro) |
| 3 | idem | `CodigosComComplementacaoPermitida` (linha 73) | `"HOMOLOGACAO"` | renomear |
| 4 | `src/configuracao/Unifesspa.UniPlus.Configuracao.Infrastructure/Persistence/Seed/FaseCanonicaSeed.cs` | linha 59 | seed da fase `HOMOLOGACAO` | renomear |
| 5 | idem | linhas 113 e 119 | seed de `HABILITACAO` e `HOMOLOGACAO_RESULTADO_FINAL` | manter (PA-1 para o primeiro) |
| 6 | `.../Persistence/Seed/PrecedenciaFaseSeed.cs` | linha 30 | aresta `INSCRICAO` para `HOMOLOGACAO` | renomear a sucessora; a aresta continua existindo |
| 7 | idem | linhas 33, 34 e 35 | arestas com `HABILITACAO` e `HOMOLOGACAO_RESULTADO_FINAL` | manter (PA-1 para as duas primeiras) |
| 8 | `.../Persistence/Configurations/FaseCanonicaConfiguration.cs` | `ConfigurarChecks`, `HasData` | CHECKs `ck_fase_canonica_codigo_canonico` e `ck_fase_canonica_complementacao` e a semente, todos gerados do catálogo e do `FaseCanonicaSeed` | derivado; a #2322 traz a migração que troca o código em dados e CHECKs |
| 9 | `.../Persistence/Configurations/PrecedenciaFaseConfiguration.cs` | `ConfigurarChecks`, `HasData` | CHECKs `ck_precedencia_fase_antecessora_canonica` e `ck_precedencia_fase_sucessora_canonica` e a semente | derivado; mesma migração |
| 10 | `src/configuracao/Unifesspa.UniPlus.Configuracao.Contracts/FaseCanonicaSnapshot.cs` | comentário do parâmetro `Codigo` (linha 20) | exemplo `"HOMOLOGACAO"` | renomear o exemplo |
| 11 | `src/selecao/Unifesspa.UniPlus.Selecao.Domain/Entities/FaseCronograma.cs` | comentário de `Codigo` (linha 62) | exemplo `"HOMOLOGACAO"` | renomear o exemplo |
| 12 | idem | `FinalidadesQueAtende` (linha 112) | compara `Codigo` com `FormularioProcesso.CodigoFaseHabilitacao` para decidir que a fase atende a finalidade `Habilitacao` | manter; ponto em aberto para o líder técnico (PA-3) |
| 13 | `src/selecao/Unifesspa.UniPlus.Selecao.Domain/Entities/FormularioProcesso.cs` | `CodigoFaseHabilitacao` (linha 19) | constante `"HABILITACAO"` | manter; PA-1 e PA-3 |
| 14 | `src/selecao/Unifesspa.UniPlus.Selecao.Domain/MotivosDeDecisao/PoliticaSemSubclassificacao.cs` | `PoliticaDaHomologacaoDasInscricoes` (linha 41) | `"HOMOLOGACAO"` é a fase em que o tipo de decisão `HOMOLOGACAO_DAS_INSCRICOES` é avaliado (`AvaliaNaFase`) | renomear para `HOMOLOGACAO_INSCRICOES`, a menos que a #2322 ou a #2338 troquem a regra pela capacidade declarada na fase, caso em que o literal sai (remover); PA-3 |
| 15 | idem | `PoliticaDaAnaliseDocumental` (linha 44) | `"HABILITACAO"` | manter; PA-1 e PA-3 |
| 16 | idem | `PoliticaDaHeteroidentificacao`, `PoliticaDaAvaliacaoBiopsicossocial`, `PoliticaDaClassificacao` | `HETEROIDENTIFICACAO`, `AVALIACAO_BIOPSICOSSOCIAL`, `CLASSIFICACAO` | manter; PA-3 |
| 17 | `src/configuracao/Unifesspa.UniPlus.Configuracao.Infrastructure/Persistence/Seed/SementePsrMedicina2027.cs` | `PontoHabilitacao` (linha 47) | `"HABILITACAO"` como ponto de resolução de cinco fatos semeados da habilitação (linhas 198 a 225) | manter; PA-1. O ponto de resolução é um código de fase gravado em `fato_candidato.ponto_resolucao` (seção 7) |
| 18 | `.../Persistence/Seed/FatoCandidatoSeed.cs` | `PontoResolucaoInscricao` (linha 61) | `"INSCRICAO"` | manter |
| 19 | `src/configuracao/Unifesspa.UniPlus.Configuracao.Domain/Entities/FatoCandidato.cs` | `ValidarPontoResolucao` (linha 259) e checagem de precedência (linha 554) | valida o ponto contra `FaseCanonicaCatalogo.EhCanonico`; sem literal | derivado |
| 20 | `src/configuracao/.../Entities/ModeloFormulario.cs`, `.../Converters/ConteudoDoModeloJson.cs`, `src/configuracao/.../Application/Commands/ModelosFormulario/ConteudoLido.cs`, `.../Mappings/ModeloFormularioMapping.cs`, `Configuracao.Contracts/ModeloFormularioView.cs` | `FaseCodigo` do documento exigido do modelo (linhas 94, 636 e 686; 71; 115 e 142; 45; 35) | o modelo cita a fase por código, em JSON, sem literal no código | derivado; o dado fica no cadastro de modelos (seção 7) |
| 21 | `src/selecao/.../Application/Commands/ProcessosSeletivos/SelecaoDosDocumentosDoModelo.cs` | linhas 150 a 155 | resolve a fase do modelo pelo código no cronograma do processo e **recusa com mensagem** se não a encontra; sem literal | derivado; o modelo com código antigo é recusado ao aplicar, e não ignorado |
| 22 | `src/selecao/.../Domain/Entities/EtapaProcesso.cs` e `Infrastructure/Persistence/Configurations/EtapaProcessoConfiguration.cs` | `FaseCodigo` / coluna `fase_codigo` | etapa ligada à fase por código; sem literal | derivado; dado (seção 7) |
| 23 | `src/selecao/.../Persistence/Configurations/FaseCronogramaConfiguration.cs`; `Canonicalization/LeituraDoCronograma.cs`, `LeituraDasEtapas.cs`, `SerializacaoDasEtapas.cs`; `Repositories/FasesNoEnvelope.cs` | `codigo` e `faseConcluinteCodigo` da fase; `faseCodigo` da etapa, no envelope congelado | sem literal; o envelope da versão publicada guarda o código e entra no hash | derivado; dado imutável por desenho (seção 7) |
| 24 | `src/configuracao/Unifesspa.UniPlus.Configuracao.Domain/Entities/FaseCanonica.cs` (linha 233) e `Configuracao.API/Errors/FaseCanonicaErrorMappings.cs` (linha 65) | mensagem "só é permitida nas fases de homologação e recursos" | prosa, sem código | manter (conferir com a página de erro do portal, seção 3.11) |

Nenhum literal de fase aparece em `contracts/` (o OpenAPI declara `codigo` como texto livre, sem enumeração), nas coleções Postman, na configuração de permissões nem em `.github/`, `docker/` e `infra/`.

#### Usos por código dos demais códigos de fase

As fases mantidas decidem comportamento por código nestes pontos de produção. Nenhum muda; a lista existe para que a #2322 não crie outro ponto igual ao de `HABILITACAO` (PA-3). Uso por leitura do catálogo, sem comparar código, não é enumerado: muda só pela fonte.

| Arquivo | Linha | Código | Uso | Ação |
|---|---|---|---|---|
| `src/configuracao/Unifesspa.UniPlus.Configuracao.Domain/Entities/FaseCanonica.cs` | 208 | `SOLICITACAO_ISENCAO` | coerência da coleta de solicitação de isenção com o código | manter |
| idem | 222 | `AVALIACAO` | só a fase de avaliação pode agrupar etapas | manter |
| `src/configuracao/Unifesspa.UniPlus.Configuracao.Domain/Services/ConferenciaDaRegraDeAplicabilidade.cs` | 88 e 89 | `INSCRICAO` | confere se o fato é conhecido até a inscrição | manter |
| `src/configuracao/Unifesspa.UniPlus.Configuracao.Infrastructure/Persistence/Configurations/FaseCanonicaConfiguration.cs` | 146 | `AVALIACAO` | CHECK `ck_fase_canonica_agrupa_etapas` | manter |
| `.../Persistence/Seed/FaseCanonicaSeed.cs` | 47, 53 e 71 | `INSCRICAO`, `SOLICITACAO_ISENCAO`, `AVALIACAO` | seed de fase | manter |
| `.../Persistence/Seed/FatoCandidatoSeed.cs` | 61 e 315 | `INSCRICAO`, `SOLICITACAO_ISENCAO` | ponto de resolução de fatos semeados | manter |
| `.../Persistence/Seed/SementePsrMedicina2027.cs` | 46, 507 e 509 | `INSCRICAO` | ponto de resolução e fase do documento exigido do modelo | manter |

#### Usos de `CodigoFaseHabilitacao` nos testes

A constante tem 16 usos: os dois de produção (linhas 12 e 13 da tabela acima) e os 14 abaixo, em testes. Todos ficam como estão, e acompanham PA-1; os dois que passam a constante a `ProdutoDaFase.Criar` a usam como **código de tipo de ato** de dado de teste, não como fase.

| Arquivo (em `tests/`) | Linha | O que o código representa | Ação |
|---|---|---|---|
| `Unifesspa.UniPlus.Selecao.IntegrationTests/ProcessosSeletivos/FormularioRenderizavelPersistenciaTests.cs` | 462 | código da fase do cronograma de teste | manter; PA-1 |
| idem | 464 | código de tipo de ato do produto da fase (dado de teste) | manter; PA-1 |
| `Unifesspa.UniPlus.Selecao.IntegrationTests/ProcessosSeletivos/EnvelopeCodecRoundTripTests.cs` | 77 | código da fase do cronograma de teste | manter; PA-1 |
| idem | 79 | código de tipo de ato do produto da fase (dado de teste) | manter; PA-1 |
| `Unifesspa.UniPlus.Selecao.Application.UnitTests/Commands/FatoDeElegibilidadeGateTests.cs` | 164 | código da fase do cronograma de teste | manter; PA-1 |
| idem | 208 | ponto de resolução de um fato de teste (código de fase) | manter; PA-1 |
| `Unifesspa.UniPlus.Selecao.Domain.UnitTests/Entities/FatoDeElegibilidadeDoAcrescimoTests.cs` | 51 | código da fase do cronograma de teste | manter; PA-1 |
| idem | 172 | ponto de resolução do fato no catálogo de teste (código de fase) | manter; PA-1 |
| `Unifesspa.UniPlus.Selecao.Domain.UnitTests/Entities/RepeticaoPorGrupoTests.cs` | 48 | código da fase do cronograma de teste | manter; PA-1 |
| `Unifesspa.UniPlus.Selecao.Domain.UnitTests/Entities/RetificacaoEixoDoFatoColetadoTests.cs` | 104 | código da fase do cronograma de teste | manter; PA-1 |
| `Unifesspa.UniPlus.Selecao.Domain.UnitTests/Entities/FaseEfetivaDosFatosTests.cs` | 73 | código da fase do cronograma de teste | manter; PA-1 |
| `Unifesspa.UniPlus.Selecao.Domain.UnitTests/Entities/RetificacaoFatosDasVersoesPublicadasTests.cs` | 163 e 237 | código da fase do cronograma de teste | manter; PA-1 |
| `Shared/FormularioDeTeste.cs` | 264 | repete a regra por código de `FaseCronograma.FinalidadesQueAtende` (finalidade para fase) | manter; PA-3 |

### 3.4 Migrações e instantâneos de modelo (Configuracao)

Migração aplicada não se altera. Todos os arquivos abaixo ficam como estão, e a migração nova da #2322 troca o código nos dados e nos CHECKs.

| Arquivo | O que contém | Ação |
|---|---|---|
| `20260701143441_AddFaseCanonicaETipoBanca.cs` | CHECKs `ck_fase_canonica_codigo_canonico` e `ck_fase_canonica_complementacao` com `HOMOLOGACAO` | manter (migração aplicada) |
| `20260715223421_AddPrecedenciaFaseEAtributosFaseCanonica.cs` | CHECKs de precedência e seed das seis arestas (`INSCRICAO` para `HOMOLOGACAO`, linha 82) | manter (migração aplicada) |
| `20260831204124_AdicionaFaseSolicitacaoIsencao.cs` | recria os três CHECKs com `HOMOLOGACAO` (linhas 32 a 81) | manter (migração aplicada) |
| `20260901022333_SemeiaFasesCanonicas.cs` | `INSERT` das quinze fases, com `HOMOLOGACAO` na linha 39 | manter (migração aplicada); a migração nova faz o `UPDATE` do código |
| `20260902185317_AmpliaTipoBancaEFaseCanonicaBiopsicossocial.cs` | recria os CHECKs com `HOMOLOGACAO` (linhas 74 a 207) | manter (migração aplicada) |
| `20261001042908_AdicionaModelosFormulario.cs` | CHECK `ck_modelos_formulario_finalidade` com `HABILITACAO` (finalidade) | manter (PA-2) |
| 103 arquivos `*.Designer.cs`, de `20260701143441_AddFaseCanonicaETipoBanca.Designer.cs` a `20261113024443_AdicionaManifestacoesDaProtecaoDoCatalogo.Designer.cs` | instantâneo do modelo de cada migração, com os dados semeados da época | manter (gerados, não se editam) |
| `ConfiguracaoDbContextModelSnapshot.cs` | instantâneo atual do modelo, com seed e CHECKs | derivado; é regenerado pela migração nova (`dotnet ef migrations add`) |

A migração nova da #2322 precisa, na ordem: derrubar os CHECKs que listam o código, trocar `HOMOLOGACAO` por `HOMOLOGACAO_INSCRICOES` em `fase_canonica` (código), `precedencia_fase` (antecessora e sucessora) e nos pontos de resolução (`fato_candidato.ponto_resolucao`), reescrever o JSON de `modelos_formulario.conteudo` onde `faseCodigo` cita o código, e recriar os CHECKs com o catálogo novo. As linhas de `fases_cronograma`, `etapas_processo` e as versões congeladas são dados de processo, e o caminho delas é o recadastro (seção 7).

### 3.5 Valores citados em outras camadas do código de produção

- `HOMOLOGACAO_RESULTADO_FINAL` aparece só no catálogo, no seed de fase e no seed de precedência: manter.
- Nenhuma permissão nem regra de autorização cita fase por código (seção 3.9).

### 3.6 Testes da API

Ocorrências por arquivo de teste, com as linhas e a ação. `Habilitacao` como nome de método ou de membro (`FinalidadeFormulario.Habilitacao`, 50 arquivos de teste e produção) não é código de vocabulário e acompanha a decisão de PA-2.

| Arquivo (em `tests/Unifesspa.UniPlus.*`) | Categoria | Linhas | Ação |
|---|---|---|---|
| `Regras.UnitTests/Formularios/AgregadoNaAvaliacaoTests.cs` | código de etapa de teste (`HABILITACAO`), não é fase | 18 | manter |
| `Regras.UnitTests/Formularios/RecorteDaFinalidadeTests.cs` | finalidade-HABILITACAO | 19 | manter (ponto em aberto) |
| `Regras.UnitTests/Formularios/RecorteDaFinalidadeTests.cs` | códigos de etapa e de termo de teste (`HABILITACAO:DOCUMENTOS`, `HABILITACAO:FAMILIA`, `HABILITACAO:VERACIDADE`), não são fase | 26,27,78,122,130 | manter |
| `Selecao.IntegrationTests/ProcessosSeletivos/DescarteAposDeslocamentoDeOrdemPersistenciaTests.cs` | fase-HOMOLOGACAO | 89,102,126,222,240,264,363 | renomear |
| `Selecao.IntegrationTests/ProcessosSeletivos/DescarteAposDeslocamentoDeOrdemPersistenciaTests.cs` | ato-HOMOLOGACAO_INSCRICOES | 103,241,364 | manter |
| `Selecao.IntegrationTests/ProcessosSeletivos/ProdutosDaFaseReconciliacaoPersistenciaTests.cs` | ato (cadastro) | 43,63,64,81,106,124,125,160,161,175 | manter |
| `Selecao.IntegrationTests/ProcessosSeletivos/ProdutosDaFaseReconciliacaoPersistenciaTests.cs` | fase-HOMOLOGACAO | 200 | renomear |
| `Selecao.IntegrationTests/ProcessosSeletivos/SemLiteralDeTipoDeAtoTests.cs` | ato (cadastro) | 157 | manter |
| `Selecao.IntegrationTests/ProcessosSeletivos/EnvelopeCodecRecusaDerivacaoETiposDeAtoTests.cs` | finalidade-HABILITACAO | 87 | manter (ponto em aberto) |
| `Selecao.IntegrationTests/ProcessosSeletivos/EnvelopeCodecRoundTripTests.cs` | finalidade-HABILITACAO | 98 | manter (ponto em aberto) |
| `Selecao.IntegrationTests/Outbox/Cascading/DadosDoAtoDeTeste.cs` | ato (cadastro) | 15 | manter |
| `Selecao.IntegrationTests/Outbox/Cascading/CronogramaFasesJanelaComOffsetEndpointTests.cs` | fase-HOMOLOGACAO | 65 | renomear |
| `Selecao.IntegrationTests/Outbox/Cascading/CronogramaFasesLoteDeRecusasEndpointTests.cs` | fase-HOMOLOGACAO-resultado-final | 51 | manter |
| `Selecao.IntegrationTests/Outbox/Cascading/FatosDasVersoesPublicadasNaRetificacaoEndpointTests.cs` | finalidade-HABILITACAO | 49,52,202 | manter (ponto em aberto) |
| `Selecao.IntegrationTests/Outbox/Cascading/FormulariosEndpointTests.cs` | finalidade-HABILITACAO | 96 | manter (ponto em aberto) |
| `Selecao.IntegrationTests/MotivosDeDecisao/MotivosDoProcessoEndpointTests.cs` | decisao-HOMOLOGACAO_DAS_INSCRICOES | 57 | manter |
| `Selecao.IntegrationTests/MotivosDeDecisao/MotivoDeDecisaoAdminEndpointTests.cs` | decisao-HOMOLOGACAO_DAS_INSCRICOES | 385 | manter |
| `Configuracao.IntegrationTests/FatosCandidato/FatoCandidatoAdminEndpointTests.cs` | fase-HABILITACAO | 87,266,312,326,354,371,495 | manter (ponto em aberto) |
| `Configuracao.IntegrationTests/Sementes/SementePsrMedicina2027Tests.cs` | modelo-codigo | 38 | manter |
| `Configuracao.IntegrationTests/FasesCanonicas/FaseCanonicaPersistenceTests.cs` | fase-HABILITACAO | 170 | manter (ponto em aberto) |
| `Configuracao.IntegrationTests/FasesCanonicas/FaseCanonicaEndpointTests.cs` | fase-HOMOLOGACAO | 168,181 | renomear |
| `Configuracao.IntegrationTests/FasesCanonicas/FaseCanonicaEndpointTests.cs` | fase-HABILITACAO | 242,248 | manter (ponto em aberto) |
| `Configuracao.IntegrationTests/PrecedenciasFase/PrecedenciaFaseEndpointTests.cs` | fase-HABILITACAO | 225 | manter (ponto em aberto) |
| `Configuracao.IntegrationTests/Vocabularios/VocabulariosEndpointTests.cs` | fase-HOMOLOGACAO | 102 | renomear |
| `Configuracao.IntegrationTests/Vocabularios/VocabulariosEndpointTests.cs` | fase-HABILITACAO | 109 | manter (ponto em aberto) |
| `Configuracao.IntegrationTests/Vocabularios/VocabulariosEndpointTests.cs` | fase-HOMOLOGACAO-resultado-final | 113 | manter |
| `Configuracao.IntegrationTests/ModelosFormulario/ModeloFormularioReaderTests.cs` | finalidade-HABILITACAO | 54,57 | manter (ponto em aberto) |
| `Configuracao.IntegrationTests/ModelosFormulario/ModeloFormularioAdminEndpointTests.cs` | finalidade-HABILITACAO | 56,66,271,614 | manter (ponto em aberto) |
| `Configuracao.IntegrationTests/ModelosFormulario/ModeloFormularioAdminEndpointTests.cs` | modelo-codigo | 274 | manter |
| `Configuracao.IntegrationTests/PrecedenciasFase/PrecedenciaFasePersistenceTests.cs` | fase-HOMOLOGACAO | 34,161,180 | renomear |
| `Configuracao.IntegrationTests/PrecedenciasFase/PrecedenciaFasePersistenceTests.cs` | fase-HABILITACAO | 37,38 | manter (ponto em aberto) |
| `Configuracao.IntegrationTests/PrecedenciasFase/PrecedenciaFasePersistenceTests.cs` | fase-HOMOLOGACAO-resultado-final | 39 | manter |
| `Selecao.Application.UnitTests/Queries/ObterConformidadeProcessoSeletivoQueryHandlerTests.cs` | fase-HABILITACAO | 325 | manter (ponto em aberto) |
| `Selecao.Application.UnitTests/Queries/ListarProdutosPrevistosDoProcessoQueryHandlerTests.cs` | ato (cadastro) | 30 | manter |
| `Selecao.Application.UnitTests/Queries/ListarProdutosPrevistosDoProcessoQueryHandlerTests.cs` | fase-HOMOLOGACAO | 152,156,187 | renomear |
| `Selecao.Application.UnitTests/Queries/ObterFormularioRenderizavelQueryHandlerTests.cs` | fase-HABILITACAO | 290,292 | manter (ponto em aberto) |
| `Selecao.Application.UnitTests/Queries/ObterFormularioRenderizavelQueryHandlerTests.cs` | finalidade-HABILITACAO | 350,351,352 | manter (ponto em aberto) |
| `Selecao.Application.UnitTests/Queries/PreVisualizarProcessoSeletivoQueryHandlerTests.cs` | fase-HABILITACAO | 229,318 | manter (ponto em aberto) |
| `Selecao.Application.UnitTests/Queries/PreVisualizarProcessoSeletivoQueryHandlerTests.cs` | finalidade-HABILITACAO | 312 | manter (ponto em aberto) |
| `Selecao.Application.UnitTests/Commands/DefinirOpcoesDeclaradasCommandHandlerTests.cs` | finalidade-HABILITACAO | 70,77 | manter (ponto em aberto) |
| `Selecao.Application.UnitTests/Commands/ConferenciaDoCicloRecursalAbertoTests.cs` | fase-HOMOLOGACAO | 129 | renomear |
| `Selecao.Application.UnitTests/Commands/DefinirFatosColetadosCommandHandlerTests.cs` | finalidade-HABILITACAO | 82,84 | manter (ponto em aberto) |
| `Selecao.Application.UnitTests/Commands/ReconferenciaDasCopiasDoItemTests.cs` | finalidade-HABILITACAO | 222 | manter (ponto em aberto) |
| `Selecao.Application.UnitTests/Commands/VocabularioDeFatosTests.cs` | finalidade-HABILITACAO | 146 | manter (ponto em aberto) |
| `Selecao.Application.UnitTests/Commands/PublicarProcessoSeletivoCommandHandlerTests.cs` | fase-HABILITACAO | 418 | manter (ponto em aberto) |
| `Selecao.Application.UnitTests/Commands/PublicarProdutoDoEditalCommandHandlerTests.cs` | ato (cadastro) | 29 | manter |
| `Selecao.Application.UnitTests/Commands/PublicarProdutoDoEditalCommandHandlerTests.cs` | fase-HOMOLOGACAO | 79,118,127,136,150,163,180,199,209,220,261,271,283,303,318 | renomear |
| `Selecao.Application.UnitTests/Commands/DefinirDocumentosExigidosCommandHandlerTests.cs` | fase-HOMOLOGACAO | 1021,1028,1032,1047,1052,1062,1085,1087 | renomear |
| `Selecao.Application.UnitTests/Commands/DefinirDocumentosExigidosCommandHandlerTests.cs` | fase-HABILITACAO | 1107,1108,1118,1145 | manter (ponto em aberto) |
| `Selecao.Application.UnitTests/Commands/FecharRetificacaoUnidadeDeFaseIniciadaTests.cs` | fase-HABILITACAO | 95 | manter (ponto em aberto) |
| `Selecao.Application.UnitTests/Services/ProjecaoDaAvaliacaoDaInscricaoTests.cs` | fase-HABILITACAO | 28,47,63,94 | manter (ponto em aberto) |
| `Configuracao.Domain.UnitTests/Entities/PrecedenciaFaseTests.cs` | fase-HOMOLOGACAO | 20,33,37,86,160,163,172,185,188,197,198,210,211,224,248,255 | renomear |
| `Configuracao.Domain.UnitTests/Entities/PrecedenciaFaseTests.cs` | fase-HABILITACAO | 227,228 | manter (ponto em aberto) |
| `Configuracao.Domain.UnitTests/Entities/PrecedenciaFaseTests.cs` | fase-HOMOLOGACAO-resultado-final | 229 | manter |
| `Configuracao.Domain.UnitTests/Entities/ModeloFormularioTests.cs` | modelo-codigo | 38,281,378 | manter |
| `Configuracao.Domain.UnitTests/Entities/AprovacaoDaProtecaoTests.cs` | fase-HABILITACAO | 187 | manter (ponto em aberto) |
| `Configuracao.Domain.UnitTests/Entities/FaseCanonicaTests.cs` | fase-HOMOLOGACAO-resultado-final | 86 | manter |
| `Configuracao.Domain.UnitTests/Entities/FaseCanonicaTests.cs` | fase-HOMOLOGACAO | 218,227,235,304 | renomear |
| `Configuracao.Domain.UnitTests/Entities/FaseCanonicaTests.cs` | fase-HABILITACAO | 247,256,264 | manter (ponto em aberto) |
| `Configuracao.Domain.UnitTests/Services/RegrasPadraoTests.cs` | fase-HABILITACAO | 144,147,152 | manter (ponto em aberto) |
| `Configuracao.Domain.UnitTests/Services/AlteracaoDaDefinicaoDoFatoTests.cs` | fase-HABILITACAO | 27,35,48,61,69,81,90,226,227,230,231,289,297,298,300,329,382,386,400,406,417,433,451,452,456,457,463,464 | manter (ponto em aberto) |
| `Configuracao.Domain.UnitTests/Services/AgregadoDoAdministradorTests.cs` | fase-HABILITACAO | 71,100,101,123,124,216,222 | manter (ponto em aberto) |
| `Selecao.Domain.UnitTests/Entities/ConjuntoBasicoNoFormularioDeInscricaoTests.cs` | fase-HABILITACAO | 107 | manter (ponto em aberto) |
| `Selecao.Domain.UnitTests/Entities/EtapaVinculadaAFaseTests.cs` | fase-HABILITACAO | 242,251,297,323,328,334,347,351,358,363,364,365,370,379,383,387 | manter (ponto em aberto) |
| `Selecao.Domain.UnitTests/Entities/ProcessoSeletivoCronogramaTests.cs` | fase-HOMOLOGACAO | 78,142,144,161,185,188,205,208,272,357,363,582,586 | renomear |
| `Selecao.Domain.UnitTests/Entities/ProcessoSeletivoCronogramaTests.cs` | fase-HABILITACAO | 158,164,165 | manter (ponto em aberto) |
| `Selecao.Domain.UnitTests/Entities/ProcessoSeletivoCronogramaTests.cs` | fase-HOMOLOGACAO-resultado-final | 166,236,238 | manter |
| `Selecao.Domain.UnitTests/Entities/ProcessoSeletivoCronogramaTests.cs` | ato (cadastro) | 275,276,360,361 | manter |
| `Selecao.Domain.UnitTests/Entities/RemocaoDeFaseIniciadaTests.cs` | fase-HABILITACAO | 26,39,92 | manter (ponto em aberto) |
| `Selecao.Domain.UnitTests/Entities/ConformidadeCronogramaTests.cs` | fase-HABILITACAO | 262,269,349,447,453,499 | manter (ponto em aberto) |
| `Selecao.Domain.UnitTests/Entities/ConformidadeCronogramaTests.cs` | fase-HOMOLOGACAO | 276,284,291,305,335,476 | renomear |
| `Selecao.Domain.UnitTests/Entities/ConformidadeCronogramaTests.cs` | ato-HOMOLOGACAO_INSCRICOES | 277,278,281,291,292,295,309 | manter |
| `Selecao.Domain.UnitTests/Entities/ConformidadeCronogramaTests.cs` | ato (cadastro) | 336,337,477,478 | manter |
| `Selecao.Domain.UnitTests/Entities/ProcessoSeletivoPublicarTests.cs` | fase-HOMOLOGACAO | 450 | renomear |
| `Selecao.Domain.UnitTests/Entities/ProcessoSeletivoRestaurarConfiguracaoTests.cs` | fase-HABILITACAO | 616,635,642 | manter (ponto em aberto) |
| `Selecao.Domain.UnitTests/Entities/ProcessoSeletivoDocumentosExigidosTests.cs` | fase-HABILITACAO | 157,160,174,192,214,217,224,239,243,313,319,339,346,347,352,356,368,380,397,407,626,636 | manter (ponto em aberto) |
| `Selecao.Domain.UnitTests/Services/AvaliacaoDosDocumentosDaInscricaoTests.cs` | fase-HABILITACAO | 33 | manter (ponto em aberto) |
| `Selecao.Domain.UnitTests/MotivosDeDecisao/ProcessoParaMotivos.cs` | fase-HOMOLOGACAO | 29 | renomear |
| `Portal.UnitTests/Certames/ComposicaoDaPaginaDoCertameTests.cs` | ato (dado de teste; `HOMOLOGACAO` é um código de tipo de ato fictício do dado, não de fase) | 22 | manter; a renomeação da fase não o alcança. Trocar por `HOMOLOGACAO_INSCRICOES`, código de ato do seed, é opcional e só alinha o dado de teste ao seed |
| `Selecao.Domain.UnitTests/Entities/ConformidadePublicabilidadeEstruturalTests.cs` | fase-HABILITACAO | 256,271 | manter (ponto em aberto) |
| `Selecao.Domain.UnitTests/Entities/ConformidadePublicabilidadeEstruturalTests.cs` | fase-HOMOLOGACAO | 635,672 | renomear |
| `Configuracao.Application.UnitTests/Commands/AtualizarFaseCanonicaCommandHandlerTests.cs` | fase-HOMOLOGACAO | 60 | renomear |
| `ArchTests/SolutionRules/PublicacoesSemRamificacaoPorTipoAtoTests.cs` | ato (cadastro) | 13 | manter |
| `Configuracao.Application.UnitTests/Queries/PreVisualizarModeloFormularioQueryHandlerTests.cs` | modelo-codigo | 221 | manter |
| `Configuracao.Application.UnitTests/Commands/CriarPrecedenciaFaseCommandHandlerTests.cs` | fase-HOMOLOGACAO | 20,62,102,116,120 | renomear |
| `Selecao.Domain.UnitTests/Entities/FaseCronogramaTests.cs` | ato (cadastro) | 274,275 | manter |
| `Configuracao.Application.UnitTests/Commands/RemoverPrecedenciaFaseCommandHandlerTests.cs` | fase-HOMOLOGACAO | 20 | renomear |
| `Configuracao.Application.UnitTests/Commands/CriarFaseCanonicaCommandHandlerTests.cs` | fase-HOMOLOGACAO | 72 | renomear |
| `Configuracao.Application.UnitTests/Commands/ModeloFormularioCommandHandlerTests.cs` | modelo-codigo | 234,397,413 | manter |
| `Configuracao.Application.UnitTests/Commands/ModeloFormularioCommandHandlerTests.cs` | finalidade-HABILITACAO | 397 | manter (ponto em aberto) |
| `Configuracao.Application.UnitTests/Commands/AtualizarPrecedenciaFaseCommandHandlerTests.cs` | fase-HOMOLOGACAO | 53,62 | renomear |
| `Configuracao.Application.UnitTests/Commands/AlterarDefinicaoDoFatoCandidatoCommandHandlerTests.cs` | fase-HABILITACAO | 58,134,135,136,139,200 | manter (ponto em aberto) |
| `Configuracao.Application.UnitTests/Commands/AlterarDefinicaoDoFatoCandidatoCommandHandlerTests.cs` | modelo-codigo | 71,114,226,310,323 | manter |
| `ArchTests/SolutionRules/SeedTiposAtoTests.cs` | ato (cadastro) | 128,129,131,136,174,175 | manter |
| `ArchTests/SolutionRules/SeedTiposAtoTests.cs` | ato-HOMOLOGACAO_INSCRICOES | 130,175,176,183 | manter |

Observações sobre os testes:

- Os testes de `fase-HOMOLOGACAO` com valor literal (`"HOMOLOGACAO"`) viram `"HOMOLOGACAO_INSCRICOES"`. Onde o teste só precisa de uma fase qualquer com `permite_complementacao`, prefira a constante nova do catálogo a um segundo literal.
- `PrecedenciaFasePersistenceTests.cs` e `PrecedenciaFaseTests.cs` conferem o seed das seis arestas contra a lista escrita à mão; o par `INSCRICAO` para `HOMOLOGACAO` muda nos dois.
- `VocabulariosEndpointTests.cs` confere a resposta do vocabulário de fases (`GET /api/configuracao/vocabularios/fases-canonicas`): passa a esperar `HOMOLOGACAO_INSCRICOES` e a não devolver `HOMOLOGACAO`.
- `FaseCanonicaEndpointTests.cs`: as linhas 168 e 181 usam `HOMOLOGACAO` como código válido de uma fase criada em testes de coerência (`agrupaEtapas` e origem de data): renomear o literal. As linhas 242 e 248 usam `HABILITACAO` num teste de criação com unidade sugerida: manter (PA-1). A recusa do código antigo é teste novo, que a #2322 escreve.
- `ConformidadeCronogramaTests.cs`, `ProcessoSeletivoCronogramaTests.cs` e `ProcessoSeletivoDocumentosExigidosTests.cs` usam `HOMOLOGACAO` como fase de cronograma de teste: renomear.

### 3.7 Scripts da API

| Arquivo | Uso | Ação |
|---|---|---|
| `scripts/roteiro-processo-teste-inscricao.sh`, linhas 277 e 280 | seleciona a fase `HABILITACAO` do cadastro e exige que ela exista | manter; PA-1 |
| idem, linha 647 | declara a fase `HABILITACAO` no cronograma do processo de teste | manter; PA-1 |
| idem, linhas 730 e 731 | acha a fase `HABILITACAO` no cronograma e aborta se faltar | manter; PA-1 |
| idem, linha 757 | `finalidade: "HABILITACAO"` ao declarar o documento exigido | manter; PA-2 |
| `scripts/roteiro-processo-teste-notas-do-enem.sh` | não cita fase de homologação nem de habilitação | manter |

O roteiro de teste não cita `HOMOLOGACAO`; quem o usa em HML precisa do cadastro de fases já com o código novo para os cronogramas que cite.

### 3.8 Documentos da API

| Arquivo | Uso | Ação |
|---|---|---|
| `docs/adrs/0113-fase-x-etapa-eixo-temporal-e-eixo-de-pontuacao.md`, linhas 95 a 98 | lista as seis arestas estruturais, entre elas `INSCRICAO→HOMOLOGACAO` | renomear, por emenda na ADR, que a #2322 já prevê atualizar |
| `docs/adrs/0137-formularios-por-finalidade-e-modelos-por-tipo-de-processo.md`, linha 36 | finalidades `INSCRICAO`, `ISENCAO_TAXA` e `HABILITACAO` | manter; PA-2 |
| `docs/adrs/0103-ato-normativo-generalizado-retificacao-como-relacao.md`, linha 65 | exemplos de tipo de ato (`CONVOCACAO`, `HOMOLOGACAO_ANALISE`, `LISTA_ESPERA`); `HOMOLOGACAO_ANALISE` não existe no seed (o código é `HOMOLOGACAO_ANALISE_DOCUMENTAL`) | ponto em aberto para o líder técnico (PA-6); emenda de ADR |
| `docs/adrs/0113`, emenda 1 e demais menções à fase por nome | prosa | manter |

### 3.9 Contratos OpenAPI, Postman e permissões

- `contracts/openapi.*.json`: nenhum código de fase, de decisão, de formulário ou de ato enumerado; `codigo` é texto. As sete ocorrências de "Homologação" são a descrição do servidor de homologação. Ação: manter.
- Coleções Postman (`src/*/*/postman/*.json`): zero ocorrências dos tokens. Manter.
- Configuração de permissões (catálogos `Permissoes`, `Authorization`): nenhuma permissão, recurso ou precedência cita fase por código. Manter.

### 3.10 Web

Varrido na `main` do `uniplus-web`. O SDK do web não é gerado a partir do OpenAPI para estes códigos: `libs/shared-data/openapi/*.openapi.json` não os enumera, e a lista de códigos que o web usa é escrita à mão em `libs/shared-data/src/lib/api/configuracao/fases-canonicas.api.ts` (`CODIGOS_FASE_CANONICA`, que "espelha" o catálogo da API).

Código de produção do web:

| Arquivo | Linha | Uso | Ação |
|---|---|---|---|
| `libs/shared-data/src/lib/api/configuracao/fases-canonicas.api.ts` | 29 | `'HOMOLOGACAO'` em `CODIGOS_FASE_CANONICA` | renomear |
| idem | 36 e 40 | `'HABILITACAO'`, `'HOMOLOGACAO_RESULTADO_FINAL'` | manter (PA-1 para o primeiro) |
| `apps/configuracao/src/app/features/fases-canonicas/fase-canonica-form.ts` | 6 | `CODIGOS_FASE_COM_COMPLEMENTACAO = ['HOMOLOGACAO', 'RECURSOS']` | renomear |
| `apps/configuracao/src/app/features/fases-canonicas/fase-canonica-formulario.component.html` | 257 | texto exibido ao usuário: "Aplicável apenas às fases HOMOLOGACAO e RECURSOS." | renomear (texto de tela; mudança de tela fica na uniplus-web#1446) |
| `apps/configuracao/src/app/features/fases-canonicas/fases-canonicas-lista.component.html` | 67 | `fase.codigo === 'HABILITACAO'` decide um trecho da lista | manter; PA-1 e PA-3 |
| `apps/selecao/src/app/features/processo-seletivo/wizard/passos/formulario/formularios-por-finalidade.ts` | 26, 28, 48 e 49 | `FINALIDADE_HABILITACAO`, `FASE_HABILITACAO` e a regra "a finalidade `HABILITACAO` só se oferece na fase com esse código" | manter; PA-2 e PA-3 |
| `libs/shared-ui/src/lib/components/editor-de-formulario/modelo/constantes.ts` | 30 | opção de finalidade `{ valor: 'HABILITACAO', rotulo: 'Habilitação' }` | manter; PA-2 |
| `apps/selecao/src/app/features/processo-seletivo/wizard/modelos/exigencias.models.ts` | 71 | comentário com a finalidade | manter; PA-2 |
| `apps/selecao/src/app/app.routes.ts` (linha 62) e `apps/selecao/src/app/features/homologacao/homologacao.routes.ts` (linha 4) | | `HOMOLOGACAO_ROUTES`: rota da área de homologação do web | manter; é identificador de rota, não código de vocabulário |

Todos os arquivos do web com os tokens, produção e teste, por arquivo (a ação de `fase-HOMOLOGACAO` é a troca do literal; os testes de `precedencias-fase` também citam o texto exibido, como "INSCRICAO → HOMOLOGACAO"):

| Arquivo | Categoria | Linhas | Ação |
|---|---|---|---|
| `apps/configuracao-e2e/src/specs/precedencias-fase.escritas.flow.spec.ts` | fase-HOMOLOGACAO | 13 | renomear |
| `apps/configuracao-e2e/src/specs/precedencias-fase.flow.spec.ts` | fase-HOMOLOGACAO | 22,41,136,139,226,278,320 | renomear |
| `apps/configuracao-e2e/src/specs/precedencias-fase.visual.spec.ts` | fase-HOMOLOGACAO | 14 | renomear |
| `apps/configuracao/src/app/features/fases-canonicas/fase-canonica-form.ts` | fase-HOMOLOGACAO | 6 | renomear |
| `apps/configuracao/src/app/features/fases-canonicas/fase-canonica-formulario.component.html` | fase-HOMOLOGACAO | 257 | renomear |
| `apps/configuracao/src/app/features/fases-canonicas/fases-canonicas-lista.component.html` | fase-HABILITACAO | 67 | manter (ponto em aberto) |
| `apps/configuracao/src/app/features/fases-canonicas/fases-canonicas.page.spec.ts` | fase-HOMOLOGACAO | 257 | renomear |
| `apps/configuracao/src/app/features/fatos-candidato/fatos-candidato.testing.ts` | fase-HOMOLOGACAO | 134 | renomear |
| `apps/configuracao/src/app/features/modelos-formulario/modelo-formulario-edicao.page.spec.ts` | fase-HOMOLOGACAO | 177,501,512,518 | renomear |
| `apps/configuracao/src/app/features/precedencias-fase/precedencia-form.spec.ts` | fase-HOMOLOGACAO | 18,31,60,65 | renomear |
| `apps/configuracao/src/app/features/precedencias-fase/precedencias-fase-lista.component.spec.ts` | fase-HOMOLOGACAO | 10,58,60,74 | renomear |
| `apps/configuracao/src/app/features/precedencias-fase/precedencias-fase.escritas.spec.ts` | fase-HOMOLOGACAO | 172 | renomear |
| `apps/configuracao/src/app/features/precedencias-fase/precedencias-fase.page.spec.ts` | fase-HOMOLOGACAO | 41,64,113,151,167,168,217,221 | renomear |
| `apps/configuracao/src/app/features/precedencias-fase/precedencias-fase.recusas.spec.ts` | fase-HOMOLOGACAO | 94,108,336 | renomear |
| `apps/configuracao/src/app/features/precedencias-fase/precedencias-fase.testing.ts` | fase-HOMOLOGACAO | 28 | renomear |
| `apps/configuracao/src/app/features/tipos-banca/tipos-banca.page.spec.ts` | fase-HOMOLOGACAO | 194 | renomear |
| `apps/portal/src/app/features/inscricao/formulario/inscricao-do-candidato.page.spec.ts` | fase-HABILITACAO | 607 | manter (ponto em aberto) |
| `apps/selecao-e2e/src/fixtures/cenario-da-projecao.ts` | fase-HABILITACAO | 130 | manter (ponto em aberto) |
| `apps/selecao-e2e/src/specs/bloqueio-por-protecao-pendente.backend.spec.ts` | finalidade-HABILITACAO | 56 | manter (ponto em aberto) |
| `apps/selecao-e2e/src/specs/etapa-da-fase.authenticated.spec.ts` | fase-HABILITACAO | 41,107 | manter (ponto em aberto) |
| `apps/selecao-e2e/src/specs/unidade-da-fase.authenticated.spec.ts` | HABILITACAO (fase ou finalidade) | 109 | manter (ponto em aberto) |
| `apps/selecao/src/app/app.routes.ts` | identificador-rota-web | 62 | manter (não é código de vocabulário) |
| `apps/selecao/src/app/features/homologacao/homologacao.routes.ts` | identificador-rota-web | 4 | manter (não é código de vocabulário) |
| `apps/selecao/src/app/features/processo-seletivo/processo-seletivo.page.spec.ts` | finalidade-HABILITACAO | 588 | manter (ponto em aberto) |
| `apps/selecao/src/app/features/processo-seletivo/publicacoes/modo-da-publicacao.spec.ts` | ato (cadastro) | 15 | manter |
| `apps/selecao/src/app/features/processo-seletivo/publicacoes/previstos-do-edital.spec.ts` | ato (cadastro) | 19,46 | manter |
| `apps/selecao/src/app/features/processo-seletivo/publicacoes/publicacoes-do-processo.page.spec.ts` | ato (cadastro) | 84 | manter |
| `apps/selecao/src/app/features/processo-seletivo/publicacoes/publicar-documento.component.spec.ts` | ato (cadastro) | 78 | manter |
| `apps/selecao/src/app/features/processo-seletivo/simulacao/simulacao-do-processo.page.spec.ts` | finalidade-HABILITACAO | 31,41,44 | manter (ponto em aberto) |
| `apps/selecao/src/app/features/processo-seletivo/wizard/modelos/exigencias.models.ts` | HABILITACAO (fase ou finalidade) | 71 | manter (ponto em aberto) |
| `apps/selecao/src/app/features/processo-seletivo/wizard/passos/cronograma/cronograma-do-certame.testing.ts` | fase-HOMOLOGACAO | 107 | renomear |
| `apps/selecao/src/app/features/processo-seletivo/wizard/passos/cronograma/cronograma.component.spec.ts` | HABILITACAO (fase ou finalidade) | 3332 | manter (ponto em aberto) |
| `apps/selecao/src/app/features/processo-seletivo/wizard/passos/cronograma/precedencia-de-fases.spec.ts` | fase-HOMOLOGACAO | 10,38,56,78,93 | renomear |
| `apps/selecao/src/app/features/processo-seletivo/wizard/passos/fase/configuracao-da-fase.spec.ts` | ato (cadastro) | 42,43 | manter |
| `apps/selecao/src/app/features/processo-seletivo/wizard/passos/fase/fase.component.spec.ts` | finalidade-HABILITACAO | 673,800 | manter (ponto em aberto) |
| `apps/selecao/src/app/features/processo-seletivo/wizard/passos/fase/resumo-da-exigencia.spec.ts` | HABILITACAO (fase ou finalidade) | 48,56,252,290 | manter (ponto em aberto) |
| `apps/selecao/src/app/features/processo-seletivo/wizard/passos/formulario/citantes-no-processo.spec.ts` | finalidade-HABILITACAO | 80 | manter (ponto em aberto) |
| `apps/selecao/src/app/features/processo-seletivo/wizard/passos/formulario/citantes-no-processo.spec.ts` | fase-HABILITACAO | 81 | manter (ponto em aberto) |
| `apps/selecao/src/app/features/processo-seletivo/wizard/passos/formulario/formulario-de-inscricao.testing.ts` | HABILITACAO (fase ou finalidade) | 134 | manter (ponto em aberto) |
| `apps/selecao/src/app/features/processo-seletivo/wizard/passos/formulario/formulario.component.spec.ts` | finalidade-HABILITACAO | 559,576,597,635,642,731,736,747,904,934 | manter (ponto em aberto) |
| `apps/selecao/src/app/features/processo-seletivo/wizard/passos/formulario/formulario.component.spec.ts` | fase-HABILITACAO | 563,577,885,900,928,935 | manter (ponto em aberto) |
| `apps/selecao/src/app/features/processo-seletivo/wizard/passos/formulario/formulario.component.spec.ts` | HABILITACAO (fase ou finalidade) | 671,701,707,760,895,912,917,1013,1036,1037,1083 | manter (ponto em aberto) |
| `apps/selecao/src/app/features/processo-seletivo/wizard/passos/formulario/formularios-por-finalidade.spec.ts` | fase-HABILITACAO | 52,146,147 | manter (ponto em aberto) |
| `apps/selecao/src/app/features/processo-seletivo/wizard/passos/formulario/formularios-por-finalidade.spec.ts` | finalidade-HABILITACAO | 58,88,98,139 | manter (ponto em aberto) |
| `apps/selecao/src/app/features/processo-seletivo/wizard/passos/formulario/formularios-por-finalidade.spec.ts` | HABILITACAO (fase ou finalidade) | 61,90,103,115,117,122,123 | manter (ponto em aberto) |
| `apps/selecao/src/app/features/processo-seletivo/wizard/passos/formulario/formularios-por-finalidade.ts` | finalidade-HABILITACAO | 26 | manter (ponto em aberto) |
| `apps/selecao/src/app/features/processo-seletivo/wizard/passos/formulario/formularios-por-finalidade.ts` | fase-HABILITACAO | 28 | manter (ponto em aberto) |
| `apps/selecao/src/app/features/processo-seletivo/wizard/passos/formulario/modelo-de-formulario.spec.ts` | fase-HABILITACAO | 90,127,129,132,133,134,163 | manter (ponto em aberto) |
| `apps/selecao/src/app/features/processo-seletivo/wizard/passos/formulario/modelo-de-formulario.spec.ts` | HABILITACAO (fase ou finalidade) | 106,147,157 | manter (ponto em aberto) |
| `apps/selecao/src/app/features/processo-seletivo/wizard/passos/formulario/modelo-de-formulario.spec.ts` | finalidade-HABILITACAO | 114,156,161 | manter (ponto em aberto) |
| `apps/selecao/src/app/features/processo-seletivo/wizard/passos/formulario/problemas-do-formulario.spec.ts` | fase-HABILITACAO | 15 | manter (ponto em aberto) |
| `apps/selecao/src/app/features/processo-seletivo/wizard/passos/formulario/problemas-do-formulario.spec.ts` | HABILITACAO (fase ou finalidade) | 99 | manter (ponto em aberto) |
| `apps/selecao/src/app/features/processo-seletivo/wizard/shared/exigencias/alcance-das-exigencias.spec.ts` | fase-HABILITACAO | 21,58 | manter (ponto em aberto) |
| `apps/selecao/src/app/features/processo-seletivo/wizard/shared/exigencias/arquivo-da-exigencia.spec.ts` | HABILITACAO (fase ou finalidade) | 14,21 | manter (ponto em aberto) |
| `apps/selecao/src/app/features/processo-seletivo/wizard/shared/exigencias/consulta-das-exigencias.spec.ts` | HABILITACAO (fase ou finalidade) | 45,120,124,150 | manter (ponto em aberto) |
| `apps/selecao/src/app/features/processo-seletivo/wizard/shared/exigencias/consulta-das-exigencias.spec.ts` | fase-HABILITACAO | 53,73,169 | manter (ponto em aberto) |
| `apps/selecao/src/app/features/processo-seletivo/wizard/shared/exigencias/exigencias-documentais.spec.ts` | fase-HABILITACAO | 31,36,47,52,153,175 | manter (ponto em aberto) |
| `apps/selecao/src/app/features/processo-seletivo/wizard/shared/exigencias/exigencias-documentais.spec.ts` | HABILITACAO (fase ou finalidade) | 122,164,181 | manter (ponto em aberto) |
| `apps/selecao/src/app/features/processo-seletivo/wizard/shared/exigencias/exigencias.testing.ts` | HABILITACAO (fase ou finalidade) | 17,23,30 | manter (ponto em aberto) |
| `apps/selecao/src/app/features/processo-seletivo/wizard/shared/exigencias/formulario-da-exigencia.spec.ts` | fase-HABILITACAO | 34 | manter (ponto em aberto) |
| `apps/selecao/src/app/features/processo-seletivo/wizard/shared/exigencias/formulario-da-exigencia.spec.ts` | HABILITACAO (fase ou finalidade) | 45,53,57,81,142 | manter (ponto em aberto) |
| `apps/selecao/src/app/features/processo-seletivo/wizard/shared/exigencias/gravacao-das-exigencias.spec.ts` | fase-HABILITACAO | 22,328,331,334 | manter (ponto em aberto) |
| `apps/selecao/src/app/features/processo-seletivo/wizard/shared/exigencias/gravacao-das-exigencias.spec.ts` | HABILITACAO (fase ou finalidade) | 164,309 | manter (ponto em aberto) |
| `apps/selecao/src/app/features/processo-seletivo/wizard/shared/exigencias/leitura-das-exigencias.spec.ts` | fase-HABILITACAO | 26,85,190,219,257,340,354 | manter (ponto em aberto) |
| `apps/selecao/src/app/features/processo-seletivo/wizard/shared/exigencias/leitura-das-exigencias.spec.ts` | HABILITACAO (fase ou finalidade) | 206,232,279,298 | manter (ponto em aberto) |
| `apps/selecao/src/app/features/processo-seletivo/wizard/shared/gatilho-de-exigencia.spec.ts` | HABILITACAO (fase ou finalidade) | 574,578 | manter (ponto em aberto) |
| `apps/selecao/src/app/features/processo-seletivo/wizard/shared/gatilho-de-exigencia.spec.ts` | fase-HABILITACAO | 590,686 | manter (ponto em aberto) |
| `apps/selecao/src/app/features/processo-seletivo/wizard/shared/hidratacao.spec.ts` | fase-HOMOLOGACAO | 189,296 | renomear |
| `apps/selecao/src/app/features/processo-seletivo/wizard/shared/hidratacao.spec.ts` | ato (cadastro) | 198,199,308,309,368 | manter |
| `apps/selecao/src/app/features/processo-seletivo/wizard/shared/producao-dos-fatos.spec.ts` | HABILITACAO (fase ou finalidade) | 57,119,176,233,278,296,311,418,425,453 | manter (ponto em aberto) |
| `apps/selecao/src/app/features/processo-seletivo/wizard/shared/producao-dos-fatos.spec.ts` | fase-HABILITACAO | 128,130,140,149,168,183,346,354,355,380,423,439 | manter (ponto em aberto) |
| `apps/selecao/src/app/features/processo-seletivo/wizard/shared/producao-dos-fatos.spec.ts` | finalidade-HABILITACAO | 345,379 | manter (ponto em aberto) |
| `libs/shared-data/src/lib/api/configuracao/fases-canonicas.api.ts` | fase-HOMOLOGACAO | 29 | renomear |
| `libs/shared-data/src/lib/api/configuracao/fases-canonicas.api.ts` | HABILITACAO (fase ou finalidade) | 36 | manter (ponto em aberto) |
| `libs/shared-data/src/lib/api/configuracao/fases-canonicas.api.ts` | fase-HOMOLOGACAO-resultado-final | 40 | manter |
| `libs/shared-data/src/lib/api/configuracao/precedencias-fase.api.spec.ts` | fase-HOMOLOGACAO | 26 | renomear |
| `libs/shared-data/src/lib/api/publicacoes/tipos-ato.api.spec.ts` | ato (cadastro) | 24 | manter |
| `libs/shared-data/src/lib/api/publicacoes/tipos-ato.api.spec.ts` | ato (código de tipo de ato na URL, teste de escape; `RESULTADO/HOMOLOGACAO` não é fase) | 126,129 | manter |
| `libs/shared-data/src/lib/api/selecao/cronograma-do-processo.api.spec.ts` | ato (cadastro) | 142,156,225,234 | manter |
| `libs/shared-ui/src/lib/components/editor-de-formulario/editor-de-formulario.spec.ts` | finalidade-HABILITACAO | 909,939 | manter (ponto em aberto) |
| `libs/shared-ui/src/lib/components/editor-de-formulario/modelo/constantes.ts` | HABILITACAO (fase ou finalidade) | 30 | manter (ponto em aberto) |
| `libs/shared-ui/src/lib/components/editor-de-formulario/modelo/modelo-do-formulario.spec.ts` | fase-HABILITACAO | 495 | manter (ponto em aberto) |
| `libs/shared-ui/src/lib/components/editor-de-formulario/modelo/modelo-do-formulario.spec.ts` | HABILITACAO (fase ou finalidade) | 719 | manter (ponto em aberto) |
| `libs/shared-ui/src/lib/components/formulario-do-candidato/documentos-da-inscricao.spec.ts` | fase-HABILITACAO | 310 | manter (ponto em aberto) |

### 3.11 Portal (`uniplus-developers`)

Não é alterado por esta issue. A uniplus-developers#455 já foi integrada; o que segue entra como ponto para o portal, sem reabrir a issue.

| Arquivo | Linha | Uso | Ação |
|---|---|---|---|
| `src/data/produto/requisitos-mvp-selecao.ts` | 141 | lista dos dezesseis códigos de fase, com `HOMOLOGACAO` | ponto para o portal: renomear |
| `src/data/produto/requisitos-mvp-selecao.ts` | 6744 | separa o vocabulário de tipos de decisão do de fases e de bancas | manter |
| `src/data/produto/requisitos-mvp-selecao.ts` | 7878 | já diz que a fase é reconhecida pela capacidade configurada e cita `HOMOLOGACAO_INSCRICOES` | manter |
| `docs/erros/uniplus.configuracao.fase_canonica.codigo_fora_do_conjunto_canonico.mdx` | 31 | lista os códigos aceitos, com `HOMOLOGACAO` | ponto para o portal: renomear |
| `docs/erros/uniplus.configuracao.fase_canonica.complementacao_apenas_fases_permitidas.mdx` | 4, 10, 11, 25 e 34 | cita `HOMOLOGACAO` como fase que admite complementação | ponto para o portal: renomear |
| `docs/erros/uniplus.selecao.formulario_processo.codigo_duplicado.mdx` | 38 e 39 | exemplo com código de formulário `HABILITACAO_COMPLEMENTAR` no lugar de `HABILITACAO` (finalidade) | manter; PA-2 |
| `docs/erros/uniplus.selecao.motivo_de_decisao.tipo_obrigatorio.mdx` | 24 e 30 | lista os seis tipos de decisão, com `HOMOLOGACAO_DAS_INSCRICOES` | manter |
| `docs/erros/uniplus.selecao.processo_seletivo.tipo_de_ato_unico_declarado_mais_de_uma_vez.mdx` | 81 e 82 | cita `RESULTADO_HOMOLOGACAO` e `HOMOLOGACAO_INSCRICOES` como tipos de ato | manter; PA-4 |

## 4. Vocabulário 2: tipo de decisão

Origem: `src/selecao/Unifesspa.UniPlus.Selecao.Domain/Enums/TipoMotivoDeDecisaoCodigo.cs` (código do wire) e `TipoMotivoDeDecisao.cs` (enumerado). Vocabulário **fechado em código**. A coluna `motivos_de_decisao.tipo` grava o **nome do membro do enumerado** (`HasConversion<string>`), não o código do wire, então renomear código do wire não toca dado.

| Código atual | Código novo ou mantido | Política e fase em que é avaliado | Ação |
|---|---|---|---|
| `ISENCAO_DA_TAXA` | mantido | `PoliticaDaIsencaoDaTaxa`; avaliada na fase que coleta a solicitação de isenção (capacidade) | manter |
| `HOMOLOGACAO_DAS_INSCRICOES` | mantido | `PoliticaDaHomologacaoDasInscricoes`; hoje avaliada na fase de código `HOMOLOGACAO` (linha 14 da seção 3.3) | manter o código do tipo; o literal da fase segue a seção 3.3 |
| `ANALISE_DOCUMENTAL` | mantido | `PoliticaDaAnaliseDocumental`; hoje avaliada na fase de código `HABILITACAO` | manter; PA-1 e PA-3 |
| `HETEROIDENTIFICACAO` | mantido | `PoliticaDaHeteroidentificacao`; fase `HETEROIDENTIFICACAO` | manter; PA-3 |
| `AVALIACAO_BIOPSICOSSOCIAL` | mantido | `PoliticaDaAvaliacaoBiopsicossocial`; fase `AVALIACAO_BIOPSICOSSOCIAL` | manter; PA-3 |
| `CLASSIFICACAO` | mantido | `PoliticaDaClassificacao`; fase `CLASSIFICACAO` | manter; PA-3 |

Usos do tipo `HOMOLOGACAO_DAS_INSCRICOES`:

| Arquivo | Uso | Ação |
|---|---|---|
| `src/selecao/Unifesspa.UniPlus.Selecao.Domain/Enums/TipoMotivoDeDecisaoCodigo.cs` (linhas 11 e 20) | constante e mapa código para enumerado | manter |
| `src/selecao/Unifesspa.UniPlus.Selecao.Domain/Enums/TipoMotivoDeDecisao.cs` (linha 17) | membro `HomologacaoDasInscricoes = 2` | manter |
| `src/selecao/Unifesspa.UniPlus.Selecao.Domain/MotivosDeDecisao/PoliticasDosTiposDeMotivo.cs` (linha 12) | registra a política | manter |
| `tests/Unifesspa.UniPlus.Selecao.IntegrationTests/MotivosDeDecisao/MotivoDeDecisaoAdminEndpointTests.cs` (linha 385), `MotivosDoProcessoEndpointTests.cs` (linha 57) | testes de endpoint | manter |
| Portal: `docs/erros/uniplus.selecao.motivo_de_decisao.tipo_obrigatorio.mdx` e requisito da linha 6744 | texto que lista os tipos | manter |

Usos do mecanismo de código do tipo de decisão, sem literal, para os seis códigos (o código entra e sai pelo mapa de `TipoMotivoDeDecisaoCodigo`):

| Arquivo | Uso | Ação |
|---|---|---|
| `src/selecao/Unifesspa.UniPlus.Selecao.API/Controllers/FiltroDaListagemDeMotivos.cs` (linhas 24 e 34) | converte e lista os códigos aceitos no filtro da listagem | manter |
| `src/selecao/Unifesspa.UniPlus.Selecao.Application/Commands/MotivosDeDecisao/CriarMotivoDeDecisaoCommandHandler.cs` (linha 27) | converte o código recebido na criação do motivo | manter |
| `src/selecao/Unifesspa.UniPlus.Selecao.Domain/MotivosDeDecisao/ValidacaoDoMotivo.cs` (linhas 84 e 102) | recusa tipo desconhecido e sentido não admitido, citando o código | manter |
| `src/selecao/Unifesspa.UniPlus.Selecao.Application/Mappings/MotivoDeDecisaoMapping.cs` (linhas 16 e 30) | devolve o código do tipo no contrato | manter |
| `src/selecao/Unifesspa.UniPlus.Selecao.Domain/MotivosDeDecisao/PoliticaSemSubclassificacao.cs` (linha 29) e `EscolhaDosMotivos.cs` (linha 91) | citam o código do tipo em mensagem | manter |

Os tipos novos de decisão são da [uniplus-api#2338](https://github.com/unifesspa-edu-br/uniplus-api/issues/2338): fora do escopo.

## 5. Vocabulário 3: formulário

Dois códigos distintos moram aqui: a **finalidade** do formulário (fechada em código) e o **código do modelo** de formulário (cadastro).

### 5.1 Finalidade

Origem: `src/shared/Unifesspa.UniPlus.Regras/Formularios/EstruturaFormulario.cs` (constantes `FinalidadeInscricao`, `FinalidadeIsencaoTaxa`, `FinalidadeHabilitacao`) e o enumerado `FinalidadeFormulario`. Gravada em `modelos_formulario.finalidade`, em `formularios_processo` e no envelope da versão publicada; aparece na rota `/formularios/{finalidade}` e no JSON das requisições.

| Código atual | Código novo ou mantido | Ação |
|---|---|---|
| `INSCRICAO` | mantido | manter |
| `ISENCAO_TAXA` | mantido | manter |
| `HABILITACAO` | mantido neste mapa | manter; ponto em aberto para o líder técnico (PA-2) |

Usos de `INSCRICAO` e `ISENCAO_TAXA` como finalidade (mantidos): constantes e conversões em `EstruturaFormulario.cs` (linhas 44, 45, 182, 183, 191 e 192), CHECK `ck_modelos_formulario_finalidade` (`ModeloFormularioConfiguration.cs`, linha 31) e a ADR-0137. A fase que atende cada uma é decidida pela capacidade da fase (`ColetaInscricao`, `ColetaSolicitacaoIsencao`), e não por código; só a habilitação usa o código.

Usos de `HABILITACAO` como finalidade:

| Arquivo | Uso | Ação |
|---|---|---|
| `src/shared/Unifesspa.UniPlus.Regras/Formularios/EstruturaFormulario.cs` (linhas 46, 184 e 193) | constante e conversões de e para o enumerado | manter; PA-2 |
| `src/configuracao/.../Persistence/Configurations/ModeloFormularioConfiguration.cs` (linha 31) | CHECK `ck_modelos_formulario_finalidade` | manter; PA-2 |
| `src/configuracao/.../Persistence/Migrations/20261001042908_AdicionaModelosFormulario.cs` | o mesmo CHECK na migração aplicada | manter |
| `src/configuracao/Unifesspa.UniPlus.Configuracao.Contracts/ModeloFormularioView.cs` (linha 48) | comentário com os tokens | manter; PA-2 |
| `src/selecao/Unifesspa.UniPlus.Selecao.API/Controllers/FormulariosController.cs` (linha 30) | comentário; rota por finalidade | manter; PA-2 |
| `src/selecao/Unifesspa.UniPlus.Selecao.Application/DTOs/DocumentoExigidoDto.cs` (linha 20), `PreVisualizacaoDoProcessoDto.cs` (linha 91) e `Commands/ProcessosSeletivos/DefinirDocumentosExigidosCommand.cs` (linha 62) | comentários com os tokens | manter; PA-2 |
| `docs/adrs/0137` (linha 36) | decisão das finalidades | manter; PA-2 |
| Testes e web | seções 3.6 e 3.10, categoria `finalidade-HABILITACAO` | manter; PA-2 |

### 5.2 Código do modelo de formulário

Origem: cadastro `configuracao.modelos_formulario.codigo`, semeado por `SementePsrMedicina2027` (`PSR_INSCRICAO_SEM_PROVA`, `PSR_INSCRICAO_COM_PROVA`, `PSR_MEDICINA_2027_HABILITACAO`) e pelo modelo de isenção da taxa. É **cadastro configurável**: o código é chave do modelo, e não vocabulário de fase.

| Código | Definido em | Ação |
|---|---|---|
| `PSR_MEDICINA_2027_HABILITACAO` (`ModeloDeHabilitacao`) | `SementePsrMedicina2027.cs` (linha 93) | manter; é a chave de um modelo de habilitação, e não a fase |
| `HABILITACAO_MEDICINA` | só em testes da API (`ModeloFormularioTests.cs`, `ModeloFormularioCommandHandlerTests.cs`, `PreVisualizarModeloFormularioQueryHandlerTests.cs`, `AlterarDefinicaoDoFatoCandidatoCommandHandlerTests.cs`) | manter |
| `HABILITACAO_POR_PROCURADOR` | código de um fato semeado (`SementePsrMedicina2027.cs`, linhas 202 e 532), não de fase nem de finalidade | manter |
| `HABILITACAO_COMPLEMENTAR` | exemplo no portal | manter |

O que um modelo de formulário cita por código **de fase** é o `faseCodigo` de cada documento exigido, em `modelos_formulario.conteudo` (JSON). As sementes só citam `INSCRICAO`; um modelo cadastrado em HML pode citar `HOMOLOGACAO` (por exemplo, para complementação documental). Esse é o caso "modelo de formulário que cita o código antigo da fase": consta como ação **renomear no dado**, pela migração nova (seção 3.4) e pelo recadastro (seção 7). Ao aplicar um modelo a um processo, o documento cuja fase não existe no cronograma é **recusado com mensagem** (seção 3.3, linha 21), e não ignorado; o documento descartado antes por condição não chega a consultar a fase.

## 6. Vocabulário 4: tipo de ato

Origem: **cadastro versionado de Publicações** (`TipoAtoPublicado`), referência em `seeds/seed-tipos-ato.json`, que o ArchTest `SeedTiposAtoTests` e o health check `CatalogoDeTiposAtoDeclarado` conferem. A ADR-0103 proíbe literal de tipo de ato no domínio; por isso, ao contrário do tipo de decisão, o tipo de ato **não é vocabulário fechado em código**. A issue descreve os dois como fechados em código; o código diz outra coisa, e isso consta como PA-4.

| Código atual no seed | Código novo ou mantido | Ação |
|---|---|---|
| `EDITAL_ABERTURA`, `EDITAL_RETIFICACAO`, `AVISO`, `COMUNICADO`, `CONVOCACAO`, `CHAMADA`, `ERRATA`, `GABARITO`, `LISTA_ESPERA`, `CONFIRMACAO_INTERESSE`, `RESULTADO_CLASSIFICACAO`, `RESULTADO_ISENCAO`, `RESULTADO_HETEROIDENTIFICACAO`, `RESULTADO_AVALIACAO_BIOPSICOSSOCIAL` | mantidos | manter |
| `HOMOLOGACAO_INSCRICOES` | mantido | manter |
| `HOMOLOGACAO_ANALISE_DOCUMENTAL` | mantido | manter |
| `HOMOLOGACAO_RECURSOS` | mantido | manter |
| `HOMOLOGACAO_HABILITACAO` | mantido | manter; PA-4 |
| `RESULTADO_HOMOLOGACAO` ("Resultado da homologação das inscrições") | mantido | manter; PA-4 |

Usos:

| Arquivo | Uso | Ação |
|---|---|---|
| `seeds/seed-tipos-ato.json` (linhas 68, 81, 94, 211 e 237) | definição dos cinco tipos de homologação | manter |
| `tests/Unifesspa.UniPlus.ArchTests/SolutionRules/SeedTiposAtoTests.cs` (linhas 128 a 136 e 174 a 183) | lista esperada de resultados e de únicos por objeto | manter |
| `tests/Unifesspa.UniPlus.ArchTests/SolutionRules/PublicacoesSemRamificacaoPorTipoAtoTests.cs` (linha 13) | comentário de exemplo | manter |
| `tests/.../Selecao.IntegrationTests/ProcessosSeletivos/SemLiteralDeTipoDeAtoTests.cs` (linha 157) | mensagem de teste que cita `HOMOLOGACAO_ANALISE` | manter; PA-6 |
| `tests/.../Selecao.IntegrationTests/Outbox/Cascading/DadosDoAtoDeTeste.cs` (linha 15) | tipo de ato do dado de teste | manter |
| Demais testes da seção 3.6 com categoria `ato (cadastro)` | produtos de fase e atos de teste | manter |
| `scripts/consolida-tipos-ato-por-materia.mapa-hml.tsv` | mapa do catálogo de HML: `RESULTADO_PRELIMINAR_HOMOLOGACAO` para `RESULTADO_HOMOLOGACAO`; `RESULTADO_PRELIMINAR_HABILITACAO` e `RESULTADO_DEFINITIVO_HABILITACAO` para `RESULTADO_HABILITACAO`; cita `HOMOLOGACAO_RESULTADO_FINAL` (tipo de ato de HML) | manter; PA-4 |
| `scripts/consolida-tipos-ato-por-materia.sh` | aplica o mapa pela API de Publicações | manter |
| Web (`RESULTADO_HOMOLOGACAO` em `modo-da-publicacao.spec.ts`, `previstos-do-edital.spec.ts`, `publicacoes-do-processo.page.spec.ts`, `publicar-documento.component.spec.ts`, `configuracao-da-fase.spec.ts` e `tipos-ato.api.spec.ts`) | dados de teste | manter |

O tipo de ato `HOMOLOGACAO_INSCRICOES` já é o código qualificado e não muda na #2322. Criar tipo de ato novo é da [uniplus-api#2327](https://github.com/unifesspa-edu-br/uniplus-api/issues/2327).

## 7. Códigos antigos a recusar e efeito no HML

### 7.1 O que passa a ser recusado

Só a fase tem código renomeado. Onde o código antigo deixa de ser aceito, e como:

| Onde | Como o código antigo é recusado |
|---|---|
| Criação e atualização de fase canônica | "fora do conjunto canônico" (`uniplus.configuracao.fase_canonica.codigo_fora_do_conjunto_canonico`), pela guarda de domínio e pelo validator |
| Criação e atualização de precedência de fase | mesma recusa, pela guarda de domínio |
| Ponto de resolução de fato (`FatoCandidato.ValidarPontoResolucao`) | recusa `FatoCandidato.PontoResolucaoInvalido`, porque o ponto precisa ser código canônico |
| Aplicação de um modelo de formulário a um processo | recusa com mensagem quando a fase que o `faseCodigo` do documento cita não existe no cronograma do processo (`SelecaoDosDocumentosDoModelo`) |
| Banco: `fase_canonica` e `precedencia_fase` | CHECKs recriados sem o código antigo; o `INSERT` direto é recusado |
| Cadastro e edição do próprio modelo de formulário | **não** recusa: o `faseCodigo` do documento só é conferido quanto a estar preenchido e a ser texto válido, e é normalizado, sem conferência contra o catálogo (ver PA-7) |

O vocabulário de fases passa a devolver `HOMOLOGACAO_INSCRICOES` com o mesmo rótulo e a não devolver `HOMOLOGACAO`. Nenhum código de tipo de decisão, de finalidade de formulário nem de tipo de ato muda nesta entrega, então nenhum deles tem código antigo a recusar.

### 7.2 O que depende de recadastro no HML

O seed da API chega ao HML por migração; o que não chega é o dado de processo e de cadastro editado em HML. O roteiro de implantação em HML é o já usado: backup do banco, apagar processos e inscrições, subir a versão nova, recadastrar a partir do backup. O seed e o backup do HML **não** foram lidos nesta varredura (o conteúdo do banco de HML não é acessível a este levantamento), de modo que a lista abaixo é a das colunas que **podem** guardar o código antigo, a conferir no backup antes de apagar:

| Onde (schema e tabela) | Coluna | Natureza | Caminho no HML |
|---|---|---|---|
| `configuracao.fase_canonica` | `codigo` | seed e cadastro | a migração nova troca o código; sem recadastro |
| `configuracao.precedencia_fase` | `antecessora_codigo`, `sucessora_codigo` | seed e cadastro | a migração nova troca; sem recadastro |
| `configuracao.fato_candidato` | `ponto_resolucao` | cadastro | a migração nova troca onde valer `HOMOLOGACAO`; as sementes só usam `INSCRICAO` e `HABILITACAO` |
| `configuracao.modelos_formulario` | `conteudo` (JSON, `documentosExigidos[].faseCodigo`) | cadastro | a migração nova reescreve o JSON; modelos de HML recadastrados a partir do backup precisam trazer o código novo |
| `selecao.fases_cronograma` | `codigo`, `fase_concluinte_codigo` | dado de processo | recadastro do processo (backup, apagar, subir, recadastrar), com o JSON de exportação do processo já com o código novo |
| `selecao.etapas_processo` | `fase_codigo` | dado de processo | recadastro do processo |
| `selecao.versoes_configuracao` | `configuracao_congelada` (jsonb), `configuracao_congelada_canonica` (bytea) e o hash | versão publicada, imutável | recadastro do processo; não se reescreve versão publicada, porque o hash cobre o código |

Conferência a rodar no HML, em leitura, antes de apagar (substitui adivinhar o que está no backup):

```sql
SELECT 'fase_canonica' AS tabela, count(*) FROM configuracao.fase_canonica WHERE codigo = 'HOMOLOGACAO'
UNION ALL SELECT 'precedencia_fase', count(*) FROM configuracao.precedencia_fase
  WHERE 'HOMOLOGACAO' IN (antecessora_codigo, sucessora_codigo)
UNION ALL SELECT 'fato_candidato', count(*) FROM configuracao.fato_candidato WHERE ponto_resolucao = 'HOMOLOGACAO'
UNION ALL SELECT 'modelos_formulario', count(*) FROM configuracao.modelos_formulario
  WHERE conteudo::text LIKE '%"faseCodigo": "HOMOLOGACAO"%' OR conteudo::text LIKE '%"faseCodigo":"HOMOLOGACAO"%'
UNION ALL SELECT 'fases_cronograma', count(*) FROM selecao.fases_cronograma
  WHERE 'HOMOLOGACAO' IN (codigo, fase_concluinte_codigo)
UNION ALL SELECT 'etapas_processo', count(*) FROM selecao.etapas_processo WHERE fase_codigo = 'HOMOLOGACAO'
UNION ALL SELECT 'versoes_configuracao', count(*) FROM selecao.versoes_configuracao
  WHERE configuracao_congelada::text LIKE '%"HOMOLOGACAO"%';
```

A consulta só conta linhas; não lê dado pessoal. O backup usado no recadastro precisa trazer o código novo (condição de liberação da #2322 em HML).

O catálogo de tipos de ato de HML não é o seed (os códigos de ato de HML citados na seção 6 vêm do mapa de consolidação): ele **não** é afetado pela renomeação da fase.

## 8. Inventário adiado

O que foi encontrado e não será renomeado agora, com a justificativa:

| Item | Justificativa | Decisão |
|---|---|---|
| Fase `HABILITACAO` e `CodigoFaseHabilitacao` | A decisão de 11/11/2026 qualifica a homologação; não fala de habilitação. `HABILITACAO` tem hoje um sentido só em cada vocabulário que a usa (fase e finalidade), então não há confusão de código a desfazer | ponto em aberto para o líder técnico (PA-1) |
| Finalidade de formulário `HABILITACAO` | Mesma razão; renomear mexe em rota (`/formularios/{finalidade}`), CHECK, envelope publicado, web e portal | ponto em aberto para o líder técnico (PA-2) |
| Regras por código de fase já existentes (`FaseCronograma.FinalidadesQueAtende`, `PoliticaSemSubclassificacao` com cinco fases, `FASE_HABILITACAO` e a lista do web) | Contradizem a diretriz de reconhecer a fase pela capacidade configurada. Substituir exige a declaração por fase da #2338 e uma decisão sobre como a finalidade de habilitação encontra a sua fase | ponto em aberto para o líder técnico (PA-3) |
| Tipos de ato de homologação com nomes parecidos (`HOMOLOGACAO_INSCRICOES` e `RESULTADO_HOMOLOGACAO`; `HOMOLOGACAO_HABILITACAO`) e códigos que só existem no catálogo de HML (`RESULTADO_HABILITACAO`, `HOMOLOGACAO_RESULTADO_FINAL`) | A decisão qualifica o ato como `HOMOLOGACAO_INSCRICOES`. Não é possível dizer, sem o catálogo de HML, se `RESULTADO_HOMOLOGACAO` e `HOMOLOGACAO_INSCRICOES` são duas matérias ou a mesma. Criar ou fundir tipo de ato é da #2327 | ponto em aberto para o líder técnico (PA-4) |
| Finalidade/formulário de homologação | Não existe. Se a regra de homologação da #2322 vier a precisar de formulário, será finalidade nova, e não rename | ponto em aberto para o líder técnico (PA-5) |
| Cadastro do modelo de formulário que não confere o `faseCodigo` do documento contra o catálogo | Hoje só a aplicação do modelo ao processo recusa; mudar isso é decisão de escopo da #2322 | ponto em aberto para o líder técnico (PA-7) |
| `HOMOLOGACAO_ANALISE` citado na ADR-0103 e em mensagem de teste, sem existir no seed | Exemplo desatualizado; corrigir é emenda de ADR | ponto em aberto para o líder técnico (PA-6) |
| Migrações aplicadas e `*.Designer.cs` | Imutáveis por regra do repositório | manter |
| Literais de teste de `HABILITACAO` (fase e finalidade) | Acompanham PA-1 e PA-2 | manter |
| `HOMOLOGACAO_ROUTES` e a pasta `features/homologacao` do web | Rota da área de homologação, não código de vocabulário | manter |
| Outras fases citadas por constante (`INSCRICAO`, `SOLICITACAO_ISENCAO`, `AVALIACAO`) | Não mudam | manter |
| Texto de tela, páginas de erro e requisitos do portal | Mudança de tela é da uniplus-web#1446; o portal recebe o que a seção 3.11 lista como ponto para o portal | ponto para o portal |

## 9. Pontos em aberto para o líder técnico

Nenhum deles tem decisão neste documento. Onde há uma sugestão, ela é só uma opção para a decisão.

- **PA-1. `HABILITACAO` como código de fase: manter ou renomear.** Opção A: manter, porque o código tem um sentido só. Opção B: qualificar (por exemplo, pela matéria que a fase confere) como se fez com a homologação. Se B, os usos listados com "PA-1" nas seções 3 e 4 passam de manter a renomear, e a #2322 ou uma issue própria os executa.
- **PA-2. `HABILITACAO` como finalidade de formulário: manter ou renomear.** A finalidade é fechada em código e vai à rota, ao CHECK e ao envelope publicado. Independe de PA-1 só se a finalidade deixar de ser acoplada à fase de mesmo código (PA-3).
- **PA-3. Regra por código de fase já existente.** A fase que atende a finalidade de habilitação e a fase em que cada tipo de decisão é avaliado são reconhecidas por código de fase, ao contrário da isenção, que usa a capacidade configurada. Decidir se a #2322 troca o literal de homologação pelo critério da capacidade e se as demais (`HABILITACAO`, `HETEROIDENTIFICACAO`, `AVALIACAO_BIOPSICOSSOCIAL`, `CLASSIFICACAO`) seguem na #2338 ou numa issue própria.
- **PA-4. Tipo de ato.** (a) Confirmar que o tipo de ato é cadastro, e não vocabulário fechado em código (a issue diz o segundo; o código e a ADR-0103 dizem o primeiro). (b) Dizer se `RESULTADO_HOMOLOGACAO` e `HOMOLOGACAO_INSCRICOES` são a mesma matéria. (c) Dizer se `RESULTADO_HABILITACAO` e `HOMOLOGACAO_HABILITACAO` ficam como estão.
- **PA-5. Formulário de homologação.** A issue cita "formulário" entre os vocabulários em que "homologação" aparece, mas não há finalidade nem modelo com esse nome. Confirmar se é só descrição do cenário ou se há finalidade nova prevista.
- **PA-6. Emenda da ADR-0103** para trocar o exemplo `HOMOLOGACAO_ANALISE` por um código que exista.

- **PA-7. Conferência do `faseCodigo` na escrita do modelo de formulário.** Hoje o cadastro do modelo grava o código de fase do documento exigido sem conferi-lo contra o catálogo, e só a aplicação do modelo ao processo recusa o código inexistente. Decidir se a #2322, que reescreve esse JSON, passa a conferir na escrita ou se o comportamento atual fica.

## 10. Conferência deste mapa

- Nenhum código de produção foi alterado por esta issue: o único arquivo novo é este documento.
- Todo uso listado tem ação. Os testes têm uma ação por linha da seção 3.6; o web, uma por linha da seção 3.10.
- Para conferir que nenhum uso ficou de fora depois de executar a #2322, rode os comandos da seção 2 e confira que cada ocorrência de `HOMOLOGACAO` sozinho que sobrar está nas migrações aplicadas, nos `*.Designer.cs` ou numa linha deste mapa com ação "manter" e a justificativa (por exemplo, dado de teste de tipo de ato).
