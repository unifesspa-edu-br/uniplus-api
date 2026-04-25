# Plano de validação do outbox Wolverine

Plano técnico para validar a Story #158 antes de adotar outbox transacional de
domain events no UniPlus.

## Objetivo

Validar, com evidências locais e repetíveis, se Wolverine consegue entregar o
invariante exigido pelo projeto:

> Ao salvar uma entidade que gerou `EntityBase.DomainEvents`, a mudança de banco
> e o registro durável da mensagem devem confirmar ou falhar juntos.

Este plano deve ser aprovado antes de qualquer implementação produtiva. Spikes e
testes podem ser criados somente depois da aprovação deste documento.

## Escopo

O plano cobre:

- Drenagem de `EntityBase.DomainEvents` via `PublishDomainEventsFromEntityFrameworkCore`.
- Persistência transacional de envelopes com EF Core + PostgreSQL.
- Transporte PostgreSQL do Wolverine.
- Transporte Kafka, usando a infraestrutura Docker já existente no projeto.
- Rollback e ausência de mensagens fantasmas.
- Recuperação após falha ou restart do host.
- Estratégia de retry EF vs retry Wolverine.
- Schema, tabelas e migration auditável do Wolverine.
- Observabilidade operacional: retries, dead letters, ownership, replay e limpeza.

Fora do escopo desta fase:

- Implementar handlers de negócio reais baseados em domain events.
- Atualizar `Program.cs` de produção para declarar outbox entregue.
- Atualizar `CLAUDE.md`, ADRs ou guia Wolverine como decisão final.
- Otimizações de performance e tuning fino de throughput.
- Sagas, process managers e scheduled messages.
- Outros transportes externos além de Kafka.

## Snapshot da versão em validação

| Item | Valor |
|---|---|
| Wolverine base | `5.32.1-pr2586` |
| Fonte | Feed local `vendors/nuget-local` via `nuget.config` |
| Motivo | Incorpora o fix de `DomainEventScraper` do PR JasperFx/wolverine#2586 antes de release oficial |
| Pacotes locais | `WolverineFx`, `WolverineFx.EntityFrameworkCore`, `WolverineFx.RDBMS` |
| Pacote PostgreSQL | `WolverineFx.Postgresql` oficial, se necessário para transporte/persistência PostgreSQL |
| Plano de reversão | Voltar para pacote oficial quando upstream publicar versão `5.32.2+` contendo o fix |

Se S2 ou S3 exigirem `WolverineFx.Postgresql`, há risco de conflito entre o
pacote oficial `5.32.1` e `WolverineFx.RDBMS` `5.32.1-pr2586` do feed local.
Nesse caso, gerar também `WolverineFx.Postgresql` a partir do mesmo branch do
fork e publicar o pacote no feed local antes de interpretar erros NuGet como
falha funcional do spike.

Enquanto o fix não estiver em versão oficial, qualquer troca de
`Directory.Packages.props` para `5.32.1` oficial sem patch deve ser tratada como
regressão técnica.

### Critério de saída do feed local

O feed local `wolverine-pr2586` só deve ser removido quando todos os critérios
abaixo forem verdadeiros:

- Upstream publicou versão oficial `5.32.2+` contendo o fix do
  `DomainEventScraper`.
- `Directory.Packages.props` aponta para a versão oficial.
- `nuget.config` não precisa mais do feed `vendors/nuget-local`.
- `dotnet restore` funciona sem o feed local.
- Builds Docker das APIs passam sem copiar `vendors/nuget-local`.
- Suite completa de testes do projeto passa na versão oficial.
- Os spikes aprovados deste plano continuam verdes.

Antes disso, o feed local é parte da evidência técnica da #158.

## Premissas

- A versão `5.32.1-pr2586` está disponível no feed local em `vendors/nuget-local`.
- A issue #158 continua aberta até AC1a, AC1b e AC2 ficarem verdes.
- A infraestrutura Docker local já inclui PostgreSQL e Kafka.
- A validação inicial deve usar o módulo `Selecao`, pois ele já tem
  `Edital.Publicar()` e `EditalPublicadoEvent`.
- `Ingresso` só deve receber replicação depois de uma estratégia aprovada em
  `Selecao`.

## Regra de bloqueio

Não adotar Wolverine como outbox transacional enquanto estes critérios não
estiverem comprovados:

