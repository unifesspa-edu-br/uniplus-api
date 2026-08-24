using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AdicionaCodigoTipoDeficiencia : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // O cadastro de tipos de deficiência não tem seed: nasce vazio e é
            // alimentado pelo CEPS. Como o código passa a ser obrigatório e é
            // informado pelo operador, derivar um valor a partir do nome inventaria
            // identidade semântica — justamente o que UNI-REQ-0061 exige que seja
            // declarado. Em vez disso, as linhas preexistentes são removidas e os
            // ambientes recadastram os tipos. O DELETE alcança também as linhas
            // soft-deleted: elas não têm código e o índice único parcial as ignora,
            // mas a coluna NOT NULL vale para a tabela inteira.
            migrationBuilder.Sql("DELETE FROM configuracao.tipo_deficiencia;");

            // Sem defaultValue: uma string vazia violaria o CHECK de formato logo
            // abaixo e deixaria um DEFAULT permanente no schema. O DELETE acima
            // garante que não há linha para preencher.
            migrationBuilder.AddColumn<string>(
                name: "codigo",
                schema: "configuracao",
                table: "tipo_deficiencia",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false);

            migrationBuilder.CreateIndex(
                name: "ix_tipo_deficiencia_codigo_vivo",
                schema: "configuracao",
                table: "tipo_deficiencia",
                column: "codigo",
                unique: true,
                filter: "is_deleted = false");

            migrationBuilder.AddCheckConstraint(
                name: "ck_tipo_deficiencia_codigo_formato",
                schema: "configuracao",
                table: "tipo_deficiencia",
                sql: "codigo ~ '^[A-Z][A-Z0-9_]{1,49}$'");
        }

        /// <inheritdoc />
        /// <remarks>
        /// A reversão devolve o schema ao estado anterior, mas não restaura as
        /// linhas removidas no <c>Up</c> — o cadastro volta vazio.
        /// </remarks>
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_tipo_deficiencia_codigo_vivo",
                schema: "configuracao",
                table: "tipo_deficiencia");

            migrationBuilder.DropCheckConstraint(
                name: "ck_tipo_deficiencia_codigo_formato",
                schema: "configuracao",
                table: "tipo_deficiencia");

            migrationBuilder.DropColumn(
                name: "codigo",
                schema: "configuracao",
                table: "tipo_deficiencia");
        }
    }
}
