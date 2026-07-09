---
status: "accepted"
date: "2026-07-09"
decision-makers:
  - "Tech Lead (CTIC)"
consulted:
  - "P.O. CEPS"
  - "P.O. CRCA"
informed:
  - "Equipe Uni+"
---

# ADR-0105: O ato publicado pertence a um módulo `Publicacoes` que não conhece os domínios

## Contexto e enunciado do problema

Reitoria, CEPS e CRCA publicam atos normativos — editais, avisos, comunicados — hoje espalhados por portais distintos, sem registro comum, sem cadeia de linhagem e sem prova do que foi efetivamente publicado. Com a implantação do Uni+, essas publicações passam a ser concentradas no sistema, e cada setor referencia uma publicação nas suas páginas.

Isso levanta uma pergunta de fronteira: **onde vive o ato publicado?**

A resposta óbvia — dentro do módulo `Selecao`, como a entidade `Edital` faz hoje — é refutada por dois fatos observados nos acervos reais:

1. **Um ato pode não pertencer a certame nenhum.** O `EDITAL Nº 22/2026 – CEPS` seleciona elaboradores e corretores de questões: declara ter por objeto outro edital, não um certame de candidatos. Num módulo que exigisse um processo seletivo, esse ato não teria onde morar.

2. **A numeração é da série `(órgão, ano)` e atravessa certames.** Na sequência do SiSU 2026 do CRCA faltam os Editais 11, 12, 16, 17, 21 e 22 — pertencem a outros processos seletivos correndo em paralelo. O número, portanto, não é derivável do processo nem uma sequência dedicada a ele.

Ao mesmo tempo, a consulta unificada ("todos os atos deste certame") é o **propósito** da centralização, não um efeito colateral. O risco é resolver a consulta acoplando o módulo documental aos domínios — e produzir o agregado que sabe de tudo.

## Drivers da decisão

- **Consulta unificada dos atos** — é a razão institucional de centralizar as publicações.
- **Existência de atos sem certame** — o modelo deve acomodá-los sem exceção.
- **Fronteiras de módulo (ADR-0056, R8)** — um módulo não depende do domínio de outro.
- **Referência cross-módulo por valor (ADR-0061)** — sem chave estrangeira atravessando módulo.
- **Simplicidade operacional (ADR-0097)** — a topologia é de módulos internos coabitando um processo e um banco.

## Opções consideradas

- **A**: O ato permanece no módulo `Selecao`, e o Ingresso referencia-o.
- **B**: Cada módulo possui a sua própria tabela de atos.
- **C**: Módulo **`Publicacoes`** dedicado, que possui a essência documental do ato e **não conhece** os domínios.

## Resultado da decisão

**Escolhida:** "C — módulo `Publicacoes` dedicado", porque o ato publicado é um artefato documental de um órgão, não de um certame, e há atos sem certame algum.

O módulo possui **apenas** a essência documental e normativa do ato: órgão, série, ano, número, tipo, data de publicação, documento, hash e cadeia de retificação. Mais o cadastro de tipos de ato, versionado por vigência.

Ele **não conhece** `ProcessoSeletivo`, `Chamada` nem configuração de certame. Não há coluna, chave estrangeira ou enumerado desses conceitos. **Essa ausência é a trava contra o agregado onipotente**, e é verificável.

Os domínios referenciam o ato **por valor** — `{id, hash}` —, sem chave estrangeira cruzando módulo (ADR-0061). O risco de referência órfã é mitigado pelo hash do documento.

A consulta unificada é servida por um **vínculo genérico**, `(ato, tipo_entidade, entidade_id)`, populado pelos domínios e guardado por `Publicacoes` **sem interpretação**: `tipo_entidade` é um rótulo opaco, sem enumerado fechado e sem chave estrangeira para tabela de domínio. `Selecao` vincula os seus atos ao processo seletivo; `Ingresso`, os seus à chamada. Um ato sem vínculo permanece válido.

O módulo entra como **quinto módulo interno** do monolito modular, com schema `publicacoes` no banco `uniplus`, coerente com a ADR-0097. Não cria uma segunda instância de Wolverine nem um segundo outbox.

## Consequências

### Positivas

