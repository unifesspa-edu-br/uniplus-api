# Testes E2E de autenticação contra Keycloak real

Esta suíte exercita o pipeline real `JwtBearer` da API Uni+ contra um Keycloak provisionado via Testcontainers, sem mocks no esquema de autenticação. O objetivo é provar que `OidcAuthenticationConfiguration.AddOidcAuthentication` rejeita tokens com audience errada, expirados e assinados por chave externa, garantindo rede de proteção contra regressão de segurança no código produtivo.

## O que está coberto

| # | Cenário | Caminho exercitado |
|---|---|---|
| 1 | Token válido emitido pelo realm → `/api/auth/me` | Caminho feliz: validação de issuer, audience, lifetime e signing key passam; claims do KC chegam ao `IUserContext` |
| 2 | Token válido com scope `uniplus-profile` → `/api/profile/me` | Audience mapper `aud=uniplus` ativo; claims `cpf` e `nomeSocial` mapeados a partir dos atributos do usuário |
| 3 | Token expirado → `/api/auth/me` | `ValidateLifetime` rejeita após `ClockSkew` |
| 4 | Token sem `aud=uniplus` → `/api/auth/me` | `ValidateAudience` rejeita |
| 5 | Token assinado por chave externa → `/api/auth/me` | `ValidateIssuerSigningKey` rejeita kid não publicada no JWKS do realm |
| 6 | Token com issuer diferente da Authority (assinado por chave conhecida pelo realm) → `/api/auth/me` | `ValidateIssuer` rejeita tokens emitidos por IdP arbitrário — isolamento estrito |
| 7 | `/health` da API | `OidcDiscoveryHealthCheck` reporta `Healthy` quando o discovery do Keycloak responde |

> **Sobre o cenário 6 (isolamento de `ValidateIssuer`):** o realm sintético embute um `KeyProvider` adicional cuja chave privada é também conhecida pelo código de teste (`KeycloakKnownTestKey`). O Keycloak publica essa chave pública no JWKS do realm, então o pipeline JwtBearer aceita assinaturas feitas com a privada correspondente. O cenário 6 forja um token assinado por essa chave conhecida, com audience e lifetime corretos, mas com `iss` arbitrário — assim a ÚNICA dimensão inválida é o issuer. Sem esse `KeyProvider` embutido, o cenário cairia em `ValidateIssuerSigningKey` (a falha do cenário 5), dando falsa cobertura de issuer. A chave privada está em `realm-e2e-tests.json` e em `KeycloakKnownTestKey.cs`; ambos jamais alcançam ambientes superiores.

## Stack

- **`KeycloakContainerFixture`** (em `tests/Unifesspa.UniPlus.IntegrationTests.Fixtures/Hosting/`) — usa o Testcontainers genérico para subir a imagem composta canônica `ghcr.io/unifesspa-edu-br/uniplus-keycloak:1.0.2` (Keycloak 26.5.7 + JAR `cpf-matcher` embutido), montar o realm sintético `realm-e2e-tests.json` em `/opt/keycloak/data/import/` e iniciar com `start-dev --import-realm`. A wait strategy aguarda o discovery `/realms/unifesspa-e2e/.well-known/openid-configuration` responder 200, garantindo que o realm está importado antes dos testes começarem.
- **`KeycloakCollection`** — `[CollectionDefinition("Keycloak")]` xUnit que serializa as classes que precisam do container e mantém o cold start pago apenas uma vez por execução.
- **`OidcRealApiFactory`** — subclasse de `ApiFactoryBase<Program>` que sobrescreve `ConfigureTestAuthentication` como no-op, mantendo o `JwtBearer` produtivo ativo e apontando `Auth:Authority` para o container. O método foi extraído da base como ponto de extensão semântico — subclasses E2E que precisam exercitar o pipeline real ganham um override nomeado em vez de uma flag opaca.
- **Realm sintético de teste (`docker/keycloak/realm-e2e-tests.json`)** — arquivo SEPARADO do realm canônico (`realm-export.json`), realm name `unifesspa-e2e`. Contém apenas o necessário para os 6 cenários: 4 usuários sintéticos com senha não-temporary, 2 clients confidenciais e o scope `uniplus-profile`. Esse arquivo NUNCA é montado pelo `docker-compose` nem por Helm — vive exclusivamente para o ciclo da fixture.
  - `e2e-tests` — emite tokens com `aud=uniplus` (default scope `uniplus-profile`); `access.token.lifespan: 5` para o cenário de expiração; secret plain `e2e-secret`.
  - `e2e-tests-bad-aud` — sem `uniplus-profile` nos default scopes; tokens não recebem `aud=uniplus`. Usado APENAS no cenário de audience errada.

> **Importante:** ambos os clients de teste têm `directAccessGrantsEnabled: true` e secret em texto claro INTENCIONAL — eles existem SOMENTE em `realm-e2e-tests.json`, que é um realm sintético importado em containers efêmeros via Testcontainers. O realm canônico (`realm-export.json`) que vai para dev/homologação/produção NÃO contém esses clients e NÃO permite Direct Access Grant. A separação entre os dois arquivos garante que nenhum artefato de teste seja inadvertidamente promovido para ambientes superiores.

## Como rodar localmente

### 1. Pré-requisitos do kernel (Linux 6.19+)

