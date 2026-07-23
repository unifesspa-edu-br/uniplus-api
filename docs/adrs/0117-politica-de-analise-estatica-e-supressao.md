---
status: "proposed"
date: "2026-07-22"
decision-makers:
  - "Tech Lead"
consulted: []
informed: []
---

# ADR-0117: Política de análise estática e supressão de diagnósticos

## Contexto e enunciado do problema

O `uniplus-api` compila com `AnalysisLevel=latest-all` e `TreatWarningsAsErrors=true` (`Directory.Build.props`). `latest-all` liga **todas** as regras de qualidade (CA*) como warning, e o `TreatWarningsAsErrors` as promove a erro. Nenhuma dessas duas escolhas está registrada — a política atual vive apenas na seção "Supressão de análise de código" do `CLAUDE.md` (instrução de agente), sem ADR que a governe para os desenvolvedores.

A consequência aparece no código: há **12 supressões inline** (`#pragma warning disable`) em código de produção não-gerado (excluídas as migrations EF, que trazem supressão automática). A validação foi feita **na própria base**: build completo da solution (55 projetos) sob `AnalysisLevel=latest-recommended` com os 12 pragmas removidos emite **exatamente 3 warnings** — `CA1000` em `Result.cs` (2, linhas 42–43) e `CA1873` em `MigrationServiceCollectionExtensions.cs` (1, linha 66). Todos os outros deixam de ser diagnosticados. O destino de cada supressão:

| Regra | Onde é suprimida hoje | Ocorrências | Diagnostica em `recommended`? | Destino após a migração |
|---|---|:-:|:-:|---|
| CA1819 (array em propriedade) | `ISnapshotPublicacaoCanonicalizer`, `VersaoConfiguracao`, `VendorMediaTypeAttribute`, `IdempotencyEntry` | 4 | não | pragma removido |
| CA1040 (marker interface CQRS) | `ICommand`, `IQuery` | 2 | não | pragma removido |
| CA1308 (normaliza para lowercase) | `Email`, `IdempotencyFilter` | 2 | não | pragma removido |
| CA1031 (catch genérico em boundary) | `IdempotencyFilter` | 1 | não | pragma removido |
| CA1050 (tipo fora de namespace) | `ProcessoPublicado` (Avro, escrito à mão) | 1 | **não dispara nem em `-all`** | pragma removido (supressão morta) |
| CA1000 (membro estático em tipo genérico) | `Result<T>` | 1 (2 warnings) | **sim** | → `.editorconfig` (`severity = none`, decisão de design) |
| CA1873 (logging potencialmente caro) | `MigrationServiceCollectionExtensions` | 1 | **sim** | pragma permanece (exceção pontual) |

Total: **12** supressões. Ao migrar para `recommended`, **10 pragmas são eliminados** (9 porque a regra deixa de ser diagnosticada + 1 supressão morta de `CA1050`, comprovadamente inócua — o tipo `ProcessoPublicado` já está declarado num namespace, então `CA1050` nunca dispara ali, nem sob `-all`); `CA1000` migra para uma linha no `.editorconfig`; e **resta 1 pragma inline** (`CA1873`). Resultado: **12 → 1 pragma**.

Dois fatos externos pesam na decisão. Primeiro, a Microsoft **não habilita CA1000 nem CA1308 por padrão** no .NET 10 (ambas marcadas "Enabled by default: No" na documentação oficial) — elas só nos afetam porque optamos por `-all`. Segundo, **nenhum repositório do próprio time .NET usa `-all`**: `dotnet/runtime` usa `AnalysisLevel=preview` (conjunto default), `dotnet/aspnetcore` usa `AnalysisLevel=latest` + `AnalysisMode=Default` — ambos com `TreatWarningsAsErrors=true`, como nós. A diferença é só o conjunto de regras ligadas.

Além do nível, a política de **como** suprimir precisa de critério. O `CLAUDE.md` hoje descreve uma "pirâmide" que trata `#pragma` como último recurso ("evitar"). A documentação oficial da Microsoft não estabelece essa hierarquia: apresenta `#pragma`, `[SuppressMessage]` e `severity` em `.editorconfig` como mecanismos distintos por **alcance**, e mostra o `#pragma` como o mecanismo canônico para suprimir uma violação pontual. A prática do time .NET confirma a coexistência (em `dotnet/runtime`: ~319 arquivos com `#pragma ... CA` e ~452 com `SuppressMessage`).

