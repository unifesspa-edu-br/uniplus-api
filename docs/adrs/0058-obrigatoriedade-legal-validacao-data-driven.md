---
status: "accepted"
date: "2026-05-14"
decision-makers:
  - "Tech Lead (CTIC)"
consulted: []
informed:
  - "Equipe Uni+"
---

# ADR-0058: ObrigatoriedadeLegal como validação data-driven com citação e snapshot

## Contexto e enunciado do problema

`ObrigatoriedadeLegal` é o catálogo configurável de validação identificado no protótipo do wizard de edital: cerca de 14 regras que validam um `Edital` contra requisitos legais (Lei 12.711/2023, Resolução Unifesspa, Portarias MEC) antes da publicação. As regras cobrem etapa obrigatória, modalidades mínimas, documento obrigatório por modalidade, critério de desempate exigido, atendimento PcD disponível, etc.

O protótipo validou com o CEPS o princípio: **"quando a lei muda, edita o catálogo — sem deploy"**. Isso torna `ObrigatoriedadeLegal` qualitativamente diferente dos outros sete catálogos:

- Os outros sete são **dados enumerados** (listas de tipos, locais, documentos).
- `ObrigatoriedadeLegal` é **lógica de validação executável** endereçada por dados.

O council produziu três posições que convergiram na síntese:

- **Pragmatic Engineer**: "14 regras não justificam DSL — enum tipado + predicate hardcoded em C# com annotations `[BaseLegal]`. Sem rules engine."
- **Devil's Advocate**: "ObrigatoriedadeLegal é cavalo de Troia — DSL, evaluator, modelo de versionamento, surface de teste, história de auditoria, tudo de uma vez. Separe antes que coma o módulo."
- **Product Mind**: "Quando o CEPS for processado (e vai — Lei 14.723 acabou de chegar, mandados de segurança a cada ciclo), o Jurídico precisa apresentar em juízo: *qual regra disparou, com qual base legal, com qual portaria interna, em qual data*. Predicates hardcoded falham em (b), (c), (d). Mas o caminho não é Drools/Camunda — é **validação data-driven com citação**, não rules engine."

A síntese ficou com Product Mind: **validação data-driven com citação não é rules engine no sentido Drools/Camunda**. É um evaluator finito sobre discriminated union de tipos de predicado, com metadata de citação e snapshot na vinculação ao edital.

Separadamente, o reframing do Thinker posicionou `ObrigatoriedadeLegal` como "o registry do domínio fonte do qual os outros sete catálogos derivam" — gravidade futura de um bounded context `Normativos`. O Architect aceitou como load-bearing no resource model mas propôs `Normativos` como módulo futuro. O Pragmatic rejeitou o módulo novo por YAGNI. O sponsor escolheu Pragmatic para V1: fica em Selecao com critérios de promoção documentados.

Esta ADR resolve as duas perguntas: (a) a forma de `ObrigatoriedadeLegal` e (b) se vive em um módulo `Normativos` separado hoje.

## Drivers da decisão

- **Evidência judicial**: o catálogo precisa responder, com dados estruturados, "qual regra rodou em data X com qual base legal" — sem precisar consultar código-fonte.
- **Sem deploy em mudança de lei**: editar a regra (texto, base legal, vigência) deve ser operação de dados, idêntica à edição de qualquer outro catálogo admin.
- **Conjunto fechado de formas**: as 14 regras do protótipo caem em ~8 padrões de predicado distintos; engine Turing-completa é overkill.
- **Coerência com snapshot pattern**: cada edital publicado precisa congelar quais regras se aplicaram, com hash de conteúdo (RN08).
- **Evolução previsível**: quando um segundo módulo (Ingresso, Auxilio Estudantil) precisar do mesmo pattern, há trigger documentado para promover a um módulo `Normativos`.

## Opções consideradas

- **A**: Enum tipado + predicates hardcoded em C# com atributo `[BaseLegal]`.
- **B**: Rules engine completo (Drools, Camunda, MS RulesEngine) com DSL e working memory.
- **C**: Extrair `Normativos` como módulo separado já em V1.
- **D**: Specification Pattern com hierarquia aberta de subclasses.
- **E**: Validação data-driven com discriminated union de tipos de predicado + citação + snapshot-on-bind, fica em Selecao para V1 com critérios de promoção.

## Resultado da decisão

