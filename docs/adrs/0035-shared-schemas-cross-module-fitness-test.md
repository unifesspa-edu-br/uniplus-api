---
status: "accepted"
date: "2026-05-05"
decision-makers:
  - "Tech Lead (CTIC)"
---

# ADR-0035: Schemas duplicados entre baselines OpenAPI — fitness test cross-module no lugar de `$ref` multi-arquivo

## Contexto e enunciado do problema

Os baselines `contracts/openapi.selecao.json` e `contracts/openapi.ingresso.json` (ADR-0030) duplicam schemas idênticos vindos do shared kernel:

- `ProblemDetails` (RFC 9457, ADR-0023)
- `AuthenticatedUserResponse` (`/api/auth/me`, exposto por `MapSharedAuthEndpoints`)
- `UserProfileResponse` (`/api/profile/me`, exposto por `MapSharedProfileEndpoints`)

A geração runtime (`Microsoft.AspNetCore.OpenApi` 10) emite cada documento independentemente — a partir das mesmas classes C#, mas serializadas inline em cada spec. O drift check existente (`OpenApiEndpointTests` em cada módulo) protege cada baseline contra divergir do runtime do **mesmo módulo**, mas **não detecta divergência cross-module**: alguém pode mudar `AuthenticatedUserResponse` em código + regenerar só `openapi.selecao.json` e o `openapi.ingresso.json` ficaria stale sem alarme até a próxima rodada de regeneração.

A pergunta: deduplicar via `$ref` cross-document (`shared.openapi.json#/components/schemas/ProblemDetails`) ou aceitar a duplicação inline com gating de drift cross-module?

## Drivers da decisão