- **AC1a - persistência:** comando via `ICommandBus.Send` altera entidade, gera
  `AddDomainEvent` e persiste envelope durável na mesma transação do
  `SaveChanges`, observável em `wolverine_outgoing_envelopes` ou tabela
  equivalente do transporte.
- **AC1b - entrega:** depois do commit, a mensagem chega ao destino configurado:
  tópico Kafka em S3 ou queue PostgreSQL em S2. Consumer de teste recebe
  exatamente uma mensagem por comando.
- **AC2 - rollback:** exceção depois de `AddDomainEvent` e antes do commit
  deixa entidade ausente e mensagem ausente.
- **AC3 - recuperação:** mensagem pendente persiste após falha/restart e é
  reenviada/processada depois.
- **AC4 - migration:** todas as tabelas necessárias do Wolverine são
  versionadas por migration EF ou por SQL versionado explicitamente aprovado.
- **AC5 - retry:** retry EF/Npgsql e retry Wolverine não entram em conflito com
  a transação.

Risco de violar esta regra: código produtivo pode assumir despacho automático de
domain events enquanto eventos são silenciosamente perdidos ou enviados fora da
transação. Esta é a falha que a #135 expôs e que este plano deve impedir.

## Política dev/test vs produção

Em spikes e testes de integração, é aceitável usar mecanismos de provisionamento
automático do Wolverine/JasperFx, como `AddResourceSetupOnStartup`, para reduzir
atrito e observar rapidamente quais recursos são criados.

Em produção, o schema de outbox deve ser auditável e versionado. A decisão final
deve sair do caminho S8:

- migration EF via `MapWolverineEnvelopeStorage(modelBuilder, "wolverine")`; ou
- DbContext dedicado para schema Wolverine; ou
- SQL versionado fora do EF.

Configuração de fixture não deve ser copiada para `Program.cs` de produção sem
essa decisão.

## Matriz de validação

| ID | AC relacionado | Cenário | Pergunta | Configuração mínima | Evidência esperada | Decisão |
|---|---|---|---|---|---|---|
| S0 | AC1a parcial | Handler in-memory | O fix do scraper publica domain events sem `Collection was modified`? | `PublishDomainEventsFromEntityFrameworkCore` + handler local de teste | Handler recebe `EditalPublicadoEvent` e registra evidência de execução | Sanidade do fix |
| S1 | AC1a/AC1b exploratório | Fila local durável | Local queue durável serve para nosso invariante? | `PublishAllMessages().ToLocalQueue(...)` + `UseDurableLocalQueues()` | Confirmar se envelope é persistido e se some após processamento | Provavelmente não basta para entrega externa |
| S2 | AC1a/AC1b | Transporte PostgreSQL | Wolverine entrega outbox durável sem broker externo? | `UsePostgresqlPersistenceAndTransport(...)` + `ToPostgresqlQueue(...)` | Envelope durável observável e mensagem processada | Candidato principal sem infra externa |
| S3 | AC1a/AC1b | Transporte Kafka | Wolverine entrega outbox durável com Kafka do projeto? | Kafka Docker + `PublishAllMessages().ToKafkaTopic(...)` + durable outbox | Kafka recebe mensagem somente após commit | Candidato principal com infra real |
| S4 | AC2 | Rollback | Há mensagem fantasma quando o handler falha? | Repetir S2 e S3 com exceção após `SaveChanges` | Entidade ausente e mensagem ausente | Obrigatório para AC2 |
| S5 | AC3 | Kafka indisponível | Commit da entidade sobrevive se Kafka cair? | Kafka parado durante commit, depois religado | Entidade persiste, envelope fica pendente e é enviado quando Kafka volta | Obrigatório para robustez |
| S6 | AC3 | Restart recovery | Mensagem pendente é recuperada após restart? | Derrubar host antes do relay ou consumo | Novo host recupera envelope e entrega | Obrigatório para AC3 |
| S7 | AC5 | Retry strategy | `EnableRetryOnFailure` conflita com Wolverine? | Comparar EF retry ligado/desligado com `AutoApplyTransactions` | Decisão documentada sem wrap manual frágil | Obrigatório para AC5 |
| S8 | AC4 | Migration surface | Quais tabelas entram no schema Wolverine? | `MapWolverineEnvelopeStorage(modelBuilder, "wolverine")` | Migration lista todas as tabelas usadas | Obrigatório para AC4 |
| S9 | N/A | Operação | Como investigar falhas em produção? | Forçar handler consumidor a falhar | Dead letter, retry count, ownership e replay compreensíveis | Obrigatório para operação |