**Escolhida:** "E — validação data-driven com discriminated union, citação e snapshot-on-bind em Selecao para V1", porque atende a evidência judicial e o princípio de "sem deploy" sem cair em rules engine overkill, e mantém forward-compatibilidade com extração futura para `Normativos` quando o trigger acontecer.

### Forma de `ObrigatoriedadeLegal`

Entidade em `Unifesspa.UniPlus.Selecao.Domain.Entities`:

```text
Id: Guid (v7)
TipoEditalCodigo: string                  ("*" para universal, ou FK para TipoEdital.Codigo)
Categoria: enum { Etapa, Modalidade, Desempate, Documento, Bonus, Atendimento, Outros }
RegracodIgo: string                       (ex.: ETAPA_OBRIGATORIA, MODALIDADES_MINIMAS)
Predicado: PredicadoObrigatoriedade       (discriminated union — ver abaixo)
DescricaoHumana: string                   (exibido para admin/jurídico em audit)
BaseLegal: string                         (citação: "Lei 14.723/2023 art.2º")
AtoNormativoUrl: string?                  (link para DOU/portal/PDF)
PortariaInternaCodigo: string?            ("Portaria CTIC 2026/14")
VigenciaInicio: DateOnly
VigenciaFim: DateOnly?
Hash: string                              (SHA-256 do conteúdo)
Proprietario: AreaCodigo?                 (governance per ADR-0055 + ADR-0057)
AreasDeInteresse: IReadOnlySet<AreaCodigo> (governance per ADR-0057)
— herda EntityBase (audit + soft delete) + IAuditableEntity
```

### Discriminated union de tipos de predicado

`PredicadoObrigatoriedade` é conjunto fechado de tipos (sealed abstract record + records derivados):

1. `EtapaObrigatoria(string tipoEtapa)`
2. `ModalidadesMinimas(IReadOnlyList<string> codigos)`
3. `DesempateDeveIncluir(string criterio)`
4. `DocumentoObrigatorioParaModalidade(string modalidade, string tipoDocumento)`
5. `BonusObrigatorio(IReadOnlyList<string> modalidadesAplicaveis)`
6. `AtendimentoDisponivel(IReadOnlyList<string> necessidades)`
7. `ConcorrenciaDuplaObrigatoria()`
8. `Customizado(JsonDocument parametros)` — válvula de escape; emite warning na avaliação; revisão periódica triggera nova variante tipada.

EF Core persiste como coluna `jsonb` via serialização polimórfica de `System.Text.Json` (atributos `[JsonPolymorphic]`, `[JsonDerivedType]`) usando `ValueObjectConverter`.

### Evaluator

`ValidadorConformidadeEdital` é domain service puro em `Selecao.Domain.Services`:

- Pattern match sobre a discriminated union.
- Nunca executa código fornecido por usuário.
- Sem parser, sem interpreter, sem AST.
- Assinatura: `Evaluate(Edital, IReadOnlyList<ObrigatoriedadeLegal>) → ResultadoConformidade`.
- Retorna lista de `RegraAvaliada(RegracodIgo, Aprovada, BaseLegal, PortariaInterna, DescricaoHumana, Hash)`.

Injeção de repository acontece no handler; o domain service permanece infrastructure-free.

### Snapshot-on-bind (estende RN08 e [ADR-0057](0057-areas-rbac-snapshot-historia-invariantes.md))

Quando `Edital.Publicar()` é invocado, o agregado persiste `ObrigatoriedadesSnapshot`:

- Hashes de cada `ObrigatoriedadeLegal` avaliada contra o edital no momento da publicação.
- Mais o conteúdo desnormalizado completo (predicate JSON, base legal, vigência) para reprodutibilidade independente do estado atual.
- Mais os campos de governança (Proprietario, AreasDeInteresse no momento) per [ADR-0057](0057-areas-rbac-snapshot-historia-invariantes.md) Pattern 1.

O snapshot é consultado para auditoria legal; o estado atual é consultado para operações do dia a dia.

### Endpoint de auditoria

```text
GET /api/editais/{id:guid}/conformidade-historica
```

Retorna as regras snapshotadas **como eram na publicação**, recuperável para sempre como evidência legal. Mesmo se uma regra for desativada ou editada depois, o snapshot mantém o hash original e o conteúdo via tabela append-only `ObrigatoriedadeLegalHistorico`.

### Localização: fica em Selecao para V1

