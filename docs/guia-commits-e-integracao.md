# Guia de Commits e Integração — `uniplus-api`

Este guia é a **fonte de verdade** do time do backend Uni+ para mensagens de commit e política de integração no repositório [`uniplus-api`](https://github.com/unifesspa-edu-br/uniplus-api). Cada repositório de código mantém seu próprio guia adaptado ao contexto técnico — backend (.NET 10 / C# 14 / EF Core / Wolverine / Clean Architecture) tem convenções específicas que não fazem sentido no `uniplus-web` (Angular / Nx) ou no `uniplus-docs` (especificações). O [`CONTRIBUTING.md`](../CONTRIBUTING.md) **complementa** este guia com instruções operacionais de setup, build, testes e revisão.

> Dois pilares inegociáveis:
>
> 1. **Conventional Commits em pt-BR** — mensagens padronizadas, rastreáveis, compatíveis com geração automática de changelog.
> 2. **Integração via rebase sobre a `main`** — histórico linear, sem merge commits poluindo o `git log`.

## 1. Formato da mensagem de commit

```text
<tipo>(<escopo>): <descrição curta no imperativo presente>

<corpo opcional explicando o "por quê", não o "o quê">

<rodapé opcional: Closes #N, BREAKING CHANGE: ...>
```

### Regras do subject (primeira linha)

- **Imperativo presente:** `adiciona`, `corrige`, `remove`, `atualiza` — nunca gerúndio (`Adicionando`), infinitivo (`Adicionar`) ou passado (`Adicionado`).
- **Minúsculas:** `fix:`, não `Fix:`; `feat:`, não `Feat:`.
- **Sem ponto final.**
- **Máximo ~72 caracteres** (idealmente 50).
- **Em pt-BR**, exceto termos técnicos de mercado (API, CQRS, Wolverine, EF Core, Keycloak, etc.).
- **Sem referência ao número do PR** no subject (`(#37)`) — isso é ruído; o link ao PR já vive no GitHub.

### Corpo do commit

- Explique **o motivo** da mudança, não repita o "o quê" (o diff já mostra).
- Use quando houver contexto relevante: incidentes, decisões, referências a ADRs, links para issues.
- Quebre linhas em ~72 colunas.

### Rodapé

- `Closes #N` — para fechar issues automaticamente no merge. **Mantenha fora de blocos de código** (`` `Closes #N` `` não é parseado pelo GitHub).
- `BREAKING CHANGE: <descrição>` — para mudanças incompatíveis (também pode ser marcado com `!` após o tipo: `feat!: ...`).

## 2. Tipos permitidos

| Tipo | Quando usar | Exemplo |
|---|---|---|
| `feat` | Nova funcionalidade | `feat(selecao): adiciona endpoint de homologação de inscrição` |
| `fix` | Correção de bug | `fix(ingresso): corrige cálculo de classificação por cota` |
| `refactor` | Refatoração sem mudança de comportamento | `refactor(sharedkernel): extrai value object NotaFinal` |
| `test` | Adição/correção de testes | `test(arch): adiciona regra ArchUnitNET para Domain` |
| `docs` | Documentação | `docs(adrs): adiciona ADR-0021 sobre cache distribuído` |
| `chore` | Manutenção geral (deps, configs) | `chore(deps): atualiza WolverineFx para 5.32.1` |
| `ci` | Mudanças em CI/CD | `ci: adiciona job de testes de arquitetura` |
| `perf` | Melhoria de performance | `perf(query): adiciona índice em CPF de inscrições` |
| `build` | Build e empacotamento | `build(docker): otimiza layer de restore` |
| `style` | Formatação sem mudança lógica | `style: aplica dotnet format` |

## 3. Escopos por camada e módulo

O escopo é opcional, mas **fortemente recomendado** — facilita `git log --grep "feat(selecao)"` e changelog segmentado.

A tabela canônica de escopos válidos vive em [`CONTRIBUTING.md` § Commit](../CONTRIBUTING.md#4-commit) (escopos por módulo `selecao`/`ingresso`/`shared`/`sharedkernel`, por camada `domain`/`application`/`infra`/`api`/`db`/`migrations`, e transversais `auth`/`ci`/`docker`/`deps`). Esta seção do guia mostra **como combinar tipo e escopo** sem duplicar a lista — o `CONTRIBUTING.md` é o registro autoritativo dos escopos.

Escopos mais granulares (ex.: `selecao-domain`, `selecao-api`, `arch`) podem ser usados livremente quando fizerem sentido, mantendo imperativo presente e pt-BR.

## 4. Exemplos bons e ruins

### Bons

```text
feat(selecao): adiciona endpoint de homologação de inscrição

Endpoint POST /editais/{id}/inscricoes/{cpf}/homologar permite ao
operador do CEPS deferir ou indeferir a inscrição após análise
documental. Implementa RF04 da especificação.

Closes #142
```

```text
fix(ci): atualiza upload-artifact para v4
```

```text
docs(adrs): adiciona ADR-0021 sobre estratégia de cache distribuído
```

```text
refactor(sharedkernel): extrai value object NotaFinal
```

```text
test(arch): adiciona regra ArchUnitNET para fronteira Domain → Application
```

```text
chore(deps): atualiza WolverineFx para 5.32.1
```

### Ruins (e como corrigir)

| Ruim | Corrigido | Por quê |
|---|---|---|
| `Fix: Altera versão da função upload-artifact para não acusar warnings` | `fix(ci): atualiza upload-artifact para v4` | Maiúscula, subject longo, descreve o "o quê" em vez da ação |
| `fix: Alterando versão do node` | `fix(ci): atualiza versão do Node nas actions` | Gerúndio; falta escopo |
| `feat(auth): importar realm unifesspa para keycloak (#37)` | `feat(auth): importa realm unifesspa para o Keycloak` | Infinitivo; `(#37)` é ruído |
| `chore: adicionar regras de workflow ao CONTRIBUTING` | `docs(contributing): adiciona regras de workflow obrigatório` | Mudança em `.md` é `docs`, não `chore`; usar imperativo presente |
| `Merge pull request #1 from ...` | — (não deveria existir) | Merge commit — com rebase sobre `main` não aparece |
| `Initial commit` | `chore: commit inicial` | Sem tipo |

## 5. Política de integração: rebase sobre a `main`

### O que NÃO queremos no histórico

- `Merge pull request #N from ...` — commits de merge poluindo o log.
- Commits duplicados (ex.: "scaffold da solution" aparecendo 2x porque o PR não foi squashado).
- Commits "WIP", "ajustes do PR", "fix lint" misturados com o commit da feature.
- Histórico em formato de "diamante" (várias linhas paralelas).

### A política do repositório

**Toda integração de PR na `main` é feita via rebase.** O branch protection do `uniplus-api` força essa política — apenas rebase é permitido, merge commits estão desabilitados, e o histórico precisa ser linear.

PRs exigem:

- 1 aprovação humana
- Status check `Build, Test and Coverage` passando
- Branch atualizada (rebaseada) sobre `origin/main`
- **Review realizado após o último push do PR** (`require_last_push_approval=true`)

A regra do `require_last_push_approval` tem uma consequência prática: quem dá o último push antes do approve precisa ser **diferente** de quem vai aprovar. No fluxo cross-account típico do projeto, o autor (`marmota-alpina`) faz o push e o revisor (`jf2s`) aprova. Se você forçar push depois do approve, o approval cai e precisa ser refeito.

## 6. Fluxo recomendado pelo dev

```bash
# 1. Mantenha sua branch sempre atualizada com a main via rebase
git checkout main
git pull --rebase
git checkout feature/123-meu-trabalho
git rebase main

# 2. Antes de abrir/atualizar o PR, organize seus commits
git rebase -i main
#   - squash:  junta commits relacionados ("ajustes do PR" → no commit principal)
#   - reword:  corrige mensagem para conventional commits
#   - drop:    remove commits inúteis ("WIP", "teste")

# 3. Force-push seguro (usa lease para evitar sobrescrever trabalho de outros)
git push --force-with-lease
```

### Validação local antes do PR

Rode todos os comandos abaixo e confirme que passam **antes** de abrir/atualizar o PR — todos os gates do CI são reproduzíveis localmente:

```bash
# Build com TreatWarningsAsErrors (zero avisos, zero erros)
dotnet build UniPlus.slnx

# Testes unitários (Domain, Application, ArchTests)
dotnet test UniPlus.slnx --filter "Category!=Integration"

# Testes de integração (Testcontainers — requer Docker rodando)
dotnet test UniPlus.slnx --filter "Category=Integration"

# Formatação (verifica sem aplicar — falha se houver drift)
dotnet format --verify-no-changes

# Markdownlint (se mexeu em docs/**/*.md)
npx markdownlint-cli2 'docs/**/*.md'
```

### Regras de ouro do rebase

- **NUNCA** faça rebase de uma branch compartilhada (`main`, ou branches de outros devs em colaboração ativa).
- **SEMPRE** use `--force-with-lease` em vez de `--force` ao reescrever sua própria branch.
- Se o rebase ficar complicado (muitos conflitos), peça ajuda — não force merge para "resolver".
- Antes do merge final, garanta que **todos os commits** da branch seguem Conventional Commits — use `git rebase -i main` + `reword` se necessário.

## 7. Convenções de branch

| Tipo de trabalho | Padrão de branch |
|---|---|
| Nova feature | `feature/{issue-number}-{slug}` |
| Correção de bug | `fix/{issue-number}-{slug}` |
| Refatoração | `refactor/{issue-number}-{slug}` |
| Manutenção/chore | `chore/{slug}` |
| Documentação | `docs/{slug}` |

Exemplos:

```text
feature/142-homologacao-inscricao
fix/187-cpf-invalido-no-cadastro
chore/bump-wolverine-5-32
docs/0021-cache-distribuido
```

## 8. Checklist antes de pedir merge

- [ ] Branch com nome no padrão (`feature/{issue}-{slug}`, etc.).
- [ ] Existe issue no GitHub vinculada ao trabalho (regra de ouro: **sem issue, sem código**).
- [ ] Todos os commits seguem Conventional Commits em pt-BR.
- [ ] Verbo no **imperativo presente** ("adiciona", "corrige", "remove").
- [ ] Subject em **minúsculas**, sem ponto final, máx. ~72 caracteres.
- [ ] Escopo presente quando aplicável.
- [ ] Branch **rebaseada sobre a `main` mais recente**.
- [ ] Commits "WIP" / "ajustes do PR" foram **squashados** via `git rebase -i`.
- [ ] PR vinculado à issue com `Closes #N` na descrição (fora de blocos de código).
- [ ] Build com `TreatWarningsAsErrors` passou — zero avisos, zero erros.
- [ ] Testes unitários e de integração passando localmente.
- [ ] `dotnet format --verify-no-changes` sem drift.

## 9. Particularidades do `uniplus-api`

### Migrations EF Core

- Escopo `migrations` — ex.: `feat(migrations): adiciona tabela de inscricoes`.
- Migrations nomeadas descritivamente: `AddCandidatoTable`, `AddIndexOnCpf`.
- **Nunca editar migrations já aplicadas** — criar nova migration.
- **Atenção:** migrations para entidades de domínio estão **gated pela issue [#155](https://github.com/unifesspa-edu-br/uniplus-api/issues/155)** (definição de naming convention). Não rode `dotnet ef migrations add` para entidades de domínio enquanto a issue não fechar — confira o status antes.

### Testes de arquitetura (ArchUnitNET)

- Escopo `arch` — ex.: `test(arch): adiciona regra ArchUnitNET para Domain`.
- Biblioteca canônica: `TngTech.ArchUnitNET` ([ADR-0012](adrs/0012-archunitnet-como-fitness-tests-arquiteturais.md)).
- **Bumps de minor** (ex.: `0.13.x → 0.14.0`) abrem PR dedicado com título `chore(arch-tests): bump ArchUnitNET para X.Y.0`. O PR roda a suíte completa de testes de arquitetura. Se o bump exigir reescrita de qualquer regra (não apenas atualização de assinatura), escalar para revisão do tech lead e avaliar abertura de emenda ao ADR-0012. **Bumps de patch** seguem o fluxo regular de dependências.

### Logging com `[LoggerMessage]` source generator

- O analisador `CA1848` está habilitado com `TreatWarningsAsErrors` — chamadas diretas a `_logger.LogInformation(...)`, `_logger.LogWarning(...)`, etc. são bloqueadas pelo build.
- Todo logging passa por método `partial` decorado com `[LoggerMessage]`. Detalhes de padrão e exemplos vivem no `CLAUDE.md` do repositório (referência interna do agente) e no `CONTRIBUTING.md` § "Implementar".
- Se o commit introduz logging novo, prefira o tipo `feat` ou `refactor` conforme o caso — `chore` não cabe.

### PII em logs (LGPD)

- O `PiiMaskingEnricher` ([ADR-0011](adrs/0011-mascaramento-de-cpf-em-logs.md)) mascara automaticamente CPF (`***.***.***-XX`) em todas as propriedades estruturadas — não há ação manual do dev no call site, mas o pipeline depende de placeholders nomeados (`{Cpf}`, `{CandidatoCpf}`) e não de concatenação de string.
- **Nunca logar dados pessoais identificáveis em texto livre.** Se um diff introduz log com nome completo ou e-mail, o review trava.

### Slices de referência

Antes de começar uma feature, leia o slice canônico mais próximo:

- **Command + handler:** `src/selecao/Unifesspa.UniPlus.Selecao.Application/Commands/Editais/`
- **Query + handler:** `src/selecao/Unifesspa.UniPlus.Selecao.Application/Queries/Editais/`
- **Domain event handler com cascading messages:** `PublicarEditalCommandHandler` + `EditalPublicadoEventHandler`
- **EntityTypeConfiguration:** `src/selecao/Unifesspa.UniPlus.Selecao.Infrastructure/Persistence/Configurations/`

## 10. Referências

- [Conventional Commits 1.0.0](https://www.conventionalcommits.org/pt-br/v1.0.0/) — especificação oficial (em pt-BR).
- [`CONTRIBUTING.md`](../CONTRIBUTING.md) — instruções operacionais de setup, build, testes e revisão para o `uniplus-api`.
- [Pro Git — Rewriting History](https://git-scm.com/book/en/v2/Git-Tools-Rewriting-History) — capítulo sobre `rebase -i`.
- [`docs/adrs/0011-mascaramento-de-cpf-em-logs.md`](adrs/0011-mascaramento-de-cpf-em-logs.md) — ADR do `PiiMaskingEnricher`.
- [`docs/adrs/0012-archunitnet-como-fitness-tests-arquiteturais.md`](adrs/0012-archunitnet-como-fitness-tests-arquiteturais.md) — ADR da biblioteca canônica de fitness tests.
