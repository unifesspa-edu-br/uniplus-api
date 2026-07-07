# Guia de Contribuição — Uni+ API

Este documento descreve como contribuir com o backend da plataforma Uni+. Leia-o por completo antes de abrir sua primeira contribuição.

> **Regras de commits e integração:** este repositório segue o **[Guia de Commits e Integração](docs/guia-commits-e-integracao.md)** local — fonte de verdade para mensagens de commit e política de integração no `uniplus-api`. Este `CONTRIBUTING.md` complementa o guia com instruções operacionais de setup, build, testes e revisão.

---

## Pré-requisitos

| Ferramenta | Versão mínima | Verificação |
|---|---|---|
| .NET SDK | 10.0 | `dotnet --version` |
| Docker + Compose | 27+ | `docker compose version` |
| Git | 2.40+ | `git --version` |
| GitHub CLI | 2.50+ | `gh --version` |

### Dependências via Docker Compose

O projeto depende de serviços externos que rodam localmente via Docker. Para subir
**apenas a infra** (sem as APIs nem o frontend), a partir da raiz do repositório:

```bash
docker compose -f docker/docker-compose.yml up -d
```

Isso inicia: PostgreSQL 18, Redis, Kafka (KRaft), MinIO, Apicurio e Keycloak (dev mode).
Para o stack completo (infra + APIs + frontend), veja **Subir a stack completa** abaixo.

### Subir a stack completa (backend + frontend)

Para subir **tudo** — infra + as 3 APIs + o gateway + os 4 frontends Angular
conteinerizados — copie os dois templates e suba (nada mais a editar):

```bash
cp docker/.env.example docker/.env
cp docker/docker-compose.override.example.yml docker/docker-compose.override.yml
docker compose -f docker/docker-compose.yml \
               -f docker/docker-compose.override.yml up -d --build
```

**Pré-requisito:** `uniplus-api` e `uniplus-web` clonados lado a lado em
`repositories/` — o build dos frontends usa `context: ../../uniplus-web`. As
senhas do `.env.example` são de dev local e já vêm preenchidas; só `GOVBR_*`
fica vazio (gov.br é opcional em dev). A imagem do `geo-api` vem do GHCR
(`GEO_IMAGE_TAG`, pública). Se você já tinha o stack no ar antes desta mudança,
rode uma vez `docker compose -f docker/docker-compose.yml -f docker/docker-compose.override.yml down -v --remove-orphans`
(o `--import-realm` do Keycloak não reimporta um realm já existente).

#### Manter a infra local do Geo em dia

O repositório `unifesspa-geo-api` não publica tag `latest` — a versão consumida
localmente fica fixada em `GEO_IMAGE_TAG` (`docker/.env`). Como `docker/.env` e
`docker/docker-compose.override.yml` são **locais e gitignored**, um `git pull`
não os atualiza. Ao notar uma nova release do Geo:

1. Confira a tag mais recente na aba Releases do `unifesspa-geo-api` e atualize
   `GEO_IMAGE_TAG` no seu `docker/.env`.
2. Rode `diff docker/docker-compose.override.yml docker/docker-compose.override.example.yml`
   para conferir se o template ganhou variáveis novas desde a última vez que
   você copiou — o serviço `geo-api` costuma subir `healthy` mesmo com uma
   variável faltando (a validação pode ficar adiada para o primeiro uso real),
   então a ausência não aparece no health check, só ao exercitar a
   funcionalidade correspondente.
3. Propague sem recriar o stack inteiro:
   `docker compose -f docker/docker-compose.yml -f docker/docker-compose.override.yml pull geo-api`
   seguido de `up -d geo-api`.

| App | URL | Client OIDC |
|---|---|---|
| selecao | http://localhost:4200 | `selecao-web` |
| ingresso | http://localhost:4201 | `ingresso-web` |
| portal | http://localhost:4202 | `portal-web` |
| configuracao | http://localhost:4203 | `configuracao-web` |