`ObrigatoriedadeLegal` vive em `Unifesspa.UniPlus.Selecao.Domain.Entities`. **Não** está em `Parametrizacao` (apenas catálogos cross-cutting per [ADR-0056](0056-parametrizacao-modulo-e-read-side-carve-out.md)). **Não** está em módulo `Normativos` separado ainda.

### Critérios de promoção a futuro módulo `Normativos`

Promover quando **qualquer um** dos seguintes acontecer:

1. Um segundo módulo (Ingresso, Auxilio Estudantil, Recursos) precisa validar seus próprios workflows contra regras com a mesma shape (discriminated predicate + base legal + vigência + snapshot).
2. Número de `RegracodIgo` distintos passa de 50 (limite de complexidade operacional).
3. Jurídico pede relatórios cross-edital ("me mostre todos os editais que referenciaram Lei 14.723 art.2º entre 2024 e 2026") que exigem registry normativo, não apenas snapshots por edital.
4. Variante `Customizado` é usada em produção 3 vezes — sinal de que a discriminated union está estreita.
5. Dois módulos distintos escreveram tipos de predicado com ≥60% de overlap estrutural e divergiram em semântica (adicionado pelo Architect em R2).

Até que algum trigger dispare, `ObrigatoriedadeLegal` é entidade de domínio Selecao, exposta via endpoints REST Parametrizacao-style hospedados em `Selecao.API` para edição admin.

### Integração com governança de áreas

Per [ADR-0055](0055-organizacao-institucional-bounded-context.md) e [ADR-0057](0057-areas-rbac-snapshot-historia-invariantes.md):

- **Regras universais** (Lei 12.711, regulamentação federal): `Proprietario = null`, `AreasDeInteresse = ∅`. Edita só `plataforma-admin`.
- **Regras CEPS-específicas** (Resolução Unifesspa para processos seletivos): `Proprietario = "CEPS"`, `AreasDeInteresse = ["CEPS"]` ou `["CEPS","PROEG"]` quando a PROEG supervisiona.
- **Futuras regras CRCA** para matrícula: quando `ObrigatoriedadeLegal` estender a escopo Ingresso (trigger 1 acima), regras CRCA-owned com `Proprietario = "CRCA"`.

### REST surface

- `GET /api/catalogos/obrigatoriedades?tipoEdital={codigo}` — leitura pública, filtrada por tipo + áreas do caller; inclui regras universais (`"*"`).
- `GET /api/catalogos/obrigatoriedades/{id:guid}` — single rule com HATEOAS `_links`.
- `POST /api/admin/catalogos/obrigatoriedades` — admin scoped per [ADR-0057](0057-areas-rbac-snapshot-historia-invariantes.md); `Idempotency-Key` obrigatório.
- `PUT /api/admin/catalogos/obrigatoriedades/{id:guid}` — soft-delete a versão anterior, insere nova (hash muda); idempotente.
- `DELETE /api/admin/catalogos/obrigatoriedades/{id:guid}` — soft-delete; regras já snapshotadas em editais permanecem intactas.
- `GET /api/editais/{id:guid}/conformidade` — evaluator roda ruleset atual contra o edital (wizard passo 12).
- `GET /api/editais/{id:guid}/conformidade-historica` — regras snapshotadas no momento da publicação (evidência legal).

Vendor MIME: `application/vnd.uniplus.obrigatoriedade-legal.v1+json`.

## Consequências

### Positivas

- "Quando a lei muda, edita o catálogo — sem deploy" é honrado para o conjunto fechado de 8 tipos de predicado.
- Auditoria de evidência legal é first-class: `(rule_hash, base_legal, portaria_interna, vigencia, snapshot_id)` respondem as quatro perguntas forenses do Jurídico.
- RN08 (parameter freeze) é operacionalizado para regras legais, não só para catálogos tabulares.
- Discriminated union previne proliferação de DSL — cada nova variante exige adição explícita no código + amendment de ADR.
- Forward-compatível com extração futura de `Normativos` — critérios de promoção explícitos.
- Passo 12 do wizard (Revisão) é leitura pura contra o evaluator; meta de latência < 200ms p95 é trivial.
- Integra com [ADR-0057](0057-areas-rbac-snapshot-historia-invariantes.md) áreas RBAC e snapshot patterns uniformemente.

### Negativas

- Válvula de escape `Customizado(JsonDocument)` é hazard YAGNI se usada liberalmente.
- Tabela `ObrigatoriedadeLegalHistorico` e snapshots por edital crescem monotonicamente (~200 linhas/ano — negligível).
- Evaluator + admin CRUD UI dá mais trabalho que predicates hardcoded (~2-3 dias extras).
- Promoção futura a `Normativos` exige migração de linhas + adição de publishing de eventos. Mitigado por shape da entidade desenhada para esse movimento.

