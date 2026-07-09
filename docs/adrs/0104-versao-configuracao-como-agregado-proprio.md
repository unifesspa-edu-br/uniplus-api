---
status: "accepted"
date: "2026-07-09"
decision-makers:
  - "Tech Lead (CTIC)"
consulted:
  - "Backend (CTIC)"
informed:
  - "Equipe Uni+"
---

# ADR-0104: A vigência da configuração ordena versões, não documentos

## Contexto e enunciado do problema

Hoje `SnapshotPublicacao` pende de `Edital` por `EditalId`, e `ProcessoSeletivo.EditalVigente` resolve a configuração vigente **ordenando editais** por `DataPublicacao`.

Isso funde duas grandezas ortogonais no mesmo campo:

- o **documento normativo**, que tem órgão, série, número, assinante e uma data de publicação **documental** — a que o PDF declara ("13/03/2026, às 19h00min");
- a **versão da configuração**, que tem ordem, hash e vigência.

A consequência aparece no banco. Para obter ordem total entre editais, existe o índice `ux_editais_processo_data_publicacao`, que impõe **unicidade da data de publicação** dentro de um processo. Ele serializa o conjunto errado: dois atos podem ser publicados no mesmo instante sem que isso decida coisa alguma, porque o que ordena a configuração é a versão, não o documento.

Pior: a prática institucional torna a data documental **imprópria como chave de ordenação**. Uma retificação **republica com o mesmo número e a mesma data de publicação** do ato original, anotando apenas a data em que a retificação ocorreu. Ordenar por essa data produziria um empate entre a versão retificada e a original.

E há um risco latente. Hoje `DataPublicacao = clock.GetUtcNow()`, de modo que a data documental e o instante do sistema coincidem por construção. No momento em que a data documental passar a ser **declarada** pelo usuário — como o número do ato já é, e como a importação de acervo histórico exigirá —, a retroatividade passa a existir, e a ordenação da configuração vigente passa a poder ser reescrita pelo digitador.

## Drivers da decisão

- **Determinismo do que valia em cada instante** — a resolução da configuração vigente é a base da RN08 e de toda auditoria posterior.
- **Imunidade à retroatividade** — a data documental deve poder ser declarada sem que isso altere qual configuração vigorava.
- **Fronteira de módulo** — o documento normativo migra para o módulo `Publicacoes` (ADR-0105); a configuração permanece em `Selecao`.
- **Ausência de base em produção** — o custo de mudar o modelo agora é baixo.

## Opções consideradas

- **A**: Manter o snapshot como apêndice do edital e ordenar por `DataPublicacao`.
- **B**: `VersaoConfiguracao` como **agregado próprio**, com `vigente_a_partir_de` (relógio do sistema) ordenando, e vínculo ao ato criador por valor.
- **C**: Manter o vínculo atual e acrescentar um número de sequência ao edital.

## Resultado da decisão

**Escolhida:** "B — `VersaoConfiguracao` como agregado próprio", porque separar a grandeza que ordena da grandeza que documenta é o antídoto à retroatividade, e porque o seletor de vigência não deve precisar saber o que é um tipo de ato.

```text
VersaoConfiguracao                       [append-only, forense]
    Id, ProcessoSeletivoId
    NumeroVersao          -- monotônico por processo; UNIQUE(processo, numero)
    VigenteAPartirDe      -- relógio do SISTEMA: é o que ORDENA
    SchemaVersion, AlgoritmoHash, ConfiguracaoCongelada, HashConfiguracao
    AtoCriadorId          -- referência POR VALOR ao ato publicado; NOT NULL, UNIQUE
    AtoCriadorHash
    AtoCriadorRetificaId  -- o ato que o ato criador retifica; nulo na versão 1
```

`AtoCriadorId` **não é chave estrangeira**: o ato vive no módulo `Publicacoes`, e a referência cross-módulo é por valor (ADR-0061). A garantia forense é local à tabela de versões — `NOT NULL` mais `UNIQUE` — e é **mais forte** que o atual `ux_snapshot_publicacao_edital_id`: toda versão tem exatamente um ato criador, e um ato cria no máximo uma versão.

**Não há ciclo de inserção.** O identificador e o hash da versão são calculados **antes** de persistir. O ato é gravado primeiro e a versão depois — inclusive quando é o ato congelante que a cria e que precisa registrar a referência a ela.

