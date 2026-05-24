---
status: "proposed"
date: "2026-05-24"
decision-makers:
  - "Tech Lead (CTIC)"
---

# ADR-0068: Relógio via TimeProvider injetado, obrigatório em todo o `src/`

## Contexto e enunciado do problema

Parte do código novo do `uniplus-api` já lê o relógio de forma testável via `System.TimeProvider` (BCL .NET 8+): idempotência ([ADR-0027](0027-idempotency-key-store-postgresql.md)), cursor pagination ([ADR-0026](0026-paginacao-cursor-opaco-cifrado.md) — `CursorEncoder`), a entidade `ObrigatoriedadeLegal` ([ADR-0058](0058-obrigatoriedade-legal-validacao-data-driven.md)) e vários handlers. A convenção existia de facto, mas **não estava declarada nem enforçada**, e a fundação a contradizia.

A fundação e os interceptors de auditoria — anteriores a essa convenção — liam o relógio estático direto: `EntityBase.CreatedAt = DateTimeOffset.UtcNow` (inicializador), `EntityBase.MarkAsDeleted` (instante interno), `DomainEventBase.OccurredOn = DateTimeOffset.UtcNow`, `AuditableInterceptor`/`SoftDeleteInterceptor`, geração de protocolos (`Inscricao`, `ProtocoloConvocacao`), e o cache de token do Schema Registry. Resultado: três efeitos colaterais — (1) `CreatedAt` tinha duas fontes (o inicializador do domínio **e** o `AuditableInterceptor`, que o sobrescreve no `Added`); (2) `OccurredOn` e tempos de negócio eram não-determinísticos em teste; (3) nada impedia o próximo `DateTimeOffset.UtcNow` de entrar.

A questão a decidir é: **como tornar o acesso a relógio uniforme, testável e à prova de regressão em todo o `src/`** — e, decidido isso, **se o `TimeProvider` deve ser obrigatório (injetado sempre) ou opcional com fallback `TimeProvider.System`**.

## Drivers da decisão

- **Determinismo de teste.** Tempos de negócio (`OccurredOn`, `DataManifestacao`, prefixo de protocolo, vigência) e timestamps de auditoria precisam ser fixáveis via um `TimeProvider` fixo em teste (test double, ou `FakeTimeProvider` quando for preciso simular avanço) sem `Thread.Sleep`/tolerância de janela.
- **Fonte única por timestamp.** `CreatedAt`/`UpdatedAt`/`DeletedAt` são audit trail de persistência; ter o domínio e o interceptor ambos escrevendo o instante é ambíguo.
- **Coerência interna.** O projeto já padronizou `TimeProvider`; falta declarar e generalizar — espelha a postura de "padrão único" da [ADR-0032](0032-guid-v7-para-identidade-de-entidades.md) e da abstração canônica única da [ADR-0033](0033-iusercontext-como-abstracao-de-autenticacao.md).
- **À prova de regressão.** A convenção não pode depender de disciplina de revisão; precisa de enforcement automático no build, como a fitness function de `Guid.NewGuid()`.
- **Clean Architecture.** A solução não pode arrastar dependência de infraestrutura para o domínio — `TimeProvider` é primitivo de BCL (como `DateTimeOffset`), então pode residir no domínio sem violar a regra de dependência.

## Opções consideradas

- **A. `TimeProvider` injetado e obrigatório** — sem default `null`, sem fallback `TimeProvider.System` em código de negócio; relógio sempre fornecido pelo caller; fitness test bane leitura estática.
- **B. `TimeProvider` injetado, opcional com fallback `System`** — `TimeProvider? clock = null` → `clock ?? TimeProvider.System`; baixo churn, mas mantém um caminho de não-determinismo.
- **C. Abstração própria `IDateTimeProvider`/`IClock`** — interface custom registrada na DI.
- **D. Manter `DateTimeOffset.UtcNow` inline** — status quo; descartado de antemão por não ser testável nem determinístico.

## Resultado da decisão

**Escolhida:** "A — `TimeProvider` injetado e obrigatório", porque é a única opção que fecha o backdoor de não-determinismo sem reintroduzir dívida.

**Sem exceções, sem fallback escondido.** Todo método de negócio que lê o relógio recebe `TimeProvider clock` obrigatório (com `ArgumentNullException.ThrowIfNull`) — entidades (`Edital.Publicar`, `Inscricao.Criar/Confirmar`, `Convocacao.Criar/Aceitar/Recusar`, `Matricula.Efetivar`), value objects (`ProtocoloConvocacao.Gerar`), factories de conveniência (`ObrigatoriedadeLegal.Criar`) e serviços de infraestrutura (interceptors, repositórios, `OAuthBearerAuthenticationHeaderValueProvider`, endpoints smoke). A opção B foi rejeitada explicitamente: `clock ?? TimeProvider.System` compila para qualquer caller que passe `null`, e um teste que esqueça o relógio passa silenciosamente a depender do relógio real — exatamente o que se quer eliminar.

**Fonte única de auditoria nos interceptors.** `EntityBase.CreatedAt` perde o inicializador e passa a ser carimbado exclusivamente pelo `AuditableInterceptor` no `SaveChanges` (estado `Added`) a partir do `TimeProvider` injetado; `UpdatedAt` idem no `Modified`; `MarkAsDeleted(string, DateTimeOffset)` recebe o instante do `SoftDeleteInterceptor`. Uma entidade transiente (pré-persistência) tem `CreatedAt` default — comportamento correto: ela ainda não foi criada do ponto de vista do audit trail.

**`OccurredOn` é instante de negócio, fornecido na origem.** `DomainEventBase(DateTimeOffset OccurredOn)` torna o campo parâmetro obrigatório do construtor; o método de entidade que levanta o evento passa `clock.GetUtcNow()`.

