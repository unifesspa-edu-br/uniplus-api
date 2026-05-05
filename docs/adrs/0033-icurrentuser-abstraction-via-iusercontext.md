---
status: "accepted"
date: "2026-05-05"
decision-makers:
  - "Tech Lead (CTIC)"
---

# ADR-0033: `IUserContext` como abstração canônica para acesso ao principal autenticado

## Contexto e enunciado do problema

Endpoints e handlers que precisam do principal autenticado historicamente usariam o anti-pattern:

```csharp
string userId = User.FindFirstValue("sub")!;
```

Esse padrão tem três problemas que escalariam com cada novo endpoint user-scoped:

1. **String mágica** — `"sub"` é convenção OIDC (RFC 7519) mas `ClaimTypes.NameIdentifier` é convenção .NET. Dois devs vão divergir; mudança de IdP fica espalhada.
2. **`!` (null-forgiving) sem garantia** — endpoint anônimo, middleware fora de ordem, ou bug de configuração geram `NullReferenceException` em runtime em vez de erro semântico.
3. **Acoplamento HTTP em handlers de Application** — `User.FindFirstValue` exige `HttpContext` ou `ClaimsPrincipal`, dependências que Application camada não deveria carregar (Clean Architecture, ADR-0002, fitness test R3 da ADR-0012).

A issue #313 propôs criar `ICurrentUser` + `IRequiredCurrentUser`. Durante a implementação, descobriu-se que **`IUserContext`** já existia em `Application.Abstractions/Authentication/` (introduzido em PRs anteriores) com cobertura idêntica: `UserId`, `Name`, `Email`, `Cpf`, `NomeSocial`, `Roles`, `HasRole`, `GetResourceRoles`. A pergunta passou a ser: criar nova abstração paralela ou consolidar a existente?

## Drivers da decisão

- **Single source of truth para claim mapping.** Tem que existir UM lugar que sabe traduzir tokens OIDC (sub, email, realm_access.roles, resource_access.\*.roles, claims customizadas Uni+ como CPF e Nome Social) para um modelo de domínio limpo. Dois lugares fazendo isso convidam divergência.
- **Acessibilidade em Application.** Handlers Wolverine, validators FluentValidation, e regras de domínio que precisem do principal devem injetar a abstração diretamente, sem depender de `HttpContext`.
- **Fail-fast em uso indevido.** Endpoints `[Authorize]` que assumem auth não devem ter `string?` propagando até virar NRE — devem ter contrato não-nullable que falha cedo.
- **Não duplicar.** Cobertura de testes existente do `HttpUserContext` cobre claim mapping completo (Keycloak realm_access, resource_access, OIDC standard, customizadas Uni+). Recriar no formato `ICurrentUser` perderia esse capital ou exigiria duplicação de testes.

## Opções consideradas

- **A. Consolidar em `IUserContext`** — adicionar `IsAuthenticated` e introduzir `IRequiredUserContext` (variante não-nullable); usar a infra existente.
- **B. Criar `ICurrentUser`/`IRequiredCurrentUser` como solicitado pela issue #313** — duplicar shape; deprecar `IUserContext`; migrar tudo.
- **C. Manter status quo (`User.FindFirstValue` direto)** — descartado, é exatamente o anti-pattern que a issue veio resolver.

## Resultado da decisão

**Escolhida:** "A — Consolidar em `IUserContext`", porque é a única opção que respeita a infra de claim mapping já em produção sem reintroduzir débito (duplicação de tests, mismatch de claim policies entre duas implementações, custo de migração de código existente que já consome `IUserContext`).

A nomenclatura `ICurrentUser` proposta na issue era válida mas não trazia benefício técnico; `IUserContext` é equivalente semanticamente e está estabelecida.

### Forma do contrato

`IUserContext` (em `Application.Abstractions/Authentication/`) ganha `bool IsAuthenticated { get; }`:

```csharp
public interface IUserContext
{
    bool IsAuthenticated { get; }     // novo
    string? UserId { get; }            // sub claim (OIDC) com fallback ClaimTypes.NameIdentifier
    string? Name { get; }              // ClaimTypes.Name → preferred_username → name
    string? Email { get; }             // ClaimTypes.Email → email
    string? Cpf { get; }               // claim Uni+ uniplus.cpf
    string? NomeSocial { get; }        // claim Uni+ uniplus.nome_social
    IReadOnlyList<string> Roles { get; }       // realm_access.roles + ClaimTypes.Role
    bool HasRole(string role);
    IReadOnlyList<string> GetResourceRoles(string resourceName);  // resource_access.<r>.roles
}
```

