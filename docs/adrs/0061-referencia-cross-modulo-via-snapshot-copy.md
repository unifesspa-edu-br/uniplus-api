---
status: "accepted"
date: "2026-05-14"
decision-makers:
  - "Tech Lead (CTIC)"
consulted: []
informed:
  - "Equipe Uni+"
---

# ADR-0061: Referência cross-módulo via snapshot-copy (não FK)

## Contexto e enunciado do problema

[ADR-0056](0056-parametrizacao-modulo-e-read-side-carve-out.md) estabelece o carve-out read-side cross-módulo: módulos consomem dados de referência uns dos outros via `IXxxReader` em assemblies `.Contracts`. A ADR deixou implícita uma questão crítica sobre **semântica de escrita** quando uma entidade de domínio em um módulo referencia um catálogo em outro módulo.

Exemplo concreto: `Selecao.LocalProva` (local de prova) precisa do endereço físico. Per [ADR-0056](0056-parametrizacao-modulo-e-read-side-carve-out.md), o endereço vive em `Parametrizacao.Endereco`. Pergunta: `LocalProva.EnderecoId` carrega referência viva por foreign key para `Endereco` em outro banco, ou tira snapshot dos dados do endereço no momento do binding?

A restrição é firme: per [ADR-0054](0054-naming-convention-e-strategy-migrations.md) e `docs/guia-banco-de-dados.md`, a plataforma usa **bancos PostgreSQL isolados por módulo** (connection strings e usuários separados). Não há schema compartilhado; **FK cross-banco não é exequível** no PostgreSQL.

Diretriz do sponsor durante o brainstorming do TechSpec (2026-05-13): "Não pretendo fazer FK cross-db, podemos apenas usar os dados para evitar ficar cadastrando manual toda vez e usar os dados com snapshot no destino".

Isso é coerente com dois patterns já binding no projeto:

- **RN08** de `docs/visao-do-projeto.md` — congelamento de parâmetros no `Edital.Publicar()`: cada parâmetro que influencia a classificação é snapshotado no edital para que auditoria retrospectiva reproduza exatamente o estado da publicação.
- **Pattern 1 do [ADR-0057](0057-areas-rbac-snapshot-historia-invariantes.md)** — snapshot do `Proprietario` + `AreasDeInteresse` de cada item de catálogo referenciado por um edital, persistido em `EditalGovernanceSnapshot`.

Esta ADR estende a mesma disciplina ao binding cross-módulo de dados de referência: quando uma entidade de domínio do módulo A se vincula a um catálogo do módulo B, a operação copia os dados relevantes para dentro da entidade consumidora como value object imutável.

## Drivers da decisão

- **Reprodutibilidade legal**: RN08 e [ADR-0057](0057-areas-rbac-snapshot-historia-invariantes.md) já estabelecem snapshot como disciplina do projeto.
- **Isolation de bancos**: FK cross-banco não é opção técnica.
- **UX admin**: admin não quer redigitar o mesmo endereço de Marabá toda vez (validado pelo protótipo).
- **Operacionalidade**: evitar projection machinery (consumers Kafka, replay, reconciliação) para um caso 1-DB → 1-consumer.
- **Defensibilidade em mandado de segurança**: "qual endereço foi comunicado ao candidato?" deve ser respondível independentemente de edições posteriores no catálogo.

## Opções consideradas

- **A**: Validação síncrona cross-DB via `IXxxReader.ExisteAsync` no handler, sem snapshot.
- **B**: Projeção Kafka — réplica local read-only de `Endereco` em `Selecao`.
- **C**: Nenhuma referência — admin redigita endereço a cada `LocalProva`.
- **D**: Re-resolução ao vivo na leitura, sem snapshot.
- **E**: Snapshot-copy (value object embedded) com `OrigemId` opcional para rastreabilidade (escolhida).

## Resultado da decisão

**Escolhida:** "E — snapshot-copy via value object embedded, com `{Catalogo}OrigemId` opcional como rastreabilidade sem FK", porque entrega o ganho de UX (admin escolhe de catálogo) sem perder a defensibilidade do snapshot, e respeita a isolation entre bancos.

### Regras do pattern

1. A entidade consumidora armazena os dados do catálogo como **value object embedded** (EF Core `OwnsOne`), não apenas como ID por foreign key.
2. A entidade consumidora opcionalmente armazena `{Catalogo}OrigemId: Guid?` como **rastreabilidade** — aponta para a linha do catálogo de origem, mas **sem FK no banco**.
3. `IXxxReader.ObterPorIdAsync` e `ListarVisiveisAsync` suportam a **UI admin de seleção** (admin escolhe linha existente em vez de digitar campos).
4. A operação de escrita resolve os dados do catálogo via reader e **copia** os campos relevantes para o value object embedded.
5. Edições subsequentes na linha-fonte do catálogo **não** propagam para a entidade consumidora. Snapshot é imutável pela vida da entidade (sujeito a soft-delete ou re-bind explícito).
6. **Nenhuma declaração `FOREIGN KEY`** existe no banco consumidor apontando para o banco produtor.