O kernel 6.19 da Arch Linux exige que o módulo `veth` seja carregado manualmente antes do Docker conseguir criar bridges para containers. Sem isso, o Testcontainers falha com erros de network endpoint:

```bash
sudo modprobe veth
```

Para tornar persistente entre boots:

```bash
echo veth | sudo tee /etc/modules-load.d/veth.conf
```

### 2. Pré-cache da imagem (opcional, recomendado para a primeira execução)

A imagem composta `ghcr.io/unifesspa-edu-br/uniplus-keycloak:1.0.2` tem ~600MB. Sem cache local, o cold start do Testcontainers pode levar 1–2 minutos só no pull. Para evitar surpresa:

```bash
docker pull ghcr.io/unifesspa-edu-br/uniplus-keycloak:1.0.2
```

### 3. Rodar a suíte

```bash
dotnet test tests/Unifesspa.UniPlus.Selecao.IntegrationTests \
  --filter "FullyQualifiedName~AuthE2ETests"
```

Para rodar junto com os demais testes do módulo (`AuthEndpointsTests`, `ProfileEndpointsTests`, `OutboxCascading*`):

```bash
dotnet test tests/Unifesspa.UniPlus.Selecao.IntegrationTests
```

A `KeycloakCollection` garante que o container suba uma única vez para todas as classes da collection.

## Tempo esperado

| Cenário | Meta | Comentário |
|---|---|---|
| Hot start (imagem em cache, kernel pronto) | < 30s para a suíte E2E completa | O cenário 3 (token expirado) sozinho consome ~36s pelo delay de `ClockSkew + lifespan`. A meta vale para os 5 cenários sem a janela de expiração — quando essa janela for tunada via injeção de tempo, a meta volta a valer. |
| Cold start (pull da imagem, container fresh) | < 2min | Dominado pelo pull da imagem do GHCR. |

## Pinning da imagem

A imagem é pinned em **patch fixo** (`1.0.2`) para garantir parity bit-a-bit entre `docker/docker-compose.yml`, esta suíte e os pipelines de CI. Atualizações de patch na imagem composta passam por bump explícito acompanhado de validação manual — trocar para `1.x` ou `latest` quebraria reproducibilidade dos testes e da experiência local. Quando a imagem for atualizada no compose, o pinning aqui é atualizado no mesmo PR.

## Troubleshooting

| Sintoma | Causa provável | Como resolver |
|---|---|---|
| `failed to set up container ... endpoint with name ... already exists` | Módulo `veth` ausente no kernel 6.19+ | `sudo modprobe veth` antes de rodar a suíte |
| Cold start > 2min na primeira execução | Pull da imagem do GHCR sem cache local | Rodar `docker pull ghcr.io/unifesspa-edu-br/uniplus-keycloak:1.0.2` antes |
| Cenário de token expirado falha esporadicamente | Variação de relógio entre host e container | O delay já cobre `ClockSkew + lifespan`; se persistir, conferir se o host está com NTP sincronizado |
| `connection refused` na wait strategy | Imagem composta não expõe `/realms/unifesspa/.well-known/openid-configuration` | Verificar se o `realm-export.json` foi montado em `/opt/keycloak/data/import/` e se o `--import-realm` está no comando |
| Discovery health check `Unhealthy` no cenário 6 | Authority injetada na API não bate com o endpoint do container | Conferir que `OidcRealApiFactory.GetConfigurationOverrides` usa `keycloak.Authority` (URL com porta efêmera publicada) |

## Fora de escopo desta suíte

A suíte cobre o pipeline JWT com IdP real. Ficam fora dela:

- **Identity Brokering gov.br** (Authenticator SPI `cpf-matcher`, flow custom, broker IdP) — coberto por testes unitários em `unifesspa-edu-br/uniplus-keycloak-providers` e por smoke E2E (`scripts/smoke-test-cpf-matcher.sh`). E2E completo contra gov.br homologação requer redirect URI registrado e Keycloak HML, não é viável aqui.
- **Federation OpenLDAP sintética** — também coberta no smoke do repo de providers; integrar aqui acrescentaria um segundo container e dobraria o cold start sem ganho proporcional para os 6 cenários listados.
- **Realm "slim de teste" separado** — desnecessário; o `realm-export.json` versionado já é slim (`identityProviders: []`).

Esses itens ganham follow-up próprio quando a story de QA E2E real contra gov.br staging entrar no backlog.

## Referências

- [`OidcAuthenticationConfiguration`](../src/shared/Unifesspa.UniPlus.Infrastructure.Core/Authentication/OidcAuthenticationConfiguration.cs) — pipeline alvo dos testes
- [`docker/keycloak/realm-export.json`](../docker/keycloak/realm-export.json) — realm canônico
- [`docs/adrs/0016-keycloak-como-identity-provider.md`](adrs/0016-keycloak-como-identity-provider.md) — Keycloak como resource server
- [`docs/adrs/0020-identity-brokering-govbr.md`](adrs/0020-identity-brokering-govbr.md) — Identity Brokering via SPI `cpf-matcher`
- [Testcontainers for .NET](https://dotnet.testcontainers.org/) — biblioteca de containers efêmeros
- Issue de origem: [`unifesspa-edu-br/uniplus-api#145`](https://github.com/unifesspa-edu-br/uniplus-api/issues/145)