- **ADR-0030 invariant:** o runtime é a fonte de verdade do contrato. Qualquer mecanismo de deduplicação não pode quebrar o drift check baseline-vs-runtime.
- **Custo de tooling:** introduzir `$ref` multi-arquivo exige Redocly CLI (Node.js) ou similar como dep de build/CI. Para 3 schemas duplicados de ~10-30 LOC cada (total ~80 LOC), o ROI é negativo hoje.
- **Compatibilidade com codegens:** `openapi-generator`, `NSwag`, `Kiota` preferem specs **flat** (sem `$ref` cross-file). Stripe e GitHub publicam **bundled** mesmo autorando multi-file — a multi-file é benefício de autoria, não de consumo.
- **`Microsoft.AspNetCore.OpenApi` 10:** **não suporta** `$ref` cross-document nativo. `CreateSchemaReferenceId` controla apenas o id intra-document; bugs abertos em [aspnetcore#63090](https://github.com/dotnet/aspnetcore/issues/63090) e [#60164](https://github.com/dotnet/aspnetcore/issues/60164) confirmam que o foco do time é correção de `$ref` intra-document.
- **Risco real concreto:** divergência cross-module silenciosa, não tamanho do arquivo. O risco é endereçável sem multi-file.

## Opções consideradas

- **A. Aceitar duplicação como tech debt sem ação.** Status quo.
- **B. Post-processar baselines via Redocly `bundle`/`split` em build step.** Runtime emite inline; pipeline extrai shared schemas e re-injeta `$ref`.
- **C. Aceitar duplicação inline + adicionar fitness test cross-module verificando byte-equality dos schemas com mesmo nome.** Sem novo tooling.
- **D. Manter `shared.openapi.json` companion publicado em paralelo (não substitui inline).** Documentação adicional para `uniplus-developers` portal.

## Resultado da decisão

**Escolhida:** "C — fitness test cross-module", porque é a única opção que (1) preserva o runtime-first invariant da ADR-0030 sem mudanças, (2) custa um único arquivo C# de teste sem dependência de Node.js, e (3) detecta exatamente o risco que importa hoje (divergência silenciosa cross-module).

A opção A foi rejeitada porque deixa o risco descoberto. A opção B foi rejeitada por desproporcionalidade — Redocly em CI é a estratégia da indústria quando há ≥10 specs ou ≥dezenas de schemas compartilhados (Stripe, GitHub), não quando há 3. A opção D pode ser somada à C numa iteração futura quando o `uniplus-developers` portal demandar (ver _trigger de reavaliação_ abaixo); ela não substitui a fitness test.

### Forma do contrato

`OpenApiSharedSchemasInSyncTests` em `tests/Unifesspa.UniPlus.ArchTests/SolutionRules/`:

```csharp
[Fact(DisplayName = "ADR-0035: schemas com mesmo nome em baselines diferentes são byte-equivalentes")]
public void Schemas_Compartilhados_Devem_Ser_ByteEquivalentes()
{
    // 1. Carrega contracts/openapi.{selecao,ingresso}.json
    // 2. Calcula intersection de nomes em components.schemas
    // 3. Asserts byte-equivalência com SerializeCanonical (WriteIndented = true)
    // 4. Mensagem inclui o comando de fix (UPDATE_OPENAPI_BASELINE=1 …)
}
```

A intersection é computada dinamicamente — a regra captura schemas adicionais sem mudança de código quando o time mover novos tipos para shared (Cursor envelopes, `_links`, paginated responses).

### Trigger de reavaliação

Promover para opção B (Redocly post-process) quando **ao menos um** dos seguintes acontecer:

- `uniplus-developers` portal entregue e demandando spec multi-file para documentação canônica
- ≥10 schemas compartilhados entre módulos (limiar de complexidade onde tooling paga o overhead)
- Integradores externos pedindo formato multi-file
- Surgir terceiro módulo (ex.: `recursos`, `homologacao`) — multiplicidade quadruplica o risco de drift e provavelmente justifica `$ref` companion

Ao promover, **adicionar** Redocly em build step; **não remover** o fitness test cross-module — ele continua útil como sentinela do `bundle` output.

## Consequências

### Positivas

- Risco de drift cross-module endereçado por uma ferramenta C# nativa, sem aumentar a superfície de tooling do projeto.
- Mensagem de erro do teste é actionable — aponta o módulo stale e o comando de fix.
- Regra captura schemas futuros automaticamente (intersection dinâmica).
- ADR-0030 (runtime-first) permanece intocada — o spec emitido continua sendo a única fonte de verdade.

### Negativas

- A duplicação de bytes nos baselines persiste. Para review humano de PR, schemas idênticos aparecem em dois lugares — ruído visual.
- Codegens externos não ganham reuso semântico (mas também não perdem nada — eles ignoram `$ref` cross-file de qualquer forma).
- O fitness test compara apenas componentes registrados em `components.schemas`. Schemas inline em `responses.<status>.content` (caso o runtime regrida no future) não são detectados.

### Neutras

- Decisão é reversível. Promover para Opção B no trigger acima é incremento, não breaking change.

## Confirmação

- `OpenApiSharedSchemasInSyncTests.Schemas_Compartilhados_Devem_Ser_ByteEquivalentes` — 1 fact, roda na pipeline `dotnet test UniPlus.slnx` em CI.
- Validação por prova negativa durante implementação: alterar manualmente `ProblemDetails.title.type` em um dos baselines fez o teste falhar com mensagem clara.
- `contracts/README.md#roadmap` atualizado: item "shared.openapi.json com `$ref`" substituído por referência a esta ADR.

## Prós e contras das opções

### A — Aceitar duplicação sem ação

- Bom, porque é zero-cost.
- Ruim, porque o risco de drift cross-module fica descoberto.

### B — Redocly `bundle`/`split` em CI

- Bom, porque alinha com a estratégia da indústria (Stripe, GitHub) para specs multi-arquivo.
- Bom, porque emite `shared.openapi.json` consumível pelo portal de devs.
- Ruim, porque adiciona dep Node.js em CI + step de build + risco de pipeline brittle (reportes de race em [Spectral#2640](https://github.com/stoplightio/spectral/issues/2640)) para um problema que ainda não materializou.

### C — Fitness test cross-module (escolhida)

- Bom, porque é proporcional ao risco real e mantém o runtime-first.
- Bom, porque captura novos schemas compartilhados sem mudança de código.
- Ruim, porque não elimina a duplicação visual nos baselines.

### D — `shared.openapi.json` companion publicado em paralelo

- Bom, porque dá documentação centralizada para o portal de devs.
- Ruim, porque exige sincronização manual ou geração — soma overhead sem resolver o risco de drift sozinho.

Combinável com C numa iteração futura.

## Mais informações

- [ADR-0023 — ProblemDetails RFC 9457 canônico](0023-problemdetails-rfc-9457-canonico-para-todo-erro.md)
- [ADR-0030 — OpenAPI 3.1 contract-first via Microsoft.AspNetCore.OpenApi](0030-openapi-3-1-contract-first-microsoft-aspnetcore-openapi.md)
- [Microsoft.AspNetCore.OpenApi customization (.NET 10)](https://learn.microsoft.com/aspnet/core/fundamentals/openapi/customize-openapi?view=aspnetcore-10.0)
- [aspnetcore#63090 — invalid schema references](https://github.com/dotnet/aspnetcore/issues/63090)
- [Redocly CLI — bundle/split](https://redocly.com/docs/cli/commands/bundle)
- [GitHub rest-api-description (Redocly + multi-file → bundled)](https://github.com/github/rest-api-description)
- [Stripe openapi (publica bundled flat)](https://github.com/stripe/openapi)
- Origem: follow-up Frente 1.2 do plano `~/.claude/plans/uniplus-api-backend-followups-cleanup.md`; issue [#323](https://github.com/unifesspa-edu-br/uniplus-api/issues/323)