### Exemplo: `LocalProva` referenciando `Endereco`

`Selecao.LocalProva`:

```csharp
public sealed class LocalProva : EntityBase, IAuditableEntity
{
    public string Codigo { get; private set; }                     // "MARABA"
    public string Nome { get; private set; }                       // display
    public EnderecoSnapshot Endereco { get; private set; }         // OwnsOne — IMUTÁVEL
    public Guid? EnderecoOrigemId { get; private set; }            // rastreabilidade (sem FK)
    public int CapacidadeMaxima { get; private set; }
    public string? ResponsavelExame { get; private set; }
    public IReadOnlyList<string> CondicoesAcessibilidade { get; }
    // governança de áreas per ADR-0057
}

public sealed record EnderecoSnapshot(
    string CEP,
    string Logradouro,
    string Numero,
    string? Complemento,
    string Bairro,
    string Municipio,
    string MunicipioIbgeId,
    string UF);
```

Quando admin cria um `LocalProva` via `POST /api/admin/selecao/locais-prova`, o handler:

1. Chama `_enderecoReader.ObterPorIdAsync(command.EnderecoOrigemId)`.
2. Devolve `Result.Failure(DomainError("LocalProva.EnderecoOrigemNaoEncontrado", ...))` se nada vier.
3. Senão monta `EnderecoSnapshot` a partir da resposta do reader.
4. Cria o agregado `LocalProva` com o snapshot dentro.
5. Persiste.

Os campos de endereço ficam congelados em `selecao.locais_prova`. Se um admin de `Parametrizacao` editar o `Endereco` depois, o `LocalProva` mantém seu snapshot original — coerente com RN08 ("auditoria deve reproduzir o estado no momento do binding").

### Onde o pattern se aplica

- `Selecao.LocalProva.Endereco` ← `Parametrizacao.Endereco` (caso sponsor-clarified).
- `Selecao.Edital.ConfiguracaoModalidade.Modalidade` ← `Parametrizacao.Modalidade` no `Publicar()` (per RN08 + Pattern 1 de [ADR-0057](0057-areas-rbac-snapshot-historia-invariantes.md)).
- `Selecao.Edital.DocumentoExigido.TipoDocumento` ← `Parametrizacao.TipoDocumento` no `Publicar()`.
- `Selecao.Edital.AtendimentoEspecial.NecessidadeEspecial` ← `Parametrizacao.NecessidadeEspecial` no `Publicar()`.
- Futuro: qualquer `Ingresso.Matricula` referenciando `Modalidade`, `TipoDocumento` etc. segue o mesmo pattern.

### Onde o pattern NÃO se aplica

- Referências intra-módulo (ex.: `Selecao.Etapa.EditalId` → `Selecao.Edital.Id`) — FK normal no mesmo banco.
- Joins dentro do mesmo DbContext entre entidades de catálogo (ex.: `Parametrizacao.TipoDocumento` referenciando outra entidade `Parametrizacao`) — FK normal.
- Referência a `OrganizacaoInstitucional.AreaOrganizacional` via `AreaCodigo: string` — é por código (não por id) e `AreaCodigo` é imutável post-criação per Invariante 2 do [ADR-0055](0055-organizacao-institucional-bounded-context.md). Mesma disciplina por mecanismo diferente.

### Por que `OrigemId` opcional mas sem FK

`EnderecoOrigemId` serve para:

- **UX admin**: "este LocalProva veio do Endereco X — abre o original para comparar".
- **Relatórios de auditoria**: "todos os LocalProva criados a partir do Endereco X".

Não é garantia referencial. A linha-fonte pode ter sido soft-deletada; o ID de rastreabilidade permanece. Relatórios lidam com `IsDeleted` graciosamente via agregador de aplicação.

### Papel do reader

`IEnderecoReader` (e análogos) tem três usos:

1. **Listas de seleção admin** — `ListarVisiveisAsync` popula pickers.
2. **Validação na escrita** — `ObterPorIdAsync` resolve a linha-fonte para montar o snapshot.
3. **Workflow opcional de re-bind** — admin pode re-resolver `LocalProva.EnderecoOrigemId` e atualizar o snapshot (ação explícita, auditada).

O reader **não** materializa o valor continuamente na entidade consumidora. Não há tabela de projeção espelhando `Endereco` em `Selecao`. O cache distribuído Redis 5 minutos sobre o reader (Pattern 4 do [ADR-0056](0056-parametrizacao-modulo-e-read-side-carve-out.md)) atende as leituras.

## Consequências

### Positivas

- **Reprodutibilidade de auditoria**: `LocalProva` histórico preserva o endereço exato que foi comunicado ao candidato no binding. Defensável em juízo.
- **Sem FK cross-banco**: respeita isolation do [ADR-0054](0054-naming-convention-e-strategy-migrations.md); zero acoplamento de schema.
- **Sem projection machinery**: nada de consumer Kafka, sem janela de eventual consistency no read-side, sem risco de replay storm.
- **Coerente com RN08 e Pattern 1**: a mesma disciplina de snapshot uniformemente aplicada a referência cross-módulo e a metadata de governança de áreas.
- **Papel do reader claro**: admin escolhe de catálogo existente (UX); write copia dado (integridade).
- **Custo de armazenamento negligível**: 8 campos de endereço × 6 LocalProvas × poucos editais/ano = trivial.

