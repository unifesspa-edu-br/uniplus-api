---
status: "accepted"
date: "2026-06-15"
decision-makers:
  - "Tech Lead (CTIC)"
consulted: []
informed:
  - "Equipe Uni+"
---

# ADR-0077: Identidade institucional canônica de `Unidade`

## Contexto e enunciado do problema

A [ADR-0055](0055-organizacao-institucional-bounded-context.md) estabeleceu `OrganizacaoInstitucional` como bounded context dedicado, tendo `AreaOrganizacional` — um **roster fechado** de cinco áreas (CEPS, CRCA, PROEG, PROGEP, PLATAFORMA) — como agregado. Essa modelagem do eixo organizacional foi **descontinuada**: a estrutura administrativa real da Unifesspa não é um roster fechado de cinco áreas, e sim uma árvore aberta de aproximadamente 690 unidades em até seis níveis. O eixo organizacional passou a ser a entidade `Unidade`, um cadastro **aberto e hierárquico**.

A `Unidade` entrega **identidade rica** — `Nome`, `Alias`, `Slug`, `Sigla`, `Codigo`, hierarquia por `UnidadeSuperiorId`, marcação acadêmica, vigência e origem — com `Slug`/`Sigla`/`Codigo` únicos entre unidades vivas, `Alias` não-único, e histórico de identificadores preservando a vigência de cada valor. Essa identidade é decisão vinculante e já está implementada e em uso (a busca textual da listagem de unidades pesquisa sobre esses identificadores).

O cabeçalho da entrega, porém, ancora-se apenas na ADR-0055 — que descreve a `AreaOrganizacional`, não a identidade de `Unidade`. Falta o **registro formal** dessa decisão: para rastreabilidade, e porque a frente de Autorização (ADR-0078 em diante) referencia a identidade de `Unidade` como sujeito e como recurso.

O problema é **como identificar uma `Unidade` de forma estável e auditável**: distinguindo a referência imutável usada em relacionamentos dos rótulos institucionais que mudam por ato administrativo, garantindo unicidade onde ela importa sem impedir o agrupamento popular por alias.

## Drivers da decisão

- **Referência estável** para relacionamentos (FKs internas, snapshots cross-módulo, cursor keyset, sujeito/recurso de autorização) que não quebre quando rótulos mudam.
- **Unicidade dos rótulos institucionais** (`Sigla`, `Codigo`, `Slug`) entre unidades vivas — evita ambiguidade em listas, URLs e integração.
- **`Alias` é agrupamento popular**, naturalmente compartilhado entre divisões de uma mesma pró-reitoria/centro — não pode ser único.
- **Identificadores mudam por ato institucional** (portaria); a auditoria precisa responder "que sigla esta unidade tinha em 2024".
- **`Slug` é forma normalizada** para URL e integração de identidade — distinto da sigla real (que tem hífen, caixa mista, ponto).
- **Estrutura real aberta e hierárquica** (~690 unidades, seis níveis) — não roster fechado.
- **Base para a frente de Autorização**: o sujeito e o recurso referenciam `Unidade` por `Id` estável (ADR-0078, ADR-0079, ADR-0087).

## Opções consideradas

- **A**: Identificar `Unidade` por um rótulo natural (sigla ou código) como chave de relacionamento.
- **B**: `Id` (Guid v7) como referência universal imutável; `Slug`/`Sigla`/`Codigo` únicos entre vivos; `Alias` não-único; histórico de identificadores versionando os rótulos por vigência; cadastro aberto e hierárquico.
- **C**: Manter `AreaOrganizacional` (roster fechado da ADR-0055) como eixo organizacional.

## Resultado da decisão

**Escolhida:** "B — `Id` imutável com rótulos únicos versionados", porque separa a referência estável (relacionamentos) dos rótulos institucionais mutáveis, preserva a auditoria temporal de identidade e reflete a estrutura administrativa real (aberta, hierárquica).

### `Id` (Guid v7) é a referência universal

O `Id` é imutável (via `EntityBase`, [ADR-0032](0032-guid-v7-para-identidade-de-entidades.md)) e gerado na criação. Toda referência — `UnidadeSuperiorId`, FKs cross-módulo por snapshot-copy, cursor keyset de paginação, sujeito e recurso de autorização — usa o `Id`, **nunca um rótulo**. Rótulos podem mudar por portaria; o `Id` não.

### `Slug`, `Sigla` e `Codigo` são únicos entre unidades vivas

Os três identificadores são únicos entre unidades não excluídas, via **índice único parcial** com filtro `is_deleted = false` (naming snake_case da [ADR-0054](0054-naming-convention-e-strategy-migrations.md); soft-delete opt-in). Um valor liberado por soft-delete pode ser reusado por outra unidade. A checagem de colisão na aplicação espelha o filtro de "vivos".

### `Alias` é não-único

`Alias` é rótulo de agrupamento popular — várias divisões compartilham o alias da pró-reitoria ou centro pai. É indexado para busca, **sem** restrição de unicidade.

### `Slug` é distinto de `Sigla`

`Slug` é a forma normalizada (kebab-case) usada em caminhos de URL e integração de identidade; `Sigla` é o identificador institucional real, que pode conter hífen, caixa mista e ponto. São campos separados, com papéis distintos — `Slug` não é derivado automático de `Sigla`.

### Histórico de identificadores preserva a vigência

