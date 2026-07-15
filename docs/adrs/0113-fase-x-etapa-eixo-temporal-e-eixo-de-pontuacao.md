---
status: "accepted"
date: "2026-07-15"
decision-makers:
  - "Tech Lead"
consulted:
  - "CEPS (dono do processo)"
informed:
  - "Equipe Seleção"
  - "Equipe Configuração"
---

# ADR-0113: Fase × Etapa — eixo temporal e eixo de pontuação do processo seletivo

## Contexto e enunciado do problema

O envelope canônico da publicação (ADR-0109) tem 17 blocos; o bloco
`cronogramaFases` é hoje o literal `{"status":"nao_construido"}`
(`SnapshotPublicacaoCanonicalizer.cs:88`). O certame publica sem calendário.

O cadastro de referência já existe e está pronto, sem consumidor:
`Unifesspa.UniPlus.Configuracao.Domain.Entities.FaseCanonica` nomeia, em domínio
fechado (`FaseCanonicaCatalogo.Codigos`), as quatorze fases do ciclo de vida de um
processo seletivo — inscrição, homologação, avaliação, recursos, resultado,
matrícula… — com `DonoTipico` e `AgrupaEtapas`/`PermiteComplementacao`. O que
falta é o consumidor: `FaseCronograma`, filho do agregado `ProcessoSeletivo`, que
declara o cronograma real de um certame a partir desse vocabulário.

Antes de construir esse consumidor, uma distinção de domínio precisava ser
declarada por escrito, porque o código já a insinuava sem nomeá-la:
`EtapaProcesso` (peso, caráter, nota mínima — o que pontua a nota final) e a fase
do cronograma (janela, ordem, dono institucional — o que organiza o tempo do
certame) são eixos diferentes, mas nada impedia confundi-los em código futuro. A
pergunta era: o cadastro de fases canônicas carrega os atributos certos para
sustentar essa distinção, e o relacionamento entre fases (o que precede o quê) é
regra de código ou dado de cadastro?

## Drivers da decisão

- O gate de publicação não pode ramificar por rótulo de código de fase, dono
  institucional ou tipo de processo (proibição já vigente no projeto,
  `SelecaoSemRamificacaoPorTipoProcesso`).
- Um processo sem prova (SiSU) precisa publicar sem etapa nem fase de avaliação —
  a distinção Fase × Etapa não pode tornar isso impossível.
- A precedência entre fases (ex.: inscrição antes de homologação) muda por
  decisão institucional, não por release do sistema — não pode ser `if` no gate.
- RN08 (congelamento de parâmetros por edital): o bloco publicado precisa ser
  autossuficiente, sem releitura do cadastro vivo em runtime para decidir gates.

## Opções consideradas

- **Fundir Fase e Etapa num único conceito** (a fase pontuada é a mesma fase do
  cronograma), reaproveitando `EtapaProcesso` para carregar também janela e
  ordem.
- **Dois eixos distintos, com o cadastro de fases carregando apenas o eixo
  temporal** — a fase organiza o tempo (janela, ordem, dono, produção de ato); a
  etapa organiza a nota (peso, caráter); a fase de avaliação apenas *agrupa*
  etapas via um sinalizador.
- **Precedência entre fases como código** (switch/if no gate por par de códigos)
  versus **precedência como dado de cadastro** lido por um reader.

## Resultado da decisão

**Escolhida:** dois eixos distintos — **Fase** é o eixo temporal
(`FaseCronograma`, Módulo Seleção: janela, ordem, dono institucional, origem da
data, ato produzido, regra de recurso); **Etapa** é o eixo de pontuação
(`EtapaProcesso`: peso, caráter, nota mínima). A ligação entre os dois é
bicondicional e vive só no sinalizador `AgrupaEtapas` do cadastro: a fase que
agrupa etapas existe **se e somente se** o processo tem ao menos uma etapa
pontuada — nos dois sentidos, fase de avaliação sem etapa é recusada e etapa sem
fase de avaliação reprova a publicação. Cardinalidades diferentes por design:
Fase é `1..*` (piso derivado da origem dos candidatos, nunca do tipo de
processo); Etapa é `0..*` (processo sem prova não tem etapa — o SiSU publica
liso).

