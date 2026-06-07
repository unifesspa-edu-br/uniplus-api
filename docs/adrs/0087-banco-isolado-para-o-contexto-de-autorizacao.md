---
status: "proposed"
date: "2026-06-02"
decision-makers:
  - "Tech Lead (CTIC)"
consulted: []
informed:
  - "Equipe Uni+"
---

# ADR-0087: Banco isolado para o contexto de autorização

## Contexto e enunciado do problema

A [ADR-0054](0054-naming-convention-e-strategy-migrations.md) estabeleceu bancos PostgreSQL **isolados por módulo**: cada bounded context tem o seu banco, e referências entre contextos não cruzam o limite por chave estrangeira.

A autorização, nesta frente, ganha um conjunto próprio de entidades persistidas: concessões excepcionais, escopos de auditoria, atuação institucional, vínculos de grupo, base legal de tratamento e a trilha de auditoria. A autorização **não é um módulo de domínio** como Seleção ou Ingresso — é um contexto **transversal** (cross-cutting), consumido por todos os módulos. A pergunta é **onde essas entidades persistem**: espalhadas pelos bancos dos módulos de domínio, num *schema* dentro de um banco compartilhado, ou num banco próprio.

## Drivers da decisão

- **Isolamento por contexto** — coerência com a ADR-0054 (cada contexto, seu banco).
- **Autz é transversal** — não pertence a nenhum módulo de domínio específico.
- **Integridade referencial intra-contexto** — as entidades de autorização se relacionam entre si por chave estrangeira.
- **Isolamento de segurança** — dados de autorização e a trilha de auditoria com fronteira própria.

## Opções consideradas

- **A**: Distribuir as entidades de autorização pelos bancos dos módulos de domínio.
- **B**: **Banco isolado dedicado** para o contexto de autorização.
- **C**: *Schema* dedicado dentro de um banco compartilhado.

## Resultado da decisão

**Escolhida:** "B — banco isolado dedicado de autorização", porque é a aplicação direta do isolamento por contexto da ADR-0054 a um contexto que é transversal e tem entidades próprias com integridade referencial entre si.

- As entidades de autorização vivem em um **banco isolado** dedicado ao contexto. As chaves estrangeiras **entre elas** são intra-banco (normais).
- Referências a entidades de **outros contextos** — `Unidade`, `Candidato` — entram por **identificador** (Guid v7), resolvidas por **leitor read-side** ([ADR-0056](0056-parametrizacao-modulo-e-read-side-carve-out.md)), **sem** chave estrangeira cruzando banco (conforme ADR-0054).
- A **trilha de auditoria forense** ([ADR-0063](0063-entidades-forensics-isentas-de-soft-delete.md)) reside nesse banco, com a fronteira de isolamento própria do contexto.

## Consequências

### Positivas

- Topologia coerente com o isolamento por contexto já adotado no projeto.
- Integridade referencial entre as entidades de autorização é garantida intra-banco.
- A fronteira própria facilita o controle de acesso e a operação da trilha de auditoria.

### Negativas

- Um banco a mais para provisionar, migrar e operar.
- Referências a `Unidade`/`Candidato` são por identificador (via leitor), não por chave estrangeira — consistência garantida na aplicação, não pelo banco.

### Neutras

- A nomenclatura do banco, as migrations e a configuração seguem a própria ADR-0054; esta ADR fixa apenas **que** o contexto de autorização tem **banco isolado**.

## Confirmação

- **Fitness test**: nenhuma entidade de autorização tem chave estrangeira para tabela de outro banco; referências externas são por identificador, resolvidas por leitor read-side.
- **Teste de migração**: o banco de autorização é criado e migrado isoladamente, sem dependência de schema de outro contexto.

## Prós e contras das opções

### A — Distribuir nas bases dos módulos de domínio

- Bom, porque reaproveita bancos existentes.
- Ruim, porque a autorização é transversal e não cabe em um módulo; espalhar suas entidades quebra a coesão do contexto e dificulta a integridade referencial entre elas.

### B — Banco isolado dedicado (escolhida)

- Bom, porque aplica o isolamento por contexto, garante integridade intra-contexto e dá fronteira própria à trilha de auditoria.
- Ruim, porque é mais um banco a operar e as referências externas passam a ser por identificador.

### C — *Schema* dentro de um banco compartilhado

- Bom, porque evita provisionar um banco novo.
- Ruim, porque tensiona o isolamento por contexto da ADR-0054 e mistura, num mesmo banco, dados de contextos que a decisão de isolamento quis separar.

## Mais informações

- Aplica a [ADR-0054](0054-naming-convention-e-strategy-migrations.md) (bancos isolados por módulo) ao contexto transversal de autorização.
- As referências cross-contexto seguem a [ADR-0056](0056-parametrizacao-modulo-e-read-side-carve-out.md) (leitor read-side) e a regra de não-FK cross-banco da ADR-0054.
- A trilha de auditoria forense isolada segue a [ADR-0063](0063-entidades-forensics-isentas-de-soft-delete.md).
- Este banco persiste as entidades decididas na [ADR-0084](0084-concessao-excepcional-e-atuacao-institucional-server-side.md) e na [ADR-0086](0086-trilha-de-auditoria-com-hmac-e-cofre.md), entre outras desta frente.
