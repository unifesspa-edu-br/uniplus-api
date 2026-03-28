# Guia de Contribuição — Uni+ API

Este documento descreve como contribuir com o backend da plataforma Uni+. Leia-o por completo antes de abrir sua primeira contribuição.

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

```bash
git checkout main
git pull origin main
git checkout -b feature/{slug-descritivo}
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
2. **Application** — use cases (commands/queries via MediatR), DTOs, validações (FluentValidation)
3. **Infrastructure** — EF Core configurations, repositórios, integrações externas
4. **API** — controllers, middleware, filtros

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

Mensagens em **conventional commits** (pt-BR):

```
tipo(escopo): descrição curta

Corpo opcional explicando o porquê da mudança.
```

**Tipos:** `feat`, `fix`, `refactor`, `test`, `docs`, `chore`, `style`, `perf`, `ci`, `build`

**Escopo:** módulo ou camada afetada — `selecao`, `ingresso`, `shared`, `infra`, `ci`

**Exemplos:**

```
feat(selecao): adicionar endpoint de inscrição de candidato
fix(ingresso): corrigir validação de CPF com dígito verificador inválido
refactor(shared): extrair value object NotaFinal para SharedKernel
test(selecao): adicionar testes de mutação no motor de classificação
```

**Regras:**
- Primeira linha: máximo 72 caracteres, sem ponto final
- Descrição em pt-BR
- Nunca adicionar `Co-Authored-By`
- Nunca usar `--no-verify`

### 5. Pull Request

```bash
# Push da branch
git push -u origin feature/{slug}

# Criar PR
gh pr create --base main
```

O PR será criado com o template padrão. Preencha todas as seções.

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