Os frontends consomem as APIs por um único `apiUrl` (o gateway Traefik em
`http://localhost:5000`, que separa o tráfego geo/portal/monólito) e autenticam
no realm **`unifesspa`** — as 3 APIs validam esse mesmo realm por padrão. Login
de teste: usuário `admin` (ver **Sessões manuais longas** abaixo para a senha).

### Desenvolver o frontend com hot reload (`nx serve`)

Os apps `*-web` do override ocupam as portas 4200-4203. Para iterar no frontend
com hot reload, suba o stack **sem** os containers web e rode o `nx serve` na
mesma porta:

```bash
docker compose -f docker/docker-compose.yml -f docker/docker-compose.override.yml \
  up -d postgres redis kafka minio apicurio keycloak \
        uniplus-api geo-api portal-api traefik
# em uniplus-web:
npx nx serve selecao        # → :4200, apiUrl=http://localhost:5000
```

### Smoke / Newman (realm `unifesspa-dev-local`)

Scripts de smoke obtêm token via **password grant**, que exige o realm
`unifesspa-dev-local` (clients com `directAccessGrantsEnabled`). Realinhe as APIs
com o override `docker-compose.smoke.yml` e suba só infra + APIs (sem buildar os
frontends):

```bash
docker compose -f docker/docker-compose.yml \
               -f docker/docker-compose.override.yml \
               -f docker/docker-compose.smoke.yml \
               up -d postgres redis kafka minio apicurio keycloak \
                     uniplus-api geo-api portal-api
```

Com as APIs healthy, obtenha um token (password grant, realm `unifesspa-dev-local`,
client `selecao-web`, usuário `admin` / senha `Changeme!123` — não-temporária no
dev-local) e use-o nas chamadas autenticadas:

```bash
TOKEN=$(curl -s http://localhost:8080/realms/unifesspa-dev-local/protocol/openid-connect/token \
  -d grant_type=password -d client_id=selecao-web \
  -d username=admin -d password='Changeme!123' | jq -r .access_token)

# mutação autenticada — direto na API UniPlus (:5200) ou via gateway (:5000)
curl -s -X POST http://localhost:5200/api/organizacao/admin/instituicao \
  -H "Authorization: Bearer $TOKEN" -H 'Content-Type: application/json' -d '{ ... }'
```

Para o ciclo CRUD completo e automatizado (LIST/POST/idempotência/409/GET/PUT/DELETE
soft/404/histórico), use a skill **`/smoke-crud`** — ela assume este stack (com o
`smoke.yml`) no ar e cuida do token e das invariantes do contrato V1.

> **Sessões manuais longas (opcional):** o `accessTokenLifespan` do realm é 300s
> por padrão; refreshes frequentes podem atrapalhar testes interativos. Para
> ampliar para 30 min durante o teste:
> ```bash
> TOKEN=$(curl -s http://localhost:8080/realms/master/protocol/openid-connect/token \
>   -d grant_type=password -d client_id=admin-cli -d username=admin -d password=admin \
>   | jq -r .access_token)
> curl -s -X PUT http://localhost:8080/admin/realms/unifesspa \
>   -H "Authorization: Bearer $TOKEN" -H 'Content-Type: application/json' \
>   -d '{"accessTokenLifespan":1800}'
> ```
> **Usuário de teste** com role `plataforma-admin`: `admin`. A senha **semeada**
> no realm-export (`docker/keycloak/realm-export.json`) é `Changeme!123` e está
> marcada como **temporária** — no primeiro login o Keycloak exige a troca. Para
> uma senha fixa e conhecida (`E2eTest!123`, a usada pelos testes E2E), rode a
> suíte E2E uma vez (o `auth-setup` reseta a senha) **ou** redefina via Keycloak
> Admin API:
> ```bash
> ADMIN_ID=$(curl -s "http://localhost:8080/admin/realms/unifesspa/users?username=admin&exact=true" \
>   -H "Authorization: Bearer $TOKEN" | jq -r '.[0].id')
> curl -s -X PUT "http://localhost:8080/admin/realms/unifesspa/users/$ADMIN_ID/reset-password" \
>   -H "Authorization: Bearer $TOKEN" -H 'Content-Type: application/json' \
>   -d '{"type":"password","value":"E2eTest!123","temporary":false}'
> ```

