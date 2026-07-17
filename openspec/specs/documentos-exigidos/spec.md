# documentos-exigidos Specification

## Purpose
Declarar quais documentos comprobatórios são exigidos de um candidato, de quem e em que fase do
cronograma — com aplicabilidade explícita (GERAL/CONDICIONAL), gatilho DNF tipado sobre fatos do
candidato, base legal 1:N, consequência de indeferimento coerente com a ação da vaga, grupo de
satisfação por documentos alternativos, e congelamento imutável no envelope canônico da
publicação (Story #554/UNI-REQ-0016).

## Requirements

### Requirement: Aplicabilidade explícita da exigência
Cada `DocumentoExigido` SHALL declarar `Aplicabilidade ∈ {GERAL, CONDICIONAL}`, nunca inferida
pela presença ou ausência de condições. `GERAL` SHALL NOT conviver com condição de gatilho viva.
Uma exigência `CONDICIONAL` sem nenhuma condição que **determina resultado** (obrigatória OU com
consequência) SHALL bloquear a publicação.

#### Scenario: GERAL com condição é recusada
- **WHEN** o admin define uma exigência `GERAL` com ao menos uma condição de gatilho
- **THEN** o comando é recusado com erro de domínio nomeado

#### Scenario: CONDICIONAL vazia que determina resultado bloqueia publicação
- **WHEN** existe uma exigência `CONDICIONAL` obrigatória (ou com consequência) sem condições e o processo é publicado
- **THEN** a publicação falha com erro nomeado indicando exigência que não se aplica a ninguém

### Requirement: Gatilho DNF tipado sobre fatos do candidato
O gatilho SHALL ser um predicado em forma normal disjuntiva sobre o vocabulário fechado de fatos
(ADR-0111). O sistema SHALL recusar fato fora do vocabulário, operador incompatível com o domínio
do fato, e valor fora do domínio. Um fato não resolvível em runtime SHALL tornar a condição
conservadoramente falsa.

#### Scenario: Fato fora do vocabulário é recusado
- **WHEN** o admin define uma condição citando um código de fato inexistente
- **THEN** o comando é recusado com erro nomeado

#### Scenario: Fato ausente em runtime torna a condição falsa
- **WHEN** o resolvedor avalia uma condição cujo fato do candidato não foi resolvido
- **THEN** a condição resolve como falsa, sem exigir o documento

### Requirement: Avaliação de fatos dinâmicos e multivalorados
A avaliação de gatilho SHALL suportar os fatos de escopo-processo `MODALIDADE` e
`CONDICAO_ATENDIMENTO`, multivalorados, validando o valor contra as modalidades/condições
ofertadas pelo processo. `IGUAL X` SHALL significar pertinência de `X` no conjunto do candidato;
`EM [..]` SHALL significar interseção não vazia.

#### Scenario: IGUAL como pertinência em fato multivalorado
- **WHEN** o candidato tem `MODALIDADE = {LB_PPI, AC}` e a condição é `MODALIDADE IGUAL LB_PPI`
- **THEN** a condição resolve verdadeira

#### Scenario: EM como interseção
- **WHEN** o candidato tem `MODALIDADE = {AC}` e a condição é `MODALIDADE EM [LB_PPI, LB_Q]`
- **THEN** a condição resolve falsa

### Requirement: Integridade referencial do gatilho
Uma condição SHALL referenciar apenas modalidade ou condição de atendimento ofertada pelo mesmo
processo seletivo.

#### Scenario: Modalidade de outro processo é recusada
- **WHEN** a condição referencia uma modalidade não ofertada pelo processo
- **THEN** o comando é recusado com erro nomeado

### Requirement: Fase como vínculo de primeira classe
`ExigidoNaFase` SHALL ser obrigatório e apontar para uma fase viva do cronograma do próprio
processo. O sistema SHALL recusar remover uma fase referenciada por exigência viva e retirar
`PermiteComplementacao` de uma fase com exigência de consequência `PENDENCIA_REENVIO`.

#### Scenario: Remoção de fase referenciada é recusada
- **WHEN** o admin tenta remover uma fase do cronograma referenciada por uma exigência viva
- **THEN** a operação é recusada com erro nomeado

#### Scenario: PENDENCIA_REENVIO exige fase com complementação
- **WHEN** o admin define consequência `PENDENCIA_REENVIO` numa fase sem `PermiteComplementacao`
- **THEN** o comando é recusado com erro nomeado

### Requirement: Consequência de indeferimento coerente
A consequência SHALL pertencer a `{ELIMINA, RECLASSIFICA_AC, REMOVE_VANTAGEM, PENDENCIA_REENVIO}`
e SHALL ser coerente com a ação da vaga, lida de `ModalidadeSelecionada.AcaoQuandoIndeferido` sem
campo duplicado. `REMOVE_VANTAGEM` SHALL exigir vantagem viva referenciada.

#### Scenario: REMOVE_VANTAGEM sem vantagem é recusada
- **WHEN** o admin define `REMOVE_VANTAGEM` sem vantagem viva (ex.: bônus regional) no processo
- **THEN** o comando é recusado com erro nomeado

#### Scenario: Reavaliação após mudança de gatilho
- **WHEN** o gatilho de uma exigência com consequência é alterado
- **THEN** a coerência consequência↔ação da vaga é reavaliada

### Requirement: Base legal 1:N com gate de publicação
Cada exigência SHALL suportar múltiplas bases legais `{Referencia, Abrangencia ∈ {FEDERAL,
ESTADUAL, MUNICIPAL, INTERNA_NORMA, INTERNA_EDITAL}, Status ∈ {PENDENTE, RESOLVIDO}}`. A
publicação SHALL exigir ≥1 base `RESOLVIDO` para toda exigência que determina resultado;
`INTERNA_EDITAL` conta sozinha; apenas bases `RESOLVIDO` congelam.

#### Scenario: Exigência que determina resultado sem base RESOLVIDO bloqueia publicação
- **WHEN** uma exigência obrigatória tem apenas bases `PENDENTE` e o processo é publicado
- **THEN** a publicação falha com erro nomeado

### Requirement: Grupo de satisfação de documentos alternativos
Um `GrupoSatisfacao` SHALL ser escopado por processo e fase; documentos do mesmo grupo SHALL ser
satisfeitos por uma única apresentação; consequências distintas dentro do grupo SHALL ser
publicáveis.

#### Scenario: Uma apresentação satisfaz o grupo
- **WHEN** duas exigências no mesmo grupo/fase existem e o candidato apresenta uma delas
- **THEN** o resolvedor considera o grupo satisfeito, com uma única pendência antes disso

### Requirement: Idade de emissão, formato e tamanho congelados na exigência
A regra de idade máxima de emissão SHALL ser tudo-nulo OU completa (`Valor`, `Unidade`,
`ReferenciaTipo`, e a fase quando ancorada a fase). Formato permitido e tamanho máximo SHALL ser
congelados na exigência (o `TipoDocumento` permanece classificatório). A idade de emissão SHALL
ser aviso, não bloqueio de presença.

#### Scenario: Idade de emissão parcial é recusada
- **WHEN** o admin informa `Valor` sem `Unidade` na regra de idade de emissão
- **THEN** o comando é recusado com erro nomeado

### Requirement: Referência temporal de fatos resolvida e congelada
O processo SHALL configurar uma `ReferenciaTemporalFatos` `{ReferenciaTipo ∈ {FIM_INSCRICAO,
INICIO_FASE, FIM_FASE, DATA_ESPECIFICA}, ReferenciaData?}`. Na publicação, a âncora SHALL ser
resolvida para uma `DateOnly` concreta e congelada como `dataReferenciaFatos` (fuso
`America/Sao_Paulo`). Não SHALL haver fallback silencioso: um gatilho por `FAIXA_ETARIA` sem
referência resolvível SHALL bloquear a publicação.

#### Scenario: FAIXA_ETARIA sem referência resolvível bloqueia publicação
- **WHEN** existe gatilho por `FAIXA_ETARIA` e a referência é `INICIO_FASE` de uma fase sem data
- **THEN** a publicação falha com erro nomeado

#### Scenario: DATA_ESPECIFICA congela a data informada
- **WHEN** a referência é `DATA_ESPECIFICA` com data informada e o processo é publicado
- **THEN** o envelope congela `dataReferenciaFatos` igual à data informada

### Requirement: Identidade estável e correlação por exigencia_id
O snapshot SHALL congelar `exigencia_id` estável por exigência; a correlação apresentação↔exigência
SHALL se dar por esse identificador, não pelo tipo de documento, suportando duas ou mais exigências
do mesmo tipo.

#### Scenario: Dois documentos do mesmo tipo resolvem por identidade
- **WHEN** existem duas exigências do mesmo `TipoDocumento` com gatilhos distintos
- **THEN** cada apresentação correlaciona à exigência por `exigencia_id`, de forma determinística

### Requirement: Bloco rico congelado no envelope canônico
O bloco `documentosExigidos.exigencias` SHALL deixar de ser stub e passar a objeto rico congelado
via `EnvelopeCodecV12` (schema 1.1→1.2, encoder V11 congelado). O congelamento SHALL ter golden
fixture byte a byte e paridade bidirecional viva↔congelada, com contraprova de órfã viva e órfã
congelada.

#### Scenario: Round-trip byte a byte
- **WHEN** o bloco de exigências é codificado e decodificado pelo V12
- **THEN** os bytes canônicos são idênticos aos da golden fixture

### Requirement: Imunidade pós-publicação
Após a publicação, alterar o cadastro de `TipoDocumento` ou a configuração viva do processo SHALL
NOT alterar o hash nem o resultado congelado. O resolvedor SHALL ler o snapshot congelado, não a
configuração viva. A ausência de versão vigente SHALL produzir erro nomeado.

#### Scenario: Alterar TipoDocumento não muda o hash
- **WHEN** um processo já publicado tem seu `TipoDocumento` de origem editado no cadastro
- **THEN** o hash da versão congelada permanece inalterado

### Requirement: Resolvedor puro por identidade
O resolvedor SHALL ser uma função `static` pura sobre `{bloco congelado, fatos resolvidos,
apresentações por exigencia_id}`, retornando saída tipada e erros nomeados para snapshot ausente,
estrutural ou semanticamente inválido; SHALL NOT ler o relógio nem a configuração viva.

#### Scenario: Snapshot inválido produz erro nomeado
- **WHEN** o resolvedor recebe um snapshot sem o bloco de exigências
- **THEN** retorna erro nomeado, nunca resultado vazio silencioso

### Requirement: Gate de conformidade em todos os caminhos de versão
O gate de conformidade documental SHALL rodar em `Publicar`, `Retificar` e `FecharRetificacao`,
substituindo a reprovação-stub `DocumentoObrigatorioParaModalidade`.

#### Scenario: Retificação também aplica o gate
- **WHEN** uma retificação viola a coerência das exigências documentais
- **THEN** a retificação é recusada pelo mesmo gate da publicação

### Requirement: Guarda fail-closed durante a construção incremental
Enquanto o bloco `exigencias` não for materializado no envelope, se existir exigência configurada,
`Publicar`, `Retificar` e `FecharRetificacao` SHALL falhar com erro nomeado, impedindo congelar
uma versão que omita documentos configurados.

#### Scenario: Publicar com exigência configurada e bloco ainda stub é recusado
- **WHEN** existe exigência configurada e o bloco `exigencias` ainda é stub no envelope
- **THEN** a publicação falha com erro nomeado até o bloco ser materializado
