---
status: "accepted"
date: "2026-04-28"
decision-makers:
  - "Tech Lead (CTIC)"
---

# ADR-0010: Audience única `uniplus` em tokens OIDC

## Contexto e enunciado do problema

A ADR-0016 adota Keycloak como identity provider OIDC do projeto. Falta especificar o valor da claim `aud` (audience, RFC 7519 §4.1.3) dos access tokens emitidos para os backends da plataforma.

A claim `aud` identifica o destinatário pretendido do token — o resource server a quem o token é dirigido. O backend valida essa claim antes de aceitar a requisição. Há duas convenções estabelecidas no mercado:

- **Audience por API** (padrão Auth0/Okta, alinhado à RFC 8707): cada backend tem identificador próprio. Token vazado fica restrito a um único serviço.
- **Audience por plataforma**: identificador único compartilhado por todos os backends da mesma superfície de produto. Novos módulos entram sem reconfigurar IdP nem frontend.

A plataforma é hoje monolito modular (ADR-0001) com dois módulos no mesmo repositório compartilhando `SharedKernel`. É percebida pelo usuário final como produto único ("Uni+"), com SSO entre as 3 SPAs.

## Drivers da decisão

- Coerência com a identidade do produto Uni+ — token "pertence à plataforma".
- Baixa fricção operacional para entrada de novos módulos.
- Simplicidade no SSO cross-app.
- Possibilidade de evolução aditiva para múltiplas audiences caso surja módulo com requisito de isolamento elevado.

## Opções consideradas

- Audience única `uniplus` (uma por plataforma)
- Audience por API (`uniplus-api`, `uniplus-selecao-api`, `uniplus-ingresso-api`, …)
- Audience múltipla desde o início (`aud: ["uniplus", "uniplus-api"]`)
- Usar `clientId` do SPA como audience

## Resultado da decisão

**Escolhida:** audience única `uniplus` para todos os access tokens emitidos pelo Keycloak aos clients web da plataforma.

Implementação:

- `oidc-audience-mapper` no client scope `uniplus-profile` com `included.custom.audience: "uniplus"`.
- Cada backend valida `ValidAudiences = ["uniplus"]`.
- Controle de acesso granular entre módulos é feito por `realm_access.roles` e por `scope` — não por `aud`.
- O `clientId` do resource server permanece `uniplus-api` no Keycloak (campo interno, não aparece no token).

Validação obrigatória dos backends:

```csharp
TokenValidationParameters
{
    ValidIssuers = ["https://auth.unifesspa.edu.br/realms/unifesspa"],
    ValidateIssuer = true,
    ValidAudiences = ["uniplus"],
    ValidateAudience = true,
}
```

O par `iss + aud` é o que efetivamente identifica o token como legítimo — nunca validar somente um dos dois.

## Consequências

### Positivas

- Coerência com a identidade do produto Uni+.
- Novos módulos entram sem criar client bearer-only nem mapper adicional.
- Configuração única para documentar, monitorar e auditar.
- Compatível com SSO cross-app por construção.

### Negativas

- Blast radius maior — token vazado vale em qualquer backend da plataforma.
- Menor aderência à RFC 8707 (Resource Indicators) — RFC informacional, largamente não implementada por IdPs mainstream.
- Diverge do padrão Auth0/Okta — equipe precisa saber que o controle de acesso fino é por scope/roles.

### Riscos

- **Módulo futuro com requisito elevado herda audience compartilhada.** Mitigação: revisar esta ADR ao introduzir o primeiro módulo desse perfil. Migração para múltiplas audiences é aditiva e retrocompatível.
- **Confusão entre `clientId` e `aud`.** Mitigação: documentação explícita; validação empírica obrigatória em cada novo backend.
- **Token substitution** (token de outra instância Keycloak com mesmo `aud`). Mitigação: validação estrita do `iss` é obrigatória em todos os backends.

## Confirmação

- Cada novo backend implementa `TokenValidationParameters` validando `iss` e `aud`.
- Pull request review verifica que `ValidateAudience` e `ValidateIssuer` estejam ambos `true`.

## Prós e contras das opções

### Audience por API

- Bom, porque limita blast radius por serviço.
- Ruim, porque cada novo módulo exige client bearer-only adicional, mapper e atualização de configuração de cada SPA — over-engineering para a fase atual do projeto.

### Audience múltipla desde o início

- Bom, porque combina os dois mundos em compatibilidade aditiva.
- Ruim, porque introduz complexidade decisória ("qual audience validar?") sem benefício concreto.

### `clientId` do SPA como audience

- Bom, porque é comportamento default do Keycloak sem mapper.
- Ruim, porque é semanticamente errado — o SPA é cliente do token, não destinatário; backend amarrado ao nome do frontend.

## Mais informações

- ADR-0016 define Keycloak como identity provider.
- [RFC 7519 — JSON Web Token (`aud` claim)](https://datatracker.ietf.org/doc/html/rfc7519#section-4.1.3)
- [RFC 8707 — Resource Indicators for OAuth 2.0](https://datatracker.ietf.org/doc/html/rfc8707)
- **Origem:** revisão da ADR interna Uni+ ADR-019 (não publicada).
