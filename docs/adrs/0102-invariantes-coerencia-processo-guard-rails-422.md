---
status: "accepted"
date: "2026-07-07"
decision-makers:
  - "Tech Lead"
---

# ADR-0102: Invariantes de coerência de processo como guard rails no banco, mapeadas a HTTP 422

> **Emenda (ADR-0104):** a decisão — violação de guard rail de banco vira `DomainError` nomeado e 422, nunca 500 — permanece integralmente vigente, e ganhou guard rails novos (os da tabela `versoes_configuracao`). O que caducou foi um dos **exemplos**: a unicidade de `data_publicacao` entre editais do mesmo processo deixou de existir, e com ela o `Edital.DataPublicacaoDuplicada`. Aquela trava não era invariante de negócio — servia só para dar ordem total entre editais, papel que agora cabe a `UNIQUE(processo, numero_versao)` sobre as versões. Dois atos publicados no mesmo instante, e a retificação que republica a data do ato original, passam a ser estados válidos. Ver [ADR-0104](0104-versao-configuracao-como-agregado-proprio.md).

## Contexto e enunciado do problema

A publicação e a retificação do `ProcessoSeletivo` ([ADR-0100](0100-canonicalizacao-hash-snapshot-publicacao.md), [ADR-0101](0101-retificacao-novo-edital-novo-snapshot-motivo.md)) introduzem invariantes de coerência enforçadas por constraints e índices únicos parciais no banco — por exemplo, a unicidade do Edital de abertura por processo, a linearidade da cadeia de retificação, a constraint de contrato abertura×retificação, e a trava contra mutação direta de conteúdo congelável após a publicação. Quando uma dessas travas dispara, a violação não pode surfar como um erro genérico de infraestrutura (HTTP 500) — é uma regra de negócio sendo enforçada, ainda que o ponto físico de detecção seja o banco, não uma checagem em memória antes do save.

O módulo já tem uma convenção estabelecida: toda violação de regra de negócio é `DomainError`, mapeada por um par (chave `Contexto.CausaPascalCase`, `DomainErrorMapping(status, wire code, título)`) registrado em `SelecaoDomainErrorRegistration`, seguindo o mecanismo cross-module `IDomainErrorMapper`/`DomainErrorMapping` já decidido pelo [ADR-0024](0024-mapeamento-domain-error-http.md). Falta formalizar que essa mesma convenção — e o mesmo status HTTP — vale quando o ponto de detecção da violação é uma constraint de banco, e não deixar essa decisão implícita a cada nova trava que o ciclo de publicação introduzir.

## Drivers da decisão

- **Consistência com o mecanismo já decidido.** O [ADR-0024](0024-mapeamento-domain-error-http.md) já estabeleceu que todo `DomainError` ganha um `DomainErrorMapping` registrado — não se justifica uma segunda convenção para erros cuja origem física é uma constraint de banco.
- **Nunca vazar exceção de infraestrutura como 500 quando a causa é regra de negócio.** Uma `DbUpdateException`/exceção de violação de constraint que representa uma regra de negócio esperada não é uma falha de infraestrutura.
- **Previsibilidade do catálogo de erros.** O módulo Seleção já usa 422 como status dominante para violação de regra de negócio — a quase totalidade dos mapeamentos existentes em `SelecaoDomainErrorRegistration` usa `Status422UnprocessableEntity`. Introduzir 409 ou outro status para este subconjunto quebraria essa previsibilidade sem ganho correspondente.
- **Nome canônico único e estável por causa.** Cada violação tem uma única chave e um único wire code — nunca variações do mesmo erro sob nomes distintos.

## Opções consideradas

- **A.** Capturar a exceção de violação de constraint no boundary de persistência (repository/Unit of Work), traduzi-la para um `DomainError` nomeado e mapeá-la via `DomainErrorMapping` registrado em `SelecaoDomainErrorRegistration` com status 422 — mesma convenção usada pelo resto do módulo.
- **B.** Deixar a exceção de banco propagar e tratá-la genericamente no middleware de exceção como 500, com log estruturado da causa.
- **C.** Duplicar cada invariante como validação em memória no domínio antes do save, tratando a constraint de banco apenas como defesa em profundidade silenciosa, sem mapeamento a `DomainError`.
- **D.** Mapear violação de constraint para HTTP 409 Conflict, em vez de 422.

## Resultado da decisão

**Escolhida:** "A — traduzir a violação no boundary para `DomainError` nomeado, mapeado a 422 registrado em `SelecaoDomainErrorRegistration`", porque é a única opção consistente com o mecanismo cross-module do ADR-0024 e com o status dominante já em uso no módulo, sem introduzir uma segunda convenção de tratamento de erro.

Esta decisão **não substitui** validação em memória no domínio antes do save (Opção C) — as duas são complementares: o domínio valida em memória tudo que puder antes de persistir; a constraint de banco é a última linha de defesa contra um gap de validação ou uma condição de corrida não coberta em memória. O que esta ADR decide é o que acontece quando a constraint dispara — seja porque a validação em memória tinha uma lacuna, seja por concorrência —, e a resposta é sempre a mesma: `DomainError` nomeado, nunca 500.

### Forma do contrato

