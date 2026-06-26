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
            // Extensões de busca — idempotentes; init-db.sql já as cria no boot,
            // mas a migration garante ambientes standalone (Testcontainers, HML,
            // PROD). Extensões são globais por banco (não por schema).
            migrationBuilder.Sql("CREATE EXTENSION IF NOT EXISTS unaccent;");
            migrationBuilder.Sql("CREATE EXTENSION IF NOT EXISTS pg_trgm;");

            // immutable_unaccent: wrapper IMMUTABLE de unaccent(), necessário para
            // índices de expressão. Vive em `public` (utilitário global, fora do
            // schema do módulo) — o mapeamento HasDbFunction fixa HasSchema("public").
            migrationBuilder.Sql(
                """
                CREATE OR REPLACE FUNCTION immutable_unaccent(text)
                  RETURNS text
                  LANGUAGE sql
                  IMMUTABLE PARALLEL SAFE STRICT AS
                $$ SELECT public.unaccent('public.unaccent', $1) $$;
                """);

            // Índices GIN trigram por campo pesquisável da Unidade — qualificados ao
            // schema do módulo (banco único, spike monólito modular).
            migrationBuilder.Sql(
                """
                CREATE INDEX IF NOT EXISTS idx_unidade_nome_trgm
                  ON organizacao.unidade USING GIN (immutable_unaccent(nome) gin_trgm_ops);

                CREATE INDEX IF NOT EXISTS idx_unidade_sigla_trgm
                  ON organizacao.unidade USING GIN (immutable_unaccent(sigla) gin_trgm_ops);

                CREATE INDEX IF NOT EXISTS idx_unidade_codigo_trgm
                  ON organizacao.unidade USING GIN (immutable_unaccent(codigo) gin_trgm_ops);

                CREATE INDEX IF NOT EXISTS idx_unidade_slug_trgm
                  ON organizacao.unidade USING GIN (immutable_unaccent(slug) gin_trgm_ops);

                CREATE INDEX IF NOT EXISTS idx_unidade_alias_trgm
                  ON organizacao.unidade USING GIN (immutable_unaccent(alias) gin_trgm_ops);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Forward-only per ADR-0054 §J.
            throw new System.NotSupportedException("Forward-only migration per ADR-0054 §J.");
        }
    }
}