**Exatamente um ato congelante por certame.** O edital de abertura, e suas retificações. Todos os demais atos apenas invocam a versão vigente (ADR-0075), sem criar configuração. `AtoCriadorRetificaId` torna isso verificável **dentro do módulo Seleção**, sem consultar `Publicacoes`: o ato criador da versão `N > 1` retifica o ato criador da versão `N − 1`. Uma versão cujo ato criador não o faça é recusada — é a trava que impede uma segunda cadeia de versões no mesmo certame.

O seletor passa a ser:

```sql
WHERE  vigente_a_partir_de <= @instante
ORDER  BY vigente_a_partir_de DESC, numero_versao DESC
LIMIT  1
```

Determinístico mesmo com empate de instante, e o índice de unicidade sobre `data_publicacao` **é removido**: dois atos publicados no mesmo instante deixam de colidir.

## Consequências

### Positivas

- A configuração vigente deixa de depender de qualquer atributo do documento — inclusive do seu tipo. O seletor fica imune à criação de novos tipos de ato.
- A data documental pode passar a ser declarada, ou importada de acervo histórico, **sem** alterar a ordem das versões.
- A garantia forense (uma versão, um ato criador) é local e mais forte que a atual.
- "Exatamente um ato congelante por certame" vira invariante verificável sem consulta cross-módulo.

### Negativas

- O contrato HTTP pretendido não muda (`Publicar`, `Retificar`, `GET .../snapshot-vigente`), mas o **mecanismo** de resolução muda de `Edital.DataPublicacao` para `VersaoConfiguracao.VigenteAPartirDe`. A equivalência precisa ser **provada por testes de contrato**, não afirmada.
- Persistência, índices e possivelmente códigos de erro mudam.
- `ProcessoSeletivo.EditalVigente` deixa de existir na forma atual.

### Neutras

- O contrato de canonicalização e o cálculo do hash da configuração congelada permanecem inalterados (ADR-0100).
- Sem base em produção, as migrations são decididas pelo mérito.

## Confirmação

- **Teste de domínio**: uma versão `N > 1` cujo ato criador não retifica o criador da versão `N − 1` é recusada com erro nomeado (ADR-0102).
- **Teste de domínio**: a versão 1 não retifica ninguém; contrato simétrico.
- **Teste de domínio**: um ato tenta criar uma segunda versão e é recusado (`UNIQUE(ato_criador_id)`).
- **Teste de contrato**: duas versões com a **mesma data documental** e vigências distintas resolvem corretamente em cada instante.
- **Teste de contrato**: antes da primeira publicação, o seletor devolve vazio, sem recorrer a qualquer versão anterior (ADR-0076).
- **Fitness test**: nenhuma chave estrangeira liga `VersaoConfiguracao` ao módulo `Publicacoes`.

## Prós e contras das opções

### A — Snapshot como apêndice do edital

- Bom, porque é o que existe.
- Ruim, porque exige unicidade da data de publicação para obter ordem total — serializando o conjunto errado.
- Ruim, porque a retificação republica com a mesma data, e o modelo depende dessa data para ordenar.
- Ruim, porque o seletor precisa passar pelo documento, ficando exposto ao seu tipo.

### B — Versão como agregado próprio (escolhida)

- Bom, porque a vigência ordena versões, e a data documental fica livre.
- Bom, porque a garantia forense é local e mais forte.
- Ruim, porque exige provar a equivalência do contrato por testes, e reescrever índices.

### C — Número de sequência no edital

- Bom, porque resolve a ordem total sem criar agregado.
- Ruim, porque mantém a conflação: o documento continua carregando a ordem da configuração. Um comunicado que não congela nada precisaria de número de sequência, ou de uma regra para não tê-lo.

## Mais informações

- [ADR-0061](0061-referencia-cross-modulo-via-snapshot-copy.md) — referência cross-módulo por valor
- [ADR-0063](0063-entidades-forensics-isentas-de-soft-delete.md) — entidades forenses, append-only
- [ADR-0075](0075-snapshot-do-ato-resolvido-no-instante.md) — a configuração vigente é resolvida no instante
- [ADR-0076](0076-contrato-snapshot-runtime-espelha-publicacao.md) — a ausência aflora; não há recurso silencioso a versão anterior
- [ADR-0100](0100-canonicalizacao-hash-snapshot-publicacao.md) — canonicalização e hash da configuração congelada
- [ADR-0103](0103-ato-normativo-generalizado-retificacao-como-relacao.md) — retificação é relação entre atos
- [ADR-0105](0105-modulo-publicacoes-registro-central-dos-atos.md) — o módulo que possui o ato publicado