- Atos sem certame — seleção interna de elaboradores, atos de outros processos — cabem no modelo sem exceção.
- A numeração pode atravessar certames, como já ocorre, sem que o modelo precise saber disso.
- A consulta unificada existe **sem** acoplar o módulo documental aos domínios.
- A fronteira é verificável: basta constatar que o módulo não possui colunas de domínio.
- O `Edital` sai do módulo `Selecao`, que passa a possuir apenas a configuração e a sua versão.

### Negativas

- Um módulo a mais para compor no host, com um `DbContext` a mais nas listas travadas por fitness test (`MigrationContextsEsperados`, isolamento cross-módulo, composição do host).
- A integridade da referência ato ↔ domínio deixa de ser garantida pelo banco e passa a ser garantida pela aplicação, com o hash como mitigação.
- A consulta "todos os atos deste certame" exige o vínculo estar populado — se um domínio esquecer de gravá-lo, o ato existe mas não aparece na consulta do certame.

### Neutras

- Com schema por módulo (ADR-0097), uma chave estrangeira de `publicacoes` para `selecao` é **tecnicamente possível**. A ausência dela deixa de ser garantida pela física e passa a ser decisão de modelagem, travada por fitness test.
- O módulo não expõe conceitos de negócio dos domínios; a sua API é sobre documentos.

## Confirmação

- **Fitness test**: o schema `publicacoes` não possui nenhuma coluna chamada `processo_seletivo_id`, `chamada_id` ou `aplicacao_prova_id`.
- **Fitness test**: nenhuma chave estrangeira do schema `publicacoes` atravessa a fronteira de outro schema de módulo. O teste **planta um canário** — cria a chave estrangeira cross-schema, verifica que o banco a aceita, remove — e só então assere que nenhuma existe. Sem o canário, um schema vazio passaria trivialmente.
- **Fitness test (R8)**: o módulo não referencia `Domain`, `Application`, `Infrastructure` ou `API` de nenhum outro módulo; só o host compõe.
- **Teste de domínio**: um ato sem vínculo é registrável e consultável.
- **Teste de domínio**: uma mesma entidade acumula vários atos; um mesmo ato vincula-se a mais de uma entidade.

## Prós e contras das opções

### A — O ato permanece no módulo `Selecao`

- Bom, porque é o que existe hoje, e nenhum módulo novo é criado.
- Ruim, porque um ato que não pertence a certame algum não tem onde morar. Um "edital de Seleção" que não é de um processo seletivo é uma contradição de escopo.
- Ruim, porque o módulo `Ingresso` passaria a depender do domínio de `Selecao` para publicar as suas convocações, violando o R8.

### B — Cada módulo com a sua tabela de atos

- Bom, porque preserva o isolamento de cada módulo.
- Ruim, porque a consulta unificada — o propósito da centralização — vira uma união entre tabelas disjuntas, com esquemas que derivam entre si com o tempo.
- Ruim, porque a cadeia de retificação não atravessaria módulos, e ela atravessa: um aviso do CRCA retifica um edital do CRCA, mas um comunicado da Reitoria pode retificar um edital do CEPS.

### C — Módulo `Publicacoes` dedicado (escolhida)

- Bom, porque acomoda o ato sem certame, que existe.
- Bom, porque a consulta unificada é servida sem que o módulo conheça os domínios.
- Bom, porque a cadeia de retificação vive num só lugar.
- Ruim, porque acrescenta um módulo a compor e a operar.
- Ruim, porque a integridade da referência ato ↔ domínio passa a ser responsabilidade da aplicação.

## Mais informações

- [ADR-0056](0056-modulo-configuracao-e-read-side-via-reader.md) — fitness tests de isolamento cross-módulo (R8)
- [ADR-0061](0061-referencia-cross-modulo-via-snapshot-copy.md) — referência cross-módulo por valor
- [ADR-0063](0063-entidades-forensics-isentas-de-soft-delete.md) — entidades forenses, append-only
- [ADR-0097](0097-topologia-de-deploy-em-tres-apis-monolito-modular.md) — monolito modular; schema por módulo no banco `uniplus`. **Esta ADR acrescenta o quinto módulo interno.**
- [ADR-0103](0103-ato-normativo-generalizado-retificacao-como-relacao.md) — retificação é relação entre atos
- [ADR-0104](0104-versao-configuracao-como-agregado-proprio.md) — a vigência ordena versões, não documentos
- Evidência empírica: acervos completos do PS Canaã dos Carajás 2026 (CEPS) e do SiSU 2026 — Edição Única (CRCA)