Quando `Slug`, `Sigla`, `Codigo` ou `Alias` mudam por ato, a entrada anterior tem sua vigência fechada e uma nova é aberta (`UnidadeIdentificadorHistorico`). A trilha responde qual valor vigorava em cada período. O `Id` permanece; apenas os rótulos versionam — o passado não é sobrescrito, registra-se um novo fato a partir da data da mudança.

### Cadastro aberto e hierárquico

`Unidade` é cadastro **aberto** (CRUD administrativo), não roster fechado. A hierarquia é modelada por `UnidadeSuperiorId` (auto-relação, FK com `Restrict`), refletindo a estrutura real (~690 unidades, até seis níveis). Estar acima na hierarquia **não** concede, por si, acesso ao que está abaixo — essa é decisão da frente de Autorização ([ADR-0079](0079-hierarquia-institucional-sem-heranca-de-permissao.md)), não desta ADR.

### O que esta ADR não decide

- **Não** decide o modelo de autorização nem a regra de hierarquia-sem-herança — frente própria (ADR-0078, ADR-0079, ADR-0087).
- **Não** decide o rename de eventual código legado de unidade — frente de refactor própria.
- **Não** reabre a decisão de bounded context da ADR-0055, que permanece válida (o módulo `OrganizacaoInstitucional` e o banco `uniplus_organizacao` seguem). Esta ADR a **refina** no plano de identidade da entidade organizacional, registrando a sucessão de `AreaOrganizacional` por `Unidade`.

## Consequências

### Positivas

- Relacionamentos estáveis: mudar sigla ou código por portaria não quebra FKs, cursores nem concessões de autorização.
- Auditoria temporal de identidade: a trilha responde qual rótulo vigorava em cada período.
- `Slug` normalizado dá URLs e integração previsíveis, sem acoplar à sigla real.
- Base limpa para a frente de Autorização referenciar `Unidade` por `Id`.

### Negativas

- Custo de manter o histórico de identificadores a cada mudança de rótulo.
- Unicidade parcial (entre vivos) exige índice filtrado e uma checagem de colisão na aplicação que espelhe o mesmo filtro.

### Neutras

- Registro retroativo: a entidade já está implementada e em uso (a busca textual da listagem pesquisa sobre os identificadores). Esta ADR formaliza a decisão já vigente.

## Confirmação

1. Entidade `Unidade` em `Unifesspa.UniPlus.OrganizacaoInstitucional.Domain` com os campos descritos e factory com validação.
2. Índices únicos parciais `ix_unidade_slug_vivo`, `ix_unidade_sigla_vivo`, `ix_unidade_codigo_vivo` (filtro `is_deleted = false`) e índice não-único de `alias`.
3. `UnidadeIdentificadorHistorico` registra `TipoIdentificador` (`Slug`/`Sigla`/`Codigo`/`Alias`), `Valor`, `VigenciaInicio`/`VigenciaFim` e `MotivoMudanca`.
4. `Id` Guid v7 via `EntityBase` (ADR-0032); hierarquia por `UnidadeSuperiorId` com FK `Restrict`.
5. Testes de unidade e integração do módulo cobrem a unicidade entre vivos, o histórico na mudança de identificador e a busca acento/caixa-insensível sobre os identificadores.

## Prós e contras das opções

### A — Rótulo natural (sigla/código) como chave de relacionamento

- **Prós**: dispensa um identificador técnico; relacionamentos legíveis a olho nu.
- **Contras**: rótulos mudam por ato administrativo; cada mudança quebraria FKs, cursores e concessões de autorização, ou exigiria atualização em cascata. Sem referência estável, a auditoria não consegue afirmar que duas linhas se referem à mesma unidade ao longo do tempo.

### B — `Id` imutável com rótulos únicos versionados (escolhida)

- **Prós**: referência estável desacoplada dos rótulos; auditoria temporal de identidade; unicidade onde importa sem impedir alias compartilhado; base direta para autorização por `Id`.
- **Contras**: exige histórico de identificadores e índices únicos parciais; um identificador técnico que não aparece nos rótulos institucionais.

### C — Manter `AreaOrganizacional` (roster fechado, ADR-0055)

- **Prós**: nenhum trabalho adicional; roster pequeno e controlado por ADR.
- **Contras**: não modela a estrutura real (árvore aberta de ~690 unidades em seis níveis); adicionar unidade seria ato de governança por ADR, inviável na escala real; não comporta identidade rica nem histórico de rótulos.

## Mais informações

- [ADR-0055](0055-organizacao-institucional-bounded-context.md) — `OrganizacaoInstitucional` como bounded context; refinada aqui no plano de identidade (a `AreaOrganizacional` foi sucedida por `Unidade`).
- [ADR-0032](0032-guid-v7-para-identidade-de-entidades.md) — Guid v7 universal em `EntityBase`; fundamenta o `Id` como referência estável.
- [ADR-0054](0054-naming-convention-e-strategy-migrations.md) — naming `snake_case` e isolamento por banco; fundamenta os índices únicos parciais.
- [ADR-0079](0079-hierarquia-institucional-sem-heranca-de-permissao.md) — hierarquia institucional sem herança de permissão; consome a identidade de `Unidade`.
- [ADR-0078](0078-modelo-de-autorizacao-pbac-abac.md) e [ADR-0087](0087-banco-isolado-para-o-contexto-de-autorizacao.md) — frente de Autorização que referencia `Unidade` por `Id`.
- Issue unifesspa-edu-br/uniplus-api#595.
