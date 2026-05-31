---
status: "accepted"
date: "2026-05-31"
decision-makers:
  - "Tech Lead"
---

# ADR-0074: A base legal da exigência documental é 1:N e enforçada por uma validação de publicação

## Contexto e enunciado do problema

Toda exigência documental que determina resultado — obrigatória, ou com consequência de indeferimento — precisa de embasamento legal rastreável. Esse embasamento pode ter **mais de uma fonte**: por exemplo, uma lei federal somada à cláusula do edital.

Um campo único de base legal por exigência é insuficiente (não acomoda múltiplas fontes) e, sozinho, não garante que toda exigência publicada tenha de fato base resolvida — abrindo espaço para publicar um edital com exigência que determina resultado sem amparo legal registrado.

A questão a decidir é: **como modelar a base legal de uma exigência e onde garantir que ela esteja resolvida**.

## Drivers da decisão

- **Rastreabilidade legal.** Toda exigência que determina resultado deve ter base legal registrada e auditável.
- **Múltiplas fontes.** É preciso suportar várias bases por exigência, de abrangências diferentes (federal, estadual, municipal, norma interna, edital).
- **À prova de vácuo na publicação.** Não se pode publicar exigência que determina resultado sem base resolvida.
- **Ponto de enforcement.** Sob a [ADR-0070](0070-validacao-runtime-avalia-snapshot-congelado.md), o congelamento na publicação é o momento natural para validar a base legal.

## Opções consideradas

- **A. Base legal 1:N por exigência + validação de publicação** que exige ≥1 base ativa `RESOLVIDO` para quem determina resultado.
- **B. Campo único de base legal por exigência** — não acomoda múltiplas fontes nem garante resolução.
- **C. Validação contínua em runtime** — desnecessária sob a ADR-0070, já que o que governa o runtime é o snapshot congelado.

## Resultado da decisão

**Escolhida:** "A — base legal 1:N com validação na publicação", porque acomoda múltiplas fontes e garante o amparo legal exatamente no congelamento.

A base legal é **1:N** por exigência, cada uma com referência, abrangência (`FEDERAL`, `ESTADUAL`, `MUNICIPAL`, `INTERNA_NORMA`, `INTERNA_EDITAL`) e `status ∈ {PENDENTE, RESOLVIDO}`. Uma validação de publicação exige, para toda exigência que determina resultado, **≥1 base ativa `RESOLVIDO`** — de qualquer abrangência. A cláusula do edital (`INTERNA_EDITAL`) conta sozinha, por discricionariedade do administrador embasada na aprovação do próprio edital.

Apenas bases `RESOLVIDO` congelam no snapshot; `PENDENTE` é rascunho e não congela. Sob a ADR-0070, **a validação de publicação é o único ponto de enforcement** — não há malha contínua em runtime.

## Consequências

### Positivas

- Rastreabilidade legal garantida no momento da publicação.
- Suporte a múltiplas fontes de embasamento por exigência.
- À prova de vácuo: exigência que determina resultado não publica sem base resolvida.

### Negativas

- A publicação fica bloqueada enquanto uma exigência que determina resultado não tiver base resolvida — comportamento desejado, porém é uma trava a mais no fluxo de publicação.

## Confirmação

- **Bloqueio por ausência:** exigência que determina resultado sem base `RESOLVIDO` impede a publicação.
- **Bloqueio por só-`PENDENTE`:** base apenas `PENDENTE` não satisfaz a validação.
- **Robustez:** rebaixar (`RESOLVIDO`→`PENDENTE`), reassociar ou remover (soft-delete) a única base `RESOLVIDO` é apanhado na próxima validação de publicação.

## Mais informações

- Requisito de rastreabilidade: **UNI-REQ-0059**.
- Legislação aplicável às exigências documentais do processo seletivo: Lei 12.711/2012, Lei 13.409/2016, Lei 14.129/2021, LBI 13.146/2015.
- [ADR-0070](0070-validacao-runtime-avalia-snapshot-congelado.md) — sob a leitura do snapshot congelado, a publicação é o ponto de enforcement da base legal.
- Regra de negócio **RN08** — só bases `RESOLVIDO` congelam no snapshot.
