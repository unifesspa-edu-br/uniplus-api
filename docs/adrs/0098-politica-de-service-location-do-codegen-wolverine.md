---
status: "accepted"
date: "2026-06-26"
decision-makers:
  - "Tech Lead (CTIC)"
consulted:
  - "Documentação oficial Wolverine (guide/codegen, guide/migration 6.0)"
informed: []
---

# ADR-0098: Política de service location do codegen Wolverine (`NotAllowed` + allow-list por tipo)

## Contexto e enunciado do problema

O Wolverine gera, em tempo de compilação (codegen), o adaptador de cada chain de
handler — inlinando a construção das dependências do handler para evitar reflexão e
service location em runtime. Quando uma dependência tem registro que o codegen não
consegue "enxergar" (uma _opaque lambda factory_, ou um tipo concreto não-público),
o Wolverine cai em **service location**: resolve aquela dependência via
`IServiceProvider` no momento da invocação.

No Wolverine 5.x o default é `ServiceLocationPolicy.AllowedButWarn` — service location
funciona, apenas emite warning. **No Wolverine 6.0 o default vira
`ServiceLocationPolicy.NotAllowed`** ([migration guide](https://wolverinefx.net/guide/migration)):
qualquer chain cuja árvore de dependências exija service location passa a lançar
`InvalidServiceLocationException` na geração de código. Esta ADR decide a postura do
`uniplus-api` diante dessa mudança, antecipando-a.

Uma verificação empírica sob `NotAllowed` (forçando a geração de toda chain CQRS dos
3 hosts — monólito, Geo, Portal) revelou dois grupos de ofensores, com causas-raiz
distintas:

1. **Unit of Work dos módulos** (`ISelecaoUnitOfWork`, `IConfiguracaoUnitOfWork`,
   `IOrganizacaoInstitucionalUnitOfWork`) — registradas como
   `AddScoped<IXUnitOfWork>(sp => sp.GetRequiredService<XDbContext>())`, uma **lambda
   factory opaca** que encaminha a interface para a MESMA instância do `XDbContext`
   (o DbContext implementa a interface UoW).
2. **Tipos concretos `internal`** injetados (transitivamente) em handlers — cache
   invalidators do Organização (`InstituicaoCacheInvalidator`, `UnidadeCacheInvalidator`)
   e os readers do Geo (`EstadoReader`, `CidadeReader`, `CepResolver`, `CepReader`
   etc.). O codegen não consegue `new` um tipo não-público, então service-loca.

   Além desses, o `CepResolver` do Geo depende de `Lazy<ICacheService>` — registrado
   como `AddScoped(sp => new Lazy<ICacheService>(...))` para DIFERIR o connect do Redis
   (cache-aside do lookup de CEP, ADR-0090/0092) —, outra lambda factory inerentemente
   opaca.

## Drivers da decisão

- **Forward-compat com Wolverine 6.0** sem migração de emergência futura.
- **Atomicidade write+evento do outbox transacional** (ADR-0004) é inegociável: a UoW
  PRECISA encaminhar para a mesma instância de DbContext que os repositórios usam e que
  o `AutoApplyTransactions` enrola na transação.
- **Clean Architecture** (ADR-0001/0003): `Infrastructure.Core` (helper Wolverine
  compartilhado) não pode referenciar a Application de cada módulo.
- **Mecanismos oficiais do framework** — sem reflexão ad-hoc, sem wrapper para "enganar"
  o codegen, sem afrouxar a política globalmente para esconder problemas corrigíveis.
- **Guarda automática contra regressões** — uma nova lambda opaca vazando para um
  handler deve falhar cedo, não degradar silenciosamente para service location.

## Opções consideradas

- **(A) `NotAllowed` permanente + allow-list por tipo** — trava a política no helper
  compartilhado; corrige na raiz o que dá (tipos concretos → `public`) e declara opt-in
  explícito (`AlwaysUseServiceLocationFor<T>()`) só para os tipos cuja opacidade é
  obrigatória (UoW forwarding, `Lazy<T>`).
- **(B) `AllowedButWarn` + opt-in** — mantém o default do 5.x; apenas adiciona os
  opt-ins e torna os concretos públicos, sem travar a política.
- **(C) `AlwaysAllowed`** — desliga o diagnóstico. Descartada de imediato: esconde o
  problema e perde o forward-compat.

## Resultado da decisão

**Escolhida: (A) `NotAllowed` permanente + allow-list por tipo.**

`opts.ServiceLocationPolicy = ServiceLocationPolicy.NotAllowed` é travado no helper
compartilhado `WolverineOutboxConfiguration` — aplica-se aos 3 hosts. A correção de
cada ofensor segue a **ordem de preferência da doc do Wolverine**:

1. **Root fix (preferido) — tornar o tipo concreto `public`** quando não há requisito
   de instância compartilhada. Aplicado aos cache invalidators do Organização e a todos
   os readers do Geo (`EstadoReader`, `CidadeReader`, `DistritoReader`, `BairroReader`,
   `LogradouroReader`, `GeoProximidadeReader`, `CepResolver`, `CepReader`). O codegen
   passa a construí-los inline — sem service location e com melhor performance. Mesmo
   padrão já aplicado aos repositórios em ADR-0097/spike.
2. **`AlwaysUseServiceLocationFor<T>()` (opt-in sancionado)** apenas quando o root fix é
   impossível porque a opacidade é obrigatória:
   - as 3 UoW dos módulos — `AddScoped<IXUnitOfWork, XDbContext>()` ingênuo criaria uma
     SEGUNDA instância de DbContext por escopo, quebrando a atomicidade do ADR-0004;
   - `Lazy<ICacheService>` do Geo — não há forma `AddScoped<Lazy<T>, TImpl>()`
     equivalente.

**Layering (OCP/SRP):** cada módulo/host é dono dos seus opt-ins, declarados num
`*CodegenRegistration` na sua própria `*.API` (onde os tipos são referenciáveis e onde
referenciar `Wolverine.*` é permitido — a borda de composição, não a Application). O
composition root compõe: o host do monólito chama os 3 `ConfigurarCodegenWolverine` no
`configureRouting`; o host Geo chama o seu. O helper compartilhado
`WolverineOutboxConfiguration` permanece **agnóstico** dos tipos de cada módulo —
adicionar um novo módulo/UoW no futuro não exige editá-lo.

O **Portal** e o **Ingresso** não têm handler que injete a sua UoW (Portal tem 0
handlers; Ingresso idem), então não precisam de opt-in — e a guarda passa a quebrar
automaticamente se um handler futuro injetar a UoW sem o opt-in correspondente.

## Consequências

### Positivas

- Boot/teste limpo sob a política que vira default no Wolverine 6.0 — migração para o
  6.0 não reabre este gap.
- A política `NotAllowed` + a suíte de integração viram **guarda automática**: qualquer
  nova dependência opaca em chain de handler quebra o teste, nomeando o tipo ofensor.
- Service location eliminado de quase toda chain → codegen inlina as dependências
  (menos indireção em runtime).
- Opt-ins explícitos e documentados tornam **auditável** qual tipo usa service location
  e por quê (referenciando ADR-0004 / o `Lazy<T>` de cache-aside).

### Negativas

- Tornar tipos `public` amplia a superfície de visibilidade da Infrastructure (aceitável
  — são tipos de infraestrutura, não contrato externo; mesmo precedente dos repositórios).
- Adicionar um módulo com UoW exige lembrar de declarar o seu opt-in — mitigado pela
  guarda, que falha de forma legível apontando o caminho.

### Neutras

- A política é setada no helper compartilhado, mas os opt-ins ficam por host/módulo —
  duas responsabilidades em camadas diferentes (intencional, por Clean Arch).

## Confirmação

- Guarda de regressão `ServiceLocationGuardTests` (host monólito em
  `Unifesspa.UniPlus.Host.IntegrationTests`; host Geo em
  `Unifesspa.UniPlus.Geo.IntegrationTests`), apoiada no helper compartilhado
  `ServiceLocationCodegenGuard`: assere que (a) a política está travada em `NotAllowed`
  e (b) nenhuma chain CQRS do host dispara `InvalidServiceLocationException` — falha
  nomeando o tipo ofensor.
- `dotnet test UniPlus.slnx` exercita as chains de escrita/leitura dos 3 hosts sob
  `NotAllowed`; uma dependência opaca nova quebra a suíte.

### Nota empírica: lambdas de lifetime *Singleton* não disparam

Só lambdas factory opacas de lifetime **Scoped** (e tipos concretos não-públicos)
disparam service location no codegen. Lambdas **Singleton** (ex.: `IConnectionMultiplexer`
do `AddUniPlusCache`, registrado por `AddSingleton(sp => ConnectionMultiplexer.Connect(...))`,
e os demais singletons da borda — `IDomainErrorMapper`, `IMinioClient`,
`ISchemaRegistryClient`) são **pré-resolvidas** pelo Wolverine na construção da classe
gerada e **não** exigem opt-in. Verificado empiricamente: uma chain do Organização que
injeta `IUnidadeCacheInvalidator` → `ICacheService`/`RedisCacheService` →
`IConnectionMultiplexer` **gera sem `InvalidServiceLocationException`** sob `NotAllowed`,
falhando apenas em runtime (ao conectar no Redis), nunca na geração. Para não mascarar
esse caminho de produção, a guarda do monólito sobe com o `RedisCacheService` real (e não
o `FakeInMemoryCacheService` das demais suítes) — exercita a forma de DI de produção.

## Prós e contras das opções

### (A) `NotAllowed` permanente + allow-list

- Bom, porque adota cedo o default do 6.0 e trava o ganho com guarda automática.
- Bom, porque distingue por achado: root fix onde dá, opt-in só onde a opacidade é
  obrigatória — sem afrouxar a política globalmente.
- Ruim, porque um módulo/UoW novo precisa lembrar do opt-in (mitigado pela guarda).

### (B) `AllowedButWarn` + opt-in

- Bom, porque é a mudança mínima.
- Ruim, porque não é forward-compat: o warning persiste e uma regressão não quebra o
  build, só loga — o ganho não fica travado.

### (C) `AlwaysAllowed`

- Bom, porque silencia o diagnóstico sem mais trabalho.
- Ruim, porque esconde o problema, perde o forward-compat e remove a guarda.

## Mais informações

- [ADR-0003](0003-wolverine-como-backbone-cqrs.md) — Wolverine como backbone CQRS
  (Application/Domain não importam `Wolverine.*`; a borda de composição API/host pode).
- [ADR-0004](0004-outbox-transacional-via-wolverine.md) — atomicidade write+evento via a
  MESMA instância de DbContext (razão de a UoW exigir forwarding e, portanto, opt-in).
- [ADR-0097](0097-topologia-de-deploy-em-tres-apis-monolito-modular.md) — topologia de 3
  hosts (monólito, Geo, Portal) sobre a qual a política e os opt-ins se aplicam.
- [Guia Golden Path Wolverine](../guia-wolverine-golden-path.md) — seção "Service
  location e codegen" documenta o padrão de registro de UoW e o opt-in por módulo.
- [Wolverine — codegen / service location](https://wolverinefx.net/guide/codegen.html)
- [Wolverine — migration guide 6.0](https://wolverinefx.net/guide/migration)
