---
status: "accepted"
date: "2026-04-28"
decision-makers:
  - "Tech Lead (CTIC)"
---

# ADR-0016: Keycloak como identity provider OIDC do `uniplus-api`

## Contexto e enunciado do problema

O `uniplus-api` precisa autenticar usuários com diferentes perfis (administrador, gestor, avaliador, candidato) e validar tokens de aplicações frontend e de integrações externas. A autenticação precisa ser centralizada, suportar SSO entre as três SPAs do `uniplus-web` e ser compatível com o Gov.br via OIDC (Login Único).

## Drivers da decisão

- Custo zero de licenciamento para universidade pública.
- Suporte nativo a OIDC com federação para Gov.br.
- Self-hosted, dados de identidade na infraestrutura institucional.
- Compatibilidade com SSO entre múltiplos clients web.

## Opções consideradas

- Keycloak 26.5 (Apache 2.0, Red Hat/CNCF)
- Auth0 (SaaS)
- Autenticação própria com JWT customizado
- ASP.NET Identity + Duende IdentityServer

## Resultado da decisão

**Escolhida:** Keycloak 26.5 como identity provider OIDC central da plataforma. O `uniplus-api` é resource server que valida tokens emitidos pelo realm `unifesspa`.

Práticas obrigatórias do lado do `uniplus-api`:

- **Validação stateless de tokens JWT** — backends não mantêm sessão server-side.
- **Validação estrita de `iss` e `aud`** (ver ADR-0010 — audience única `uniplus`). Configuração canônica:

```csharp
TokenValidationParameters
{
    ValidIssuers = ["https://auth.unifesspa.edu.br/realms/unifesspa"],
    ValidateIssuer = true,
    ValidAudiences = ["uniplus"],
    ValidateAudience = true,
}
```

- **Verificação de `realm_access.roles`** para autorização granular.
- **Refresh token rotation** ativo no realm.
- **Atributos customizados** (nome social, CPF) gerenciados como claims do realm.

A integração federada com Gov.br é configurada no Keycloak via Identity Provider externo (OIDC) — esta ADR cobre apenas o lado do `uniplus-api` como resource server. A configuração do brokering gov.br (alias, mappers, flow customizado de first-broker-login com SPI `cpf-matcher`, `client_id` por hostname, `client_secret_basic`) é detalhada na ADR-0020.

> **Imagem do Keycloak.** Em todos os ambientes (dev, CI, HML, PROD) o Uni+ usa a imagem composta `ghcr.io/unifesspa-edu-br/uniplus-keycloak:1.x` — base `quay.io/keycloak/keycloak:26.5.7` + JAR `cpf-matcher` embutido. Não é Keycloak vanilla. O artefato e o ciclo de release vivem no repo `unifesspa-edu-br/uniplus-keycloak-providers` e a documentação de consumo está em [`docker/keycloak/README.md`](../../docker/keycloak/README.md#imagem-do-keycloak).

## Consequências

### Positivas

- Autenticação centralizada para toda a plataforma.
- SSO entre as 3 SPAs sem implementação adicional.
- Federação com Gov.br via padrão OIDC, sem código customizado.
- Tokens JWT stateless — backends sem sessão server-side, escala horizontal trivial.

### Negativas

- Mais um serviço para operar (JVM + banco do Keycloak).
- Configuração inicial complexa (realms, clients, flows, mappers, audience).
- Keycloak consome recursos significativos (~512MB RAM mínimos).

### Riscos

- **Keycloak como SPOF.** Mitigado com deploy em alta disponibilidade (2+ réplicas) e health checks no Kubernetes.
- **Confusão entre `clientId` e `aud`.** Mitigado pelo enforcement explícito da ADR-0010 e por validação empírica em cada novo backend.
- **Token substitution.** Mitigado pela validação obrigatória de `iss` em conjunto com `aud`.

## Confirmação

- Health check `/health/auth` valida que a chave pública do realm é acessível e que validação de token simulado passa.
- Suíte de integração executa fluxo de obtenção e validação de token contra realm de teste em CI.

## Prós e contras das opções

### Keycloak

- Bom, porque é OSS Apache 2.0 com SSO nativo e federação OIDC.
- Ruim, porque exige operação ativa (não é serviço gerenciado).

### Auth0

- Bom, porque elimina operação.
- Ruim, porque custo proibitivo para volume de universidade pública e dados fora da infraestrutura institucional.

### Autenticação própria

- Bom, porque elimina dependência externa.
- Ruim, porque reinventa SSO, gestão de sessão, federação com Gov.br e console administrativo — alto custo de manutenção.

### Duende IdentityServer

- Bom, porque é nativo do .NET.
- Ruim, porque tem licença comercial em produção.

## Mais informações

- ADR-0010 define a audience única `uniplus`.
- ADR-0017 define K8s + Helm para o deploy do `uniplus-api` (e do Keycloak via charts próprios).
- ADR-0020 detalha o identity brokering gov.br via Keycloak (configuração de IdP externo, mappers, first-broker-login).
- **Origem:** revisão da ADR interna Uni+ ADR-008 (não publicada). Esta ADR cobre apenas a parte server-side; o consumo OIDC pelo frontend é decisão própria do `uniplus-web`.
