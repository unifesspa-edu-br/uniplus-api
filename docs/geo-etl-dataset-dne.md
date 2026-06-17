# ETL do Geo — ingestão do dataset DNE

Guia operacional da carga de reference data do módulo `Geo` (localidades, endereçamento e georreferência nacional) a partir do dataset **DNE Correios + IBGE**. Decisões em [ADR-0090](adrs/0090-modulo-geo-localidades.md), [ADR-0091](adrs/0091-postgis-georreferencia-nts.md) e [ADR-0092](adrs/0092-etl-carga-dne-reference-data.md).

## Estratégia: schema de staging + SELECT streamado

O dataset DNE é distribuído como **15 dumps SQL** (um por tabela, `tbl_cep_{versao}_n_*`, todas as colunas `varchar`; a maior — `logradouro` — tem ~1,4M linhas / ~382 MB). Esses dumps **não são versionados** neste repositório: a base DNE é proprietária (Correios) e exige licença institucional.

A ingestão segue dois passos desacoplados:

1. **Provisionamento (fora do ciclo da API):** os dumps são restaurados num **schema de staging** (`dne_staging`) do banco `uniplus_geo`, via `psql`. Os dumps são auto-suficientes (`DROP TABLE IF EXISTS` + `CREATE TABLE` + `INSERT`), então re-restaurar é limpo e idempotente.
2. **Carga (serviço de `Geo.Infrastructure`):** o ETL lê o staging por `SELECT` **streamado** (`DbDataReader` — leitura linha-a-linha, uso de memória estável mesmo nas tabelas grandes), aplica **parse tolerante** (`'-'`/vazio → `null`, `'S'`/`'N'` → `bool`, lat/long → `geography(Point,4326)`), resolve as FKs e faz **upsert por chave natural** no schema do domínio (`public`).

```
dumps .sql  ──psql -f──▶  dne_staging (15 tabelas varchar)
                              │  SELECT streamado (DneStagingFonte)
                              ▼
        parse tolerante ─▶ upsert por chave natural ─▶ public.* (domínio Uni+)
```

### Por que staging-DB (e não parsear os .sql em C#)

- **Streaming nativo:** o `DbDataReader` do Npgsql resolve os ~382 MB de logradouro sem carregar o arquivo em memória — sem reimplementar um parser de `INSERT` textual (frágil com escaping/acentos/`NULL`).
- **Resolução de FK:** as FKs internas da DNE (`id_cidade`, `distrito_id`, `bairro_id`, inteiros **instáveis entre releases**) ficam disponíveis para JOIN no staging. O domínio nunca usa esses inteiros como identidade — usa Guid v7 ([ADR-0032](adrs/0032-guid-v7-como-identidade.md)) + chave natural.
- **Recarga idempotente:** ver abaixo.

## Restauração do staging (dev)

```bash
# 1. Descompactar os dumps (ex.: tbl_cep_202601_postgresql.zip) num diretório.
# 2. Criar o schema de staging e restaurar cada dump:
psql "$GEO_CONN" -c 'DROP SCHEMA IF EXISTS dne_staging CASCADE; CREATE SCHEMA dne_staging;'
for f in tbl_cep_202601_n_*.sql; do
  # os dumps criam tabelas em "public"; restaure-os sob o schema dne_staging
  psql "$GEO_CONN" -c "SET search_path TO dne_staging;" -f "$f"
done
```

> Ajuste o `search_path`/schema conforme o gerador do dump. O importador espera as tabelas `tbl_cep_{versao}_n_*` no schema configurado (`dne_staging` por padrão).

## Recarga periódica (releases mensais)

A DNE é atualizada mensalmente. Para aplicar uma nova release (ex.: `202602`):

1. Restaurar o novo dump no `dne_staging` (substitui o anterior).
2. Disparar a importação informando a versão `202602`.
3. O ETL faz **upsert por chave natural** — `sigla_iso`/`uf`/`codigo_ibge` são estáveis entre releases: insere o novo, atualiza o que mudou, grava `versao_dataset=202602` em cada linha tocada. Reaplicar a **mesma** versão é no-op (idempotente).

A política de *stale* (marcar `vigente=false` as chaves ausentes na nova release) e o gatilho operacional (seed em dev + endpoint admin `POST /api/admin/geo/importacoes`) são entregues na Story de atualização periódica do Epic.

## Parse tolerante (anti-frágil)

A fonte é toda `varchar` e traz `'-'`/vazio para dado ausente (≈27% dos municípios em `mortalidade_infantil`). Toda métrica externa é `nullable` e degrada para `null` sem abortar a carga — só chave natural e `nome` são obrigatórios. Cada degradação é registrada (sem PII — métricas IBGE públicas) num **relatório de validação** com contadores por tabela (lidos/inseridos/atualizados/órfãos/duplicados/degradados) e amostras para auditoria. A carga só aborta por falha de infraestrutura (conexão/transação), com **rollback total** (transação única).