- Toda violação de guard rail de banco relevante ao ciclo de publicação — unicidade do Edital de abertura por processo, cadeia de retificação linear, contrato abertura×retificação, numeração e vigência das versões da configuração, trava de mutação de conteúdo congelável pós-publicação — é traduzida, no boundary de persistência (repository/Unit of Work, [ADR-0042](0042-application-nao-depende-diretamente-de-dbcontext.md)), de exceção de infraestrutura para `Result.Failure(DomainError)` nomeado.
- Todo `DomainError` segue o par já estabelecido: chave `Contexto.CausaPascalCase` (ex.: `Snapshot.VigenteAusente`) + `DomainErrorMapping(status, wire code, título)` registrado em `SelecaoDomainErrorRegistration` — mesmo arquivo e mesmo mecanismo já usado pelos demais mapeamentos do módulo.
- **Status: 422 Unprocessable Entity** para toda esta categoria — não 409, não 500. É o status já dominante do módulo para "requisição bem formada que viola uma regra de negócio".
- **Wire code:** `uniplus.selecao.<contexto_snake_case>.<causa_snake_case>` — mesma taxonomia usada por todo o catálogo do módulo.
- **Resposta:** ProblemDetails RFC 9457, conforme já decidido pelo [ADR-0023](0023-wire-formato-erro-rfc-9457.md) — esta ADR não redecide o wire format; decide apenas a origem física do erro (constraint de banco) e seu destino (422 nomeado e registrado).

### Caso concreto de referência

`Snapshot.VigenteAusente` · wire code `uniplus.selecao.snapshot.vigente_ausente` · título "Nenhuma publicação vigente para o instante" · status **422**. É o erro do seletor de configuração vigente ([ADR-0075](0075-snapshot-do-ato-resolvido-no-instante.md), [ADR-0104](0104-versao-configuracao-como-agregado-proprio.md)) quando não há versão da configuração com `vigente_a_partir_de` ≤ o instante consultado — nome canônico único, sem variações, distinto do erro de conformidade de edital ainda não publicado.

## Consequências

### Positivas

- Nenhuma violação de invariante de coerência de processo surfa como 500 genérico.
- O catálogo de erros do módulo permanece a fonte única e auditável, sem uma segunda convenção para erros originados no banco.
- Consistente com o padrão já observável em ~90 mapeamentos existentes em `SelecaoDomainErrorRegistration`.

### Negativas

- Cada constraint nova exige uma tradução explícita no boundary de persistência — a constraint de banco não carrega, por si, o wire code ou o título; alguém precisa escrever esse mapeamento a cada invariante nova.

### Neutras

- A tradução no boundary depende de reconhecer a constraint física (nome do índice/constraint) que disparou — acoplamento local ao schema, aceitável porque o boundary de persistência já é a camada que conhece esse schema ([ADR-0042](0042-application-nao-depende-diretamente-de-dbcontext.md)).

## Confirmação

- Teste de integração: publicar um segundo Edital de abertura no mesmo processo retorna 422 nomeado, nunca 500.
- Teste de integração: tentar mutação direta de conteúdo congelável de um processo publicado retorna 422 nomeado.
- Teste de unidade: o seletor de configuração vigente sem publicação retorna exatamente `Snapshot.VigenteAusente`.
- Fitness test de cobertura do registry ([ADR-0024](0024-mapeamento-domain-error-http.md)): todo `DomainError` novo introduzido pelo ciclo de publicação está registrado em `SelecaoDomainErrorRegistration`.

## Prós e contras das opções

### A — tradução no boundary para `DomainError` 422 registrado (escolhida)

- Bom, porque reaproveita o mecanismo cross-module já decidido e mantém o catálogo de erros como fonte única.
- Ruim, porque exige escrever a tradução explícita para cada constraint nova.

### B — deixar propagar como 500 genérico

- Bom, porque não exige nenhum código de tradução.
- Ruim, porque expõe uma regra de negócio esperada como falha de infraestrutura — o cliente não recebe um erro acionável, e o catálogo público de erros fica incompleto.

### C — validação em memória apenas, sem mapear a constraint

- Bom, porque cobre o caso comum sem depender do banco.
- Ruim, porque não cobre o gap de concorrência — a constraint dispara sem tradução e o resultado é 500, exatamente o cenário que este ADR decide evitar.

### D — mapear a 409 Conflict

- Bom, porque 409 é a semântica HTTP genérica para conflito de estado.
- Ruim, porque introduziria uma segunda semântica de status para o mesmo tipo de falha de regra de negócio já tratado como 422 no restante do módulo, sem ganho correspondente.

## Mais informações

- [ADR-0024](0024-mapeamento-domain-error-http.md) — mecanismo cross-module `IDomainErrorMapper`/`DomainErrorMapping` que este ADR reaplica à origem "constraint de banco".
- [ADR-0023](0023-wire-formato-erro-rfc-9457.md) — wire format RFC 9457, não redecidido aqui.
- [ADR-0042](0042-application-nao-depende-diretamente-de-dbcontext.md) — boundary de persistência (repository/Unit of Work) onde a tradução acontece.
- [ADR-0075](0075-snapshot-do-ato-resolvido-no-instante.md) — caso concreto de referência `Snapshot.VigenteAusente`.
- `src/selecao/Unifesspa.UniPlus.Selecao.API/Errors/SelecaoDomainErrorRegistration.cs` — registry onde os novos mapeamentos entram.
- Issue #759 (Story) §3 e §8 — decisão originalmente fechada na modelagem, promovida a ADR por esta issue #783 (Task T2).
