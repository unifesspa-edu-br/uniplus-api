using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Unifesspa.UniPlus.OrganizacaoInstitucional.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSearchExtensionsGin : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Extensões de busca — idempotentes; init-db.sql já as cria no boot
            // do container, mas a migration garante que ambientes standalone
            // (testes de integração via Testcontainers, HML, PROD) as recebam
            // sem dependência do script de inicialização.
            migrationBuilder.Sql("CREATE EXTENSION IF NOT EXISTS unaccent;");
            migrationBuilder.Sql("CREATE EXTENSION IF NOT EXISTS pg_trgm;");

            // immutable_unaccent: wrapper IMMUTABLE em torno de unaccent(), necessário
            // para criar índices de expressão sobre unaccent(campo). A função pública
            // unaccent(text) é STABLE no PostgreSQL (conservadoramente — ela só depende
            // do dicionário estático), o que impede uso em expressões de índice. O
            // wrapper chama a implementação C diretamente com o dicionário explícito,
            // que É IMMUTABLE: mesmo input → mesmo output para qualquer transação.
            migrationBuilder.Sql(
                """
                CREATE OR REPLACE FUNCTION immutable_unaccent(text)
                  RETURNS text
                  LANGUAGE sql
                  IMMUTABLE PARALLEL SAFE STRICT AS
                $$ SELECT public.unaccent('public.unaccent', $1) $$;
                """);

            // Índices GIN trigram por campo pesquisável da Unidade.
            // Cobrem os predicados `immutable_unaccent(campo) ILIKE '%termo%'`
            // gerados pelo repositório — o planejador usa o índice para varredura
            // trigrama antes do recheck da condição ILIKE (pg_trgm + GIN, ADR-0055).
            migrationBuilder.Sql(
                """
                CREATE INDEX IF NOT EXISTS idx_unidade_nome_trgm
                  ON unidade USING GIN (immutable_unaccent(nome) gin_trgm_ops);

                CREATE INDEX IF NOT EXISTS idx_unidade_sigla_trgm
                  ON unidade USING GIN (immutable_unaccent(sigla) gin_trgm_ops);

                CREATE INDEX IF NOT EXISTS idx_unidade_codigo_trgm
                  ON unidade USING GIN (immutable_unaccent(codigo) gin_trgm_ops);

                CREATE INDEX IF NOT EXISTS idx_unidade_slug_trgm
                  ON unidade USING GIN (immutable_unaccent(slug) gin_trgm_ops);

                CREATE INDEX IF NOT EXISTS idx_unidade_alias_trgm
                  ON unidade USING GIN (immutable_unaccent(alias) gin_trgm_ops);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Forward-only per ADR-0054 §J: extensões e funções compartilhadas
            // não são dropeadas via migration para evitar quebrar outros objetos
            // que possam depender delas. Os índices GIN também permanecem — o
            // mecanismo canônico de revert é uma nova migration de compensação.
            throw new System.NotSupportedException("Forward-only migration per ADR-0054 §J.");
        }
    }
}