---

## Estrutura do projeto

```
src/
  shared/
    Unifesspa.UniPlus.SharedKernel/           → Value objects, interfaces, domain events
    Unifesspa.UniPlus.Infrastructure.Common/  → Kafka, Redis, MinIO, Serilog, OpenTelemetry
  selecao/
    Unifesspa.UniPlus.Selecao.Domain/         → Entidades e regras de negócio
    Unifesspa.UniPlus.Selecao.Application/    → Use cases, DTOs, validações
    Unifesspa.UniPlus.Selecao.Infrastructure/ → EF Core, repositórios, integrações
    Unifesspa.UniPlus.Selecao.API/            → Controllers, middleware
  ingresso/
    Unifesspa.UniPlus.Ingresso.Domain/
    Unifesspa.UniPlus.Ingresso.Application/
    Unifesspa.UniPlus.Ingresso.Infrastructure/
    Unifesspa.UniPlus.Ingresso.API/
tests/
  Unifesspa.UniPlus.Selecao.Domain.Tests/
  Unifesspa.UniPlus.Selecao.Application.Tests/
  Unifesspa.UniPlus.Selecao.Integration.Tests/
  ...
```

---

## Fluxo de trabalho

### 1. Criar branch

> **Regra de ouro:** sem issue, sem código. Toda mudança não-trivial deve ter issue vinculada no GitHub antes de criar a branch.

```bash
git checkout main
git pull --rebase origin main
git checkout -b feature/{issue-number}-{slug}
```

**Convenção de nomes:**

| Prefixo | Quando usar |
|---|---|
| `feature/` | Nova funcionalidade |
| `fix/` | Correção de bug |
| `refactor/` | Refatoração sem mudança de comportamento |
| `test/` | Adição ou correção de testes |
| `chore/` | Configuração, CI/CD, dependências |

### 2. Implementar

Siga a ordem de implementação por camada — de dentro para fora:

1. **Domain** — entidades, value objects, domain events
2. **Application** — use cases (commands via `ICommandBus`, queries via `IQueryBus`, ambos sobre Wolverine), DTOs, validações (FluentValidation)
3. **Infrastructure** — EF Core configurations, repositórios, integrações externas
4. **API** — controllers, middleware, filtros

#### Padrão de handler

