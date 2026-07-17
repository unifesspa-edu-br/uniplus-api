<!--
Rastreabilidade e fluxo de PR (repo: unifesspa-edu-br/uniplus-api · Story-pai: #554 UNI-REQ-0016)

Mapeamento PR → issue:
  PR-a → #547 (UNI-REQ-0057)   | PR-c → #549 (UNI-REQ-0059)   | PR-e → #548 (UNI-REQ-0058)
  PR-b → #892 (criada em 1.4)  | PR-d → #893 (criada em 1.5)  | CA-14 → uniplus-web#464 (1.6)

Fluxo obrigatório por PR (ver CLAUDE.md do repo + docs/guia-commits-e-integracao.md):
  - 1 PR = 1 issue; branch `feature/{issue}-{slug}` a partir da main atualizada.
  - Merge SEQUENCIAL a→b→c→d→e (cada PR rebaseia da main após o anterior entrar).
  - Cada estado mergeado é fail-closed (guarda em PR-a..PR-d; removida no PR-e).
  - Commits: conventional commits pt-BR, 3ª pessoa; sem Co-Authored-By; sem --no-verify.
  - Corpo do PR: `Closes #N` (FORA de backticks p/ fechar a issue) + referência à Story #554; sem rodapé de IA.
  - Antes do PR: `dotnet build/test UniPlus.slnx` + `/smoke-crud` + revisão Codex local pré-commit.
  - Depois do PR: `/review-pr` (zero tolerância) + aplicar feedback Codex até zero pendências + resolver review threads antes do merge (rebase merge = histórico linear; consolidar commits antes).
-->

## 1. Passo 0 — Rastreabilidade e pré-requisitos (antes de qualquer PR)

- [x] 1.1 Reescrever o corpo da issue #547 (PR-a) ao modelo atual: EntityBase puro, PUT integral, guarda fail-closed, parent #554 (usar o rascunho do Apêndice do plano)
- [x] 1.2 Reescrever o corpo da issue #548 (PR-e): remover Edital/SnapshotPublicacao/entidades de runtime; re-parentar de #555 para #554 (o vínculo de sub-issue no GitHub já apontava para #554; corpo alinhado)
- [x] 1.3 Reescrever o corpo da issue #549 (PR-c): remover SoftDeletableEntity/comandos granulares; EntityBase puro
- [x] 1.4 Criar task backend nova (PR-b — gatilho DNF + extensão dinâmica + referência temporal) como sub-issue de #554 → **#892**
- [x] 1.5 Criar task backend nova (PR-d — idade de emissão + formato + tamanho + guards de fase) como sub-issue de #554 → **#893**
- [x] 1.6 Criar sub-issue cross-repo em unifesspa-edu-br/uniplus-web para CA-14 (tela admin, WCAG 2.1 AA / e-MAG 3.1) como sub-issue de #554 → **uniplus-web#464**
- [ ] 1.7 Corrigir na governança o parent_id de UNI-REQ-0058 no requisito curado (0019 → 0016) — fora de PR de Seleção
- [x] 1.8 Reconciliar a divergência do comando de migration (CLAUDE.md Host vs guia API) e alinhar a doc — `docs/guia-banco-de-dados.md` atualizado para `--startup-project src/host/Unifesspa.UniPlus.Host` (ADR-0097), com exceção documentada para Geo/Portal

## 2. PR-a — Núcleo `DocumentoExigido` + aplicabilidade + guarda fail-closed ✅ MERGEADO

**Issue:** #547 (UNI-REQ-0057) · **Branch:** `feature/547-documento-exigido-nucleo` · **PR:** #895 (`Closes #547`) · **Merge:** 8919239c (2026-07-17)

- [x] 2.1 Modelar `DocumentoExigido` (EntityBase puro) com fase NOT NULL, snapshot-copy de TipoDocumento, aplicabilidade, obrigatório, consequência?, grupo de satisfação
- [x] 2.2 Método `ProcessoSeletivo.DefinirDocumentosExigidos` com substituição integral e MutacaoBloqueada
- [x] 2.3 Comando/handler/validator `DefinirDocumentosExigidosCommand` + `PUT {id}/documentos-exigidos` idempotente; erros → ProblemDetails
- [x] 2.4 Opt-in de codegen Wolverine para `ITipoDocumentoReader` no mesmo commit
- [x] 2.5 Regra CA-01 (GERAL não convive com condição; CONDICIONAL vazia que determina resultado reprova publicação)
- [x] 2.6 Guarda fail-closed nos 3 caminhos (Publicar/Retificar/FecharRetificacao) enquanto o bloco é stub
- [x] 2.7 Migration EF Core + Configuration da entidade
- [x] 2.8 Testes: aplicabilidade, guarda fail-closed nos 3 caminhos (falham sem o fix)
- [x] 2.9 Gates locais + revisão Codex (4 rodadas — eager-load, FK real, projeção agregada, guarda de fase referenciada); PR #895 aprovado e mergeado (rebase)

> Achados extras da revisão automatizada (Codex), corrigidos além do escopo original: eager-load de `DocumentosExigidos` no repositório (sem ele, B-01/CA-01 falhavam abertas); FK real `Restrict` para `fases_cronograma` + guarda de domínio `FaseCronograma.ReferenciadaPorExigenciaViva` (a reconciliação completa por chave estável fica para a PR-d); `DocumentosExigidos` exposto no GET agregado (`ProcessoSeletivoDto`); token de wire da aplicabilidade no GET (`GERAL`/`CONDICIONAL`, não `Geral`/`Condicional`); restauração de `DocumentosExigidos` ao descartar retificação; maxlength do snapshot de nome do tipo de documento alinhado à origem (200).

## 3. PR-b — `CondicaoGatilho` DNF + extensão dinâmica/multivalorada + referência temporal ✅ MERGEADO

**Issue:** #892 · **Branch:** `feature/892-gatilho-dnf-fatos` · **PR:** #896 (`Closes #892`) · **Merge:** 25664872 (2026-07-17)

- [x] 3.1 Modelar `CondicaoGatilho` sobre `PredicadoDnf` com integridade referencial (CA-03)
- [x] 3.2 Estender validação para domínio dinâmico (modalidades/condições ofertadas pelo processo)
- [x] 3.3 Estender avaliação para cardinalidade multivalorada: `IGUAL`=pertinência, `EM`=interseção (ADR-0111)
- [x] 3.4 VO `ReferenciaTemporalFatos` (nível processo) + validação de publicação (sem fallback silencioso)
- [x] 3.5 Definir e documentar o contrato JSON V12 por variante da referência (campos obrigatórios/proibidos)
- [x] 3.6 Testes: IGUAL/EM escalar e multivalorado; fato fora do vocabulário recusa; fato ausente → falso
- [x] 3.7 Gates locais + revisão Codex (5 rodadas — restauração no descarte, validação de campos soltos na remoção, projeção de Condicoes/ReferenciaTemporalFatos no GET, guard CA-03 de referência dinâmica viva, registro dos novos erros); PR #896 aprovado e mergeado (rebase)

> Achados extras da revisão automatizada (Codex), corrigidos além do escopo original: `AplicarGrafo` não zerava `ReferenciaTemporalFatos` ao descartar sessão editorial (mesmo raciocínio do `_documentosExigidos.Clear()` da PR-a); `DefinirReferenciaTemporalFatosCommandValidator` aceitava `Data`/`FaseId` soltos quando `Tipo` era nulo (remoção); GET agregado não expunha `DocumentoExigido.Condicoes` nem `ProcessoSeletivo.ReferenciaTemporalFatos` (quebrava round-trip GET→PUT); CA-03 só validava na escrita — `DefinirDistribuicaoVagas`/`DefinirOfertaAtendimento` podiam remover um código de MODALIDADE/CONDICAO_ATENDIMENTO referenciado por gatilho vivo sem revalidar (novo guard preciso, por código, não por Guid); os dois novos `DomainError` do guard não estavam registrados em `SelecaoDomainErrorRegistration` (pego pelo ArchTest F1 já existente).

## 4. PR-c — `DocumentoExigidoBaseLegal` 1:N + gate de publicação

**Issue:** #549 (UNI-REQ-0059) · **Branch:** `feature/549-base-legal-exigencia` · **PR:** `Closes #549` + ref. #554

- [x] 4.1 Modelar `DocumentoExigidoBaseLegal` (EntityBase puro) 1:N, editada pelo PUT integral
- [x] 4.2 Gate de publicação: ≥1 base RESOLVIDO para quem determina resultado; INTERNA_EDITAL sozinha; só RESOLVIDO congela
- [x] 4.3 Migration EF Core + Configuration
- [x] 4.4 Testes: só-PENDENTE bloqueia; rebaixar/remover a única RESOLVIDO é apanhado na publicação
- [ ] 4.5 Gates locais + revisão Codex; abrir PR `Closes #549`; `/review-pr` até zero pendências; merge sequencial

## 5. PR-d — Idade de emissão + formato + tamanho + guards de fase

**Issue:** #893 · **Branch:** `feature/893-idade-formato-tamanho` · **PR:** `Closes #893` + ref. #554

- [ ] 5.1 VO `IdadeMaximaEmissao` (tudo-nulo OU completo; âncoras de fase exigem fase viva com extremo não-nulo; DATA_SUBMISSAO válido aqui)
- [ ] 5.2 `FormatoPermitido` + `TamanhoMaximoBytes` congelados na exigência (TipoDocumento classificatório)
- [ ] 5.3 Guards backward de fase (CA-04): recusar remoção de fase referenciada e retirada de PermiteComplementacao
- [ ] 5.4 `TimeProvider` injetado (convenção de código; ADR-0068 proposed)
- [ ] 5.5 Testes: idade parcial recusada; imutabilidade de formato/tamanho por chamada; guards de fase
- [ ] 5.6 Gates locais + revisão Codex; abrir PR `Closes #893`; `/review-pr` até zero pendências; merge sequencial

## 6. PR-e — Bloco rico V12 + resolvedor puro + gate real

**Issue:** #548 (UNI-REQ-0058) · **Branch:** `feature/548-bloco-rico-resolvedor` · **PR:** `Closes #548` + ref. #554 (fecha a Story)

- [ ] 6.1 Criar `EnvelopeCodecV12`; congelar encoder/decoder V11; registrar 1.2 em `RegistroCodecsEnvelope`
- [ ] 6.2 Materializar `exigencias[]` no encoder (ordenação por bytes canônicos; null explícito canônico)
- [ ] 6.3 Resolver e congelar `dataReferenciaFatos` concreta (fuso America/Sao_Paulo)
- [ ] 6.4 Congelar `exigencia_id` estável e correlação por identidade (CA-09)
- [ ] 6.5 Resolvedor puro `static` sobre {bloco, fatos, apresentações} com erros nomeados (ADR-0076)
- [ ] 6.6 Substituir stub `DocumentoObrigatorioParaModalidade` e aplicar gate em Publicar/Retificar/FecharRetificacao; remover guarda do PR-a
- [ ] 6.7 Decompor as 5 contraprovas de CA-05 (heteroidentificação, cota, categoria incompatível, vantagem ausente, mutação posterior)
- [ ] 6.8 Golden fixture 1.2 + 3 canários; paridade bidirecional (órfã viva/congelada); imunidade (mudar TipoDocumento não muda hash)
- [ ] 6.9 Testes B-03: referência ausente/DATA_ESPECIFICA sem data/fase sem extremo/fase de outro processo; virada de dia UTC→local; retificação que muda a política
- [ ] 6.10 Gates locais + `/smoke-crud` + revisão Codex; abrir PR `Closes #548`; `/review-pr` até zero pendências; validar integração pós-merge da Story

## 7. Verificação final (Story #554)

- [ ] 7.1 `dotnet build`/`dotnet test UniPlus.slnx` verdes; ArchTests do módulo
- [ ] 7.2 `/smoke-crud` do recurso `documentos-exigidos` (PUT idempotente, 400/404/409/422, bloqueio pós-publicação; sem DELETE/soft/histórico)
- [ ] 7.3 Cenário-alvo end-to-end: nível de ensino (INSCRICAO/GERAL), renda (HABILITACAO/MODALIDADE EM cotas de renda), laudo (CONDICAO_ATENDIMENTO IGUAL PCD), reservista (SEXO IGUAL MASCULINO AND FAIXA_ETARIA MAIOR_IGUAL 18); publicar e conferir o bloco congelado
- [ ] 7.4 Confirmar #547/#548/#549 + as 2 tasks novas + a sub-issue Web fechadas/atualizadas; Story #554 fechável
