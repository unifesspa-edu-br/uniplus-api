## Why

O Processo Seletivo precisa declarar **quais documentos comprobatórios** são exigidos, **de quem**
e **em que fase** — nível de ensino na inscrição, comprovante de renda só na habilitação,
certidão de reservista só para `SEXO=MASCULINO` e `FAIXA_ETARIA≥18`, laudo só para quem tem
`CONDICAO_ATENDIMENTO=PCD`. Hoje a entidade não existe; o bloco `documentosExigidos.exigencias` do
envelope canônico é um stub `nao_construido` e o avaliador de conformidade reprova, por padrão
conservador, a exigência documental "bloqueada pela #554". Sem isso, um edital não consegue
congelar suas exigências e a validação documental em runtime não tem contra o que comparar.

## What Changes

- Nova entidade de configuração `DocumentoExigido` (filha de `ProcessoSeletivo`), com fase
  (vínculo de primeira classe), aplicabilidade explícita `GERAL`/`CONDICIONAL`, gatilho DNF sobre
  fatos do candidato, base legal 1:N, consequência de indeferimento, grupo de satisfação, idade
  máxima de emissão, formato e tamanho — editada por `PUT {id}/documentos-exigidos` idempotente
  com substituição integral.
- Extensão da avaliação de gatilho DNF para os fatos **dinâmicos e multivalorados**
  `MODALIDADE` e `CONDICAO_ATENDIMENTO` (hoje o DNF só resolve escalares): `IGUAL`=pertinência,
  `EM`=interseção (ADR-0111).
- `ReferenciaTemporalFatos` configurável no nível do processo para resolver fatos temporais
  (`FAIXA_ETARIA`): na publicação, a âncora escolhida (`FIM_INSCRICAO`/`INICIO_FASE`/`FIM_FASE`/
  `DATA_ESPECIFICA`) é **resolvida para uma data concreta e congelada** (`dataReferenciaFatos`),
  sem fallback silencioso.
- **BREAKING (forma do envelope):** o bloco `documentosExigidos.exigencias` deixa de ser stub e
  passa a objeto rico congelado → novo `EnvelopeCodecV12`, `schema_version` 1.1→1.2, encoder V11
  congelado, golden fixture 1.2 + canários.
- Gate de conformidade real substituindo o stub `DocumentoObrigatorioParaModalidade`, aplicado em
  `Publicar`, `Retificar` **e** `FecharRetificacao`; guarda transitória fail-closed enquanto o
  bloco não é materializado.
- Resolvedor puro (função `static`) que correlaciona apresentação↔exigência por `exigencia_id`
  sobre o bloco congelado + fatos, com erros nomeados (não runtime de coleta de documentos).

## Capabilities

### New Capabilities
- `documentos-exigidos`: configuração, validação de publicação, congelamento no envelope canônico
  e resolvedor puro das exigências documentais por gatilho e fase — incluindo aplicabilidade
  GERAL/CONDICIONAL, gatilho DNF (com extensão dinâmica/multivalorada e referência temporal de
  fatos), base legal 1:N, consequência, grupo de satisfação, idade de emissão/formato/tamanho,
  imunidade pós-publicação e a guarda fail-closed durante a construção incremental.

### Modified Capabilities
<!-- Não há specs OpenSpec pré-existentes em openspec/specs/; todo o contrato é greenfield nesta
     ferramenta. As dependências de #846/#847/#848/#851/#852/#853 já estão na main como código,
     não como specs OpenSpec, portanto não há capability OpenSpec a modificar aqui. -->

## Impact

- **Domínio Seleção:** `ProcessoSeletivo` (nova coleção + `DefinirDocumentosExigidos`),
  `DocumentoExigido`/`CondicaoGatilho`/`DocumentoExigidoBaseLegal`, VOs `PredicadoDnf` (estendido),
  `ReferenciaTemporalFatos`, `IdadeMaximaEmissao`; `AvaliadorConformidadeLegal` (variante real).
- **Envelope/congelamento:** `EnvelopeCodecV12`, `SnapshotPublicacaoCanonicalizer`,
  `RegistroCodecsEnvelope`, golden fixtures; `VersaoConfiguracao` congela o novo bloco.
- **Persistência:** migrations EF Core (Postgres) para as novas tabelas; snapshot-copy de
  `TipoDocumento` (ADR-0061), sem FK cross-banco.
- **Readers cross-módulo:** consumo de `ITipoDocumentoReader`, `IFatoCandidatoReader`,
  `IFaseCanonicaReader` (opt-in de codegen Wolverine só para `ITipoDocumentoReader`).
- **Frontend (`uniplus-web`):** tela admin de documentos exigidos (WCAG 2.1 AA / e-MAG 3.1) —
  sub-issue cross-repo.
- **Fora de escopo:** runtime de upload/coleta e validação por fase (UNI-REQ-0027/0028/0062/0063,
  Ingresso).
- **ADRs:** 0071, 0072, 0074, 0070, 0075, 0076, 0100, 0104, 0109, 0111, 0058, 0061, 0056, 0042,
  0063, 0068 (proposed), 0097, 0098.
