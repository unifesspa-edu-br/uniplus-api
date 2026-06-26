# Missão: forward-compat Wolverine 6.0 — eliminar service-location das chains CQRS

> Brief de trabalho para sessão nova (após `/clear`). Branch: `spike/monolito-modular`.
> Artefato de spike — remover no rollout/squash final junto com o checkpoint.

Tornar o `uniplus-api` forward-compat com o `ServiceLocationPolicy.NotAllowed` do
Wolverine 6.0: eliminar (ou opt-in explicitamente, conforme recomendação do time do
Wolverine) as registrações via lambda opaca Scoped que ainda disparam service-location
na geração de código das chains de command/query.

## Contexto

Continuação direta do commit `169b332` ("refactor(messaging): valida via
WolverineFx.FluentValidation"), que removeu o warning de service-location do middleware
de validação adotando o pacote oficial. A verificação daquele commit (boot temporário
com `ServiceLocationPolicy.NotAllowed`, simulando o default do 6.0) revelou o PRÓXIMO
bloqueador: a chain do `CriarEditalCommand` ainda lança `InvalidServiceLocationException`
por causa do `ISelecaoUnitOfWork`, registrado como "opaque lambda factory" Scoped. Esta
missão fecha esse gap em todos os módulos.

Ler antes de começar:
- `docs/spikes/refatoracao-3-apis-checkpoint.md` (seção "Follow-up remanescente para a
  migração Wolverine 6.0")
- `docs/adrs/0003-wolverine-como-backbone-cqrs.md` (emenda 2026-06-26) + `docs/adrs/0004-*`
  (outbox transacional — invariante de atomicidade write+evento)

## Fatos já levantados (verificar — é o estado atual do código)

- 6 UoW registradas via lambda opaca, forwarding da interface para o MESMO DbContext
  (o DbContext implementa a interface UoW). Arquivos:
  - `src/selecao/.../Infrastructure/DependencyInjection.cs:47` → `ISelecaoUnitOfWork`
  - `src/ingresso/.../Infrastructure/DependencyInjection.cs:29` → `IIngressoUnitOfWork`
  - `src/configuracao/.../Infrastructure/DependencyInjection.cs:33` → `IConfiguracaoUnitOfWork`
  - `src/organizacao-institucional/.../Infrastructure/DependencyInjection.cs:36` → `IOrganizacaoInstitucionalUnitOfWork`
  - `src/geo/.../Infrastructure/DependencyInjection.cs:44` → `IUnitOfWork` (base)
  - `src/portal/.../Infrastructure/DependencyInjection.cs:27` → `IUnitOfWork` (base)
- **NUANCE CRÍTICA (não cair na armadilha):** `AddScoped<IUoW, UoWImpl>()` ingênuo é
  ERRADO aqui. O `…DbContext : DbContext, I…UnitOfWork` e os repositórios dependem do
  DbContext concreto (ex.: `EditalRepository(SelecaoDbContext context, …)`). Registrar
  `AddScoped<I…UnitOfWork, …DbContext>()` cria uma SEGUNDA instância de DbContext por
  escopo → o repo escreve num contexto e o `SaveChanges` da UoW commita outro → perda de
  dados + quebra da atomicidade write+evento do outbox (ADR-0004,
  `UseEntityFrameworkCoreTransactions` + `AutoApplyTransactions` enrolam o MESMO DbContext).
  A correção PRECISA preservar o forwarding para a mesma instância.
- Outras lambdas opacas SCOPED a investigar (aparecem no relatório só se injetadas em
  chains de handler):
  - `src/shared/.../Authentication/OidcAuthenticationConfiguration.cs:96` → `IRequiredUserContext`
  - `src/geo/.../Infrastructure/DependencyInjection.cs:87-88` → `IGeoImportacaoService` / `IGeoImportacaoExecutor` (forward p/ `GeoEtlOrquestrador`)
- Singletons via lambda (`ICorrelationIdAccessor`, `IDomainErrorMapper`,
  `IConnectionMultiplexer`, `IMinioClient`, `IUniPlusEncryptionService`,
  `ISchemaRegistryClient`) PROVAVELMENTE não disparam (Wolverine pré-resolve singletons
  sem service-location per-invocação) — CONFIRMAR empiricamente, não assumir.
- Camada: `IUnitOfWork` base vive em `src/shared/Unifesspa.UniPlus.Application.Abstractions`;
  as interfaces UoW específicas vivem nas Application de cada módulo. O helper compartilhado
  `WolverineOutboxConfiguration` (Infrastructure.Core) NÃO referencia as Application dos
  módulos (regra de dependência Clean Arch). Logo, opt-ins POR TIPO têm que ser registrados
  de onde o tipo é referenciável: o composition root
  (`src/host/Unifesspa.UniPlus.Host/Program.cs`, `configureRouting`) para os 4 módulos
  internos; Geo/Portal standalone para o seu próprio `IUnitOfWork`.

## Como atacar (investigar ANTES de implementar — não chutar)

1. **Enumerar o conjunto COMPLETO empiricamente.** Setar temporariamente
   `opts.ServiceLocationPolicy = JasperFx.CodeGeneration.Model.ServiceLocationPolicy.NotAllowed;`
   no callback de `UseWolverine` em `src/shared/.../Messaging/WolverineOutboxConfiguration.cs`
   (logo após o `PersistMessagesWithPostgresql`) e rodar a suíte de INTEGRAÇÃO INTEIRA (todos
   os módulos), coletando os relatórios "Service location(s):" de CADA chain. Não parar no
   primeiro achado — montar a lista exaustiva (tipo + motivo + chain). REVERTER a linha depois.

2. **Seguir a recomendação oficial do time do Wolverine.** Ler a doc de codegen
   (context7, lib `/llmstxt/wolverinefx_net_llms-full_txt`; e o guia
   <https://wolverinefx.net/guide/codegen> + a migration guide do 6.0). O time recomenda,
   por ordem de preferência:
   1. **Ajustar a registração para uma forma que o codegen enxergue** (ex.:
      `AddScoped<TInterface, TImpl>()` concreto) — QUANDO não há requisito de instância
      compartilhada. É a opção preferida.
   2. **`opts.CodeGeneration.AlwaysUseServiceLocationFor<T>()`** — opt-in explícito por tipo,
      recomendado pela própria doc justamente para registros opacos/forwarding e EF Core
      DbContext. NÃO é gambiarra: é o mecanismo sancionado. É o caminho correto para as UoW
      (forwarding obrigatório p/ a mesma instância de DbContext).
   Decidir POR ACHADO qual das duas se aplica, com justificativa. Evitar a opção preguiçosa
   de afrouxar a política global para tipos que poderiam ser corrigidos na raiz.

3. **DECISÃO ESTRUTURAL — PROPOR AO USUÁRIO ANTES DE IMPLEMENTAR** (pipeline CQRS
   compartilhado, merece ADR). Travar a política como `NotAllowed` em definitivo (adota cedo
   o default do 6.0 + guarda contra regressões futuras) com os opt-ins por tipo, OU manter
   `AllowedButWarn` e só aplicar opt-in. Recomendação default: **NotAllowed + allow-list** —
   é o objetivo real de forward-compat e TRAVA o ganho (qualquer nova lambda opaca em handler
   passa a quebrar o boot/teste, virando guarda automática). Confirmar com o usuário e
   registrar em ADR (emenda à 0003 ou nova ADR).

4. **Resolver o layering com design limpo.** Onde registrar cada
   `AlwaysUseServiceLocationFor<I…UnitOfWork>()` sem violar Clean Arch. Padrão sugerido (OCP):
   cada módulo expõe seus opt-ins de codegen via um `Action<WolverineOptions>` (análogo ao
   `configureRouting`/`AddSelecaoMessaging` já existentes), e o composition root (host)
   compõe — o helper compartilhado permanece agnóstico dos tipos de módulo. Geo/Portal
   standalone aplicam o seu. Não duplicar conhecimento de tipo no Infrastructure.Core.

## Qualidade, princípios e maturidade (requisito explícito)

- **Maturidade / recomendações do Wolverine:** usar SOMENTE os mecanismos oficiais do
  framework (formas de registro codegen-visíveis e `AlwaysUseServiceLocationFor<T>`). Nada de
  reflection ad-hoc, nada de afrouxar a política globalmente para esconder o problema, nada de
  inventar wrapper só para enganar o codegen. Cada escolha citando a doc do Wolverine que a embasa.
- **SOLID:**
  - SRP — a responsabilidade "declarar opt-ins de codegen do módulo" pertence ao módulo, não
    ao helper compartilhado.
  - OCP — adicionar um novo módulo/UoW no futuro não deve exigir editar o Infrastructure.Core;
    o hook de composição deve ser extensível por adição.
  - DIP — manter a regra: Application/Domain não conhecem Wolverine; opt-ins ficam na borda
    de composição (API/host), não vazam para Application.
- **Clean Code:** nomes claros em pt-BR para domínio / inglês para infra (convenção do repo);
  XML doc comments no estilo do projeto explicando o PORQUÊ do opt-in (referenciando ADR-0004
  e a instância única do DbContext); zero comentário morto; sem suprimir warnings (corrigir
  causa raiz — TreatWarningsAsErrors).
- **Testabilidade — entregável de teste obrigatório:** além de não regredir a suíte, deixar uma
  GUARDA automática contra futuras lambdas opacas em chains de handler. Opções (escolher a mais
  limpa para o repo):
  - Se a política virar `NotAllowed` permanente: a própria suíte de integração já é a guarda
    (qualquer nova dep opaca quebra o boot do host nos testes) — documentar isso explicitamente
    e garantir cobertura de boot de TODOS os módulos.
  - Caso contrário: um teste de integração/arquitetura dedicado que sobe o host Wolverine com
    `ServiceLocationPolicy.NotAllowed` e assere que compila/executa um command representativo de
    cada módulo sem `InvalidServiceLocationException`.
  O teste deve falhar de forma legível apontando o tipo ofensor.

## Definition of Done

- App sobe LIMPO sob `ServiceLocationPolicy.NotAllowed` (simula o default do 6.0): zero
  `InvalidServiceLocationException` em qualquer chain de qualquer módulo (Selecao, Ingresso,
  Configuracao, Organizacao, Geo, Portal).
- Atomicidade do outbox intacta: testes de integração cascading/outbox verdes (MESMA instância
  de DbContext por escopo preservada — NÃO regredir ADR-0004).
- Guarda de regressão testável entregue (ver seção acima).
- `dotnet build UniPlus.slnx` 0 warnings (TreatWarningsAsErrors) + `dotnet test UniPlus.slnx`
  verde (baseline atual: 1709 aprovados + 1 ignorado) + `dotnet restore --locked-mode` OK (se
  mexer em pacotes, regenerar locks via `--force-evaluate`).
- Codex review (`codex review --uncommitted`); tratar achados.
- Docs: marcar o follow-up resolvido em `docs/spikes/refatoracao-3-apis-checkpoint.md`; ADR
  (emenda à 0003 ou nova) registrando a política e o uso de `AlwaysUseServiceLocationFor`;
  atualizar `docs/guia-wolverine-golden-path.md` se o padrão de registro de UoW/opt-in mudar.
- Conventional commits pt-BR, atômicos. SEM `Co-Authored-By`/atribuição de IA. SEM `--no-verify`.
  Trabalhar na branch `spike/monolito-modular` (não abrir PR sem pedir).

## Invariantes a não quebrar

- ADR-0003 (Wolverine backbone; Application/Domain não importam `Wolverine.*` — fitness test
  ArchUnitNET) e ADR-0004 (atomicidade write+evento via mesmo DbContext).
- Clean Arch: Infrastructure.Core não referencia Application dos módulos.
- Não desfazer o fix de validação do commit `169b332` (WolverineFx.FluentValidation +
  `PiiSafeValidationFailureAction`) nem o masking de PII (LGPD, Parecer DPO 002/2026).
- Strings user-facing em pt-BR; sem suprimir warnings (corrigir causa raiz).

## Receita do experimento de verificação (já validada na sessão anterior)

- Enum correto no Wolverine 5.39.5: `JasperFx.CodeGeneration.Model.ServiceLocationPolicy`
  (assembly `JasperFx`; valores `AllowedButWarn` / `AlwaysAllowed` / `NotAllowed`). A
  propriedade é `opts.ServiceLocationPolicy` (direto no `WolverineOptions`).
- O relatório de cada chain sai como "Service location(s): Service `<Tipo>`: … is an 'opaque'
  lambda factory with the Scoped lifetime and requires service location", e a chain lança
  `Wolverine.Configuration.InvalidServiceLocationException` na compilação (lazy, no 1º invoke
  → vira 500 nos testes de endpoint). Opt-in correto remove o tipo do relatório.
- Para inspecionar um tipo do Wolverine/JasperFx sem subir a app: `MetadataLoadContext` sobre
  o `bin/Debug/net10.0` do Host (que tem todas as deps + runtime) — `LoadFrom` puro não resolve
  dependências.
