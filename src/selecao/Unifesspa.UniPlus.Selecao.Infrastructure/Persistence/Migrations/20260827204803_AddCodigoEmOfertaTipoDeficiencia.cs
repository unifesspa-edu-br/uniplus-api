using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCodigoEmOfertaTipoDeficiencia : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_ofertas_tipo_deficiencia_oferta_atendimento_especializado_i",
                schema: "selecao",
                table: "ofertas_tipo_deficiencia");

            migrationBuilder.Sql("""
                                 DELETE FROM selecao.ofertas_tipo_deficiencia;
                                 """);

            migrationBuilder.AddColumn<string>(
                name: "tipo_deficiencia_codigo",
                schema: "selecao",
                table: "ofertas_tipo_deficiencia",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false);

            migrationBuilder.CreateIndex(
                name: "ix_ofertas_tipo_deficiencia_oferta_atendimento_especializado_i",
                schema: "selecao",
                table: "ofertas_tipo_deficiencia",
                columns: new[]
                {
                    "oferta_atendimento_especializado_id",
                    "tipo_deficiencia_codigo"
                },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_ofertas_tipo_deficiencia_oferta_atendimento_especializado_i",
                schema: "selecao",
                table: "ofertas_tipo_deficiencia");

            migrationBuilder.DropColumn(
                name: "tipo_deficiencia_codigo",
                schema: "selecao",
                table: "ofertas_tipo_deficiencia");

            migrationBuilder.CreateIndex(
                name: "ix_ofertas_tipo_deficiencia_oferta_atendimento_especializado_i",
                schema: "selecao",
                table: "ofertas_tipo_deficiencia",
                columns: new[] { "oferta_atendimento_especializado_id", "tipo_deficiencia_origem_id" },
                unique: true);
        }
    }
}
