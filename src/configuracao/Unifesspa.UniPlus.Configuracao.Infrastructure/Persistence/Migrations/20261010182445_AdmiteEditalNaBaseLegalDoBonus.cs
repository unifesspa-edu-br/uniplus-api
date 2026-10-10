using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AdmiteEditalNaBaseLegalDoBonus : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_base_legal_bonus_regional_tipo_instrumento",
                schema: "configuracao",
                table: "base_legal_bonus_regional");

            migrationBuilder.AddCheckConstraint(
                name: "CK_base_legal_bonus_regional_tipo_instrumento",
                schema: "configuracao",
                table: "base_legal_bonus_regional",
                sql: "tipo_instrumento IN ('LEI','DECRETO','PORTARIA','RESOLUCAO','INSTRUCAO_NORMATIVA','PARECER','EDITAL')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_base_legal_bonus_regional_tipo_instrumento",
                schema: "configuracao",
                table: "base_legal_bonus_regional");

            migrationBuilder.AddCheckConstraint(
                name: "CK_base_legal_bonus_regional_tipo_instrumento",
                schema: "configuracao",
                table: "base_legal_bonus_regional",
                sql: "tipo_instrumento IN ('LEI','DECRETO','PORTARIA','RESOLUCAO','INSTRUCAO_NORMATIVA','PARECER')");
        }
    }
}
