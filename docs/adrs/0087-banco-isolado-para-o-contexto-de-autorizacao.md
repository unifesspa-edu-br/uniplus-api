---
status: "accepted"
date: "2026-10-28"
decision-makers:
  - "Tech Lead (CTIC)"
consulted: []
informed:
  - "Equipe Uni+"
---

# ADR-0087: Persistência da autorização no schema `autorizacao` do banco `uniplus`

## Contexto e enunciado do problema

A autorização passa a gravar entidades próprias: perfis, concessões, pedidos e aprovações, instalação e, mais adiante, a trilha de auditoria ([ADR-0152](0152-governanca-das-concessoes-de-acesso.md)). Ela é transversal, consumida por todos os módulos, e não pertence a nenhum módulo de domínio.

A versão anterior desta ADR propunha um banco isolado para a autorização. Depois dela, a [ADR-0097](0097-topologia-de-deploy-em-tres-apis-monolito-modular.md) adotou, para o monólito, o banco único `uniplus` com um schema por módulo e uma única instância Wolverine com outbox. O problema é onde a autorização grava, de modo que a gravação da concessão e o evento dela entrem na mesma transação, sem acoplar a autorização ao schema dos módulos de domínio.

## Drivers da decisão

- **Atomicidade:** gravar a concessão e publicar o evento dela na mesma transação do outbox ([ADR-0004](0004-outbox-transacional-via-wolverine.md)).
- **Coerência com a topologia do monólito** (ADR-0097).
- **Integridade referencial entre as entidades da própria autorização.**
- **Fronteira com os módulos:** a autorização não depende relacionalmente das tabelas de Seleção, Ingresso ou dos cadastros.

## Opções consideradas

- **A**: Banco isolado dedicado à autorização.
- **B**: Schema `autorizacao` no banco `uniplus`.
- **C**: Entidades da autorização distribuídas pelos schemas dos módulos de domínio.

## Resultado da decisão

**Escolhida:** "B — schema `autorizacao` no banco `uniplus`", porque mantém a gravação e o evento numa única transação do outbox do monólito e segue o mesmo padrão de schema por módulo da ADR-0097.

- A autorização tem um `DbContext` próprio, com `HasDefaultSchema("autorizacao")`, composto pelo host como os demais módulos internos, e migrations próprias aplicadas na subida.
- As chaves estrangeiras entre entidades da autorização ficam dentro do schema `autorizacao`.
- Referências a outros contextos (unidade, processo seletivo, candidato) entram por identificador, resolvidas por leitor ([ADR-0056](0056-modulo-configuracao-e-read-side-via-reader.md)). Não há chave estrangeira do schema `autorizacao` para o schema de outro módulo.
- A autorização usa o mesmo banco e a mesma instância Wolverine do monólito; não há outbox próprio.
- A trilha de auditoria forense ([ADR-0086](0086-trilha-de-auditoria-com-hmac-e-cofre.md)) não é decidida aqui: a fronteira dela (retenção, backup, confidencialidade) depende da encarregada de dados.

## Consequências

### Positivas

- A concessão e o evento dela são gravados de forma atômica.
- Nenhum banco novo para provisionar, migrar e operar.
- Integridade referencial entre as entidades da autorização garantida pelo banco.

### Negativas

- O isolamento é de schema, não de banco: a mesma role do monólito lê e escreve no schema `autorizacao`.
- Referências a unidade e candidato são por identificador, com consistência garantida pela aplicação.

### Neutras

- O nome do schema e das migrations segue a [ADR-0054](0054-naming-convention-e-strategy-migrations.md).

## Confirmação

- **Teste de arquitetura:** nenhuma entidade da autorização referencia tipo de domínio de outro módulo.
- **Teste de integração:** as migrations da autorização criam o schema `autorizacao` sem chave estrangeira para outro schema, e a gravação de uma concessão com o evento dela confirma ou desfaz os dois juntos.

## Prós e contras das opções

### A — Banco isolado

- Bom, porque dá fronteira própria à autorização.
- Ruim, porque separa a gravação do outbox do monólito: a concessão e o evento dela deixam de ser atômicos, e há um banco a mais para operar.

### B — Schema `autorizacao` no banco `uniplus` (escolhida)

- Bom, porque preserva a transação única e segue o padrão do monólito.
- Ruim, porque o isolamento é só de schema.

### C — Distribuir pelos schemas dos módulos

- Bom, porque não cria nada novo.
- Ruim, porque a autorização é transversal e perderia a integridade referencial entre as próprias entidades.

## Mais informações

- **Reescrita em 2026-10-28:** a versão proposta em 2026-06-02 escolhia o banco isolado, antes da ADR-0097.
- **Pendente:** role própria para o schema `autorizacao`, com privilégios separados da role do monólito. Hoje as migrations rodam na subida com a mesma role da aplicação; separar privilégios pede rodar as migrations fora da subida, com uma role proprietária do schema.
- **Pendente:** a fronteira da trilha forense, a decidir com a encarregada de dados.
