---
status: "accepted"
date: "2026-05-16"
decision-makers:
  - "Tech Lead (CTIC)"
consulted: []
informed:
  - "Equipe Uni+"
---

# ADR-0063: Entidades forensics append-only são isentas de soft-delete

## Contexto e enunciado do problema

A política de persistência do Uni+ exige soft-delete em toda entidade
persistida (`EntityBase` carrega `IsDeleted`/`DeletedAt`/`DeletedBy`,
`SoftDeleteInterceptor` converte `EntityState.Deleted` em `Modified`).
O motivo é LGPD art. 37 + reprodutibilidade jurídica — nenhuma escrita
some sem trilha.

A Story #460 (ADR-0058 + ADR-0057 Pattern 1) introduz duas tabelas com
semântica distinta: **append-only forensic snapshots**.

- `obrigatoriedade_legal_historico`: uma linha por mutação (Insert /
  Update / soft-delete) de `ObrigatoriedadeLegal`, contendo o JSON
  canônico do conteúdo no momento e o hash determinístico — a evidência
  jurídica de "qual regra rodou em data X com qual base legal".
- `edital_governance_snapshot`: linha capturada por `Edital.Publicar()`
  (#462) com o resolvido `(rule_hash, base_legal, portaria, vigência,
  predicado, proprietário, áreas)` no momento da publicação.

Para essas tabelas, o ciclo de vida é fundamentalmente diferente: só
existe `INSERT`. Qualquer `UPDATE`/`DELETE` em produção é tratado como
incidente operacional (corrupção de evidência). Aplicar soft-delete
quebraria o invariante:

- Carregar `IsDeleted`/`DeletedAt`/`DeletedBy` sugere semântica de
  "apagar" linhas — fonte de confusão para devs que olharem o schema.
- O `SoftDeleteInterceptor` converte `Deleted → Modified`. Para
  append-only não existe `Deleted`; aceitá-lo silenciaria um bug.
- O custo de manter colunas que NUNCA são populadas adiciona ruído ao
  schema e aos JSON-of-truth (`obrigatoriedade_legal_historico.conteudo_jsonb`).

Sem decisão explícita, a regra global "toda entidade tem soft-delete"
do projeto colide com a forma plena dessas entidades — gerando
ambiguidade para todo agente/dev que tocar o módulo.

## Drivers da decisão

- **Coerência semântica:** soft-delete só faz sentido onde "apagar"
  existe como operação de domínio. Em forensic append-only, "apagar"
  é incidente.
- **Rastreabilidade da exceção:** qualquer regra global do projeto
  precisa documentar suas exceções com critério, não em CLAUDE.md
  (que orienta agentes, não vincula código).
- **Enforcement automatizável:** a exceção deve ser detectável por
  fitness test ArchUnitNET — um dev não pode acidentalmente criar
  entidade sem `EntityBase` e sem o marcador.
- **Auditoria de mutação anômala:** se houver `UPDATE`/`DELETE` em
  produção contra essas tabelas, o sistema deve detectar (logs Postgres,
  trigger ou audit policy) — o invariante "só INSERT" é load-bearing.

## Opções consideradas

- **A**: Todas as entidades herdam `EntityBase` sem exceção (status quo
  do CLAUDE.md). As linhas `IsDeleted=true/DeletedAt!=null` em tabelas
  forensic seriam tratadas operacionalmente como incidente; sem
  marcador estrutural.
- **B**: Interface marcadora `IForensicEntity` em `Kernel.Domain.Interfaces`
  com fitness test verificando que (1) entidades não herdam `EntityBase`,
  (2) tabelas correspondentes não têm colunas de soft-delete na migration,
  (3) qualquer modificação no schema dessas tabelas exige PR review explícito.
- **C**: Tornar a forma plena de `EntityBase` configurável — atributo
  `[NoSoftDelete]` em propriedade desabilita o interceptor. Requer
  refactor invasivo em `SoftDeleteInterceptor` e quebra simetria de
  cross-cutting.

## Resultado da decisão

**Escolhida:** "B — interface marcadora `IForensicEntity`", porque ela
endereça os quatro drivers: explicita a exceção, é rastreável por
fitness test, e mantém o `SoftDeleteInterceptor` simples (continua
operando apenas sobre `EntityBase`, ignorando o resto).

### Forma concreta

```csharp
namespace Unifesspa.UniPlus.Kernel.Domain.Interfaces;

