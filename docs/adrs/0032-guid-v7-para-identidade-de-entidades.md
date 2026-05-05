---
status: "accepted"
date: "2026-05-05"
decision-makers:
  - "Tech Lead (CTIC)"
---

# ADR-0032: Guid v7 (RFC 9562) como identidade de entidades de domínio

## Contexto e enunciado do problema

O projeto usa `EntityBase.Id` como chave primária de todas as entidades de domínio, gerada via `Guid.NewGuid()` (UUID v4 random) desde o scaffold inicial. A inconsistência foi detectada durante a review da [story #288](https://github.com/unifesspa-edu-br/uniplus-api/pull/310) (cursor pagination): o resto do projeto **já** usa `Guid.CreateVersion7()` em outros pontos (`Instance` e `traceId` de ProblemDetails em `ResultExtensions`, `GlobalExceptionMiddleware`, `VendorMediaTypeAttribute`), e a [ADR-0026](0026-paginacao-cursor-opaco-cifrado.md) (cursor pagination keyset-based) ganharia semântica temporal natural se os IDs fossem ordenáveis.

Adicionalmente, o ecossistema convergiu: **Npgsql 9 / EFCore.PG 10 já adota UUID v7** como default para `GuidValueGenerator`, **PostgreSQL 18** introduziu a função nativa `uuidv7()`, e **.NET 9+** inclui `Guid.CreateVersion7()` na BCL. A escolha agora não é "v4 vs v7 emergente", é "v4 legado vs v7 padrão da plataforma".

A questão a decidir é: **adotamos v7 universalmente, ou mantemos v4 em entidades com PII (Candidato, Inscricao) por considerar o timestamp leak relevante para LGPD?**

## Drivers da decisão

- **Coerência interna.** Mistura v4/v7 no mesmo codebase é dívida técnica; padrão único reduz carga cognitiva e elimina necessidade de ArchUnit guard duplo.
- **Cursor pagination ([ADR-0026](0026-paginacao-cursor-opaco-cifrado.md)).** Com v4, `OrderBy(e => e.Id)` em PG ordena aleatoriamente; "próxima página" não tem significado intuitivo. Com v7, "próxima página" = "criados depois" — UX natural sem código adicional.
- **Performance de I/O em PostgreSQL.** UUID v4 random insere em posições aleatórias do índice B-tree → page splits frequentes, bloat acumulado, throughput degradado em tabelas de alto volume (Inscricao com 50k+ rows por edital × dezenas de editais; outbox; audit; Idempotency cache).
- **Debugging operacional.** IDs ordenáveis facilitam rastreio temporal em logs (Loki/Tempo) e correlação cross-service.
- **LGPD.** v7 expõe os primeiros 48 bits como `unix_ts_ms` da criação. Necessário avaliar se isso vaza informação que não estava já disponível.

## Opções consideradas

- **A. v7 universal** — todas entidades usam `Guid.CreateVersion7()`, sem exceção.
- **B. v7 + override v4 em entidades sensíveis** — Candidato, Inscricao, e futuras entidades com PII alta sobrescrevem o construtor base com `Guid.NewGuid()`.
- **C. ID dual (secret + público)** — entidades sensíveis carregam dois IDs: PK opaco (v4) para uso interno + slug público (HashId/SipHash) para URLs externas.
- **D. Manter status quo (v4 universal)** — descartado de antemão por não capturar nenhum benefício do padrão emergente da plataforma.

## Resultado da decisão

**Escolhida:** "A — v7 universal", porque o argumento a favor de exceções para entidades sensíveis (opção B) não se sustenta sob análise:

**O timestamp leak do v7 não cria risco LGPD novo.** Toda entidade já carrega `CreatedAt` (campo de `EntityBase`, audit trail obrigatório por padrão do projeto) — coluna `timestamptz` adjacente que expõe a mesma informação com a mesma precisão (ms). Quem tem acesso ao registro tem ambos. O leak marginal de opacidade ao manter v4 só ajudaria contra um adversário que **já enumera IDs sem acesso ao registro** — cenário que o threat model do projeto rejeita: autorização é Keycloak/JWT + ABAC; IDs nunca foram tokens de autorização.

**v7 mantém entropia suficiente contra brute-force.** RFC 9562 exige mínimo de 32 bits aleatórios; v7 entrega 74 bits (rand_a 12 + rand_b 62), muito acima da margem necessária para resistir adivinhação. v4 tem 122 bits, mas o ganho marginal não tem aplicação no nosso modelo.

**Benefícios de localidade B-tree são reais e mensurados.** Benchmarks independentes em PG 15-18 mostram ~25% redução de footprint do índice e até ~49% mais throughput de insert em volumes não-triviais. Importa especificamente em Inscricao (pico em janelas de candidatura), outbox transacional ([ADR-0004](0004-outbox-transacional-via-wolverine.md)), e Idempotency cache ([ADR-0027](0027-idempotency-key-store-postgresql.md)).

**Tradução EF Core 10 / Npgsql preserva-se intacta.** Npgsql escreve Guid em formato big-endian RFC 9562 no wire protocol (diferente do byte-mangling do `Microsoft.Data.SqlClient`); `OrderBy(e => e.Id)` continua traduzindo para `ORDER BY id` byte-by-byte coerente com `Guid.CompareTo > 0`. A crítica de performance circulada para SQL Server (gist sdrapkin) **não se aplica** ao stack PostgreSQL/Npgsql — daí o time do Npgsql ter adotado v7 como default em sua versão 9.0.

A opção C (ID dual) foi descartada por complexidade desproporcional ao ganho — padrão do Stripe (`cus_…` opaco) faz sentido quando o ID é o token de autorização, que não é nosso caso. Caso surja necessidade futura de slug opaco em URL pública específica (ex.: comprovante de inscrição compartilhável), pode ser introduzido como extensão localizada (campo adicional na entidade) sem alterar o PK.

### Esta ADR não decide

- **Migração de IDs v4 já existentes em produção** — não há produção; mistura v4/v7 em DBs de dev/staging é segura (Postgres uuid order é byte-by-byte canônica; v4 ficam intercalados aleatoriamente entre os v7 cronológicos sem perda de integridade).
- **Slug opaco para URL pública** — quando surgir endpoint que exponha entidade sensível em link compartilhável, criar slug separado (HashId) sem trocar o PK. Issue dedicada na época.
- **Sub-millisecond monotonicity** — `Guid.CreateVersion7()` não garante ordering estrito sub-ms (rand_a/rand_b re-randomizam a cada chamada, RFC-compliant). Aceitável: nossas cargas não geram >10k inserts/ms no mesmo nó. Reavaliar se aparecer hot path crítico.

## Consequências

### Positivas

- **Cursor pagination ganha ordering temporal sem código adicional** — clientes que paginam `/api/editais` ou futuros `/api/inscricoes` veem items em ordem cronológica natural.
- **Performance de inserts** em tabelas de alto volume — Inscricao, outbox, Idempotency cache, audit. Reduz manutenção (REINDEX, VACUUM aggressive) e latência percebida em picos de candidatura.
- **Coerência interna** — um único padrão de geração de Id em todo o codebase; `Guid.NewGuid()` agora é proibido em código de domínio (fitness test ArchUnit).
- **Debugging facilitado** — IDs ordenáveis visualmente em logs e traces (correlação Loki/Tempo).

### Negativas

- **Timestamp leak documentado** — IDs revelam aproximadamente o instante de criação (≤1 ms). Documentar explicitamente no Aviso de Privacidade do projeto e no DPIA correspondente. Mitigação: já feito acima (informação já disponível via `CreatedAt`).
- **Sub-ms monotonicity ausente** — não usar Id como ordenador secundário em algoritmos que exigem ordering estrito sub-ms (não há tal caso hoje).
- **Predictability ligeiramente reduzida** — 74 bits aleatórios em v7 vs 122 bits em v4. Sem impacto prático para nosso threat model (ver "Resultado da decisão"), mas vale documentar para futuras revisões caso o modelo mude.

### Neutras

- **Mistura v4/v7 em DBs existentes** — durante a transição (após merge desta ADR), entidades já persistidas mantêm IDs v4; novas usam v7. Ordering é estável (Postgres compara byte-by-byte canônico). Sem migração necessária.

## Confirmação

1. **Fitness test solution-wide** — `DominioNaoUsaGuidNewGuidTests` em `tests/Unifesspa.UniPlus.ArchTests/SolutionRules/` varre por regex os arquivos `.cs` de `Kernel`, `Selecao.Domain` e `Ingresso.Domain` falhando o build se encontrar `Guid.NewGuid()`. Permitido em `*.Tests.*` (seeds) e em código fora de domínio (ex.: `Instance` de ProblemDetails em Infrastructure já usa `Guid.CreateVersion7()`, sem regressão). Vive no projeto centralizado `Unifesspa.UniPlus.ArchTests` em vez de duplicado entre os Selecao/Ingresso ArchTests porque a regra cobre o `Kernel` compartilhado.
2. **Teste regressivo de cursor pagination** — cenário com IDs mistos (v4 antigos manualmente inseridos + v7 novos via factory) navegando cursor paginação; afirmar zero duplicação e zero omissão.
3. **Code review checklist** — revisor confirma que entidades novas usam `Guid.CreateVersion7()` (ou herdam de `EntityBase` que faz isso). PR template lembra dessa verificação na próxima atualização.

## Mais informações

- [ADR-0026](0026-paginacao-cursor-opaco-cifrado.md) — beneficiária direta da ordenação temporal.
- [ADR-0031](0031-decoding-de-cursor-opaco-no-boundary-http.md) — discute Guid ordering em Npgsql; agora consistente com decisão deste ADR.
- [Guid.CreateVersion7 — Microsoft Learn](https://learn.microsoft.com/en-us/dotnet/api/system.guid.createversion7) — API oficial.
- [RFC 9562 — Universally Unique IDentifiers (UUIDs)](https://datatracker.ietf.org/doc/rfc9562/) — fonte canônica; §5.7 (UUIDv7), §6.2 (timestamp privacy), §8 (security).
- [PostgreSQL 18 Datatype UUID](https://www.postgresql.org/docs/current/datatype-uuid.html) — `uuidv7()` nativo.
- [Npgsql EFCore 9.0 Release Notes](https://www.npgsql.org/efcore/release-notes/9.0.html) — adoção de UUID v7 como default.
- [Npgsql EFCore 10.0 Release Notes](https://www.npgsql.org/efcore/release-notes/10.0.html) — tradução de `Guid.CreateVersion7()` para `uuidv7()` em PG18.
- [Cybertec — Unexpected downsides of UUID keys in PostgreSQL](https://www.cybertec-postgresql.com/en/unexpected-downsides-of-uuid-keys-in-postgresql/) — análise técnica de bloat e mitigação por v7.
- [pg-uuidv7-benchmark (mblum.me)](https://mblum.me/posts/pg-uuidv7-benchmark/) — benchmark reproduzível: ~49% mais throughput insert.
- [Marc Brooker — Fixing UUIDv7 (for database use-cases)](https://brooker.co.za/blog/2025/10/22/uuidv7.html) — perspectiva contrária registrada (correlated leak em sistemas multi-tenant; não aplicável ao nosso caso single-tenant).
- Issue #311 (uniplus-api) — issue de implementação que carrega esta ADR.
