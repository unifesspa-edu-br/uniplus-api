# Plano do Spike S10 — Cascading Messages × `PublishDomainEventsFromEntityFrameworkCore`

> Extensão da matriz S0–S9 da Story uniplus-api#158, motivada pela thread upstream
> [JasperFx/wolverine#2585](https://github.com/JasperFx/wolverine/issues/2585) — em
> particular o comentário do maintainer Jeremy D. Miller recomendando, para projetos
> greenfield, o caminho idiomático de cascading messages do handler em vez do
> "magic EF Core scraping thing".

## Decisões fechadas

- **Local:** `docs/spikes/158-s10-plano-cascading-messages.md`
- **Branch:** `spikes/158-s10-cascading-messages`
- **Base da branch:** `spikes/158-s5b-kafka-kraft`
- **Escopo:** extensão da Story #158, não nova story.
- **Critério para ADR-026:** abrir ADR-026 se cascading messages for viável e demonstrar ganho em pelo menos uma das quatro dimensões objetivas:
  - menor acoplamento;
  - menos código;
  - teste unitário mais simples;
  - menor dependência de comportamento implícito do EF scraping.

## Mudança de foco

A matriz S0–S9 (já validada, ADR-025 mergeada) respondeu a pergunta original:

> **Wolverine funciona com outbox transacional?**

Resposta: sim, com 13 testes verdes. Adoção formalizada em [ADR-025](https://github.com/unifesspa-edu-br/uniplus-docs/blob/main/docs/adrs/ADR-025-outbox-wolverine-adotado-em-158.md).

Após a thread upstream, a pergunta evolui para:

> **Qual estilo Wolverine devemos adotar: EF scraping ou cascading messages?**

UniPlus é greenfield — não há código legado que justifique o caminho menos
idiomático. Vale validar antes de cristalizar a ADR-025 como caminho final
de drenagem.

Esta mudança **não invalida** a ADR-025: o caminho atual funciona. O spike
S10 verifica se há ganho objetivo em mudar para o caminho recomendado pelo
maintainer, antes da implementação produtiva.

## Contexto técnico

### Caminho atual (ADR-025)

```csharp
// Configuração no Program.cs (extension)
options.PublishDomainEventsFromEntityFrameworkCore<EntityBase>(
    entity => entity.DomainEvents);

// Handler — sem retorno explícito de eventos
public static async Task Handle(
    PublicarEditalCommand command,
    SelecaoDbContext db,
    CancellationToken ct)
{
    var edital = Edital.Criar(command.Numero, command.Titulo, command.Tipo);
    edital.Publicar();
    db.Editais.Add(edital);
    await db.SaveChangesAsync(ct);
    // Wolverine scraper drena ChangeTracker → EntityBase.DomainEvents → publish
}
```

**Características:**
- Drenagem implícita via scraper interno do Wolverine.
- Acoplamento ao `ChangeTracker` do EF Core.
- Exposição pública obrigatória de `EntityBase.DomainEvents` para o scraper enxergar.
- Bug histórico (`Collection was modified`) corrigido pelo fork local 5.32.1-pr2586.

### Caminho idiomático (a validar — S10)

```csharp
// Configuração no Program.cs — sem PublishDomainEventsFromEntityFrameworkCore

// Handler — retorna a coleção de eventos, Wolverine auto-publica
public static async Task<IEnumerable<object>> Handle(
    PublicarEditalCommand command,
    SelecaoDbContext db,
    CancellationToken ct)
{
    var edital = Edital.Criar(command.Numero, command.Titulo, command.Tipo);
    edital.Publicar();
    db.Editais.Add(edital);
    await db.SaveChangesAsync(ct);

    return edital.DomainEvents;
}
```

**Características hipotéticas (a confirmar empiricamente):**
- Drenagem explícita no return do handler.
- Independente do `ChangeTracker` — funciona com qualquer persistência.
- `EntityBase.DomainEvents` pode permanecer encapsulado (acessado só pelo handler).
- Não exercita o caminho do scraper EF — não depende do fix do PR #2586.

## Escopo

### O que será exercitado

| Cenário | Caminho atual (scraper) | Caminho idiomático (cascading) |
|---|---|---|
| Comando dispara, agregado emite evento, evento entregue ao handler local PG | S2/V4 já validado | **S10/V8 — a validar** |
| Comando + falha pós-`SaveChanges` → rollback elimina entidade e mensagem | S4-PG já validado | **S10/V9 — a validar** |
| Compatibilidade com fan-out para Kafka topic externo | S3/V5 validado | **S10/V10 — a validar** |
| Coexistência com `PersistMessagesWithPostgresql + EnableMessageTransport` | Validado | A validar implicitamente em V8 |
| Coexistência com `Policies.UseDurableOutboxOnAllSendingEndpoints` | Validado | A validar implicitamente em V8 |

### O que NÃO está no escopo

Spikes S5/S5b/S6/S7/S8/S9 da matriz original **não são re-executados**. Justificativa:
as garantias de outbox durável, restart recovery, retry strategy, schema migration
e operação vêm da configuração do host (`PersistMessagesWithPostgresql`,
`UseDurableOutboxOnAllSendingEndpoints`, etc.) — **independentes** do método
de drenagem (scraper vs cascading). Esses spikes continuam válidos para a
configuração escolhida ao final, qualquer que seja.

## Cenários

### S10/V8 — Cascading messages, caminho feliz

**Handler:**

```csharp
public static class PublicarEditalCascadingHandler
{
    public static async Task<IEnumerable<object>> Handle(
        PublicarEditalCascadingCommand command,
        SelecaoDbContext db,
        CancellationToken ct)
    {
        var edital = Edital.Criar(command.Numero, command.Titulo, command.Tipo);
        edital.Publicar();
        db.Editais.Add(edital);
        await db.SaveChangesAsync(ct).ConfigureAwait(false);

        return edital.DomainEvents;
    }
}
```

**Asserts:**
- Entidade `Edital` persistida em `editais`.
- Handler subscritor PG queue (existente, herdado da matriz S2) recebe `EditalPublicadoEvent`.
- Tabela `wolverine.wolverine_outgoing_envelopes` registra envelope durável.
- Sem `Collection was modified` (não exercita o scraper EF).

### S10/V9 — Cascading messages, rollback

**Handler:**

```csharp
public static class FalharAposSaveChangesCascadingHandler
{
    public static async Task<IEnumerable<object>> Handle(
        FalharAposSaveChangesCascadingCommand command,
        SelecaoDbContext db,
        CancellationToken ct)
    {
        var edital = Edital.Criar(command.Numero, command.Titulo, command.Tipo);
        edital.Publicar();
        db.Editais.Add(edital);
        await db.SaveChangesAsync(ct).ConfigureAwait(false);

        throw new InvalidOperationException("Spike S10 — exceção pós-SaveChanges");
        // unreachable: return edital.DomainEvents;
    }
}
```

**Asserts (paridade AC2 com S4-PG):**
- Entidade ausente em `editais` (rollback transacional).
- Envelope ausente em `wolverine_outgoing_envelopes`.
- Handler subscritor PG não recebe.

### S10/V10 — Cascading messages com Kafka fan-out

Mesmo do S10/V8 com `EditalPublicadoEvent` roteado também para Kafka topic
`edital_events`. Verifica que cascading respeita o pipeline de roteamento
configurado (`PublishMessage<T>().ToPostgresqlQueue` + `ToKafkaTopic`).

**Asserts:**
- PG queue entrega ao handler subscritor (paridade S10/V8).
- Consumer externo Confluent.Kafka recebe a mensagem no topic.

## Configuração Wolverine para o spike

Extension dedicada para o S10 — sem `PublishDomainEventsFromEntityFrameworkCore`:

```csharp
public sealed class OutboxCascadingExtension : IWolverineExtension
{
    public static string? PostgresqlConnectionString { get; set; }
    public static string? KafkaBootstrapServers { get; set; }

    public void Configure(WolverineOptions options)
    {
        if (string.IsNullOrWhiteSpace(PostgresqlConnectionString))
            throw new InvalidOperationException("ConnectionString deve ser configurada.");

        options
            .PersistMessagesWithPostgresql(PostgresqlConnectionString, "wolverine")
            .EnableMessageTransport(_ => { });

        options.Policies.UseDurableOutboxOnAllSendingEndpoints();

        options.PublishMessage<EditalPublicadoEvent>().ToPostgresqlQueue("domain-events");
        options.ListenToPostgresqlQueue("domain-events");

        if (!string.IsNullOrWhiteSpace(KafkaBootstrapServers))
        {
            options.UseKafka(KafkaBootstrapServers).AutoProvision();
            options.PublishMessage<EditalPublicadoEvent>().ToKafkaTopic("edital_events");
        }

        // SEM PublishDomainEventsFromEntityFrameworkCore — drenagem via cascading do handler.

        options.Discovery.IncludeAssembly(typeof(OutboxCascadingExtension).Assembly);
    }
}
```

**Atenção:** o assembly de testes carrega tanto `OutboxSpikeWolverineExtension`
(da matriz S0–S9) quanto `OutboxCascadingExtension`. Para evitar conflito de
campos estáticos, a fixture do S10 usa containers próprios e a extension
verifica seus próprios campos. Cada `WebApplicationFactory` carrega ambas as
extensions, então a do scraper precisa permanecer inerte quando o spike S10
está rodando — solução: `OutboxCascadingExtension` setado seus próprios
campos antes do bootstrap; `OutboxSpikeWolverineExtension` continua lendo
`PostgresqlConnectionString` original. Coexistência sob escrutínio durante
implementação — se conflitar, isolar via assembly attribute condicional.

## Critérios de comparação (4 dimensões objetivas)

Saída esperada do spike: tabela comparativa baseada nas **4 dimensões objetivas**
abaixo. Pelo menos **1 dimensão objetivamente melhor** para cascading aciona ADR-026.

### 1. Menos acoplamento

| Métrica | Atual (scraper) | Cascading |
|---|---|---|
| Dependências obrigatórias do agregado | `EntityBase.DomainEvents` **público** | `EntityBase.DomainEvents` pode ser **internal/protected** |
| Acoplamento ao EF | Acoplado ao `ChangeTracker` | Independente — funciona sem EF |
| Acoplamento ao framework | Scraper interno do Wolverine | Apenas convenção de retorno |

### 2. Menos código

| Métrica | Atual (scraper) | Cascading |
|---|---|---|
| Linhas no extension de bootstrap | 1 linha extra (`PublishDomainEventsFromEntityFrameworkCore<EntityBase>(...)`) | 0 linhas extras |
| Linhas no handler | `void`/`Task` sem retorno | `IEnumerable<object>` no retorno |
| Configuração de discovery | Mesma | Mesma |

### 3. Teste unitário mais simples

| Métrica | Atual (scraper) | Cascading |
|---|---|---|
| Testar drenagem em UnitTest | Requer mockar EF + scraper | Verificar retorno do handler diretamente |
| Validar evento emitido | Spy/mock no `IMessageBus` ou collector via DI | `Assert.Contains(typeof(EditalPublicadoEvent), result)` |
| Setup mínimo | DbContext + MessageContext + scraper mockados | Apenas DbContext mockado |

### 4. Menos dependência de "mágica" EF

| Métrica | Atual (scraper) | Cascading |
|---|---|---|
| Caminhos não-óbvios | Drenagem invisível no boot, dispara em SaveChanges | Drenagem visível no return statement |
| Onboarding de novo dev | Precisa entender scraper + ChangeTracker | Lê o handler, vê o que retorna |
| Rastreabilidade em logs | Evento aparece "do nada" | Evento aparece após `Sent` no tracking |
| Compatibilidade com aggregates não-EF | Não funciona (dependência ChangeTracker) | Funciona |

## Estrutura de arquivos

```
tests/Unifesspa.UniPlus.Selecao.IntegrationTests/Outbox/Capability/Cascading/
  OutboxCascadingFixture.cs                — Postgres + Kafka próprios (isola da fixture do scraper)
  OutboxCascadingApiFactory.cs             — sem PublishDomainEventsFromEntityFrameworkCore
  OutboxCascadingExtension.cs              — config Wolverine para cascading
  CascadingSpikeMessages.cs                — comandos novos: PublicarEditalCascadingCommand, FalharAposSaveChangesCascadingCommand
  CascadingSpikeHandlers.cs                — handlers que retornam IEnumerable<object>
  OutboxCascadingMatrixTests.cs            — S10/V8 + S10/V9
  OutboxCascadingKafkaTests.cs             — S10/V10
```

Convenção: a fixture é isolada da `OutboxCapabilityFixture` para evitar
contaminação cruzada de campos estáticos do extension. A `OutboxCascadingFixture`
sobe seus próprios containers (mesma decisão de S5b/S6 — fixtures isoladas
para spikes que mudam configuração do framework).

## Critério de aceite do spike

1. **AC-S10/V8** verde — cascading entrega evento ao handler local PG após commit.
2. **AC-S10/V9** verde — rollback elimina entidade e envelope (paridade AC2).
3. **AC-S10/V10** verde — Kafka fan-out funciona via cascading (paridade AC1b).
4. **AC-doc** — relatório `158-s10-relatorio.md` com tabela comparativa preenchida nas 4 dimensões.

## Saída do spike

Relatório `docs/spikes/158-s10-relatorio.md` com **3 caminhos possíveis**:

| Caminho | Quando aplicar |
|---|---|
| **Manter ADR-025 inalterada** (scraper EF) | Cascading viável mas equivalente em todas as 4 dimensões |
| **Atualizar ADR-025 com nota lateral** mencionando cascading como alternativa aceitável | Cascading viável, sem dimensão melhor; ambos os estilos coexistem na codebase |
| **Abrir ADR-026 substituindo parcialmente ADR-025** no item de drenagem | Cascading viável + **pelo menos 1 dimensão objetivamente melhor** |

ADR-026 segue o mesmo padrão ADR-024 → ADR-025: substitui parcialmente,
preserva ADR-025 como histórico técnico válido.

## Conclusão

Se S10/V8, S10/V9 e S10/V10 passarem, e a comparação mostrar ganho objetivo em pelo menos uma dimensão, a recomendação será abrir ADR-026 substituindo parcialmente a ADR-025: manter Wolverine + outbox durável, mas trocar o padrão preferencial de drenagem de domain events de EF scraping para cascading messages.

A decisão é cirúrgica: **não reabre Wolverine como backbone** (ADR-022 e ADR-025 permanecem nesse ponto) — só reavalia o **estilo de publicação** dos eventos drenados do agregado.

Sem o S10 verde com evidência empírica nas 4 dimensões, a ADR-025 permanece como configuração canônica.

## Estimativa de esforço

- Implementação dos 3 testes (S10/V8, V9, V10): 1–2 horas.
- Comparação + relatório: 30 minutos.
- ADR-026 (se aplicável): 1 hora.
- **Total:** meio dia de trabalho.

## Referências

- [ADR-025 — Outbox transacional Wolverine adotado em #158](https://github.com/unifesspa-edu-br/uniplus-docs/blob/main/docs/adrs/ADR-025-outbox-wolverine-adotado-em-158.md)
- [Plano de validação original (S0–S9)](158-plano-validacao-outbox-wolverine.md)
- [Relatório final consolidado da matriz S0–S9](158-relatorio-final.md)
- [JasperFx/wolverine#2585 — comentário do maintainer recomendando cascading](https://github.com/JasperFx/wolverine/issues/2585#issuecomment-4320873143)
- [Wolverine docs — Cascading Messages](https://wolverinefx.net/guide/handlers/cascading.html)
