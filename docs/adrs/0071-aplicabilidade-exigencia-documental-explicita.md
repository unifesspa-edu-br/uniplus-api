---
status: "accepted"
date: "2026-05-31"
decision-makers:
  - "Tech Lead"
---

# ADR-0071: Aplicabilidade da exigência documental é configuração explícita (`GERAL`/`CONDICIONAL`), não inferida

## Contexto e enunciado do problema

Uma exigência documental pode ser exigida de **todos** os candidatos ou apenas de **quem satisfaz um gatilho** — um predicado sobre os fatos da inscrição (por exemplo, exigir laudo apenas de quem solicitou atendimento especializado).

Antes, "exigida de todos" era **inferida da ausência de condições no gatilho**. Isso colapsava duas intenções distintas no mesmo estado físico "zero condições":

1. *exigida de todos* (universal, deliberada); e
2. *exigida de ninguém* (uma exigência condicional cujas condições foram todas retratadas).

A inferência não as distinguia: uma exigência cuja única condição fora retratada, por correção legítima, virava por engano "exigida de todos" — um efeito colateral indevido de uma operação append-only.

A questão a decidir é: **como representar a universalidade de uma exigência** — declarada explicitamente pelo administrador ou inferida do estado das condições.

## Drivers da decisão

- **Intenção explícita e auditável.** A universalidade é decisão do administrador, não um efeito derivado de um estado ambíguo.
- **Congelamento preserva a intenção declarada.** O snapshot deve preservar o que foi declarado, não recomputar universalidade de um estado ambíguo na publicação.
- **Append-only sem efeito colateral.** Retratar uma condição não pode, por si só, universalizar a exigência.

## Opções consideradas

- **A. Campo explícito `aplicabilidade ∈ {GERAL, CONDICIONAL}`** decidido pelo administrador no cadastro.
- **B. Inferir universalidade da ausência de condições** — ambíguo, como descrito no contexto.

## Resultado da decisão

**Escolhida:** "A — campo explícito de aplicabilidade", porque torna a intenção do administrador inequívoca e estável no congelamento.

Cada exigência declara sua aplicabilidade, congelada no snapshot:

- **`GERAL`** — exigida de todos os candidatos; o gatilho não é avaliado.
- **`CONDICIONAL`** — exigida de quem satisfaz o gatilho; **zero condições** passa a significar, sem ambiguidade, *exigida de ninguém*.

Guardas de coerência impedem `GERAL` conviver com condição viva e impedem publicar uma exigência `CONDICIONAL` sem condições quando ela determina resultado (obrigatória ou com consequência de indeferimento).

## Consequências

### Positivas

- Intenção do administrador explícita e auditável.
- Estado não-ambíguo: "exigida de todos" e "exigida de ninguém" deixam de compartilhar a mesma representação física.
- Correções append-only deixam de universalizar a exigência por efeito colateral.

### Negativas

- Exige decisão explícita no cadastro: a aplicabilidade passa a ser campo obrigatório.

## Confirmação

- **Domínio do campo** restrito a `{GERAL, CONDICIONAL}`.
- **Coerência `GERAL`** rejeita condição viva associada.
- **Coerência `CONDICIONAL`** exige que toda condição viva pertença a uma exigência `CONDICIONAL`.
- **Validação de publicação** bloqueia publicar exigência `CONDICIONAL` sem condições que determina resultado.

## Mais informações

- Requisito de rastreabilidade: **UNI-REQ-0057**.
- [ADR-0070](0070-validacao-runtime-avalia-snapshot-congelado.md) — a validação em runtime avalia o gatilho congelado; a aplicabilidade explícita é o primeiro campo desse gatilho.
- Regra de negócio **RN08** — a aplicabilidade declarada é congelada na publicação.
