---
status: "accepted"
date: "2026-05-31"
decision-makers:
  - "Tech Lead"
---

# ADR-0075: O snapshot que governa um ato é resolvido deterministicamente no instante do ato e gravado nele

> **Emenda (ADR-0104):** o núcleo desta decisão — seletor determinístico, instante sempre explícito, snapshot gravado no ato, ausência exposta — permanece vigente. O **mecanismo** de ordenação, não: a configuração vigente passou a ser resolvida sobre `VersaoConfiguracao.VigenteAPartirDe` (o relógio do sistema), e não mais ordenando editais por `DataPublicacao` (a data documental). Duas consequências desta ADR caducam com isso: a seleção deixou de ser "em duas etapas" (primeiro o edital vigente, depois o snapshot dele), e o empate deixou de ser impedido por unicidade sobre `(processo, data de publicação)` — o índice foi removido. O empate de instante passa a ser possível e é desempatado por `NumeroVersao` decrescente, o que preserva o determinismo que esta ADR exige. Ver [ADR-0104](0104-versao-configuracao-como-agregado-proprio.md).

## Contexto e enunciado do problema

Com a validação documental lendo o snapshot congelado ([ADR-0070](0070-validacao-runtime-avalia-snapshot-congelado.md)), é preciso definir **qual snapshot governa cada ato**.

Um processo pode ter mais de uma publicação ao longo do tempo: a publicação inicial e eventuais retificações, cada uma com seu snapshot. Avaliar um ato contra "o snapshot atual", determinado pelo relógio do sistema no momento da reavaliação, tornaria o resultado de um ato antigo dependente de retificações **posteriores** ao ato — impedindo a reprodução histórica fiel do resultado.

A questão a decidir é: **como determinar, de forma reproduzível, qual snapshot governa um ato** — e como preservar essa decisão para reavaliações futuras.

## Drivers da decisão

- **Reprodução histórica fiel.** Um ato deve ser reavaliável contra o estado vigente na época em que ocorreu.
- **Determinismo.** A seleção do snapshot não pode ser ambígua, inclusive em empates de data.
- **Auditabilidade.** O snapshot que governou um ato deve ser recuperável.
- **Relógio explícito.** Coerente com a [ADR-0068](0068-relogio-via-timeprovider-injetado.md), o instante deve ser injetado, nunca lido implicitamente do relógio interno.

## Opções consideradas

- **A. Um seletor determinístico resolve o snapshot vigente no instante dado**, com o instante sempre explícito, e o ato grava o snapshot que o governou.
- **B. O runtime usa o relógio do sistema internamente** — a reavaliação de atos antigos passa a depender de retificações posteriores; ambíguo e não reproduzível.

## Resultado da decisão

**Escolhida:** "A — seletor determinístico com instante explícito e snapshot gravado no ato", porque é a única que preserva a reprodução histórica fiel.

Um seletor retorna o snapshot da **publicação vigente** do processo — a publicação viva com a maior data de publicação **≤ instante** — de forma determinística, com empate impossível por contrato de unicidade sobre (processo, data de publicação). A escolha é em duas etapas: primeiro o edital vigente, depois o snapshot dele.

O instante é **sempre explícito**, jamais o relógio interno. O ato grava o identificador do snapshot que o governou; reavaliações futuras usam esse identificador, não recalculam pelo relógio. Quando não há publicação vigente, o seletor **expõe a ausência** (sem fallback silencioso para uma publicação anterior), tornando visível uma configuração ausente em vez de mascará-la.

## Consequências

### Positivas

- Reprodução histórica fiel: atos antigos preservam seu snapshot; uma retificação cria nova publicação que governa apenas atos novos.
- Seleção determinística e auditável; empate de data é impossível por contrato.
- A ausência de publicação vigente é exposta, não mascarada.

### Negativas

- O caller passa a ser responsável por fornecer o instante (ou o snapshot do ato) explicitamente.

## Confirmação

- **Seleção por instante:** o seletor escolhe a publicação vigente correta para um instante dado (incluindo o limite `≤`).
- **Empate impossível:** o contrato de unicidade impede duas publicações vivas com a mesma data no mesmo processo.
- **Persistência no ato:** o ato preserva, imutável, o snapshot que o governou.
- **Fidelidade histórica:** reavaliar um ato antigo depois de uma retificação posterior produz o resultado da época do ato.

## Mais informações

- Requisito de rastreabilidade: **UNI-REQ-0060**.
- [ADR-0070](0070-validacao-runtime-avalia-snapshot-congelado.md) — a leitura do snapshot congelado é o que torna necessária a resolução determinística de qual snapshot governa o ato.
- [ADR-0068](0068-relogio-via-timeprovider-injetado.md) — relógio injetado e explícito, do qual decorre o instante explícito deste seletor.
- Regra de negócio **RN08** — cada publicação congela seu próprio snapshot.
