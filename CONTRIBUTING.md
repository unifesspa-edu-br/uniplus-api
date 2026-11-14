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
**apenas a infra** (sem a API nem o frontend), a partir de `docker/` e com o `.env` copiado:

```bash
docker compose up -d postgres redis kafka kafka-init minio minio-init rabbitmq mailpit apicurio keycloak clamd-verificador verificador
```

Isso inicia: PostgreSQL 18, Redis, Kafka (KRaft) e o `kafka-init`, MinIO, RabbitMQ, Mailpit, Apicurio, Keycloak
(dev mode) e o verificador de arquivos (`clamd-verificador` e `verificador`). Para ter também o Keycloak
configurado e os gestores de acessos instalados, veja **Ambiente pronto para uso** abaixo.
Para o stack completo (infra + APIs + frontend), veja **Subir a stack completa** abaixo.

### Ambiente pronto para uso

Para ir de um clone novo a um ambiente com a API no ar, o Keycloak configurado e os dois primeiros gestores
de acessos instalados, copie o `.env` e suba o compose (só Docker):

```bash
cd docker
cp .env.example .env
docker compose up --build
```

O `.env.example` traz todos os valores do ambiente local. O compose sobe a infraestrutura, o verificador de
arquivos, a API e o serviço `ambiente-init`, que configura o Keycloak e instala os gestores pela API. Rodar de
novo não altera o estado. Para trocar uma porta ou subir um segundo ambiente, edite as variáveis `*_PORT` (e
`COMPOSE_PROJECT_NAME`) no `.env`. Detalhes e o procedimento manual em
[`docs/guia-instalacao-gestores-ambiente-local.md`](docs/guia-instalacao-gestores-ambiente-local.md).

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

> O Geo é um serviço externo desde a [ADR-0099](docs/adrs/0099-geo-como-repositorio-dedicado.md).
> Para saber quais referências ao Geo são legítimas neste repositório (infra
> local, snapshots consumidores da ADR-0096, registro histórico) e quais seriam
> resíduo da extração, ver [`docs/geo-servico-externo.md`](docs/geo-servico-externo.md).

O repositório `unifesspa-geo-api` não publica tag `latest` — a versão consumida
localmente fica fixada em `GEO_IMAGE_TAG` (`docker/.env`). Como `docker/.env` é
**local e gitignored**, um `git pull` não o atualiza. Ao notar uma nova release do Geo:

1. Confira a tag mais recente na aba Releases do `unifesspa-geo-api` e atualize
   `GEO_IMAGE_TAG` no seu `docker/.env`.
2. Propague sem recriar o stack inteiro, a partir de `docker/`:
   `docker compose pull geo-api` seguido de `docker compose up -d geo-api`.

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

### Explorar a API pela documentação navegável

O stack completo publica os seis documentos OpenAPI (os cinco do host mais o do
Portal) numa interface em **http://localhost:5299**, com seletor no topo. O host
registra apenas `MapOpenApi`, que serve o JSON: desde o .NET 9 o
`Microsoft.AspNetCore.OpenApi` gera o documento mas não fornece interface, então
a UI é escolha de quem hospeda — por isso ela vive no override de
desenvolvimento e não existe em HML/PROD.

Interface, documentos e API são servidos pelo **mesmo host** (o serviço
`openapi-docs` faz proxy de `/openapi/`, `/openapi-portal/` e `/api/`). Isso não
é detalhe de arrumação: o navegador é quem busca os documentos e dispara o "Try
it out", e uma UI em porta separada colocaria essas chamadas em outra origem —
a API não emite `Access-Control-Allow-Origin`, então todos os documentos
falhariam em CORS.

O seletor **Servers** já vem no ambiente local: o documento anuncia
`http://localhost:5299` como primeiro servidor porque o override de exemplo define
`UniPlus__OpenApi__LocalServerUrl` (no compose base, sem o proxy, o documento anuncia a porta da própria API). Sem isso o "Try it out" dispararia contra a
URL de produção — o que falha em CORS e, pior, manda requisição e credencial
para o ambiente errado. Em HML e PROD a variável não existe e o documento volta
a anunciar apenas Produção e Homologação.

Para exercitar rotas protegidas, clique em **Authorize** e cole **apenas o
token**, sem o prefixo `Bearer` — a interface o acrescenta ao cabeçalho. Colar
`Bearer <token>` gera `Authorization: Bearer Bearer <token>` e um `401` que
parece expiração. As rotas administrativas exigem a role `plataforma-admin`.

O `PERSIST_AUTHORIZATION` guarda o token entre recargas, mas não o renova: ao
trocar de token, use **Authorize → Logout** antes de colar o novo, senão a
interface continua enviando o anterior.

