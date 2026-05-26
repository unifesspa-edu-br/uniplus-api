# Proposta de topologia — coabitação de Event Sourcing (Marten) com o backbone CRUD (EF Core) no uniplus-api

> Complemento ao [relatório do spike #540](adr-0069-event-sourcing-findings.md). Fecha a questão "host-level" do gate G2 da [ADR-0069](../adrs/0069-event-sourcing-seletivo-marten-contextos-criticos.md) no nível de **decisão de arquitetura** (a validação executável está especificada na §6).

## 1. Reenquadramento

O spike deixou "em aberto" rodar o outbox do Marten **junto** com o outbox EF Core num mesmo host, sob a hipótese de **um message store por host**. Essa hipótese era verdadeira **antes do Wolverine 5** — e o projeto já roda **Wolverine 5.39**.

- O issue [JasperFx/wolverine#1025](https://github.com/JasperFx/wolverine/issues/1025) ("mix & match message stores") foi **fechado**; o Wolverine 5 combina Marten, EF Core e múltiplos bancos no mesmo processo, com **inbox/outbox transacional por database**.
- O Marten 8 tem **projeções EF Core first-class** (`Marten.EntityFrameworkCore`): o `SaveChangesAsync` participa da transação do Marten (mesma conexão), tabelas migradas pelo Weasel.

Conclusão: **não é preciso microserviço.** A questão vira escolher a topologia mais limpa, não contornar um impedimento.

## 2. Princípio orientador

Separar três planos que costumam ser confundidos:

| Plano | Dono | Conteúdo |
|-------|------|----------|
| **Event store** | Marten | Fatos canônicos do agregado (append-only), por schema próprio |
| **Message store** | Wolverine | Estado operacional de entrega: inbox/outbox, retry, DLQ, sagas — **compartilhado** |
| **Integration events** | Tradução explícita | Contratos públicos derivados dos fatos, **nunca o fato cru** |

A topologia limpa para o UniPlus = **event store seletivo + message store operacional compartilhado + tradução explícita para eventos de integração**.

## 3. Topologia recomendada (Opção 1A)

Manter o **monólito modular num processo**. Adicionar Marten **apenas** para os agregados event-sourced (módulo crítico), em **schema Marten próprio** (event store). Manter um **único plano operacional de mensageria** no schema `wolverine` já existente sempre que o banco físico for o mesmo — preservando o backbone atual (`WolverineOutboxConfiguration`). Integração cross-módulo permanece **eventual, por eventos**.

Por quê: para um time pequeno (CTIC), múltiplos schemas de envelope multiplicam superfície operacional (agents, DLQ, replay, métricas, troubleshooting) sem ganho. Um event store dedicado + um message store operacional compartilhado é o ponto de equilíbrio.

> **A decidir explicitamente na validação:** se o store Marten consegue **enrolar no mesmo schema `wolverine`** (plano operacional único) junto do store EF Core, ou se exige schema separado. A §6 prova isso — a escolha é feita de olhos abertos, não por omissão.

## 4. Alternativas

- **Opção 2 — read models do ES via projeções EF Core do Marten.** Tecnicamente elegante (`EfCoreSingleStreamProjection` + `AddEntityTablesFromDbContext`, atômico **só inline**). **Não default**: começar com projeções/documentos Marten; adotar EF Core projections só com necessidade relacional concreta (consulta complexa, reporting SQL, tabela versionada por projeção).
- **Opção 3 — host/serviço dedicado ao ES.** Só sob requisito de **isolamento forte** (deploy/escala independentes, blast radius). Maior custo operacional.
- **Opção 4 — Marten só como event store, outbox único EF Core** (`SessionOptions.ForTransaction`). Parece conservadora, mas é **menos limpa**: perde o Aggregate Handler Workflow, exige plumbing de conexão/transação EF×Marten e aumenta a chance de bugs discretos. Manter como **fallback operacional/político**, não como recomendação técnica.

## 5. Riscos operacionais (não subestimar)

1. **Durability agents + leader election.** Wolverine roda um durability agent por message store, com leader election em produção. O spike usa `DurabilityMode.Solo` — **não prova comportamento de cluster**. A validação precisa de ≥2 réplicas.
2. **DLQ e replay deixam de ser "um lugar só"** se houver múltiplos stores. O runbook precisa saber onde consultar/reprocessar.
3. **Sem ordering global cross-store.** Entre evento vindo do EF e evento vindo do Marten, tratar como eventual, **idempotente**, sem ordenação global.
4. **Migrations: Weasel × EF Core.** Marten/Weasel e EF migrations coexistem; **não misturar** a posse de um mesmo schema. Respeitar a política de migrations/host startup já canônica no projeto (ADR-0039/0054).

## 6. O que a validação DEVE provar (fecha o G2 a 100%)

> **Status: viabilidade da topologia PROVADA; hardening operacional pendente.** Topologia comprovada: **EF Core 'main' + Marten 'ancillary'** (`AddMartenStore<IEditalEsStore>().IntegrateWithWolverine()`), compartilhando o envelope storage operacional. A config ingênua (dois 'main') falha com `InvalidWolverineStorageConfigurationException` — erro acionável que nomeia o remédio. **Observação sobre o handler ancillary:** num host em que o Marten é apenas ancillary (sem `AddMarten` primário), a injeção de `IDocumentSession` via `[MartenStore]` **não resolve** a sessão (ver achado no item 11); o padrão adotado injeta o store marcador (`IEditalEsStore`) e abre a sessão explicitamente.
>
> Esta lista distingue o que os testes **já exercitam** do que ainda **falta** validar antes de produção (✅ provado · ⟳ pendente). O essencial de viabilidade — coexistência dos stores + cluster com failover — está ✅; os itens ⟳ são hardening operacional, a fazer no piloto.

A validação cobre, num **host único com EF Core produtivo + Marten integrado**:

1. ✅ um handler EF e um append Marten no mesmo processo, com atomicidade local (`CoexistenciaTests`);
2. ⟳ rollback EF não grava envelope / rollback Marten não grava evento nem envelope (no co-host — ainda não exercitado aqui; o rollback isolado está provado em `AtomicidadeTests`);
3. ✅ ausência de colisão de schema (boot provisiona os dois stores — `CoexistenciaBootTests`);
4. ⟳ **recovery após crash** entre commit e dispatch (não exercitado);
5. ⟳ Kafka/PG indisponível → retry e depois entrega (não exercitado);
6. ⟳ **DLQ/replay** observável no store correto (não exercitado);
7. ✅ **≥2 réplicas em `Balanced`** exercitam leader election + failover + ejeção (`MultiNoLeaderElectionTests`);
8. ✅ **nenhuma** promessa de transação cross-store (por design; não há handler abrangendo dois stores);
9. ⟳ migrations aplicadas pela ferramenta dona do schema, sem conflito EF×Weasel (no spike os schemas são auto-criados; a posse em produção segue ADR-0039/0054 — não exercitado como teste);
10. ⟳ se adotar projeção EF Core do Marten: provar inline-atômico, async-eventual, rebuild e drift de schema (Opção 2, não adotada no spike);
11. ✅ **escala horizontal — concorrência otimista por stream**, em dois níveis:
    - **Mecanismo** (`ConcorrenciaStreamTests`): dois writers no mesmo stream (a concorrência é imposta no **banco**, independe do processo) — o perdedor leva `EventStreamUnexpectedMaxEventIdException`, faz retry com estado fresco, nada perdido.
    - **Ponta a ponta no host ES** (`EscalaDuasReplicasTests`): **duas réplicas da mesma API em ≥2 `IHost` `Balanced`** (host ES-only, Marten primário — `ConfiguracaoSpike`) sobre um Postgres recebem `RetificarEdital` concorrentes para o mesmo stream; o handler `[WriteAggregate]` processa e o **Wolverine retenta automaticamente** os conflitos (política `OnException<EventStreamUnexpectedMaxEventIdException>().RetryWithCooldown`) → **todas** as retificações convergem, sem lost update.
    - É serialização por-stream (não lock global), então escala. Pré-requisitos de produção: instâncias em `Balanced` (não `Solo`) e schema provisionado no deploy.
    - ✅ **Ponta a ponta na topologia CO-HOSPEDADA, com processos reais** (`EscalaCoHospedadaProcessosTests`): **duas instâncias da API em processos/portas separados** (host executável dedicado, EF main + Marten ancillary, `Balanced`) sobre um Postgres retificam o mesmo stream concorrentemente via HTTP → todas convergem, sem lost update. Processos separados = caches de code-gen JasperFx independentes.
    - **Achado** (corrige a conclusão anterior): a falha do handler ancillary `[MartenStore]` **não** era só artefato in-process — num host em que o Marten é **apenas ancillary** (sem `AddMarten` primário), a injeção de `IDocumentSession` via `[MartenStore]` **não resolve**. Padrão robusto adotado: o handler injeta o **store marcador** (`IEditalEsStore`) e abre a sessão explicitamente; o conflito do `FetchForWriting` é retentado pela política `OnException<EventStreamUnexpectedMaxEventIdException>()`.

## 6.1. Como testar 2 réplicas + leader election (com Docker)

A eleição de líder do Wolverine é **coordenada pelo banco** (bully algorithm + advisory lock + tabela `wolverine_nodes` com heartbeat), não pela rede. Logo a forma **fiel e determinística** de exercitar um cluster é:

- **Postgres compartilhado** via Testcontainers (Docker) — é o message store comum.
- **2 réplicas = 2 instâncias `IHost` no mesmo processo de teste**, ambas em `DurabilityMode.Balanced` (não `Solo`), apontando para o mesmo banco + schema `wolverine`. Para o Wolverine, cada `IHost` é um **nó distinto** com heartbeat próprio — é cluster real, não simulação.
- **Tuning de teste:** reduzir os intervalos de heartbeat/health-check/reatribuição (ex.: `opts.Durability.*PollingTime` na casa de ~1s) para a eleição e o failover ocorrerem em segundos; asserts com timeout generoso.

> Uma variante com **docker-compose (2 contêineres da API + Postgres)** é possível como smoke operacional de processo/SO, mas **não fortalece a prova de eleição** (que é coordenada pelo banco) e é mais lenta e sujeita a flakiness. Fica como opcional, não como a validação canônica.

**Asserções do teste multi-nó:**

1. **Registro:** ambos os nós aparecem em `wolverine_nodes` (contagem = 2); exatamente um detém o agente de liderança (`wolverine://leader` em `wolverine_node_assignments`).
2. **Exactly-once no cluster:** publicar N mensagens duráveis com os 2 nós ativos → o coletor recebe N, sem duplicatas (competing consumers correto).
3. **Failover:** parar o nó líder → o sobrevivente assume a liderança dentro de T; o nó parado é ejetado (contagem = 1); o processamento continua.
4. **Recovery pós-crash:** enfileirar trabalho durável/agendado e derrubar o líder antes do dispatch → o novo líder recupera e entrega (sem perda, sem duplicata).

**Detecção do líder:** consultar `wolverine.wolverine_node_assignments` pelo agente de liderança (nome exato descoberto via `information_schema` em runtime, para não acoplar a internals); na ausência do nome esperado, degradar para a asserção operacionalmente equivalente "o cluster mantém um líder e segue processando após o failover".

Pré-requisito de ambiente: módulo `veth` carregado para o Testcontainers (kernel atual).

## 7. Impacto em ADRs

- **ADR-0069:** registrar a topologia escolhida (Opção 1A) como condição do piloto.
- **ADR-0004:** emendar o enquadramento "outbox único" para "**outbox por store / message storage operacional compartilhado** (Wolverine 5)".
- Avaliar **nova ADR** dedicada à topologia de coabitação event store × message store.

## 8. Fontes

- [Wolverine #1025 — mix & match message stores](https://github.com/JasperFx/wolverine/issues/1025)
- [Jeremy Miller — Wolverine 5 and Modular Monoliths (out/2025)](https://jeremydmiller.com/2025/10/27/wolverine-5-and-modular-monoliths/)
- [Wolverine — Ancillary Marten Stores](https://wolverinefx.net/guide/durability/marten/ancillary-stores)
- [Wolverine — Leader Election](https://wolverinefx.net/tutorials/leader-election)
- [Wolverine — EF Core / Weasel migrations](https://wolverinefx.net/guide/durability/efcore/migrations)
- [Marten — EF Core projections](https://martendb.io/events/projections/efcore)