> **Nota temporária (#782):** o slice `Edital` usado historicamente como referência
> pedagógica deste padrão foi removido — era o agregado legado, pré-inversão
> ProcessoSeletivo↔Edital. O bloco de código abaixo ilustra a FORMA do padrão
> CQRS/Wolverine (ainda válida), mas os tipos citados (`CriarEditalCommand`,
> `IEditalRepository` etc.) não existem mais no código. Reescrita completa com um
> slice de referência vivo (`ProcessoSeletivo`) está planejada para a T4 (#785).

Commands implementam `ICommand<TResponse>`, queries implementam `IQuery<TResponse>` — ambos definidos em `Application.Abstractions/Messaging`. Handlers seguem a convenção do Wolverine: classe `static`, método `Handle` (`Task<TResponse>` ou `(TResponse, IEnumerable<object>)` para cascading messages) e dependências resolvidas como parâmetros do método.

```csharp
// Command + handler (forma ilustrativa — ver nota acima)
public sealed record CriarEditalCommand(...) : ICommand<Result<Guid>>;

public static class CriarEditalCommandHandler
{
    public static async Task<Result<Guid>> Handle(
        CriarEditalCommand command,
        IEditalRepository editalRepository,
        IUnitOfWork unitOfWork,
        CancellationToken cancellationToken)
    {
        // ... lógica de domínio + persistência
    }
}

// Query + handler (forma ilustrativa — ver nota acima)
public sealed record ObterEditalQuery(Guid Id) : IQuery<EditalDto?>;

public static class ObterEditalQueryHandler
{
    public static async Task<EditalDto?> Handle(
        ObterEditalQuery query,
        IEditalRepository editalRepository,
        CancellationToken cancellationToken) => /* ... */;
}
```

**Validação automática:** o middleware de validação FluentValidation do Wolverine (ativado por `UseFluentValidation` em `Infrastructure.Core/Messaging/Middleware/MessagingMiddlewarePolicies`) descobre todos os `IValidator<T>` registrados via `AddValidatorsFromAssembly` e valida o command/query antes do handler. Não chame `validator.ValidateAsync(...)` no handler — falhas viram `FluentValidation.ValidationException` capturada pelo `GlobalExceptionMiddleware` como `ProblemDetails 422`.

**Logging automático:** o `WolverineLoggingMiddleware` registra entrada/saída do handler com tempo de execução, removendo a necessidade de qualquer behavior de pipeline em código de aplicação. Logs específicos de domínio dentro do handler seguem o padrão `[LoggerMessage]` source generator (.NET 6+): a classe é `partial`, o método é `private static partial void Log{Acao}` decorado com `[LoggerMessage(Level = ..., Message = "...")]`, e o `ILogger` entra como primeiro parâmetro do método. Chamadas diretas a `logger.LogInformation(...)` etc. são bloqueadas pelo analisador `CA1848` (com `TreatWarningsAsErrors`) — o source generator evita avaliação de argumentos quando o log level está desativado, elimina boxing de value types e parseia o template uma única vez na compilação.

### 3. Testar

```bash
# Compilar sem warnings
dotnet build --warnaserrors

# Testes unitários
dotnet test --filter "Category!=Integration"

# Testes de integração (requer Docker)
dotnet test --filter "Category=Integration"

# Formatação
dotnet format --verify-no-changes
```

Todos os comandos acima devem passar antes de abrir o PR.

### 4. Commit

As regras de formato, tipos permitidos e convenções gerais de mensagem estão definidas no **[Guia de Commits e Integração § 1–4](docs/guia-commits-e-integracao.md)**. Siga-o na íntegra.

**Escopos válidos neste repositório:**

| Escopo | Quando usar |
|---|---|
| `selecao` | Código do módulo Seleção (`src/selecao/**`) |
| `ingresso` | Código do módulo Ingresso (`src/ingresso/**`) |
| `shared` | Código compartilhado (`src/shared/**`) |
| `sharedkernel` | SharedKernel especificamente (entidades base, value objects, Result pattern) |
| `domain` | Camada Domain de qualquer módulo (entidades, regras de negócio, eventos) |
| `application` | Camada Application (commands/queries Wolverine, validações FluentValidation) |
| `infra` | Camada Infrastructure genérica (Kafka, Redis, MinIO, OpenTelemetry) |
| `api` | Camada API (controllers, middleware, filtros) |
| `db` | Configurações de banco (EF Core, conexão, tuning) |
| `migrations` | Migrations EF Core |
| `auth` | Autenticação/autorização, Keycloak, Gov.br |
| `ci` | GitHub Actions, workflows, pipeline |
| `docker` | Dockerfiles, `docker-compose` |
| `deps` | Atualização de pacotes NuGet |

Escopos mais granulares podem ser usados livremente quando fizerem sentido (ex.: `selecao-domain`, `selecao-api`, `test(arch)`), mantendo imperativo presente e pt-BR.

**Alguns exemplos canônicos:**

```
feat(selecao): adiciona endpoint de homologação de inscrição
fix(ingresso): corrige cálculo de classificação por cota
refactor(sharedkernel): extrai value object NotaFinal
test(arch): adiciona testes de arquitetura para módulo Ingresso
feat(migrations): adiciona tabela de inscrições
chore(deps): atualiza WolverineFx para 5.32.1
```

Para uma bateria completa de exemplos (bons e ruins, com justificativas), consulte [Guia § 4](docs/guia-commits-e-integracao.md#4-exemplos-bons-e-ruins).

### 5. Rebase sobre `main`

Integração via **rebase** — sem merge commits poluindo o histórico. Procedimento completo e regras de ouro em **[Guia de Commits e Integração § 5–6](docs/guia-commits-e-integracao.md#5-política-de-integração-rebase-sobre-a-main)**.

Resumo aplicado ao `uniplus-api`: mantenha a branch rebaseada sobre `origin/main`, organize os commits com `rebase -i` (squash/reword/drop) antes do PR e use `git push --force-with-lease` ao reescrever história — nunca `--force` puro.

### 6. Pull Request

```bash
# Push da branch
git push -u origin feature/{issue-number}-{slug}

# Criar PR (Closes #N na descrição fecha a issue automaticamente no merge)
gh pr create --base main
```

O PR será criado com o template padrão. Preencha todas as seções.

---

## Regras de integração

Este repositório adota políticas de proteção de branch para garantir histórico limpo e previsível:

- Apenas **Rebase** é permitido
- **Merge commits** são desabilitados
- Histórico linear é obrigatório
- PRs exigem:
  - 1 aprovação
  - Status check `Build, Test and Coverage` passando
  - Branch atualizada com a `main`
  - Review realizado após o último push do PR

Essas regras são aplicadas automaticamente via GitHub aos colaboradores sem permissão administrativa. Casos excepcionais podem ser tratados por admins do repositório.

---

## Padrões de código

### Clean Architecture

| Regra | Detalhe |
|---|---|
| Domain não referencia nenhuma outra camada | Domain é puro — sem dependências de framework |
| Application referencia apenas Domain | Use cases dependem de interfaces, não de implementações |
| Infrastructure implementa interfaces do Domain/Application | Inversão de dependência |
| API referencia Application e Infrastructure (via DI) | Composição na raiz |
| Código de produção é indiferente ao ambiente para comportamento | [ADR-0053](docs/adrs/0053-zero-test-environment-branches-in-production-code.md): `IsEnvironment("...")` e `EnvironmentName == "..."` banidos em `src/`. Customização de teste vive em `ApiFactoryBase<TEntryPoint>`. HML/sanidade = Production semanticamente — Vault injeta config |

### Naming

| Elemento | Convenção | Exemplo |
|---|---|---|
| Classes, métodos, propriedades | PascalCase (inglês) | `CandidatoService`, `CalcularNotaFinal()` |
| Variáveis locais, parâmetros | camelCase (inglês) | `candidatoId`, `notaFinal` |
| Interfaces | `I` + PascalCase | `ICandidatoRepository` |
| Tabelas (EF Core) | PascalCase plural | `Candidatos`, `Inscricoes` |
| Strings user-facing | pt-BR | `"Inscrição realizada com sucesso"` |
| Mensagens de erro da API | pt-BR | `"CPF já cadastrado neste processo"` |

### Validação

- Usar **FluentValidation** para todas as validações de input
- Validar na camada Application (validators de commands/queries)
- Domain valida invariantes internas (construtor, métodos de domínio)

### Exception handling

- Nunca lançar `Exception` genérica — usar exceções de domínio tipadas
- API retorna `ProblemDetails` (RFC 7807) em todos os erros
- Incluir `traceId` nas respostas de erro

### Entity Framework e migrations

Referências canônicas:

- [`docs/guia-banco-de-dados.md`](docs/guia-banco-de-dados.md) — instruções operacionais (naming, tipos PG, soft delete, audit, workflow de migration, FAQ).
- [ADR-0054](docs/adrs/0054-naming-convention-e-strategy-migrations.md) — decisões binding (snake_case via `EFCore.NamingConventions`, forward-only, migration por Story).

Regras essenciais (mais detalhes no guia):

- **Configurações** via `IEntityTypeConfiguration<T>` (nunca data annotations).
- **Naming convention global** (`snake_case`) aplicada automaticamente pelo helper `UseUniPlusNpgsqlConventions` — não usar `HasColumnName` para mapear `CreatedAt → created_at`.
- **Migration por Story** que altera schema, com nomenclatura `{Verbo}{Objeto}` em pt-BR indicativo presente (`AdicionaCampoBonus`, `RemoveColunaObsoleta`). Sem squash inicial.
- **Forward-only**: revert = nova migration `Reverte<X>`. `Down()` proibido em produção.
- **Nunca editar migrations já aplicadas** — criar nova.
- **Soft delete obrigatório** via `HasQueryFilter(e => !e.IsDeleted)` + `SoftDeleteInterceptor` (já no helper).
- **Aplicação automática no startup** via `MigrationHostedService<TContext>` registrado antes do `WolverineRuntime` em cada Program.cs ([ADR-0039](docs/adrs/0039-provisioning-schema-wolverine-via-deploy.md) + [#419](https://github.com/unifesspa-edu-br/uniplus-api/issues/419)).

---

## Segurança

### Obrigatório em toda contribuição

- **PII masking:** CPF nunca aparece completo em logs — usar `***.999.999-**` (padrão CGU/IN Unifesspa, Parecer DPO 002/2026)
- **Sem secrets no código:** nunca hardcodar credenciais, tokens ou chaves
- **Soft delete:** nunca usar `DELETE` físico — marcar como deletado
- **Autorização:** todo endpoint deve ter `[Authorize]` com policy adequada
- **Input sanitizado:** não confiar em input do usuário — validar e sanitizar
- **Upload seguro:** validar MIME type, tamanho máximo e extensões permitidas

### LGPD

- Dados pessoais (CPF, nome, endereço, renda) devem ser criptografados em repouso
- Logs nunca devem conter dados pessoais identificáveis
- Respostas de API devem retornar apenas os campos necessários
- Consentimento deve ser verificado antes de processar dados sensíveis opcionais

---

## Testes

### Obrigatório

| Tipo | Onde | Framework | Quando |
|---|---|---|---|
| Unitário | Domain, Application | xUnit + AwesomeAssertions + NSubstitute | Toda lógica de negócio (ver [ADR-0021](docs/adrs/0021-adocao-awesomeassertions-como-biblioteca-de-assertions.md) para a escolha da biblioteca de assertions) |
| Integração | Infrastructure, API | Testcontainers + WebApplicationFactory | Endpoints e repositórios |

### Recomendado

| Tipo | Onde | Framework | Quando |
|---|---|---|---|
| Mutação | Domain | Stryker.NET | Motor de classificação, fórmulas legais |
| Property-based | Domain | FsCheck | Cálculos matemáticos, fórmulas de nota |
| Contrato | API | Pact | Eventos Kafka entre módulos |

### Convenções de teste

```csharp
// Naming: Método_Cenário_ResultadoEsperado
[Fact]
public void CalcularNotaFinal_ComBonusRegional_DeveAplicarBonusSobreTotal()
{
    // Arrange
    var candidato = CandidatoBuilder.ComNota(8.5m).ComBonus(0.20m).Build();

    // Act
    var resultado = candidato.CalcularNotaFinal();

    // Assert
    resultado.Should().Be(10.2m);
}
```

- Usar **Bogus** para dados de teste (nomes, CPFs, endereços fictícios)
- Usar **Testcontainers** para testes de integração (PostgreSQL, Redis, Kafka)
- Nunca usar dados reais de candidatos em testes

---

## Quality gates

O PR será bloqueado se qualquer gate falhar:

- [ ] Build sem erros e sem warnings (`dotnet build --warnaserrors`)
- [ ] Todos os testes passando
- [ ] Cobertura de código >= 80% nas camadas Domain e Application
- [ ] SonarQube: zero issues críticos ou bloqueadores
- [ ] Nenhuma vulnerabilidade crítica em dependências
- [ ] Formatação correta (`dotnet format`)
- [ ] Pacotes proibidos ausentes (`bash tools/forbidden-deps/check.sh`) — ver [§ Pacotes proibidos](#pacotes-proibidos)

### Pacotes proibidos

Algumas dependências foram banidas por ADR (licença incompatível, segurança, manutenção interrompida etc.). O job **`Forbidden dependencies`** do CI executa [`tools/forbidden-deps/check.sh`](tools/forbidden-deps/check.sh) e bloqueia o merge se encontrar reintrodução. Veja [`tools/forbidden-deps/README.md`](tools/forbidden-deps/README.md) para o detalhamento do mecanismo.

Para checar localmente antes do push:

```bash
bash tools/forbidden-deps/check.sh
```

Banidos atualmente:

| Pacote | Substituto | ADR |
|--------|-----------|-----|
| `FluentAssertions` (v8+) | [`AwesomeAssertions`](https://www.nuget.org/packages/AwesomeAssertions) | [ADR-0021](docs/adrs/0021-adocao-awesomeassertions-como-biblioteca-de-assertions.md) |

Para banir uma nova dependência: criar/atualizar a ADR correspondente, adicionar entrada em [`tools/forbidden-deps/check.sh`](tools/forbidden-deps/check.sh) e atualizar a tabela em [`tools/forbidden-deps/README.md`](tools/forbidden-deps/README.md).

---

## Auto-update de dependências (Dependabot)

O repositório usa **Dependabot** para detectar updates de pacotes NuGet (`Directory.Packages.props` central) e GitHub Actions, configurado em [`.github/dependabot.yml`](.github/dependabot.yml).

### Como funciona

- **Schedule semanal:** segunda 06:00 BRT — antes do daily.
- **Grouping:** PRs agrupam pacotes do mesmo trem para reduzir ruído (todos `OpenTelemetry.*` num PR só, todos `WolverineFx*` em outro, e assim por diante).
- **Major bumps são ignorados** pelo bot — versões `X.0.0` exigem revisão arquitetural manual (release notes, breaking changes, ADR se necessário). Quando precisar, o dev abre o PR explicitamente.
- **Patch e minor** entram automaticamente como PRs com label `chore` + `deps` (ou `chore` + `ci` para Actions).

### Tratamento dos PRs do bot

1. **CI verde primeiro.** Mesmas 5 checagens dos PRs humanos (build/test/coverage, ADR lint, Spectral, forbidden-deps, PR author org member). Se o `NU1902` (vulnerabilidade) aparecer no restore, o bump já está corrigindo — PR é ainda mais prioritário.
2. **Auto-merge condicional.** Patches sem CVE conhecida podem ser mergeados sem review humano profundo (CI verde basta). Minors merecem leitura rápida do diff de comportamento. Majors **nunca** chegam aqui (ignorados pelo bot).
3. **Validação local opcional.** Para mudanças sensíveis (Wolverine, EF Core, Npgsql, Serilog), rodar `dotnet test UniPlus.slnx` localmente antes de aprovar.

### Como adicionar um novo grupo

Editar `.github/dependabot.yml` adicionando entrada em `updates[0].groups`. Padrão glob (`*` por sufixo) já cobre famílias inteiras.

---

## Checklist antes de pedir merge

Complementa os Quality gates acima (verificações de CI automáticas) com higiene de commits e branch — essas são verificações humanas:

- [ ] Branch no padrão (`feature/{issue}-{slug}`, `fix/{issue}-{slug}`, `chore/{slug}` ou `docs/{slug}`)
- [ ] Issue vinculada no GitHub (regra de ouro: sem issue, sem código)
- [ ] Todos os commits seguem Conventional Commits em pt-BR
- [ ] Verbo no **imperativo presente** (`adiciona`, `corrige`, `remove`, `atualiza`) — nunca infinitivo (`adicionar`) nem gerúndio (`Adicionando`)
- [ ] Subject em minúsculas, sem ponto final, máx. ~72 caracteres
- [ ] Escopo presente (do conjunto definido em [§ Commit](#4-commit)) quando aplicável
- [ ] Branch **rebaseada** sobre a `main` mais recente
- [ ] Commits "WIP" / "ajustes do PR" foram **squashados** via `git rebase -i`
- [ ] PR vinculado à issue com `Closes #N` na descrição

---

## Revisão de código

Todo PR passa por revisão humana. O revisor verifica:

1. **Segurança** — PII, injection, OWASP, LGPD
2. **Arquitetura** — Clean Architecture, SOLID, acoplamento entre módulos
3. **Qualidade** — legibilidade, complexidade, duplicação
4. **Testes** — cobertura, casos de borda, assertions significativas
5. **Domínio** — regras de negócio corretas, conformidade legal

O PR precisa de **1 aprovação** para ser mergeado.

---

## Dúvidas

- Abra uma issue com a label `needs-refinement` para discutir abordagens
- Consulte a documentação em [uniplus-docs](https://github.com/unifesspa-edu-br/uniplus-docs) para contexto de requisitos e regras de negócio
