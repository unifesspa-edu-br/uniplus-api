---
status: "accepted"
date: "2026-06-17"
decision-makers:
  - "Tech Lead (CTIC)"
consulted: []
informed:
  - "Equipe Uni+"
---

# ADR-0092: Reference data do Geo sem soft-delete, recarregado por upsert

## Contexto e enunciado do problema

As entidades de localidade do módulo `Geo` ([ADR-0090](0090-modulo-geo-localidades.md)) são **reference data** de origem externa: o catálogo de municípios/UFs vem do IBGE e o endereçamento, do DNE (Diretório Nacional de Endereços, Correios). Esses dados não são criados nem editados por usuários do Uni+ — são **carregados por ETL** a partir de uma versão datada do dataset oficial e **recarregados** quando uma nova versão é publicada.

O Uni+ adota **soft-delete opt-in** ([ADR sobre `ISoftDeletable`](../../CLAUDE.md)): só entidades com exclusão lógica de negócio derivam de `SoftDeletableEntity` e carregam `is_deleted`. A pergunta é se as entidades do `Geo` devem ter soft-delete e qual a semântica de "remoção" de uma localidade que saiu do dataset oficial.

## Drivers da decisão

- **Origem externa autoritativa** — a fonte da verdade é o dataset oficial (IBGE/DNE), não o Uni+.
- **Recarga idempotente** — uma nova versão do dataset deve reconciliar o estado sem efeitos colaterais de exclusão lógica.
- **Sem exclusão de negócio** — não há "lixeira" nem auditoria de exclusão para um município; ele existe ou não no dataset vigente.
- **Coerência com o soft-delete opt-in** — não carregar a capability onde ela não faz sentido.
- **Proveniência** — rastrear de qual versão do dataset cada registro veio.

## Opções consideradas

- **A**: Entidades do `Geo` derivam de `SoftDeletableEntity` (com `is_deleted`), como as entidades de negócio.
- **B**: **Entidades do `Geo` derivam de `EntityBase` puro** (sem soft-delete); a recarga reconcilia por upsert na chave natural.

## Resultado da decisão

**Escolhida:** "B — `EntityBase` puro, sem soft-delete; recarga por upsert na chave natural", porque reference data de origem externa não tem exclusão lógica de negócio: o estado vigente é uma projeção da versão corrente do dataset oficial.

- As entidades de localidade derivam de **`EntityBase` puro** — identidade (Guid v7) + timestamps de auditoria, **sem** `is_deleted` e **sem** o filtro global de soft-delete.
- A carga/recarga do ETL **reconcilia por upsert na chave natural** (ex.: código IBGE do município): insere o que é novo, atualiza o que mudou. Registros ausentes da nova versão são tratados pela política de proveniência do ETL (marcação de vigência por versão de dataset), não por exclusão lógica genérica.
- Cada registro carrega a **proveniência** (versão do dataset e indicador de vigência), de modo que a recarga é auditável e reproduzível sem `is_deleted`.

## Consequências

### Positivas

- Modelo fiel à natureza do dado: o `Geo` projeta o dataset oficial vigente, sem semântica de lixeira.
- Recarga idempotente e auditável por versão de dataset.
- Coerência com o soft-delete opt-in — a capability não é carregada onde não agrega.

### Negativas

- "Remoção" de uma localidade exige uma política de vigência no ETL (não há `is_deleted` para esconder linhas) — responsabilidade que passa para o pipeline de carga.

### Neutras

- A modelagem concreta das entidades de localidade e do versionamento de dataset entra nas Stories de domínio/ETL do Epic; esta ADR fixa apenas a **isenção de soft-delete** e a **reconciliação por upsert**.

## Confirmação

- **Fitness test**: `SoftDeleteOptInConventionTests` inclui o `GeoDbContext` e confirma que nenhuma entidade do `Geo` mapeia `is_deleted` nem aplica o filtro de soft-delete.
- **ETL**: a recarga de uma nova versão do dataset reconcilia por upsert na chave natural sem apagar fisicamente o histórico de proveniência.

## Prós e contras das opções

### A — `SoftDeletableEntity` (com soft-delete)

- Bom, porque reaproveita a convenção das entidades de negócio.
- Ruim, porque introduz semântica de exclusão lógica que não existe para reference data; uma localidade não é "excluída por um usuário", ela sai do dataset oficial.

### B — `EntityBase` puro + upsert (escolhida)

- Bom, porque modela fielmente reference data de origem externa e torna a recarga idempotente e auditável por versão.
- Ruim, porque transfere a política de vigência/remoção para o ETL, que precisa tratá-la explicitamente.

## Mais informações

- Aplica-se às entidades do módulo `Geo` ([ADR-0090](0090-modulo-geo-localidades.md)); o mecanismo de georreferência é fixado na [ADR-0091](0091-postgis-georreferencia-nts.md).
- Coerente com o soft-delete opt-in (`ISoftDeletable`) do projeto: a capability fica apenas onde há exclusão lógica de negócio.
- A entidade-sonda transitória do scaffold já segue esta decisão (deriva de `EntityBase`), e é substituída pelas entidades reais nas Stories de domínio.
