using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AdicionaCodigoRecursoAcessibilidade : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // O cadastro de recursos de acessibilidade não tem seed: nasce vazio e é
            // alimentado pelo CEPS. O código passa a ser obrigatório e é informado pelo
            // operador; derivá-lo do nome inventaria a identidade semântica que o
            // candidato escolhe e que a oferta congela. As linhas preexistentes saem e
            // os ambientes recadastram os recursos. O DELETE alcança também as linhas
            // soft-deleted: a coluna NOT NULL vale para a tabela inteira.
            migrationBuilder.Sql("DELETE FROM configuracao.recurso_acessibilidade;");

            // Sem defaultValue: uma string vazia violaria o CHECK de formato logo abaixo
            // e deixaria um DEFAULT permanente no schema. O DELETE acima garante que não
            // há linha para preencher.
            migrationBuilder.AddColumn<string>(
                name: "codigo",
                schema: "configuracao",
                table: "recurso_acessibilidade",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false);

            migrationBuilder.CreateIndex(
                name: "ix_recurso_acessibilidade_codigo_vivo",
                schema: "configuracao",
                table: "recurso_acessibilidade",
                column: "codigo",
                unique: true,
                filter: "is_deleted = false");

            migrationBuilder.AddCheckConstraint(
                name: "ck_recurso_acessibilidade_codigo_formato",
                schema: "configuracao",
                table: "recurso_acessibilidade",
                sql: "codigo ~ '^[A-Z][A-Z0-9_]{1,49}$'");
        }

        /// <inheritdoc />
        /// <remarks>
        /// A reversão devolve o schema ao estado anterior, mas não restaura as linhas
        /// removidas no <c>Up</c>.
        /// </remarks>
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_recurso_acessibilidade_codigo_vivo",
                schema: "configuracao",
                table: "recurso_acessibilidade");

            migrationBuilder.DropCheckConstraint(
                name: "ck_recurso_acessibilidade_codigo_formato",
                schema: "configuracao",
                table: "recurso_acessibilidade");

            migrationBuilder.DropColumn(
                name: "codigo",
                schema: "configuracao",
                table: "recurso_acessibilidade");
        }
    }
}