`IRequiredUserContext` (novo) expõe o subset necessário para handlers/controllers que assumem auth, sem nullables. Implementação `RequiredUserContext` projeta `IUserContext` falhando rápido com `InvalidOperationException` quando `IsAuthenticated == false` ou claim crítica está ausente.

### Implementação e DI

`HttpContextCurrentUser` foi descartado em favor do `HttpUserContext` já existente (`Infrastructure.Core/Authentication/`). DI registra ambos como Scoped na pipeline de auth (`OidcAuthenticationConfiguration`):

```csharp
services.AddScoped<IUserContext, HttpUserContext>();
services.AddScoped<IRequiredUserContext>(sp =>
    new RequiredUserContext(sp.GetRequiredService<IUserContext>()));
```

### Esta ADR não decide

- Como o claim mapping específico do Keycloak evolui — escopo do `HttpUserContext` (uma classe parcial bem-coberta por testes em `Infrastructure.Core.UnitTests/Authentication/`).
- Política de deprecação (não há) — não havia código consumindo um `ICurrentUser` que não existia.
- Como migrar callers que ainda usam `User.FindFirstValue` direto — fitness test ArchUnit (próxima story de hardening) detectará e migrará incrementalmente.

## Consequências

### Positivas

- **Single source of truth** — claim mapping vive em UM ponto (`HttpUserContext`); troca de IdP ou mudança de claim policy é local.
- **Sem duplicação** — testes existentes do `HttpUserContext` cobrem o caminho; `RequiredUserContext` adiciona apenas testes de fail-fast.
- **Application camada limpa** — handlers Wolverine injetam `IUserContext` (abstração da camada Application.Abstractions) sem dependência de `HttpContext`.
- **Fail-fast explícito** — `IRequiredUserContext` em `[Authorize]` endpoints torna NRE em runtime impossível; trocada por `InvalidOperationException` com mensagem útil.
- **Pronto pra próximas stories** — `IdempotencyFilter` (story #286) já consome `IUserContext.UserId` para `scope`; cursor user-binding (story #312, próxima) consumirá o mesmo.

### Negativas

- **Issue #313 fechada com nomenclatura diferente da proposta original** (`IUserContext` vs `ICurrentUser`). Documentação precisa apontar para o nome real.
- **`IRequiredUserContext` é projeção, não outro backend** — se algum dia precisarmos de "required mas com fallback de impersonation/test-double", será necessária classe nova. Aceitável: não há requirement hoje.

### Neutras

- Variantes que não usam `IsAuthenticated` continuam funcionando — adicionar a property é backward-compatible para callers que só consomem nullables existentes.

## Confirmação

1. **DI registrado** — `OidcAuthenticationConfiguration.AddOidcAuthentication` registra ambos `IUserContext → HttpUserContext` e `IRequiredUserContext → RequiredUserContext` (proxy).
2. **Testes unit** — `HttpUserContextTests` ganhou cobertura de `IsAuthenticated` (3 cenários: identity com auth type, anonymous, HttpContext null). `RequiredUserContextTests` cobre fail-fast em todos os accessors.
3. **Adoção downstream** — `IdempotencyFilter` (story #286, já em main) consome `IUserContext.UserId` para `scope` em vez de extrair claim manualmente. Story #312 (próxima) idem.
4. **Fitness test futuro** (escopo da story #291) — ArchUnit proibirá `User.FindFirstValue` direto em código de produção fora de `HttpUserContext`.

## Mais informações

- [ADR-0002](0002-clean-architecture-com-quatro-camadas.md) — regra de dependências (Application sem Infrastructure).
- [ADR-0010](0010-audience-unica-uniplus-em-tokens-oidc.md) — fonte das claims que `HttpUserContext` mapeia.
- [ADR-0016](0016-keycloak-como-identity-provider.md) — formato Keycloak (realm_access, resource_access).
- [ADR-0011](0011-mascaramento-de-cpf-em-logs.md) — claim CPF é PII; `HttpUserContext.Cpf` deve seguir mascaramento ao logar.
- [Ardalis CleanArchitecture template](https://github.com/ardalis/CleanArchitecture) — padrão `ICurrentUserService` análogo (referência inicial da issue #313).
- [eShopOnContainers](https://github.com/dotnet-architecture/eShopOnContainers) — padrão `IIdentityService` análogo.
- Issue #313 — issue que motivou a abstração.
