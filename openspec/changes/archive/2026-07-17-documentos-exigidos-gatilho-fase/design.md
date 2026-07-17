## Context

O agregado `ProcessoSeletivo` (módulo Seleção) é configurado em rascunho e **congelado** na
publicação numa `VersaoConfiguracao` forense (append-only), cujo `ConfiguracaoCongelada` é o
envelope canônico versionado (ADR-0100/0109). As dependências desta change já estão na `main`
como código: rol de fatos + leitor (#846/#847), cronograma de fases (#851), quadro de vagas
(#848), avaliador de conformidade legal (#852/#853), envelope v2 (ADR-0109). O bloco
`documentosExigidos.exigencias` é hoje um stub `nao_construido`, e a variante de conformidade
`DocumentoObrigatorioParaModalidade` reprova por padrão "pela #554". A máquina de gatilho DNF
(`PredicadoDnf`) foi entregue **só para fatos escalares** — `MODALIDADE`/`CONDICAO_ATENDIMENTO`
(dinâmicos/multivalorados) foram deliberadamente delegados a esta change.

## Goals / Non-Goals

**Goals:**
- Configurar exigências documentais por gatilho e fase, congelá-las no envelope e resolver a
  correlação apresentação↔exigência por identidade — de forma auditável e imune pós-publicação.
- Estender o DNF para os fatos dinâmicos/multivalorados centrais do domínio.
- Manter cada estado mergeado fail-closed (nunca congelar versão que omita documentos).

**Non-Goals:**
- Runtime de upload/coleta de documentos e validação por fase (UNI-REQ-0027/0028/0062/0063).
- Módulo Ingresso (habilitação, matrícula).
- Motor que coleta apresentações e chama o resolvedor (só o resolvedor puro entra).

## Decisions

- **Entidade `DocumentoExigido` como filha de `ProcessoSeletivo`, `EntityBase` puro** (sem
  soft-delete). *Por quê:* o regime append-only/forense pertence à `VersaoConfiguracao`, não à
  configuração viva, que é mutável com auditoria de agregado. Alternativa (SoftDeletableEntity)
  rejeitada — contradiz o modelo da `main`.
- **Referência ao `TipoDocumento` por snapshot-copy (ADR-0061), não FK.** Bancos isolados por
  módulo tornam FK cross-banco inexequível; a exigência copia código/nome/categoria + `OrigemId`.
- **Gatilho reusa a forma DNF (`PredicadoDnf`) e estende a avaliação** para domínio dinâmico e
  cardinalidade multivalorada (`IGUAL`=pertinência, `EM`=interseção — ADR-0111). Alternativa
  (novo motor) rejeitada — a forma DNF já é contrato congelado.
- **`ReferenciaTemporalFatos` resolve-e-congela data concreta** no nível do processo. Admin
  escolhe a âncora; a publicação resolve para `DateOnly` e congela `dataReferenciaFatos` (forma
  normativa da ADR-0111). `DATA_SUBMISSAO` **não** entra na idade do candidato (fica só na idade
  do documento). *Por quê:* idade do candidato deve ser uniforme e reproduzível no certame;
  congelar a política e reler quebraria a forma da ADR e a auditabilidade. Sem default silencioso.
- **Bloco rico via `EnvelopeCodecV12` (schema 1.2), congelando o V11.** Mover `exigencias` de
  stub para objeto rico é mudança de forma (ADR-0109 D1); evoluir o V11 no lugar é proibido pelas
  guardas de codec e fitness. Golden fixture 1.2 + 3 canários.
- **Resolvedor puro `static`** sobre `{bloco, fatos, apresentações por exigencia_id}`, com erros
  nomeados (ADR-0076). Não recebe `TimeProvider`.
- **Guarda fail-closed transitória** nos 3 caminhos de versão (`Publicar`/`Retificar`/
  `FecharRetificacao`) desde o PR-a, removida no PR-e. *Por quê:* 1 PR = 1 task com merge
  sequencial cria janela em que publicar congelaria versão sem os documentos.
- **Sequência de entrega:** PR-a (núcleo + aplicabilidade + guarda) → PR-b (gatilho + extensão
  DNF + referência temporal) → PR-c (base legal) → PR-d (idade/formato/tamanho + guards de fase)
  → PR-e (bloco rico V12 + resolvedor + gate real + remover guarda).

## Risks / Trade-offs

- **Janela de congelamento parcial entre PRs** → guarda fail-closed com contraprova nos 3
  caminhos como DoD do PR-a.
- **Bump de schema 1.1→1.2 quebra golden fixtures** → novo V12 + fixture dedicada; V11 congelado;
  canários adversariais.
- **Fuso UTC vs `DateOnly`** (submit `01:30Z` cai no dia local anterior em UTC-03) → resolver por
  `America/Sao_Paulo` (`DateTimeOffsetExtensions` já existe) + contraprova na virada do dia.
- **Âncora de fase sem data** (`FaseCronograma.Inicio/Fim` anuláveis; fase `DELEGADA` sem janela)
  → publicação recusa `INICIO_FASE`/`FIM_FASE` sem o extremo escolhido.
- **Retificação que muda a política temporal** → atos anteriores preservam data/instante; novos
  recebem a nova; reavaliação nunca relê config viva nem relógio (ADR-0075).
- **Opt-in de codegen Wolverine ausente** → registrar `ITipoDocumentoReader` no mesmo commit do
  handler (os readers de fato/fase já estão registrados).

## Migration Plan

- Migrations EF Core (Postgres) por PR para as novas tabelas (exigência, condição de gatilho,
  base legal) — comando `--startup-project src/host/...Host` (CLAUDE.md; reconciliar o guia).
- Sem produção: migrations decididas pelo mérito; validar SQL não-trivial (CHECK/jsonpath) contra
  Postgres real antes.
- Rollback: cada PR é atômico e reversível; a guarda fail-closed garante que nenhum estado
  intermediário congela versão inconsistente.

## Open Questions

- Confirmar na modelagem a origem determinística exata de cada âncora temporal e o contrato JSON
  do V12 por variante (campos obrigatórios/proibidos, nulidade) antes do PR-b.
- Reconciliar a divergência documental do comando de migration (CLAUDE.md `Host` vs guia `API`).
- Corpos das issues #547/#548/#549 precisam ser reescritos ao modelo atual antes de implementar
  (Passo 0); criar tasks novas para PR-b/PR-d e a sub-issue Web (CA-14).