**De onde tirar o token depende de como você subiu a stack.** No stack completo
acima, as APIs validam o realm `unifesspa`, cujos clients `*-web` não têm direct
access grant — o token vem do fluxo de login do navegador, pelos frontends em
4200-4203. Para obtê-lo por linha de comando, suba com o override de smoke (ver
**Smoke / Newman** abaixo), que realinha as APIs ao realm `unifesspa-dev-local`:

```bash
curl -s -X POST http://localhost:8080/realms/unifesspa-dev-local/protocol/openid-connect/token \
  -d client_id=selecao-web -d username=admin -d password='Changeme!123' -d grant_type=password \
  | jq -r .access_token
```

Um token do realm errado é recusado pelo emissor e toda chamada volta 401 —
conferir o realm antes de investigar o token.

O token é aceito porque o documento declara o esquema de segurança Bearer e cada
rota protegida o referencia — a declaração sai do mesmo lugar que já adiciona o
`401`/`403` ao contrato, a partir da presença de `[Authorize]` no endpoint.

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
> marcada como **temporária** — no primeiro login o Keycloak exige a troca.
> `scripts/setup-keycloak-dev.sh` já reseta essa senha para `Uni+Teste26`,
> não temporária (ver `docker/keycloak/README.md`) — rodar o script antes
> de tentar ROPC com `admin`. Para uma senha fixa e conhecida diferente
> (`E2eTest!123`, a usada pelos testes E2E), rode a suíte E2E uma vez (o
> `auth-setup` reseta a senha) **ou** redefina via Keycloak Admin API:
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

Commands implementam `ICommand<TResponse>`, queries implementam `IQuery<TResponse>` — ambos definidos em `Application.Abstractions/Messaging`. Handlers seguem a convenção do Wolverine: classe `static`, método `Handle` (`Task<TResponse>` ou `(TResponse, IEnumerable<object>)` para cascading messages) e dependências resolvidas como parâmetros do método.