**`TimeProvider` do BCL, não abstração própria (rejeita C).** `TimeProvider` (.NET 8+) é o ponto Schelling da plataforma: tem `FakeTimeProvider` oficial (`Microsoft.Extensions.TimeProvider.Testing`), cobre relógio, timers e timestamps de alta resolução, e o projeto já o usa. Uma interface custom seria reinvenção com menos capacidade e mais carga de manutenção.

**`TimeProvider.System` só em composition roots.** Permanece permitido apenas onde a composição acontece e não há `IServiceProvider` para resolver: registrations de DI (`services.TryAddSingleton(TimeProvider.System)` em `AddUniPlusEfInterceptors`, paginação e idempotência) e o callback de configuração do Wolverine (`SchemaRegistryServiceCollectionExtensions.CreateClient`, que roda antes de `builder.Build()`). Em código de teste, `TimeProvider.System` (ou um `FakeTimeProvider`) é escolha explícita do teste, não default de produção.

### Esta ADR não decide

- **Identidade determinística (UUIDv7).** `EntityBase.Id` e `DomainEventBase.EventId` continuam gerados por `Guid.CreateVersion7()` internamente — tornar a identidade determinística é escopo da [ADR-0032](0032-guid-v7-para-identidade-de-entidades.md), não desta convenção. O leak de timestamp embutido no UUIDv7 já foi avaliado lá.
- **Migração de medições de duração.** Usos de `Stopwatch.GetTimestamp()` (ex.: `RequestLoggingMiddleware`) medem *elapsed*, não *wall-clock*; ficam fora desta convenção.
- **Tornar determinísticos os testes que hoje passam `TimeProvider.System`.** Migrar suítes para `FakeTimeProvider` e assertar instantes exatos é melhoria incremental de qualidade de teste, não requisito desta decisão.

## Consequências

### Positivas

- **Determinismo de relógio disponível em todo o domínio e infra** — qualquer cenário (vigência, expiração de cursor/idempotência, `OccurredOn`, protocolo) é fixável por um `TimeProvider` fixo em teste; os interceptors de auditoria já têm testes determinísticos assertando o instante exato.
- **`CreatedAt`/`UpdatedAt`/`DeletedAt` com fonte única** — o `AuditableInterceptor`/`SoftDeleteInterceptor` é a autoridade; some a ambiguidade domínio×interceptor.
- **Convenção à prova de regressão** — o fitness test falha o build no primeiro `DateTimeOffset.UtcNow` cru reintroduzido em `src/`.
- **Coerência com o stack** — `TimeProvider` do BCL, zero dependência nova; alinhado ao padrão emergente da plataforma.

### Negativas

- **Assinaturas de domínio carregam `TimeProvider`** — métodos que antes não tinham parâmetro agora recebem o relógio; o ganho de testabilidade justifica.
- **Churn de call-sites de teste** — testes que construíam interceptors/entidades sem relógio passam a fornecer `TimeProvider.System` ou um fake explícito.
- **`CreatedAt` default em entidade transiente** — código que lê `CreatedAt` antes do `SaveChanges` vê `default`; correto semanticamente, mas exige atenção (após persistir, o interceptor já carimbou a instância rastreada).

### Neutras

- **`TimeProvider.System` segue presente em composition roots** — é o lugar legítimo do relógio real; não é exceção à regra, é a fronteira dela.

## Confirmação

1. **Fitness test solution-wide** — `RelogioViaTimeProviderTests` em `tests/Unifesspa.UniPlus.ArchTests/SolutionRules/` varre por regex os arquivos `.cs` de `src/` (excluindo `obj/`/`bin/`, com strip de comentários) e falha o build ao encontrar `DateTime`/`DateTimeOffset` `.UtcNow`/`.Now`. Espelha `DominioNaoUsaGuidNewGuidTests` ([ADR-0032](0032-guid-v7-para-identidade-de-entidades.md)) — mesma abordagem textual, justificada porque ArchUnitNET enxerga dependência de tipo, não chamada de membro.
2. **Compilador** — `TimeProvider` obrigatório (sem default) faz o build falhar em qualquer caller que omita o relógio; `ArgumentNullException.ThrowIfNull(clock)` cobre passagem de `null` em runtime.
3. **Code review** — revisor confirma que código novo recebe `TimeProvider` por injeção e que `TimeProvider.System` aparece apenas em composition roots ou em teste.

## Mais informações

- [ADR-0032](0032-guid-v7-para-identidade-de-entidades.md) — fitness function irmã (`Guid.NewGuid()`); a identidade UUIDv7 fica fora desta convenção.
- [ADR-0033](0033-iusercontext-como-abstracao-de-autenticacao.md) — mesma postura de "abstração canônica única" para um cross-cutting concern.
- [ADR-0004](0004-outbox-transacional-via-wolverine.md) / [ADR-0005](0005-cascading-messages-para-drenagem-de-domain-events.md) — `OccurredOn` viaja no envelope do outbox; agora com instante de negócio determinístico.
- [ADR-0027](0027-idempotency-key-store-postgresql.md) / [ADR-0026](0026-paginacao-cursor-opaco-cifrado.md) — já consumiam `TimeProvider`; esta ADR generaliza o padrão.
- [TimeProvider — Microsoft Learn](https://learn.microsoft.com/dotnet/api/system.timeprovider) — API oficial (BCL .NET 8+).
- [Microsoft.Extensions.TimeProvider.Testing — FakeTimeProvider](https://learn.microsoft.com/dotnet/core/extensions/timeprovider) — controle de tempo em teste.
- Issues #127 / #390 (uniplus-api) — origem do `SoftDeleteInterceptor`/`AuditableInterceptor` cujo carimbo de tempo passa a ser injetado.