## Variantes esperadas e observadas

Esta tabela deve ser preenchida durante a execução dos spikes. Ela reaproveita a
linha de raciocínio da reprova técnica anterior, mas separa o esperado após o
fix do resultado observado neste ciclo.

| Variante | Configuração | Esperado após fix | Observado | Status |
|---|---|---|---|---|
| V0 | Stack base sem routing durável | `DomainEvents` podem ser drenados, mas sem envelope durável observável | TBD | TBD |
| V1 | `Policies.UseDurableLocalQueues()` sem routing explícito | Confirmar se há fila local convencional e se envelope é observável | TBD | TBD |
| V2 | `PublishAllMessages().ToLocalQueue(...)` + local durable | Não deve ocorrer `Collection was modified`; definir se envelope some após consumo | TBD | TBD |
| V3 | `PublishMessage<EditalPublicadoEvent>().ToLocalQueue(...)` + local durable | Evento deve ser roteado; definir se serve ou não para AC1a/AC1b | TBD | TBD |
| V3a | Handler real via `IMessageBus.InvokeAsync` sem retry EF | Não deve conflitar com transação; ainda pode não provar durabilidade | TBD | TBD |
| V3a' | `PublishAllMessages().ToLocalQueue(...)` no pacote `5.32.1-pr2586` | Fix deve remover exceção do scraper; durabilidade ainda precisa ser provada | TBD | TBD |
| V4 | PostgreSQL transport `ToPostgresqlQueue(...)` | Deve provar outbox durável sem Kafka | TBD | TBD |
| V5 | Kafka transport com durable outbox | Deve provar caminho real com broker externo | TBD | TBD |
| V6 | Kafka indisponível no commit | Entidade confirma e envelope fica recuperável para relay posterior | TBD | TBD |
| V7 | Restart recovery | Mensagem pendente sobrevive ao restart e é entregue | TBD | TBD |

Ao preencher `Observado`, registrar comando executado, commit/branch, versão dos
pacotes e tabelas consultadas.

## Decisões pendentes da #158

| Caminho | Decisão atual | Gatilho para fechar | Saída esperada |
|---|---|---|---|
| Caminho 1 - upgrade/fix Wolverine | TBD | S0, V3a' e cenários duráveis sem `Collection was modified` | Confirmar se `5.32.1-pr2586` ou `5.32.2+` oficial desbloqueia a stack |
| Caminho 2 - migration das tabelas Wolverine | TBD | S8 confirmar cobertura real das tabelas usadas | Escolher migration EF, DbContext dedicado ou SQL versionado |
| Caminho 3 - retry EF vs retry Wolverine | TBD | S7 comparar conflito e custo de operação | Definir política de retry para DbContexts usados por handlers Wolverine |
| Caminho 4 - Plano B interceptor próprio | Reservado | S2/S3/S4/S5/S6 falharem ou exigirem complexidade injustificável | Decidir se cria outbox próprio com tabela e worker UniPlus |
| Transporte principal | TBD | S2 e S3 compararem garantias e operação | Definir se PostgreSQL transport é suficiente ou se Kafka é obrigatório no fluxo |

## Detalhamento dos spikes

### S0 - Handler in-memory

Objetivo: provar que a correção do `DomainEventScraper` removeu a exceção
`Collection was modified` e que os domain events são drenados.

Não prova outbox durável. É apenas um teste de sanidade.

Validar:

- `Edital.Publicar()` adiciona `EditalPublicadoEvent`.
- `SaveChanges` dentro de handler Wolverine dispara o scraper.
- Handler local de teste recebe o evento.
- O evento recebido tem `EditalId` e `NumeroEdital` corretos.

Falha bloqueante:

- `Collection was modified`.
- Handler não recebe o evento.

### S1 - Fila local durável

Objetivo: entender exatamente o comportamento de `UseDurableLocalQueues`.

Validar:

- Se a mensagem entra em alguma tabela Wolverine antes do consumo.
- Se a mensagem some imediatamente após processamento.
- Se é possível observar estado pendente quando o consumidor é bloqueado ou
  falha.

Critério de decisão:

- Se não houver resíduo observável estável, este caminho não serve para AC1a.
- Se a mensagem não chegar a um destino configurado após o commit, este caminho
  não serve para AC1b, embora possa servir como transporte local interno.