Slice de referência (Story #759, T4 #785): `src/selecao/Unifesspa.UniPlus.Selecao.Application/Commands/ProcessosSeletivos/PublicarProcessoSeletivoCommandHandler.cs`.

```csharp
// Command + handler que muta agregado e emite domain event (cascading)
public sealed record PublicarProcessoSeletivoCommand(
    Guid ProcessoSeletivoId,
    string? Numero,
    DateOnly PeriodoInscricaoInicio,
    DateOnly PeriodoInscricaoFim,
    Guid DocumentoEditalId) : ICommand<Result>;

public static class PublicarProcessoSeletivoCommandHandler
{
    public static async Task<(Result Resposta, IEnumerable<object> Eventos)> Handle(
        PublicarProcessoSeletivoCommand command,
        IProcessoSeletivoRepository processoSeletivoRepository,
        IUnitOfWork unitOfWork,
        CancellationToken cancellationToken)
    {
        // ... carrega o agregado, chama ProcessoSeletivo.Publicar, persiste ...
        return (Result.Success(), processo.DequeueDomainEvents().Cast<object>());
    }
}

// Query + handler (leitura pura, sem cascading)
public sealed record ObterProcessoSeletivoQuery(Guid Id) : IQuery<ProcessoSeletivoDto?>;

public static class ObterProcessoSeletivoQueryHandler
{
    public static async Task<ProcessoSeletivoDto?> Handle(
        ObterProcessoSeletivoQuery query,
        IProcessoSeletivoRepository processoSeletivoRepository,
        CancellationToken cancellationToken) => /* ... */;
}
```

**Validação automática:** o middleware de validação FluentValidation do Wolverine (ativado por `UseFluentValidation` em `Infrastructure.Core/Messaging/Middleware/MessagingMiddlewarePolicies`) descobre todos os `IValidator<T>` registrados via `AddValidatorsFromAssembly` e valida o command/query antes do handler. Não chame `validator.ValidateAsync(...)` no handler — falhas viram `FluentValidation.ValidationException` capturada pelo `GlobalExceptionMiddleware` como `ProblemDetails 422`.

**Logging automático:** o `WolverineLoggingMiddleware` registra entrada/saída do handler com tempo de execução, removendo a necessidade de qualquer behavior de pipeline em código de aplicação. Logs específicos de domínio dentro do handler seguem o padrão `[LoggerMessage]` source generator (.NET 6+): a classe é `partial`, o método é `private static partial void Log{Acao}` decorado com `[LoggerMessage(Level = ..., Message = "...")]`, e o `ILogger` entra como primeiro parâmetro do método. Chamadas diretas a `logger.LogInformation(...)` etc. são bloqueadas pelo analisador `CA1848` (com `TreatWarningsAsErrors`) — o source generator evita avaliação de argumentos quando o log level está desativado, elimina boxing de value types e parseia o template uma única vez na compilação.

### 3. Testar

```bash
# Compilar: só os avisos das métricas de qualidade (MA0051, S104, S107, S3776, S1541) são admitidos
dotnet build

# Testes unitários
dotnet test --filter "FullyQualifiedName!~IntegrationTests"

# Testes de integração (requer Docker)
dotnet test --filter "FullyQualifiedName~IntegrationTests"

# Formatação — a flag é OBRIGATÓRIA (ver aviso abaixo)
dotnet format --verify-no-changes --exclude-diagnostics CA1515
```

O split acima é pelo **nome do projeto** (`tests/*.IntegrationTests` precisa de Docker; o resto,
não), não por marcação individual de classe — o nome é estrutural e verificável, uma marcação por
classe depende de disciplina e envelhece em silêncio a cada classe nova. Ver AGENTS.md § Testing
Guidelines.

Todos os comandos acima devem passar antes de abrir o PR. O de formatação é o mesmo que o job
`Formatação (dotnet format)` roda no CI, e ele bloqueia o merge — ver
[§ Formatação e o filtro CA1515](#formatação-e-o-filtro-ca1515).

> ⚠️ **Nunca rode `dotnet format` sem `--exclude-diagnostics CA1515`.** Sem a flag, o comando
> converte as classes de teste de `public` para `internal` e quebra o build inteiro — 688 erros,
> entre eles `xUnit1000: Test classes must be public`. Detalhes em
> [§ Formatação e o filtro CA1515](#formatação-e-o-filtro-ca1515).

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

### Código que se explica

O código é a primeira documentação. Quem lê um método precisa entender o que ele faz pelo nome e pelo corpo, sem depender de comentário. Antes de escrever um comentário, tente dizer a mesma coisa no próprio código.

**Nomes expressivos e semânticos.** O nome diz o que a coisa é ou faz no domínio, não como é feita.

| Evite | Prefira | Por quê |
|---|---|---|
| `ProcessarDados()`, `Executar()` | `HomologarInscricoes()` | o verbo e o objeto dizem a intenção |
| `lista`, `dados`, `aux`, `temp`, `obj` | `inscricoesHomologadas` | a variável diz o que guarda |
| `bool flag`, `bool status` | `EstaVigente`, `PodeRecorrer` | booleano se lê como pergunta |
| `int tipo` com números mágicos | `enum SituacaoDoPedido` | o valor tem nome |
| `qtdInscCand` | `quantidadeDeInscricoesDoCandidato` | abreviação só se for do domínio (CPF, ENEM) |

**C# idiomático.** Use a linguagem e a biblioteca padrão em vez de código feito à mão:

- `switch` de expressão e pattern matching (`is null`, `is { Situacao: SituacaoDoPedido.Pendente }`) no lugar de cadeias de `if`;
- `record` para valor imutável;
- `ArgumentNullException.ThrowIfNull` e retorno antecipado (guard clause) no lugar de `if` aninhado;
- LINQ quando deixa a intenção mais clara, e laço quando a consulta esconde efeito colateral ou está em caminho quente;
- nullable reference types levados a sério: sem `!` para calar o compilador.

**Extraia em vez de comentar.** Um bloco que precisa de comentário para dizer o que faz vira um método com esse nome, ou uma variável explicativa:

```csharp
// Evite: o comentário repete a condição
// Verifica se o período já terminou e se ainda não houve decisão
if (agora > periodo.Fim && pedido.DecididoEm is null) { … }

// Prefira: o nome diz o quê
if (periodo.EncerradoEm(agora) && pedido.EstaPendente) { … }
```

#### Comentários

Comentário explica o **porquê** que o código não consegue mostrar:

- a regra de negócio por trás de uma decisão, com o `UNI-REQ` correspondente;
- o motivo de uma escolha que parece estranha;
- uma limitação externa, como comportamento de biblioteca, banco ou provedor;
- um cuidado de concorrência, segurança ou LGPD.

```csharp
// Ordena também pelo Id: duas inscrições podem ter o mesmo instante, e a paginação
// por keyset precisa de ordem total para não repetir nem pular linhas.
.OrderBy(i => i.RecebidaEm).ThenBy(i => i.Id)
```

Não se comenta o que o código já diz. O comentário também não conta histórico ("antes era assim", "corrigido no PR tal") — o git conta —, e código comentado não fica no repositório. Comentário desatualizado é defeito: ao mudar o código, revise o comentário ao lado. Comentário dentro do método conta na MA0051 ([§ Métricas de qualidade](#métricas-de-qualidade)), mais um motivo para extrair em vez de explicar.

#### Documentação XML

Documentar com XML (`///`) os tipos e membros públicos é boa prática, principalmente o que outro módulo ou camada consome: contratos, abstrações de `shared`, agregados e serviços de domínio. A documentação diz o que o nome não diz:

- **propósito:** para que o tipo ou o método existe;
- **contrato:** o que exige (pré-condições), o que garante e o que devolve na falha;
- **exceções:** `<exception>`, quando o método lança;
- **unidade e formato:** fuso, moeda, unidade de medida.

```csharp
/// <summary>
/// Concede o perfil a partir de um pedido já aprovado, presa à versão do perfil aprovada no pedido.
/// </summary>
/// <returns>
/// Falha <c>PedidoErrors.NaoAprovado</c> quando o pedido ainda não foi aprovado.
/// </returns>
public Result<Concessao> Conceder(PedidoDeConcessao pedido, DateTimeOffset agora)
```

Documentação que só repete o nome não acrescenta nada: `/// <summary>Obtém o pedido.</summary>` em `ObterPedido` não diz mais que o nome, e fica de fora. Membro privado e teste se explicam pelo nome; documentação neles é exceção, para o porquê não óbvio. A documentação XML acima do método não conta na MA0051, e nenhum comentário conta na S104.

### Value objects no lugar de primitivos

Conceito de domínio com significado próprio não trafega como `string`, `Guid`, `DateTimeOffset` ou `decimal` soltos. Um e-mail institucional, uma justificativa, um período de vigência ou uma nota têm regras e nome; o primitivo esconde as duas coisas e deixa o compilador aceitar um valor no lugar do outro. Modele o conceito como `record` (value object) e evite a obsessão por primitivos.

**Agrupe só o que é coeso.** Um value object ou um objeto de parâmetro reúne dados que andam juntos e que, juntos, significam algo no domínio. É proibido criar um agrupamento artificial só para baixar a contagem de parâmetros: isso é código escrito para caber na métrica, que a [§ Métricas de qualidade](#métricas-de-qualidade) já recusa. Se um método recebe parâmetros demais e eles não formam um conceito, o problema é de responsabilidade, não de assinatura.

**Validação conforme a [ADR-0125](docs/adrs/0125-dominio-fonte-unica-validacao-sem-fluentvalidation-duplicado.md).** O `record` carrega os dados; a validação fica acumulada num único `errors[]` no agregado. Um value object que valide algo contribui para o mesmo lote, sem fail-fast: a criação devolve todos os erros de uma vez, nunca só o primeiro.

```csharp
// Evite: primitivos soltos; e-mail do beneficiário, justificativa e validade sugerida não dizem o que são
public static Result<PedidoDeConcessao> Criar(
    PessoaNoToken solicitante, string? emailDoBeneficiario, Perfil perfil, EscopoDaConcessao escopo,
    string? justificativa, IDominiosInstitucionais dominios, ICatalogoDePermissoesConsultavel catalogo,
    DateTimeOffset instante, DateTimeOffset? validaAteSugerida = null)

// Prefira: cada grupo é um conceito do pedido, e os dados de cada um andam juntos.
// Quem pede e para quem se pede: o solicitante e o e-mail do beneficiário
public sealed record PartesDoPedido(PessoaNoToken Solicitante, string? EmailDoBeneficiario)
{
    // Contribui para o lote do agregado; não interrompe no primeiro erro
    public void Validar(IDominiosInstitucionais dominios, List<DomainError> errors) { … }
}

// O que o solicitante declara: a justificativa e a validade sugerida (e, quando houver, o documento anexado)
public sealed record DeclaracaoDoPedido(string? Justificativa, DateTimeOffset? ValidaAteSugerida)
{
    public void Validar(DateTimeOffset instante, List<DomainError> errors) { … }
}

public static Result<PedidoDeConcessao> Criar(
    PartesDoPedido partes, Perfil perfil, EscopoDaConcessao escopo, DeclaracaoDoPedido declaracao,
    IDominiosInstitucionais dominios, ICatalogoDePermissoesConsultavel catalogo, DateTimeOffset instante)
{
    List<DomainError> errors = [];
    partes.Validar(dominios, errors);
    declaracao.Validar(instante, errors);
    // … demais invariantes do agregado acumulam no mesmo errors[]
    return errors.Count > 0 ? Result<PedidoDeConcessao>.ValidationFailure(errors) : …;
}
```

```csharp
// Contra-exemplo: agrupamento artificial, só para a assinatura caber em 7 parâmetros.
// Serviços, catálogo e instante não formam um conceito de domínio com o perfil.
public sealed record ContextoDoCriar(Perfil Perfil, IDominiosInstitucionais Dominios,
    ICatalogoDePermissoesConsultavel Catalogo, DateTimeOffset Instante);
```

**Builder só nos testes.** No domínio não se usa builder: ele tira do compilador a garantia de que os dados obrigatórios foram informados e espalha as invariantes entre o builder e o agregado. A construção de domínio passa pela fábrica (`Criar`), com parâmetros obrigatórios e validação acumulada. Nos testes de cenário, use Test Data Builders com padrões válidos e métodos expressivos no lugar de atalhos de montagem repetidos (ver [§ Convenções de teste](#convenções-de-teste)). Os testes da própria fábrica continuam chamando a fábrica diretamente, porque ela é o alvo do teste.

### Formatação e o filtro CA1515

O comando de formatação deste repositório é sempre:

```bash
dotnet format --verify-no-changes --exclude-diagnostics CA1515
```

É exatamente o que o job **`Formatação (dotnet format)`** executa no CI, e ele bloqueia o merge.
Rode antes do push para não descobrir divergência só no PR.

**A flag `--exclude-diagnostics CA1515` não é opcional — não a remova.**

O `CA1515` ("Consider making public types internal") comporta-se de forma diferente nas duas
ferramentas. O pacote `xunit.analyzers` fornece um `DiagnosticSuppressor` para o `CA1515`, que o
`dotnet build` respeita — por isso as classes `public sealed class XTests` não são sinalizadas na
compilação. O `dotnet format` não aplica esse suppressor: enxerga os 339 tipos públicos dos
projetos de teste como violações e, quando executado sem `--verify-no-changes`, **aplica a
"correção"** — converte todos para `internal`. O xUnit exige classes públicas para descobrir os
testes, então o resultado é o build inteiro quebrado com `xUnit1000: Test classes must be public`.

Manter o `CA1515` ativo no build é desejável: ele pega tipos públicos legítimos que deveriam ser
`internal`, inclusive dentro de projetos de teste que o suppressor não cobre por não conterem
métodos de teste (helpers e fixtures). Os atributos `[SuppressMessage]` que ainda existem em
`tests/*/Infrastructure/*ApiFactory.cs` são dívida: supressão é proibida em código novo
(ver [§ Métricas de qualidade](#métricas-de-qualidade)). Por isso a regra é filtrada apenas no
comando do `dotnet format`, e não silenciada no `.editorconfig`.

A migration não é excluída do gate: o que o `dotnet ef` gera fora da política (UTF-8 BOM, usings sem
separação de grupo) está declarado na seção `[src/**/Migrations/**.cs]` do `.editorconfig`, e a migration
passa pelo `dotnet format` como qualquer outro arquivo.

### `git blame` e commits de formatação

O commit que normalizou a formatação do repositório tocou 205 arquivos, o que faria o `git blame`
apontar para ele em vez de para o commit que realmente escreveu cada linha. O
`.git-blame-ignore-revs` na raiz resolve isso — o GitHub o respeita automaticamente na web.
Localmente, configure uma vez por clone:

```bash
git config blame.ignoreRevsFile .git-blame-ignore-revs
```

Ao adicionar um commit novo a esse arquivo, inclua apenas mudanças comprovadamente sem alteração
de comportamento (espaçamento, ordenação de `using`, encoding) — nunca renomeações de símbolo,
que o blame precisa mostrar.

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
- [ADR-0170](docs/adrs/0170-contexto-de-persistencia-unico-para-os-modulos-do-monolito.md) — o contexto único, a migration única e os scripts SQL por módulo.
- [ADR-0054](docs/adrs/0054-naming-convention-e-strategy-migrations.md) — naming (`snake_case` via `EFCore.NamingConventions`).

Regras essenciais (mais detalhes no guia):

- **Configurações** via `IEntityTypeConfiguration<T>` (nunca data annotations).
- **Naming convention global** (`snake_case`) aplicada automaticamente pelo helper `UseUniPlusNpgsqlConventions` — não usar `HasColumnName` para mapear `CreatedAt → created_at`.
- **Uma migration só: `Inicial`**, do `MonolitoDbContext`, no projeto `src/host/Unifesspa.UniPlus.Monolito.Persistencia` (pasta `Migrations`). Não há migration por módulo nem por story. Mudou o modelo: apague a migration e o snapshot e gere de novo (`dotnet ef migrations add Inicial --project src/host/Unifesspa.UniPlus.Monolito.Persistencia --startup-project src/host/Unifesspa.UniPlus.Host --context MonolitoDbContext --output-dir Migrations`).
- **Ao rebasear, descarte a migration própria e gere outra** a partir do modelo já rebaseado: o snapshot único concentra os conflitos entre branches, e resolvê-los à mão é fonte de erro.
- **`has-pending-model-changes` limpo**: o job de build do CI roda `dotnet ef migrations has-pending-model-changes` e falha quando o modelo mudou e a migration não foi gerada de novo. Rode antes do push, depois de recompilar (com `--no-build` ele lê o assembly sem a migration nova).
- **O que o EF não representa vai em script SQL do módulo**, nunca em `migrationBuilder.Sql` à mão: extensões, funções, gatilhos, exclusion constraints, índices de expressão e sementes por SQL ficam em `Persistence/Scripts/Pre/*.sql` (antes da migration) e `Persistence/Scripts/*.sql` (depois dela), embutidos no assembly do módulo (`<EmbeddedResource Include="Persistence\Scripts\**\*.sql" />`), numerados (`001_...`) e **idempotentes** (`IF NOT EXISTS`, `CREATE OR REPLACE`, `DROP ... IF EXISTS` antes de `ADD`, `ON CONFLICT DO NOTHING`): cada um roda numa transação própria, e reaplicar é no-op.
- **Objetos compartilhados existem uma vez só**, no `Infrastructure.Core` (`btree_gist` e `plataforma.normalizar_para_comparacao`), e rodam primeiro. Script de um módulo não referencia o esquema de outro.
- **Sementes por código** (o que depende de regra da aplicação, não de SQL fixo) ficam na etapa posterior do módulo (`IContribuicaoDoModulo.ExecutarEtapaPosteriorDaMigrationAsync`), que roda depois dos scripts pós.
- **Ordem da subida** (uma vez por banco, sob trava consultiva): scripts `Pre` (compartilhados, depois os dos módulos) → `MigrateAsync` → scripts pós (mesma ordem) → etapa posterior de cada módulo. Roda na subida padrão e no Job `ApplyAndExit`; as fixtures de teste usam `UniPlusDbContext.SubirOBancoAsync()`, que cobre as três primeiras fases.
- **Soft delete opt-in** (`SoftDeletableEntity`): o filtro `!IsDeleted` e o `SoftDeleteInterceptor` vêm da convenção do modelo e do helper, sem `HasQueryFilter` manual.
- **Aplicação automática no startup** pelo `MigrationHostedService<TContext>` registrado por `AddMonolitoPersistencia` antes do `WolverineRuntime` ([ADR-0039](docs/adrs/0039-provisioning-schema-wolverine-via-deploy.md) + [#419](https://github.com/unifesspa-edu-br/uniplus-api/issues/419)).

### Métricas de qualidade

As métricas abaixo valem para todo código que o PR cria ou altera, de produção e de teste. Cada uma é medida por um analisador de mercado que roda em toda build ([ADR-0161](docs/adrs/0161-metricas-medidas-pelos-analisadores.md)), e **o número que vale é o do aviso do analisador**: não há contagem paralela. O texto abaixo explica como cada um conta, para você prever o aviso antes de compilar. A revisão cobra os limites no código novo ou alterado e julga as duas regras que nenhum analisador mede (5 e 6).

| # | Regra | Limite | Analisador | Produção | Testes |
|---|---|---|---|---|---|
| 1 | Linhas por método | 15 | MA0051 (Meziantou) | sim | sim |
| 2 | Linhas com código por arquivo | 400 | S104 (Sonar) | sim | sim |
| 3 | Parâmetros por método ou construtor | 7 | S107 (Sonar) | sim | sim |
| 4 | Complexidade cognitiva por método | 15 | S3776 (Sonar) | sim | sim |
| 4 | Complexidade ciclomática por método | 10 | S1541 (Sonar) | sim | não roda em projeto de teste |
| 5 | Supressão de aviso do analisador | proibida | revisão ([ADR-0160](docs/adrs/0160-supressao-de-aviso-do-analisador-proibida.md)) | sim | sim |
| 6 | Código escrito só para caber na métrica | reprovado | revisão | sim | sim |

Os limites ficam no `.editorconfig` (MA0051) e no `SonarLint.xml` (regras do Sonar). Mudar um limite exige mudar a ADR-0161 e o arquivo de configuração no mesmo PR.

**1. Método: até 15 linhas pela MA0051.** A MA0051 conta as linhas do corpo: da linha da chave de abertura até a linha da chave de fechamento, menos uma. Com a chave em linha própria, padrão do projeto, isso dá as linhas entre as chaves mais uma.

- **Contam:** toda linha dentro do corpo, inclusive linha em branco, comentário de qualquer tipo (`//`, `/* */`, `///`) e linha só com chave de bloco interno.
- **Não contam:** a assinatura (mesmo quebrada em várias linhas), os atributos e a documentação XML acima do método.
- **Corpo de expressão (`=>`):** contam as linhas depois da linha do `=>`; um método de uma linha só conta 0.
- **O que é medido:** método, construtor, acessor de propriedade (`get`/`set` com corpo) e função local. Lambda e método anônimo contam dentro do método que os contém. A função local conta no método e é medida também sozinha.
- **Onde vale:** produção e teste, inclusive cada `[Fact]` e cada `[Theory]`. Ficam fora só a configuração do EF Core (as classes `IEntityTypeConfiguration<T>` e os mapeamentos de tipo próprio, todos nas pastas `Persistence/Configurations/`) e as migrations, desligadas por glob no `.editorconfig`: mapeamento longo se divide por assunto, e a revisão cobra.

Exemplo: a MA0051 conta **8** linhas neste teste. São as 7 linhas entre as chaves, com as duas em branco e o comentário, mais a da chave final. Atributo e assinatura não entram.

```csharp
[Fact]
public void Aprovar_PedidoPendente_RegistraQuemDecidiu()
{
    var pedido = PedidoDeConcessaoFake.Pendente();

    // Quem aprova precisa ser outra pessoa: o pedido recusa autoaprovação.
    var resultado = pedido.Aprovar(Aprovador, Agora);

    resultado.IsSuccess.Should().BeTrue();
    pedido.DecididoPor.Should().Be(Aprovador);
}
```

**2. Arquivo: até 400 linhas com código pela S104.**

- **Contam:** as linhas com código, inclusive `using`, `namespace`, declarações e linhas só com chave.
- **Não contam:** linhas em branco e linhas só de comentário (`//`, `/* */`, `///`). Um arquivo de 460 linhas com 70 delas em branco ou de documentação tem 390 linhas com código e está dentro do limite.
- **Onde vale:** todo `.cs` de `src/` e `tests/`, inclusive a configuração do EF escrita à mão, auxiliares, fixtures e bases de teste.
- **Ficam fora:** as migrations e o snapshot (desligados por glob) e o código gerado (`*.g.cs`, `*.Designer.cs`), que o analisador já ignora.

**3. Parâmetros: até 7** por método, construtor (inclusive o primário) ou função local, pela S107. `CancellationToken` conta.

**4. Complexidade: cognitiva até 15 pela S3776 e ciclomática até 10 pela S1541**, por método. O Sonar não roda a S1541 em projeto de teste; ali vale a S3776.

**5. Supressão de aviso: proibida** ([ADR-0160](docs/adrs/0160-supressao-de-aviso-do-analisador-proibida.md)).

- **Proibido:** calar aviso com `[SuppressMessage]`, `GlobalSuppressions.cs`, `#pragma warning disable` ou `<NoWarn>`. O aviso se corrige no código.
- **Design ruim se refaz:** quando o analisador revela design ruim, o design é refeito. Por exemplo, um `bool?` usado como três estados vira um `enum` com os três estados.
- **Permitido:** desligar no `.editorconfig`, com comentário justificando, uma regra que não se aplica ao projeto inteiro ou a uma camada inteira. Camada é código com o mesmo papel, como testes, controllers ou configuração do EF, selecionado por glob de caminho. Isso é decisão de projeto, não supressão por símbolo.
- **Supressão disfarçada:** seção que isenta um arquivo ou um tipo específico, e não uma camada inteira, também é proibida.

**6. Código feito só para caber na métrica é reprovado**, mesmo dentro dos limites. Exemplos:

- método-ponte que só repassa a chamada;
- sobrecarga ou serviço que só repassa;
- estado fatiado sem sentido: campos ou classes criados só para dividir um método;
- parâmetros agrupados num objeto artificial que não existe no domínio;
- vista ou cópia da entidade;
- coleção viva exposta.

A divisão segue responsabilidade, conforme SOLID, Clean Code e as recomendações da comunidade .NET. Juntar linhas para enganar a contagem (duas instruções na mesma linha, encadeamento artificial) também é reprovado: o `dotnet format` e a revisão desfazem.

#### Exemplo

Divisão correta: o handler orquestra, e a regra fica no agregado, num método com responsabilidade e nome de domínio. A MA0051 conta 11 linhas no `Handle` e 10 no `Aprovar`.

```csharp
public async Task<Result> Handle(AprovarPedidoCommand command, CancellationToken cancellationToken)
{
    var pedido = await _pedidos.ObterAsync(command.PedidoId, cancellationToken);
    if (pedido is null)
        return Result.Failure(PedidoErrors.NaoEncontrado);

    var aprovacao = pedido.Aprovar(command.Aprovador, _relogio.Agora);
    if (aprovacao.IsFailure)
        return aprovacao;

    await _concessoes.AdicionarAsync(Concessao.DoPedido(pedido), cancellationToken);
    return Result.Success();
}

// No agregado PedidoDeConcessao:
public Result Aprovar(UsuarioRef aprovador, DateTimeOffset agora)
{
    if (Situacao != SituacaoDoPedido.Pendente)
        return Result.Failure(PedidoErrors.JaDecidido);
    if (aprovador == Solicitante)
        return Result.Failure(PedidoErrors.AprovadorIgualAoSolicitante);

    Situacao = SituacaoDoPedido.Aprovado;
    DecididoPor = aprovador;
    DecididoEm = agora;
    return Result.Success();
}
```

Divisão reprovada: o método longo foi cortado em pedaços que só repassam, sem responsabilidade própria.

```csharp
public Task<Result> Handle(AprovarPedidoCommand command, CancellationToken cancellationToken)
    => HandleInterno(command, cancellationToken);              // método-ponte

private Task<Result> HandleInterno(AprovarPedidoCommand command, CancellationToken cancellationToken)
    => ExecutarParte1(new ContextoDoHandle(command, cancellationToken)); // objeto artificial
```

#### Ver os avisos

`dotnet build` mostra os avisos das cinco regras, e cada build grava o relatório SARIF de cada projeto em `artifacts/analise/`. Para ver só as métricas:

```bash
dotnet build UniPlus.slnx -c Release 2>&1 | grep -E "warning (MA0051|S104|S107|S3776|S1541)" | sort -u
```

#### Dívida existente e CI

O CI **não barra** a dívida antiga. As cinco regras são aviso (`WarningsNotAsErrors` no `Directory.Build.props`) e valem para o que o PR cria ou altera:

- método novo ou alterado cumpre os limites;
- arquivo que o PR cria ou altera termina com até 400 linhas com código; se já passava, o PR o divide por assunto.

A dívida é paga por módulo, cada um com sua issue. Quando um módulo zera, os projetos dele (de `src/` e de teste, que têm o mesmo prefixo de nome) saem do `WarningsNotAsErrors` no `Directory.Build.props`. Com o `TreatWarningsAsErrors`, os avisos das cinco regras passam a erro, e o CI barra aquele módulo:

```xml
<!-- Módulo Seleção sem dívida de métricas: o CI barra. -->
<MetricasBarradas Condition="$(MSBuildProjectName.StartsWith('Unifesspa.UniPlus.Selecao.'))">true</MetricasBarradas>
<WarningsNotAsErrors Condition="'$(MetricasBarradas)' != 'true'">$(WarningsNotAsErrors);MA0051;S104;S3776;S1541;S107</WarningsNotAsErrors>
```

Uma seção do `.editorconfig` com `severity = error` não basta: o `WarningsNotAsErrors` rebaixa o diagnóstico a aviso de novo.
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
    // Test Data Builder: padrões válidos, só o que importa ao cenário aparece
    var candidato = new CandidatoBuilder().ComNota(8.5m).ComBonus(0.20m).Build();

    // Act
    var resultado = candidato.CalcularNotaFinal();

    // Assert
    resultado.Should().Be(10.2m);
}
```

- Montar cenários com **Test Data Builders** (padrões válidos, métodos expressivos) em vez de repetir a chamada à fábrica em cada teste; o builder vive só no projeto de testes e chama a fábrica de domínio. Os testes da própria fábrica a chamam diretamente — ver [§ Value objects no lugar de primitivos](#value-objects-no-lugar-de-primitivos)
- Usar **Bogus** para dados de teste (nomes, CPFs, endereços fictícios)
- Usar **Testcontainers** para testes de integração (PostgreSQL, Redis, Kafka)
- Nunca usar dados reais de candidatos em testes
- Testes de arquitetura usam só regras declarativas do **ArchUnitNET** (e reflexão sobre assinaturas onde ele não alcança); nada de regex sobre o fonte, leitor de IL próprio ou árvore do Roslyn no teste — ver [orientação para revisão](orientacao-para-revisao.md), que também lista o que é conferido só na revisão

---

## Quality gates

O PR será bloqueado se qualquer gate falhar:

- [ ] Build sem erros e sem warnings além das métricas em modo aviso da ADR-0155 (`dotnet build`)
- [ ] Todos os testes passando
- [ ] Cobertura de código >= 80% nas camadas Domain e Application
- [ ] Nenhuma vulnerabilidade crítica em dependências
- [ ] Formatação correta — job `Formatação (dotnet format)` no CI; ver [§ Formatação e o filtro CA1515](#formatação-e-o-filtro-ca1515)
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
- [ ] Conceitos de domínio modelados como value objects, sem primitivos soltos nem agrupamento artificial para caber na métrica, e builder só nos testes ([§ Value objects no lugar de primitivos](#value-objects-no-lugar-de-primitivos))
- [ ] Nenhum aviso novo de MA0051, S104, S107, S3776 ou S1541 no código criado ou alterado, inclusive testes, e nenhuma supressão de aviso ([§ Métricas de qualidade](#métricas-de-qualidade)) — o CI ainda não os barra

---

## Revisão de código

Todo PR passa por revisão humana. O revisor verifica:

1. **Segurança** — PII, injection, OWASP, LGPD
2. **Arquitetura** — Clean Architecture, SOLID, acoplamento entre módulos; a técnica dos testes de arquitetura e os itens da [orientação para revisão](orientacao-para-revisao.md)
3. **Qualidade** — legibilidade, complexidade, duplicação e as [métricas de qualidade](#métricas-de-qualidade), pelos avisos dos analisadores
4. **Testes** — cobertura, casos de borda, assertions significativas
5. **Domínio** — regras de negócio corretas, conformidade legal

O PR precisa de **1 aprovação** para ser mergeado.

---

## Dúvidas

- Abra uma issue com a label `needs-refinement` para discutir abordagens
- Consulte a documentação em [uniplus-docs](https://github.com/unifesspa-edu-br/uniplus-docs) para contexto de requisitos e regras de negócio