## Drivers da decisão

- Alinhar a configuração de análise à prática consolidada do time .NET, em vez de um `-all` que nenhum deles adota.
- Reduzir o ruído de supressões inline que não representam dívida de disciplina, e sim efeito colateral de configuração.
- Distinguir com clareza **decisão de design** (regra que não se aplica ao projeto) de **exceção pontual** (uma violação localizada e justificada).
- Poder decidir caso a caso — via opt-in explícito no `.editorconfig` — quais regras de valor que o `-all` ligava em bloco (ex.: CA1062, null-check em argumento de método público) continuam ativas, em vez de ligar tudo ou nada.
- Dar à issue de aplicação (#146, reescopada) uma autoridade pública para citar — ADR, não `CLAUDE.md`.

## Opções consideradas

- **Opção A — Manter `latest-all`** e apenas padronizar a sintaxe das supressões existentes.
- **Opção B — `AnalysisLevel=latest-recommended`** + opt-in nomeado no `.editorconfig` das regras extras que quisermos manter, e opt-out justificado das que são decisão de design.
- **Opção C — `AnalysisLevel=latest` (`AnalysisMode=Default`)**, espelhando `dotnet/aspnetcore`.

## Resultado da decisão

**Escolhida:** "Opção B — `latest-recommended` com opt-in/opt-out nomeado", porque reduz as 12 supressões inline a 1 sem abrir mão das regras de qualidade que agregam valor real, e torna cada exceção remanescente uma decisão explícita e rastreável.

A decisão tem duas partes indissociáveis:

**1. Nível de análise.** `AnalysisLevel` passa de `latest-all` para `latest-recommended` no `Directory.Build.props`. Regras úteis que o `recommended` não liga por padrão e que quisermos manter são religadas **individualmente e com comentário** no `.editorconfig` (`dotnet_diagnostic.CAxxxx.severity = error`). Regras que disparam no `recommended` mas representam decisão de design consolidada do projeto são desligadas **individualmente e com justificativa** — no caso, `CA1000` (o padrão `Result<T>` com factory methods estáticos é deliberado; a própria BCL o pratica em `EqualityComparer<T>.Default`, `ImmutableArray<T>.Empty` etc., e a regra visa "adoção de bibliotecas públicas", que não é o caso de um Kernel interno).

**2. Critério de supressão — por alcance da exceção**, substituindo a pirâmide anterior:

| Alcance da exceção | Mecanismo |
|---|---|
| A regra não se aplica ao projeto ou a uma camada inteira (é decisão, não exceção) | `.editorconfig`: `severity = none` + comentário com o porquê |
| A exceção pertence a um símbolo (membro ou tipo) | `[SuppressMessage]` no símbolo, com `Justification` preenchida |
| A exceção é de 1–2 linhas dentro de um símbolo maior | `#pragma disable/restore` + comentário na mesma linha |
| Código **gerado** (migrations EF, com supressão automática) | não suprimir manualmente |

**Regra de desempate:** se a mesma regra é suprimida por 3+ vezes pelo mesmo motivo, ela deixou de ser exceção e virou decisão — deve subir para o `.editorconfig`.

Aplicado ao estado atual (validado por build completo da solution), o resultado é **12 → 1 supressão inline**:

- **9 desaparecem** — as de `CA1819`, `CA1040`, `CA1308` e `CA1031` deixam de ser diagnosticadas em `recommended`.
- **`CA1050` em `ProcessoPublicado` é removido como supressão morta** — a regra não dispara nesse arquivo (o tipo já está declarado num namespace) nem mesmo sob `-all`; o pragma nunca teve efeito e deve ser apagado, não migrado.
- **`CA1000` (`Result<T>`)** deixa de ser `#pragma` e vira uma linha nomeada no `.editorconfig` (`severity = none` + justificativa) — é decisão de design, não exceção pontual.
- **`CA1873` em `MigrationServiceCollectionExtensions`** permanece como o **único** `#pragma`: a avaliação cara já está protegida por `IsEnabled` no call site (o analisador não enxerga a guarda) e o caminho é de startup, de baixíssima frequência — exceção pontual, de 1–2 linhas.

Nota sobre a linha "código gerado" da tabela: refere-se apenas ao código **efetivamente gerado** (migrations EF, que trazem `#pragma 612/618` automático). Classes Avro escritas à mão como `ProcessoPublicado` **não** são código gerado; nesse caso específico, aliás, a supressão nem era necessária.

## Consequências

### Positivas

- Supressões inline caem de 12 para 1 em código de produção não-gerado (a de `CA1000` migra para uma linha no `.editorconfig`; a de `CA1050` some por ser supressão morta).
- Cada exceção remanescente é ou uma política nomeada no `.editorconfig`, ou um `#pragma` de 1–2 linhas com justificativa — ambos autoexplicativos.
- Configuração alinhada ao time .NET (runtime, aspnetcore), reduzindo surpresa para quem chega de outros projetos .NET.
- A #146 passa a ter uma ADR pública como autoridade, em vez de citar o `CLAUDE.md`.

### Negativas

- Religar regras úteis do conjunto `-all` que caíram fora do `recommended` exige mapeá-las uma a uma no `.editorconfig` (custo único, na migração).
- Convívio de dois conjuntos de configuração (`AnalysisLevel` no props + linhas por regra no `.editorconfig`) — mitigado pela regra de desempate, que concentra decisões no `.editorconfig`.

### Neutras

- A pirâmide de supressão do `CLAUDE.md` é substituída pela regra por alcance; o texto do `CLAUDE.md` passa a apontar para esta ADR.

## Confirmação

- `Directory.Build.props` declara `AnalysisLevel=latest-recommended` (não `latest-all`).
- Build limpo do solution sem warnings, mantido o `TreatWarningsAsErrors=true`.
- Evidência da migração: com os 12 pragmas removidos e `recommended` ativo, o build completo da solution emite **exatamente** `CA1000` (`Result.cs`) e `CA1873` (`MigrationServiceCollectionExtensions.cs`) — nenhum outro. O primeiro é neutralizado no `.editorconfig`; o segundo é o único `#pragma` remanescente. `CA1050` não reaparece (era supressão morta).
- Toda supressão inline (`#pragma`) restante cobre no máximo o alcance de um símbolo e traz comentário com a justificativa técnica/de negócio (não o processo de revisão que a motivou).
- Toda regra desligada ou religada no `.editorconfig` traz comentário com o motivo.
- `tools/adr-lint/validate.sh` verde.

## Prós e contras das opções

### Opção A — Manter `latest-all`

- Bom, porque não requer migração de configuração.
- Ruim, porque preserva 9 supressões inline que existem só por causa do `-all`, tratando efeito de configuração como se fosse dívida de código.
- Ruim, porque diverge da prática de todo o time .NET sem uma razão registrada.

### Opção B — `latest-recommended` + opt-in/opt-out nomeado

- Bom, porque reduz as 12 supressões inline a 1 sem regredir na qualidade que importa.
- Bom, porque cada exceção remanescente vira decisão explícita e rastreável.
- Ruim, porque exige mapear individualmente as regras úteis a religar (custo único).

### Opção C — `latest` / `AnalysisMode=Default`

- Bom, porque zera as supressões (nem CA1000 nem CA1050 disparam) e espelha exatamente o `dotnet/aspnetcore`.
- Ruim, porque parte de um baseline menor que o `recommended`: regras que o `recommended` já ativa (medido no projeto de teste: CA1050, CA1822, entre outras) precisariam ser religadas uma a uma para igualar o rigor, sem vantagem sobre a opção B, que já usa opt-in nomeado.

## Mais informações

- Microsoft Learn — [Suppress code analysis warnings](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/suppress-warnings), [CA1000](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca1000), [CA1308](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca1308), [AnalysisLevel / AnalysisMode](https://learn.microsoft.com/dotnet/core/project-sdk/msbuild-props#analysismode).
- Prática de referência: `dotnet/runtime` (`AnalysisLevel=preview`) e `dotnet/aspnetcore` (`AnalysisLevel=latest` + `AnalysisMode=Default`), ambos com `TreatWarningsAsErrors=true`.
- Aplicação da política: issue #146 (reescopada) executa a migração caso-a-caso das 12 supressões atuais.
- Ao aceitar esta ADR: atualizar a seção "Supressão de análise de código" do `CLAUDE.md` para apontar para ela, substituindo a pirâmide pela regra por alcance.