### Neutras

- Não fecha discussão sobre como semântica `Ativo` por-área seria modelada se `ObrigatoriedadeLegal` virar cross-módulo (mesmo trigger e mesma discussão de [ADR-0057](0057-areas-rbac-snapshot-historia-invariantes.md) Pattern 5).

## Confirmação

- **Risco**: variante `Customizado` vira escape hatch padrão.
  **Mitigação**: emite warning logs na avaliação; revisão trimestral; nova variante tipada no próximo sprint se padrão emergir.
- **Risco**: edição admin no meio de ciclo afeta editais em andamento.
  **Mitigação**: snapshot acontece em `Publicar()`, não em render do wizard. Janela pré-publicação é consultiva; publicação é vinculante.
- **Risco**: Jurídico considera o snapshot insuficiente para algum processo específico.
  **Mitigação**: snapshot inclui `DescricaoHumana`, `BaseLegal`, `PortariaInterna`, `Hash`, serialização completa do predicate. Excede o que o COC legado produz.
- **Risco**: colisão de hash.
  **Mitigação**: SHA-256; probabilidade cosmologicamente negligível.

## Prós e contras das opções

### A — Enum tipado + predicates hardcoded

- **Prós**: Zero risco de DSL. Type safety em compile-time. Testes triviais.
- **Contras**: Quebra "sem deploy em mudança de lei". Adicionar regra exige PR + code review + CI + deploy. Ciclo Lei 14.723/2023: 3 meses da lei ao sistema. Jurídico não consegue auditar qual regra rodou sem suporte de dev — evidência judicial vira testemunho, não documento. Snapshot-on-bind não pode ser content-hashed porque a regra vive no código-fonte, não em linha.

### B — Rules engine completo (Drools/Camunda/MS RulesEngine)

- **Prós**: Expressividade arbitrária. Padrão de indústria para "aplicações rules-driven".
- **Contras**: Overkill massivo para shape finita do problema (8 categorias × ~3 padrões cada). Time nunca operou rules engine. Drools exige JVM (carga operacional). MS RulesEngine é .NET nativo mas ainda é parser + AST surface. Validações de edital são **finitas e shape-bounded**; engine Turing-completa é ferramenta errada.

### C — Extrair `Normativos` já em V1

- **Prós**: Arquiteturalmente puro. Reuso cross-módulo desde o dia 1.
- **Contras**: Prematuro. Nenhum segundo consumer existe. Sprint 3 não cabe. Risco YAGNI: construir para consumer hipotético que pode nunca materializar na forma imaginada. Critérios de promoção documentados aqui permitem extração quando o trigger disparar de fato.

### D — Specification Pattern com hierarquia aberta

- **Prós**: Extensibilidade OO clássica.
- **Contras**: "Admin escreve subclasse C#" não é o workflow (admin edita dados). Para virar data-driven, serializa a spec — daí voltamos a discriminated union com deserialização, não herança.

### E — Validação data-driven com discriminated union (escolhida)

- **Prós**: Discussão acima.
- **Contras**: Discussão acima.

## Emenda 1 (2026-05-16) — vocabulário, URL path-based e semântica do PUT

