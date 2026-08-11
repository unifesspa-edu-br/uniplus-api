using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AdicionaBuscaTrigramTermoConsentimento : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:PostgresExtension:pg_trgm", ",,");

            // Índice de expressão (não representável via HasIndex fluente do EF
            // Core) para acelerar a busca por proximidade (issue #1105):
            // lower(nome) casa com EF.Functions.TrigramsAreWordSimilar(termo,
            // nome.ToLower()) no repositório, mantendo a busca caixa-insensível.
            // lower() é IMMUTABLE nativamente — sem wrapper (diferente de
            // immutable_unaccent em Organização Institucional). Parcial por
            // is_deleted, espelhando ix_termo_consentimento_nome.
            migrationBuilder.Sql(
                """
                CREATE INDEX IF NOT EXISTS ix_termo_consentimento_nome_trgm
                  ON configuracao.termo_consentimento USING GIN (lower(nome) gin_trgm_ops)
                  WHERE is_deleted = false;
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