### S2 - Transporte PostgreSQL

Objetivo: validar um transporte durável sem broker externo.

Configuração alvo:

```csharp
opts.UsePostgresqlPersistenceAndTransport(
    connectionString,
    "wolverine",
    transportSchema: "wolverine_queues");

opts.PublishAllMessages().ToPostgresqlQueue("domain-events");
```

Validar:

- Mensagem é persistida no armazenamento durável antes de ser processada.
- Consumidor de teste recebe o payload esperado.
- As tabelas de queue e envelope são conhecidas e documentadas.
- A mensagem não é enviada se a transação da entidade falhar.

Critério de decisão:

- Se S2 passar, Wolverine é viável sem depender de Kafka para o invariante
  básico.

### S3 - Transporte Kafka

Objetivo: validar o caminho com a infraestrutura externa real do projeto.

Validar:

- Kafka sobe via Docker ou Testcontainers.
- Tópico de teste é criado ou provisionado.
- Domain event é publicado para Kafka somente depois do commit.
- Consumer de teste recebe exatamente uma mensagem por comando.
- Payload contém dados suficientes para consumidores reais.
- Headers/correlation id, quando aplicável, são preservados ou a lacuna é
  documentada.

Critério de decisão:

- Se S2 passar e S3 falhar, o problema está na configuração Kafka/transporte.
- Se S2 e S3 falharem, o problema está na estratégia Wolverine/EF/outbox.

### S4 - Rollback

Objetivo: provar ausência de mensagens fantasmas.

Executar para S2 e S3.

Fluxo:

1. Handler cria e publica `Edital`.
2. Handler chama `SaveChanges`.
3. Handler lança exceção antes do commit final da transação Wolverine.
4. Teste consulta entidade, envelope e destino.

Validar:

- `editais` não contém a entidade.
- Tabelas Wolverine não contêm mensagem confirmada.
- Kafka ou PostgreSQL queue não recebem mensagem.

Falha bloqueante:

- Entidade ausente e mensagem presente.
- Entidade presente e mensagem ausente.

### S5 - Kafka indisponível

Objetivo: validar store-and-forward real.

Fluxo:

1. PostgreSQL está disponível.
2. Kafka está indisponível ou topic endpoint está inacessível.
3. Comando publica domain event.
4. Transação de domínio confirma.
5. Mensagem fica pendente no outbox.
6. Kafka volta.
7. Wolverine envia a mensagem.

Validar:

- A entidade persiste.
- O envelope fica em estado recuperável.
- O consumer Kafka recebe a mensagem após retorno do broker.
- Não há duplicidade indevida.

Falha bloqueante:

- Commit da entidade falha por indisponibilidade temporária do Kafka.
- Mensagem é perdida.
- Mensagem é enviada antes do commit.

### S6 - Restart recovery

Objetivo: provar recuperação após falha de processo.

Fluxo:

1. Produzir mensagem durável.
2. Encerrar o host antes do relay ou antes do consumo.
3. Subir novo host apontando para o mesmo banco.
4. Confirmar entrega/processamento.

Validar:

- Ownership do envelope é recuperado.
- Mensagem não fica travada indefinidamente.
- Reprocessamento e idempotência mínima são compreensíveis.

Falha bloqueante:

- Envelope fica preso sem caminho claro de recuperação.

### S7 - Retry strategy

Objetivo: decidir retry EF vs retry Wolverine.

Variantes:

- EF `EnableRetryOnFailure` ligado + `AutoApplyTransactions`.
- EF retry desligado + retry Wolverine.
- Wrap manual por `IExecutionStrategy`, apenas para medir custo e risco.

Critério de decisão recomendado:

- Preferir desligar `EnableRetryOnFailure` em DbContexts usados por handlers
  Wolverine e centralizar retry nas policies do Wolverine.

Falha bloqueante:

- Exigir wrap manual em todo handler para preservar transação.

### S8 - Migration surface

Objetivo: decidir como versionar o schema Wolverine.

Validar:

- `MapWolverineEnvelopeStorage(modelBuilder, "wolverine")` gera migration EF
  válida.
- A migration cobre todas as tabelas realmente usadas nos cenários S2, S3, S5
  e S6.
- O diff da migration é estável e auditável.

Tabelas esperadas a confirmar:

- `wolverine_outgoing_envelopes`
- `wolverine_incoming_envelopes`
- `wolverine_dead_letters`
- `wolverine_nodes`
- `wolverine_node_assignments`
- `wolverine_node_records`
- `wolverine_control_queue`
- `wolverine_agent_restrictions`
- Tabelas adicionais de PostgreSQL transport, se usadas.

Decisões possíveis:

- Mapear storage Wolverine nos DbContexts dos módulos.
- Criar DbContext dedicado para schema Wolverine.
- Usar SQL versionado fora do EF.

Preferência inicial:

- Usar migration EF nos DbContexts dos módulos, se cobrir todas as tabelas sem
  efeitos colaterais ruins.

### S9 - Operação e observabilidade

Objetivo: saber operar o outbox antes de adotar.

Validar:

- Onde observar mensagens pendentes.
- Onde observar dead letters.
- Como identificar owner/node.
- Como reprocessar ou liberar mensagem presa.
- Como limpar mensagens antigas sem apagar auditoria útil.
- Quais logs e métricas aparecem em falhas de relay.

Saída esperada:

- Pequeno runbook operacional para o guia Wolverine.

## Ordem de execução

Executar nesta ordem:

1. S0 - Handler in-memory.
2. S2 - Transporte PostgreSQL.
3. S4 - Rollback para PostgreSQL.
4. S3 - Transporte Kafka.
5. S4 - Rollback para Kafka.
6. S5 - Kafka indisponível.
7. S6 - Restart recovery.
8. S7 - Retry strategy.
9. S8 - Migration surface.
10. S9 - Operação e observabilidade.
11. S1 - Fila local durável, se ainda houver interesse em uso interno.

Motivo da ordem:

- Primeiro isola Wolverine/EF sem Kafka.
- Depois valida Kafka como transporte real do projeto.
- Por fim fecha operação, migration e retry.

## Estrutura recomendada dos artefatos

Depois da aprovação deste plano, criar:

```text
tests/Unifesspa.UniPlus.Selecao.IntegrationTests/Outbox/
  Capability/
    OutboxCapabilityCollection.cs
    OutboxCapabilityFixture.cs
    OutboxCapabilityApiFactory.cs
    OutboxCapabilityMatrixTests.cs
    OutboxKafkaCapabilityTests.cs
    OutboxPostgresqlTransportCapabilityTests.cs
    OutboxRetryCapabilityTests.cs
    OutboxMigrationCapabilityTests.cs
    SpikeMessages.cs
    SpikeHandlers.cs
```

Os testes devem ser isolados dos testes regulares de API. Se ficarem pesados,
usar trait explícito:

```csharp
[Trait("Category", "OutboxCapability")]
```

Comando alvo:

```bash
dotnet test tests/Unifesspa.UniPlus.Selecao.IntegrationTests \
  --filter "Category=OutboxCapability"
```

## Critérios de aceite do plano

Este plano é considerado aprovado quando o time concordar com:

- A matriz de cenários S0 a S9.
- A ordem de execução.
- Os critérios bloqueantes.
- O uso de Kafka como validação obrigatória.
- A decisão de não alterar produção antes dos resultados.

## Saídas esperadas após os spikes

Ao final da validação, produzir:

- Tabela de resultados por cenário.
- Recomendação: adotar Wolverine canonical, adotar com restrições ou seguir
  Plano B.
- Decisão de migration.
- Decisão de retry.
- Runbook operacional mínimo.
- Lista de alterações produtivas necessárias para cumprir a #158.

## Plano B

Se Wolverine não provar AC1a, AC1b, AC2 e AC3 com configuração simples,
considerar interceptor próprio:

- `ISaveChangesInterceptor` coleta `EntityBase.DomainEvents`.
- Persiste eventos em tabela própria de outbox dentro da transação EF.
- Worker separado publica para Kafka.
- Retry, dead letter e replay ficam sob controle do UniPlus.

Plano B só deve ser desenhado em detalhe se S2/S3/S4/S5/S6 falharem ou exigirem
complexidade operacional injustificável.

## Referências

- ADR-022: backbone Wolverine.
- ADR-024: outbox EF não adotado na #135, origem direta deste plano.
- ADR-004: Kafka como transporte assíncrono.
- JasperFx/wolverine#2585 e JasperFx/wolverine#2586: contexto do fix e do feed local.
- Branch `spike/135-outbox-validation`: evidência prévia e comparação V0-V3a'.
- PR uniplus-api#160: introdução do feed local.
