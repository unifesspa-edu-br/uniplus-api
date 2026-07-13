---
status: "accepted"
date: "2026-07-07"
decision-makers:
  - "Tech Lead"
---

# ADR-0100: Contrato de canonicalização e hash do snapshot de publicação (RN08)

> **Nota de atualização.** A decisão desta ADR — o contrato de canonicalização e o hash reproduzível do congelamento — permanece **integralmente vigente**. Mudou apenas **onde o congelado mora**: a entidade `SnapshotPublicacao` citada abaixo não existe mais; o congelamento é uma **[`VersaoConfiguracao`](0104-versao-configuracao-como-agregado-proprio.md)**, agregado próprio e append-only, cujo par de bytes canônicos e hash é derivado exatamente pelas regras aqui definidas. O nome sobrevive no código apenas no `ISnapshotPublicacaoCanonicalizer` — resíduo de vocabulário, não uma entidade. Leia "snapshot de publicação" como "a configuração congelada de uma versão".

## Contexto e enunciado do problema

RN08 exige que a publicação do `ProcessoSeletivo` (Story #759) congele a configuração de negócio num `SnapshotPublicacao` append-only, com um hash reproduzível: a mesma configuração deve produzir sempre o mesmo hash, em qualquer runtime, máquina ou momento — é a evidência que sustenta a integridade jurídica do resultado perante mandado de segurança ou processo administrativo. Sem contrato de canonicalização explícito, "mesma configuração" é ambíguo: strings com codificação Unicode equivalente mas bytes diferentes, decimais representados com precisão variável, instantes serializados com ou sem fração de segundo, ou apenas a ordem de serialização de um objeto já produzem hashes distintos para conteúdo logicamente idêntico.

O módulo já tem um precedente direto: `HashCanonicalComputer` / `CanonicalOptions` (`src/selecao/Unifesspa.UniPlus.Selecao.Domain/ValueObjects/HashCanonicalComputer.cs`), usado pelo hash de `ObrigatoriedadeLegal` ([ADR-0058](0058-obrigatoriedade-legal-validacao-data-driven.md)) e do `rol_de_regras`. Ele já resolve ordenação recursiva de chaves (`StringComparer.Ordinal`) e serialização byte-estável via `Utf8JsonWriter` sem indentação. Ele **não** resolve normalização Unicode, decimais com escala declarada e arredondamento determinístico, formato canônico de instante, nem a persistência dos bytes canônicos como artefato de primeira classe — hoje o hash é calculado sobre um payload em memória, nunca persistido como `bytea`.

O snapshot de publicação tem escopo maior que `ObrigatoriedadeLegal`: cobre mais de dez blocos de configuração (período, etapas, vagas, modalidades, ofertas, atendimento, documentos exigidos, formulário, bônus regional, desempate, remanejamento, divulgação, classificação, cronograma de fases), vários dos quais ainda não têm implementação no momento em que esta Story é executada — chegam em Stories subsequentes da Feature #40. O contrato precisa, portanto, resolver a ambiguidade de codificação/formato e, ao mesmo tempo, não travar a publicação por causa de blocos que ainda não existem no sistema.

## Drivers da decisão

- **Reprodutibilidade jurídica (RN08).** O mesmo conteúdo de negócio produz sempre o mesmo hash, hoje e em qualquer runtime futuro — é evidência para revisão judicial e auditoria.
- **Não introduzir um segundo mecanismo de canonicalização.** O módulo já tem um mecanismo testado (`HashCanonicalComputer`); duplicar lógica equivalente em paralelo divide a fonte de verdade do que conta como "mesma configuração".
- **Consultabilidade administrativa.** Suporte e auditoria consultam o snapshot via SQL; exigir decodificação de bytes para toda consulta rotineira é inaceitável.
- **Extensibilidade sem quebrar snapshots existentes.** Blocos que a Story corrente ainda não implementa não podem impedir a publicação, e a evolução futura do formato não pode reinterpretar retroativamente snapshots já emitidos.

## Opções consideradas

- **A.** Reaproveitar o `HashCanonicalComputer` como está hoje (ordenação de chaves + camelCase), sem estendê-lo.
- **B.** Estender o contrato canônico do `HashCanonicalComputer` com normalização NFC, decimais com escala declarada e arredondamento half-even, instantes RFC 3339 sem fração de segundo, e persistir os bytes canônicos (`bytea`) como base do hash, com `jsonb` derivado por parsing.
- **C.** Adotar um formato de canonicalização de terceiros (ex.: JCS — RFC 8785) em vez de estender o mecanismo existente.
- **D.** Persistir apenas o `jsonb` da configuração congelada e recalcular o hash a partir dele no momento da consulta, sem persistir bytes canônicos.

## Resultado da decisão

**Escolhida:** "B — estender o `HashCanonicalComputer`", porque resolve as três ambiguidades identificadas (Unicode, decimal, instante) reaproveitando o mecanismo de ordenação e serialização já validado pelo ADR-0058, sem introduzir um segundo mecanismo paralelo de canonicalização no módulo.

### Contrato canônico (payload → bytes → hash)

1. **Normalização Unicode NFC.** Toda string de negócio (nomes, descrições, textos livres) é normalizada para a forma NFC antes de entrar no payload — sequências combinantes e formas pré-compostas do mesmo texto colapsam para os mesmos bytes.
2. **Decimais com escala declarada e arredondamento half-even.** Cada campo decimal declara sua escala (número de casas decimais) no schema do bloco; o valor é arredondado por half-even para essa escala antes de entrar no payload, e serializado como string decimal de largura fixa — nunca como `number` JSON de ponto flutuante, que reintroduziria ambiguidade de representação entre runtimes.
3. **Instantes em UTC, RFC 3339, sem fração de segundo.** Todo instante é convertido para UTC e serializado com sufixo `Z`, granularidade de segundo — sem frações que variem por precisão de relógio ou serializador.
4. **Omissão de campos opcionais ausentes.** Um campo opcional sem valor é omitido do payload, nunca serializado como `null` explícito — mesma convenção já aplicada por `CanonicalOptions.DefaultIgnoreCondition = WhenWritingNull`.
5. **Ordenação lexicográfica de chaves por unidades de código UTF-16, recursiva, sem espaço insignificante.** Mesma regra do `CanonicalizeRecursive` existente — este contrato formaliza o comportamento já produzido pelo `Utf8JsonWriter` sem indentação e por `StringComparer.Ordinal` (que já compara por unidade de código UTF-16 no .NET), em vez de alterá-lo.
6. **Bytes canônicos como base do hash.** O resultado da serialização acima — os bytes, não uma representação intermediária — é persistido em `configuracao_congelada_canonica bytea`. `hash_configuracao = sha256(configuracao_congelada_canonica)`, hex minúsculo.
7. **`jsonb` de consulta derivado por parsing.** A coluna `configuracao_congelada jsonb` é obtida fazendo parse dos bytes canônicos — nunca o inverso. O banco não re-serializa nem reordena chaves para produzir o `jsonb`; ele só converte bytes já canônicos para um formato consultável por SQL. Se uma versão futura do motor de banco reordenar ou renormalizar o `jsonb` internamente, o hash permanece correto porque foi calculado sobre os bytes persistidos, nunca sobre o `jsonb`.
8. **Envelope versionado.** `schema_version` (versão do conjunto de blocos do snapshot) e `algoritmo_hash` (ex.: `canonical-json/sha256@v1`) acompanham o snapshot — permitem evoluir o formato ou o algoritmo de hash sem reinterpretar snapshots antigos, que preservam o algoritmo com que foram gerados.
9. **Campos voláteis fora, identidades de negócio estáveis dentro.** Chaves surrogate de linha, campos de auditoria (`CreatedAt/By`, `UpdatedAt/By`), soft-delete e quaisquer valores puramente derivados ficam fora do payload. Identidades de negócio estáveis — códigos, identificadores de origem referenciados por outros módulos via referência cross-módulo por snapshot-copy ([ADR-0061](0061-referencia-cross-modulo-via-snapshot-copy.md)) — entram: são o que dá sentido a "duas configurações iguais" ao comparar hashes.
10. **Blocos de dimensões ainda não construídas.** Um bloco do snapshot cuja dimensão ainda não tem implementação na Story corrente entra no payload com o valor canônico `{"status": "nao_construido"}`. Isso satisfaz o invariante "todo bloco canônico está presente" sem bloquear a publicação por causa de trabalho futuro e sem inventar um valor de negócio fictício para uma dimensão que ainda não existe.

### Relação com o `HashCanonicalComputer` existente

Esta ADR **estende** `HashCanonicalComputer`/`CanonicalOptions`, não introduz um mecanismo paralelo. O computer atual já cobre os itens 5 e 6 (ordenação recursiva, serialização byte-estável via `Utf8JsonWriter`) — o alvo desta ADR acrescenta os itens 1–3 (NFC, decimais, instantes) e a persistência de bytes canônicos como artefato de primeira classe (itens 6–7), hoje ausente porque o hash de `ObrigatoriedadeLegal` nunca precisou ser persistido como `bytea` — ele é recalculado a partir da entidade viva. A implementação segue precedente de canonicalização de payload polimórfico já validado pelo [ADR-0058](0058-obrigatoriedade-legal-validacao-data-driven.md).

## Consequências

### Positivas

- Hash estável entre representações Unicode equivalentes, precisões decimais e formatos de instante distintos — elimina a classe de falso-positivo mais comum em comparação de hash de texto administrativo em pt-BR.
- Blocos de dimensão ainda não implementados não bloqueiam a publicação incremental da Feature #40.
- `jsonb` permanece consultável por SQL sem decodificar bytes, mesmo com os bytes canônicos sendo a fonte de verdade do hash.
- Reaproveita um mecanismo já testado e usado em produção pelo módulo, em vez de duplicar lógica de canonicalização.

### Negativas

- Normalização NFC e arredondamento half-even são passos novos no `HashCanonicalComputer` — superfície de teste adicional em relação ao mecanismo atual.
- Decimais serializados como string (não `number`) no payload canônico podem exigir cast explícito em consultas SQL diretas sobre o `jsonb`.

### Neutras

- Um snapshot com bloco `{"status": "nao_construido"}` permanece assim para sempre — quando a dimensão for implementada, apenas snapshots futuros carregam o valor real; snapshots antigos não são recalculados retroativamente, coerente com o princípio de que o passado não é mutado.

## Confirmação

- Teste de unidade: a mesma configuração de negócio, serializada a partir de strings em NFD e em NFC, produz o mesmo `hash_configuracao`.
- Teste de unidade: variar apenas a representação decimal além da escala declarada do campo (ex.: `10.50` vs. `10.5`) produz o mesmo hash quando o valor lógico é igual.
- Teste de integração: o hash calculado pela aplicação no momento da publicação é igual ao `sha256` recalculado sobre os bytes lidos de volta de `configuracao_congelada_canonica`.
- Teste de integração: um bloco de dimensão ainda não implementada serializa como `{"status": "nao_construido"}` e não impede a transição do processo para publicado.

## Prós e contras das opções

### A — reaproveitar o computer como está, sem estender

- Bom, porque não exige nenhum trabalho novo.
- Ruim, porque não resolve NFC, decimais nem instantes — configurações logicamente iguais podem produzir hashes diferentes; e não atende ao requisito de bytes canônicos persistidos.

### B — estender o `HashCanonicalComputer` (escolhida)

- Bom, porque resolve as três ambiguidades identificadas reaproveitando um mecanismo já testado.
- Ruim, porque endurece o contrato do tipo estático existente, que passa a ter mais responsabilidades.

### C — adotar formato de canonicalização de terceiros (JCS/RFC 8785)

- Bom, porque é um padrão externo, documentado e interoperável.
- Ruim, porque JCS não resolve decimais com escala declarada por campo nem instantes sem fração de segundo — precisaria ser complementado de qualquer forma; introduz uma dependência externa sem necessidade quando o precedente interno já validado resolve o essencial.

### D — apenas `jsonb`, hash recalculado na consulta

- Bom, porque evita persistir uma coluna `bytea` adicional.
- Ruim, porque motores de banco podem reordenar ou renormalizar `jsonb` internamente entre versões — recalcular o hash a partir do `jsonb` quebra a garantia de reprodutibilidade que RN08 exige.

## Mais informações

- [ADR-0058](0058-obrigatoriedade-legal-validacao-data-driven.md) — precedente de hash canônico data-driven sobre payload polimórfico.
- [ADR-0061](0061-referencia-cross-modulo-via-snapshot-copy.md) — referência cross-módulo por snapshot-copy, base da distinção entre identidade de negócio estável e chave surrogate.
- [ADR-0063](0063-entidades-forensics-isentas-de-soft-delete.md) — `snapshot_publicacao` segue o mesmo princípio append-only.
- `src/selecao/Unifesspa.UniPlus.Selecao.Domain/ValueObjects/HashCanonicalComputer.cs` — mecanismo estendido por esta decisão.
- Issue #759 (Story) §3 e §8 — decisão originalmente fechada na modelagem, promovida a ADR por esta issue #783 (Task T2).
- Regra de negócio **RN08** (congelamento por snapshot).