O cadastro `FaseCanonica` (Configuração, ADR-0056) ganha quatro atributos
declarados, na mesma família de `AgrupaEtapas`/`PermiteComplementacao` que já
existiam: `ProduzResultado` (bool — havendo vagas, o cronograma precisa de ao
menos uma fase que produza resultado), `ResultadoDefinitivo` (bool, implica
`ProduzResultado` — não cabe recurso contra resultado definitivo),
`ColetaInscricao` (bool — decide o piso mínimo quando a origem dos candidatos é
inscrição própria) e `OrigemData` (`PROPRIA` | `DELEGADA` — decide se a janela da
fase é obrigatória ou opcional). Nenhum desses atributos é seed: `FaseCanonica`
permanece 100% CRUD-administrado, e os valores reais das quatorze fases são um
ato operacional pós-deploy via endpoint admin — a mesma política já vigente para
`DonoTipico`/`AgrupaEtapas`/`PermiteComplementacao`.

A precedência entre fases é **dado de cadastro, não código**: `PrecedenciaFase`
(Configuração, novo cadastro) declara arestas `(AntecessoraCodigo,
SucessoraCodigo, PermiteSobreposicao)`, seed-governado com as seis arestas
estruturais do ciclo — `INSCRICAO→HOMOLOGACAO`,
`RESULTADO_PRELIMINAR→RECURSOS`, `RECURSOS→RESULTADO_FINAL`,
`RESULTADO_FINAL→HABILITACAO`, `HABILITACAO→MATRICULA`,
`HETEROIDENTIFICACAO→HOMOLOGACAO_RESULTADO_FINAL` — e um endpoint admin para
acrescentar novas arestas sem deploy. Três guardas protegem o grafo na escrita
(`PrecedenciaFase.Criar`, recebendo o conjunto de arestas vivas como parâmetro —
ADR-0042, domínio não navega/consulta): self-loop, aresta duplicada e qualquer
aresta que feche um ciclo no grafo vigente. A dependência entre fases é
condicional: vale onde as duas fases coexistem no cronograma de um processo — a
ausência de uma fase não é violação, o que preserva a publicação de um
cronograma mínimo (ex.: importação → classificação → habilitação).

**Revisão declarada do `FaseCanonicaSnapshot`.** O snapshot que o Módulo Seleção
congela por valor (ADR-0061) documentava, até esta ADR, que `AgrupaEtapas` "não
é congelado — é atributo do cadastro vivo". Essa decisão é revista: como o gate
de avaliação × etapa é bicondicional e o bloco `cronogramaFases` da versão
publicada precisa ser autossuficiente (RN08), ler o cadastro vivo em runtime para
decidir esse gate violaria o congelamento. `AgrupaEtapas` passa a ser congelado
como qualquer outro atributo do snapshot, que amplia de três para nove campos:
`(OrigemId, Codigo, DonoTipico, AgrupaEtapas, PermiteComplementacao,
ProduzResultado, ResultadoDefinitivo, ColetaInscricao, OrigemData)`.

A substituição da regra `RECURSO-MULTI-INSTANCIA` por
`RECURSO-PRAZO-ANCORADO-EM-ATO` no catálogo `rol_de_regras` é decisão e escopo da
ADR-0112/#854, não desta ADR — aqui apenas se registra que `FaseCronograma`
referencia a regra corrigida por símbolo (`RegraPrazoRecursoCodigo.AncoradoEmAto`),
nunca por literal solto.

## Consequências

### Positivas

- O gate de publicação nunca precisa saber o nome de uma fase, de um dono
  institucional ou de um tipo de processo — toda decisão lê um atributo
  declarado, seja do cadastro (`FaseCanonica`, `PrecedenciaFase`) seja do
  processo (`OrigemCandidatos`).
- Um processo sem prova (SiSU) e um processo com prova (PS regular) publicam pela
  mesma máquina de regras, sem ramo especial — a prova de indistinguibilidade é
  testável (mesma configuração, tipos diferentes, mesmo veredicto).
- Acrescentar uma aresta de precedência ou popular um atributo novo de fase é
  edição de cadastro, não deploy — CEPS/CRCA operam sem depender de release.