Durante a auditoria pré-implementação de #461, três pontos desta ADR foram
reconciliados com (a) a diretriz sponsor sobre vocabulário, (b) a convenção
de roteamento formalizada na ADR-0064 (path-based com prefixo de módulo,
otimizada para Traefik) e (c) o que foi efetivamente entregue em #520
(Story #460):

### 1.1 — URLs com prefixo de módulo (`/api/selecao/...`) sem "catálogos"

A seção §"REST surface" usava `/api/catalogos/obrigatoriedades`. O termo
"catálogo" fica reservado para um futuro conceito de domínio (Catálogo de
Serviços ou equivalente); o que estamos construindo é parametrização. E,
per ADR-0064, recursos REST seguem `/api/{modulo}/{recurso}` para permitir
roteamento path-prefix simples no Traefik (1 subdomain, 1 cert TLS, 1
origin CORS).

Mapeamento canônico:

| Antigo (desta ADR) | Novo (per ADR-0064) |
|---|---|
| `GET /api/catalogos/obrigatoriedades?tipoEdital={codigo}` | `GET /api/selecao/obrigatoriedades-legais?tipoEdital={codigo}` |
| `GET /api/catalogos/obrigatoriedades/{id:guid}` | `GET /api/selecao/obrigatoriedades-legais/{id:guid}` |
| `POST /api/admin/catalogos/obrigatoriedades` | `POST /api/selecao/admin/obrigatoriedades-legais` |
| `PUT /api/admin/catalogos/obrigatoriedades/{id:guid}` | `PUT /api/selecao/admin/obrigatoriedades-legais/{id:guid}` |
| `DELETE /api/admin/catalogos/obrigatoriedades/{id:guid}` | `DELETE /api/selecao/admin/obrigatoriedades-legais/{id:guid}` |
| `GET /api/editais/{id:guid}/conformidade` | `GET /api/selecao/editais/{id:guid}/conformidade` |
| `GET /api/editais/{id:guid}/conformidade-historica` | `GET /api/selecao/editais/{id:guid}/conformidade-historica` |

Logs estruturados, OpenAPI tags e códigos de erro continuam usando o nome
da entidade (`obrigatoriedade_legal`, `ObrigatoriedadeLegal`), nunca
"catálogo".

### 1.2 — Semântica do PUT é in-place full-replace

A seção §"REST surface" descreveu PUT como "soft-delete a versão anterior,
insere nova (hash muda)". A entrega em #520 (Story #460) convergiu para
**in-place full-replace** com auditoria via tabela append-only
`obrigatoriedade_legal_historico`:

- O método `ObrigatoriedadeLegal.Atualizar(...)` aplica todos os campos
  literalmente sobre a entidade existente (mantém o `Id`).
- O `ObrigatoriedadeLegalHistoricoInterceptor` grava uma linha no histórico
  com o `Hash` recomputado e o conteúdo canônico — invariante atômico
  garantido por estar dentro do mesmo `SaveChangesAsync`.
- A URI do recurso é estável: `GET /api/selecao/obrigatoriedades-legais/{id}`
  sempre retorna a versão vigente; reconstrução histórica é via
  `obrigatoriedade_legal_historico` ou via `EditalGovernanceSnapshot`
  (quando a regra foi vinculada a um edital publicado).
- Editais já publicados antes da edição mantêm seu `EditalGovernanceSnapshot`
  original — o hash difere do hash atual da regra; é exatamente isso que
  preserva a evidência forense.
- O `Atualizar` exige **todos os campos explicitamente** (full-replace
  semantics — defaults `null` removidos da assinatura per Codex P1 em #520).

Esta semântica é mais simples, preserva URI stability e dispensa lógica de
"reativação" de versões soft-deleted — o snapshot histórico já cumpre o
papel de evidência da versão anterior.

### 1.3 — `EditalGovernanceSnapshot` é `IForensicEntity`

A seção §"Snapshot-on-bind" referia-se a `ObrigatoriedadesSnapshot` como
nome conceitual. A entrega em #520 nomeou-o `EditalGovernanceSnapshot` e
o classificou como `IForensicEntity` (ADR-0063) — append-only, sem
soft-delete, com FK `RESTRICT` para `editais` (em #461) e para
`obrigatoriedades_legais` quando relevante.

## Mais informações

- [ADR-0013](0013-motor-de-classificacao-como-servicos-de-dominio-puros.md) — Motor de classificação como pure domain services (precedent para evaluator data-driven sem DSL).
- [ADR-0028](0028-versionamento-per-resource-content-negotiation.md) — Versionamento per-resource.
- RN08 de `docs/visao-do-projeto.md` — Congelamento de parâmetros na publicação do edital.
- DAMA-DMBOK 2 — Reference Data Management (snapshot patterns).
- Specification Pattern com Rule Repository — Microsoft Learn, DevIQ.
- [ADR-0055](0055-organizacao-institucional-bounded-context.md) — OrganizacaoInstitucional bounded context.
- [ADR-0056](0056-parametrizacao-modulo-e-read-side-carve-out.md) — Módulo Parametrizacao e carve-out read-side.
- [ADR-0057](0057-areas-rbac-snapshot-historia-invariantes.md) — RBAC por áreas com snapshot, histórico, invariantes.
- [ADR-0063](0063-entidades-forensics-isentas-de-soft-delete.md) — Entidades forensics append-only isentas de soft-delete.
- [ADR-0064](0064-convencao-roteamento-path-based-com-prefixo-modulo.md) — Convenção de roteamento path-based com prefixo de módulo.
