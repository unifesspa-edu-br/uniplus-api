---
status: "accepted"
date: "2026-05-31"
decision-makers:
  - "Tech Lead"
---

# ADR-0073: Os fatos de atendimento especializado carregam a identidade congelada da oferta; a validação lê o código congelado

## Contexto e enunciado do problema

A inscrição pode solicitar atendimento especializado — condição, recurso ou tipo de deficiência — escolhendo itens da oferta configurada no processo. O predicado de aplicabilidade de algumas exigências depende desses fatos (por exemplo, exigir laudo de quem solicitou atendimento para pessoa com deficiência).

Se o avaliador resolvesse esses fatos **relendo a oferta viva**, retratar ou recodificar a oferta depois da publicação mudaria o que a validação resolve para uma solicitação **já registrada** — quebrando a imunidade da [ADR-0070](0070-validacao-runtime-avalia-snapshot-congelado.md) pelo lado dos fatos do candidato.

A questão a decidir é: **como o fato de atendimento preserva a identidade do item de oferta escolhido** — referência à oferta viva ou cópia congelada no ato.

## Drivers da decisão

- **Imunidade também pelo lado dos fatos.** A ADR-0070 só se sustenta se nem a configuração nem os fatos forem relidos do vivo no runtime.
- **Bancos isolados por módulo, sem FK viva cross-módulo.** Coerente com a [ADR-0061](0061-referencia-cross-modulo-via-snapshot-copy.md) (snapshot-copy) e com a [ADR-0067](0067-aninhamento-tipodeficiencia-sob-pcd.md) (tipo de deficiência ancorado na condição PcD).
- **O fato preserva a identidade vigente no ato.** O que foi escolhido no momento da solicitação deve permanecer fiel a esse momento.

## Opções consideradas

- **A. Cada fato copia, no ato, a identidade da oferta (snapshot-copy intra-módulo)** e o avaliador lê o código congelado no próprio fato.
- **B. O fato guarda apenas a referência à oferta viva** e o avaliador a relê — quebra a imunidade quando a oferta muda após a publicação.

## Resultado da decisão

**Escolhida:** "A — identidade congelada copiada no fato", porque mantém a imunidade da ADR-0070 também pelo lado dos fatos e torna cada fato auto-suficiente.

Os fatos de atendimento carregam a identidade congelada, copiada da oferta no momento da solicitação. O avaliador lê esse **código congelado**, sem reler a oferta viva. Os write-path triggers validam cada fato contra a identidade congelada (mantendo-a fiel e imutável após o registro). Esta decisão estende a disciplina de snapshot-copy da ADR-0061 ao binding intra-módulo dos fatos de atendimento.

## Consequências

### Positivas

- A validação é imune a mutação ou retratação da oferta depois da publicação.
- Cada fato é auto-suficiente e auditável: carrega a própria identidade.

### Negativas

- Introduz denormalização (identidade copiada no fato), com a obrigação de garantir a imutabilidade dessa identidade.

## Confirmação

- **Fidelidade:** o fato carrega a identidade copiada fiel à oferta no momento da solicitação.
- **Imutabilidade:** a identidade congelada do fato não pode ser alterada após o registro.
- **Imunidade:** recodificar ou retratar (soft-delete) a oferta viva não muda o que a validação resolve para um fato já registrado; o aninhamento sob a condição PcD ([ADR-0067](0067-aninhamento-tipodeficiencia-sob-pcd.md)) permanece ancorado no código congelado.

## Mais informações

- Requisito de rastreabilidade: **UNI-REQ-0061**.
- [ADR-0070](0070-validacao-runtime-avalia-snapshot-congelado.md) — imunidade do processo publicado, aqui assegurada pelo lado dos fatos.
- [ADR-0061](0061-referencia-cross-modulo-via-snapshot-copy.md) — snapshot-copy, cuja disciplina esta ADR estende ao binding intra-módulo.
- [ADR-0067](0067-aninhamento-tipodeficiencia-sob-pcd.md) — aninhamento de tipo de deficiência sob a condição PcD.