### Negativas

- Dois cadastros novos em Configuração (`PrecedenciaFase`) e quatro colunas novas
  em `fase_canonica` aumentam a superfície de configuração que precisa ser
  populada corretamente antes de qualquer processo real publicar — erro de
  cadastro é possível (mitigado por CHECKs de banco e pela factory de domínio).
- A revisão do `FaseCanonicaSnapshot` (passar a congelar `AgrupaEtapas`) é uma
  mudança de contrato do snapshot antes de qualquer consumidor real existir —
  aceitável precisamente porque, confirmado nesta story, nenhum ambiente tem
  `FaseCanonica` nem `VersaoConfiguracao` populadas com o formato antigo.

### Neutras

- `tipo_janela` (prazo-limite × intervalo × data única × sem data) e o
  des-parqueamento de `IMPORTACAO`/`CONFIRMACAO_INTERESSE` no catálogo de
  quatorze fases ficam fora de escopo desta ADR — nenhum critério de aceite dela
  depende, e o par `Inicio`/`Fim` nullable já cobre o MVP via `OrigemData`.

## Confirmação

- Testes de domínio: `FaseCanonicaTests.Criar_ResultadoDefinitivoSemProduzirResultado_Falha`
  (CA-04) e a suíte `PrecedenciaFaseTests` (self-loop, aresta duplicada, ciclo
  direto e transitivo).
- Teste de integração `PrecedenciaFasePersistenceTests.Seed_MaterializaArestasCanonicas`
  (CA-05): o seed materializa as seis arestas e o `IPrecedenciaFaseReader` as
  devolve.
- CHECKs de banco espelhando as guardas de domínio (defesa em profundidade):
  `ck_fase_canonica_resultado_definitivo`, `ck_fase_canonica_origem_data`,
  `ck_precedencia_fase_sem_self_loop`, `ck_precedencia_fase_antecessora_canonica`,
  `ck_precedencia_fase_sucessora_canonica`.
- O consumo do grafo pelo gate de `FaseCronograma` (bicondicional Fase × Etapa,
  piso mínimo por `OrigemCandidatos`, precedência sem ramificação por tipo) é
  escopo de PR posterior sobre o Módulo Seleção; esta ADR fixa o contrato de
  dados que ele consome.

## Prós e contras das opções

### Fundir Fase e Etapa num único conceito

- Bom, porque evitaria um segundo agregado filho de `ProcessoSeletivo`.
- Ruim, porque a cardinalidade diverge por natureza (`1..*` vs `0..*`) e um
  processo sem prova precisaria de um caso especial para não ter "etapa" — a
  fusão reintroduziria a ramificação por tipo que o projeto proíbe.

### Dois eixos distintos, cadastro de fases só com o eixo temporal

- Bom, porque cada eixo evolui independente e a bicondicional fica um invariante
  simples e testável nos dois sentidos.
- Ruim, porque exige manter dois agregados filhos sincronizados por um
  sinalizador (`AgrupaEtapas`) em vez de um campo direto — mitigado pelos guard
  rails já existentes (CA-14/CA-15 da story #851).

### Precedência entre fases como código

- Bom, porque seria mais simples de ler numa primeira leitura do gate.
- Ruim, porque qualquer novo par de precedência exigiria deploy, e o histórico do
  projeto já mostrou (ADR-0056, ADR-0111) que vocabulário institucional pertence
  a cadastro, não a `switch`.

## Mais informações

- ADR-0056 — Módulo Configuração e read-side cross-módulo via `IXxxReader`.
- ADR-0061 — Referência cross-módulo via snapshot-copy (não FK).
- ADR-0042 — Repository + UoW obrigatórios; domínio não navega/consulta.
- ADR-0100/ADR-0109 — Canonicalização e envelope do congelamento (RN08).
- ADR-0111/ADR-0112 — Precedentes de catálogo seed-governado em Configuração.
- RN08 — Congelamento de parâmetros por edital.
- UNI-REQ-0064 — Fases canônicas e tipos de banca.
- Issue #851 — Cronograma de Fases do Processo Seletivo.