### Negativas

- Duplicação de dado: se admin corrigir typo no `Endereco`, snapshots existentes mantêm valor antigo.
  **Mitigação**: operação explícita "re-bind" re-resolve e atualiza o snapshot, auditada como ação de usuário.
- Schema do `LocalProva` cresce ~8 colunas. Aceitável.

### Neutras

- `EnderecoSnapshot` (e demais snapshots de catálogo) vive na Domain do **módulo consumidor**, não no `.Contracts` do produtor — fica claro quem é dono do shape.

## Confirmação

- **Risco**: admin assume que editar `Endereco` propaga; surpresa quando histórico não muda.
  **Mitigação**: UI admin avisa ao editar Endereco: "Mudanças aplicam apenas a futuros bindings. Para atualizar LocalProva existente, use a ação Re-bind." Documentado no runbook operacional.
- **Risco**: re-bind é usado descuidadamente, destrói trilha de auditoria.
  **Mitigação**: re-bind cria nova entrada de audit log; snapshot anterior preservado em `LocalProvaHistorico` (SCD Type 2, análogo ao Pattern 2 do [ADR-0057](0057-areas-rbac-snapshot-historia-invariantes.md)). Operação deliberada com confirmação.
- **Risco**: cresce o armazenamento se muitos catálogos adotarem o pattern.
  **Mitigação**: catálogos bounded; entidades consumidoras bounded. Negligível na escala esperada.
- **Risco**: `EnderecoOrigemId` é tratado como FK real por junior dev.
  **Mitigação**: comentário no código + fitness test ArchUnitNET — nenhuma config EF Core declara `HasOne()`; é `Guid?` puro. Linter check.

## Prós e contras das opções

### A — Validação síncrona cross-DB no handler, sem snapshot

- **Prós**: sem duplicação; economia de storage; updates de endereço propagam.
- **Contras**: sem integridade no banco; janela TTL de cache permite criação órfã (admin deleta Endereco, write acontece durante stale cache). Auditoria "qual endereço foi usado em data X?" requer join cross-banco + lógica de soft-delete — frágil.
- **Por que rejeitada**: mesmos vícios das junctions polimórficas em [ADR-0060](0060-junction-tables-por-entidade-com-view-unificada.md) — sem integridade real, audit story depende de correção da aplicação. Sponsor rejeitou explicitamente.

### B — Projeção Kafka, réplica local read-only

- **Prós**: FK real; eventual consistency; padrão event-driven.
- **Contras**: caro operacionalmente: projection handler, replay logic, dead-letter handling, jobs de reconciliação, invalidação de cache, debug de projeção stale. Prematuro para 1-DB-1-consumer. Janela de eventual consistency cria bugs visíveis para usuário ("acabei de criar Endereco X — por que LocalProva não vê?").
- **Por que rejeitada**: complexidade injustificada para V1; snapshot entrega o mesmo benefício de fluxo de dado sem a maquinaria.

### C — Sem referência alguma, admin redigita endereço

- **Prós**: o mais simples possível. Zero acoplamento cross-módulo.
- **Contras**: derrota o valor validado pelo protótipo: "admins não querem recadastrar Marabá toda vez que criamos um LocalProva". Duplicação leva a typos.
- **Por que rejeitada**: sponsor escolheu manter `Endereco` reutilizável; reduz atrito.

### D — Referência viva com re-resolução em toda leitura

- **Prós**: sem staleness; sempre dado atual.
- **Contras**: derrota RN08/audit por completo — LocalProva histórico mudaria silenciosamente o endereço quando o catálogo fonte fosse editado. Mandados de segurança seriam indefensáveis.
- **Por que rejeitada**: viola a disciplina de snapshot que existe especificamente para evidência legal reproduzível.

### E — Snapshot-copy + OrigemId opcional (escolhida)

- **Prós**: discussão acima.
- **Contras**: discussão acima.

## Mais informações

- [ADR-0056](0056-parametrizacao-modulo-e-read-side-carve-out.md) — Módulo Parametrizacao e carve-out read-side (esta ADR clarifica a semântica do write).
- [ADR-0057](0057-areas-rbac-snapshot-historia-invariantes.md) — Pattern 1 é precedente para snapshot-on-bind de metadata de governança.
- [ADR-0058](0058-obrigatoriedade-legal-validacao-data-driven.md) — Snapshot-on-bind para regras legais.
- [ADR-0060](0060-junction-tables-por-entidade-com-view-unificada.md) — Mesmo raciocínio de isolation entre bancos.
- RN08 de `docs/visao-do-projeto.md` — Congelamento de parâmetros no `Publicar()` do edital.
- [ADR-0054](0054-naming-convention-e-strategy-migrations.md) — Política de isolation de bancos.
- [ADR-0028](0028-versionamento-per-resource-content-negotiation.md) — Snapshot-on-bind no motor de classificação.