/// <summary>
/// Marca entidades append-only de evidência forense — qualquer
/// UPDATE/DELETE em produção é incidente operacional. ENTIDADES com este
/// marcador NÃO herdam EntityBase e NÃO carregam soft-delete; a tabela
/// correspondente tem schema sem IsDeleted/DeletedAt/DeletedBy.
/// </summary>
public interface IForensicEntity
{
    /// <summary>Identificador único da linha (UUID v7 do timestamp do snapshot).</summary>
    Guid Id { get; }
}
```

Aplicação inicial: `ObrigatoriedadeLegalHistorico`, `EditalGovernanceSnapshot`
(Story #460). Futuras tabelas de mesma natureza (`proprietario_historico`,
`area_interesse_binding_historico` quando entrarem em #461) implementam o
mesmo marcador.

### Fitness test

Em `tests/Unifesspa.UniPlus.ArchTests/Persistence/ForensicEntityConventionsTests.cs`:

```text
- Toda classe IForensicEntity NÃO herda EntityBase (regra de exclusão mútua)
- Toda classe IForensicEntity é sealed
- Toda classe IForensicEntity tem ao menos um construtor privado (sem instanciação direta fora da classe)
- A classe expõe Id, factory method estático, e propriedades com setter privado/init
```

### Auditoria de mutação anômala (operacional)

Em produção (ADR-0007 PostgreSQL 18), recomenda-se trigger `BEFORE
UPDATE OR DELETE` sobre as tabelas forensic que insere uma linha em
`audit.forensic_mutation_attempt` antes de bloquear a operação. Esse
trigger NÃO entra nesta Story #460 — é responsabilidade da DBA ou de
uma Story dedicada de hardening (`F-Sec` no backlog). Em V1 a defesa é
apenas a interface marcadora + ausência de operações que invoquem
`Remove()` ou `Update()` em código de aplicação.

## Consequências

### Positivas

- A regra "soft-delete em toda entidade" do projeto fica precisa: vale
  para entidades de domínio mutáveis, não para tabelas de evidência.
- Devs novos identificam a categoria pelo marcador, sem precisar ler
  comentários espalhados.
- Fitness test trava regressão: entidade forensic ganhando `EntityBase`
  por descuido falha o build.

### Negativas

- Há agora **duas hierarquias** de entidade no domínio (`EntityBase`
  vs `IForensicEntity`). Custo cognitivo +1 unidade.
- O fitness test tem que rodar em ArchTests cross-módulo (forensic
  entities podem nascer em qualquer módulo).

### Neutras

- A interface marcadora não impede um agente malicioso de criar
  `INSERT/UPDATE` direto via SQL — defesa em profundidade exige o
  trigger Postgres (escopo futuro).

## Confirmação

- Fitness test `ForensicEntityConventionsTests` em
  `tests/Unifesspa.UniPlus.ArchTests/` valida exclusão mútua com
  `EntityBase`, sealed, factory privada.
- Code review obrigatório: qualquer PR que adicione `: EntityBase` em
  classe `IForensicEntity` (ou vice-versa) precisa justificativa
  explícita e amendment desta ADR.

## Prós e contras das opções

### A — sem exceção (status quo)

- Bom: zero código novo, regra única ao ler o CLAUDE.md.
- Ruim: colunas `IsDeleted/DeletedAt/DeletedBy` mortas em tabelas que
  nunca as populam; força semântica de soft-delete onde não cabe.

### B — interface marcadora + fitness test (escolhida)

- Bom: explícito, rastreável, sem refactor em código existente.
- Bom: assimetria detectada em build.
- Ruim: duas hierarquias de entidade.

### C — atributo `[NoSoftDelete]` configurável

- Bom: uma única hierarquia (`EntityBase` parametrizável).
- Ruim: refactor invasivo no `SoftDeleteInterceptor`, quebra simetria
  com `AuditableInterceptor`, requer reflection adicional no caminho
  hot do save.

## Mais informações

- [ADR-0057](0057-areas-rbac-snapshot-historia-invariantes.md) — RBAC
  por áreas com snapshot, histórico, invariantes (Pattern 1: snapshot
  de `Edital.Publicar()`).
- [ADR-0058](0058-obrigatoriedade-legal-validacao-data-driven.md) —
  ObrigatoriedadeLegal validação data-driven (tabela `historico`
  append-only).
- LGPD Lei 13.709/2018 art. 37 — registro de operações de tratamento
  de dados pessoais (informação que motiva soft-delete, e o caráter
  imutável das evidências forensics).
- Story #460 — primeira aplicação do marcador (`ObrigatoriedadeLegalHistorico`,
  `EditalGovernanceSnapshot`).
