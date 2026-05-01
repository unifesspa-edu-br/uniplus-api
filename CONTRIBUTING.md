# Guia de Contribuição — Uni+ API

Este documento descreve como contribuir com o backend da plataforma Uni+. Leia-o por completo antes de abrir sua primeira contribuição.

> **Regras transversais de commits e integração:** este repositório segue o **[Guia de Commits e Integração](https://github.com/unifesspa-edu-br/uniplus-docs/blob/main/docs/guia-commits-e-integracao.md)** — referência oficial para todos os repositórios do projeto (`uniplus-api`, `uniplus-web`, `uniplus-docs`), mantido em `uniplus-docs`. Este documento complementa o guia com regras específicas do backend.

---

## Pré-requisitos

| Ferramenta | Versão mínima | Verificação |
|---|---|---|
| .NET SDK | 10.0 | `dotnet --version` |
| Docker + Compose | 27+ | `docker compose version` |
| Git | 2.40+ | `git --version` |
| GitHub CLI | 2.50+ | `gh --version` |

### Dependências via Docker Compose

O projeto depende de serviços externos que rodam localmente via Docker:

```bash
docker compose up -d
```

Isso inicia: PostgreSQL 18, Redis, Kafka (KRaft), MinIO e Keycloak (dev mode).

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

#### Padrão de handler (slice `Edital` como referência)

Commands implementam `ICommand<TResponse>`, queries implementam `IQuery<TResponse>` — ambos definidos em `Application.Abstractions/Messaging`. Handlers seguem a convenção do Wolverine: classe `static`, método `Handle` (`Task<TResponse>` ou `(TResponse, IEnumerable<object>)` para cascading messages) e dependências resolvidas como parâmetros do método.

```csharp
// Command + handler — referência: src/selecao/.../Commands/Editais/CriarEditalCommand*.cs
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

// Query + handler — referência: src/selecao/.../Queries/Editais/ObterEditalQuery*.cs
public sealed record ObterEditalQuery(Guid Id) : IQuery<EditalDto?>;

public static class ObterEditalQueryHandler
{
    public static async Task<EditalDto?> Handle(
        ObterEditalQuery query,
        IEditalRepository editalRepository,
        CancellationToken cancellationToken) => /* ... */;
}
```

**Validação automática:** o `WolverineValidationMiddleware` (em `Infrastructure.Core/Messaging/Middleware`) descobre todos os `IValidator<T>` registrados via `AddValidatorsFromAssembly` e valida o command/query antes do handler. Não chame `validator.ValidateAsync(...)` no handler — falhas viram `FluentValidation.ValidationException` capturada pelo `GlobalExceptionMiddleware` como `ProblemDetails 400`.

**Logging automático:** o `WolverineLoggingMiddleware` registra entrada/saída do handler com tempo de execução, removendo a necessidade de qualquer behavior de pipeline em código de aplicação. Logs específicos de domínio dentro do handler seguem o padrão `[LoggerMessage]` source generator (.NET 6+): a classe é `partial`, o método é `private static partial void Log{Acao}` decorado com `[LoggerMessage(Level = ..., Message = "...")]`, e o `ILogger` entra como primeiro parâmetro do método. Chamadas diretas a `logger.LogInformation(...)` etc. são bloqueadas pelo analisador `CA1848` (com `TreatWarningsAsErrors`) — o source generator evita avaliação de argumentos quando o log level está desativado, elimina boxing de value types e parseia o template uma única vez na compilação. Exemplo no projeto: `EditalPublicadoEventHandler` (`src/selecao/.../Events/Editais/`).

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

As regras de formato, tipos permitidos e convenções gerais de mensagem estão definidas no **[Guia de Commits e Integração § 1–4](https://github.com/unifesspa-edu-br/uniplus-docs/blob/main/docs/guia-commits-e-integracao.md)**. Siga-o na íntegra.

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

Para uma bateria completa de exemplos (bons e ruins, com justificativas), consulte [Guia § 4](https://github.com/unifesspa-edu-br/uniplus-docs/blob/main/docs/guia-commits-e-integracao.md).

### 5. Rebase sobre `main`

Integração via **rebase** — sem merge commits poluindo o histórico. Procedimento completo e regras de ouro em **[Guia de Commits e Integração § 5–6](https://github.com/unifesspa-edu-br/uniplus-docs/blob/main/docs/guia-commits-e-integracao.md)**.

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

### Entity Framework

- Configurações via `IEntityTypeConfiguration<T>` (nunca data annotations)
- Migrations nomeadas descritivamente: `AddCandidatoTable`, `AddIndexOnCpf`
- Nunca editar migrations já aplicadas — criar nova migration
- Soft delete via filtro global: `entity.HasQueryFilter(e => !e.Deletado)`

---

## Segurança

### Obrigatório em toda contribuição

- **PII masking:** CPF nunca aparece completo em logs — usar `***.***.***-XX`
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
| Unitário | Domain, Application | xUnit + FluentAssertions + NSubstitute | Toda lógica de negócio |
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
