using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RemoveProducaoEDefinitividadeDaFaseCanonica : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_fase_canonica_resultado_definitivo",
                schema: "configuracao",
                table: "fase_canonica");

            migrationBuilder.DropColumn(
                name: "produz_resultado",
                schema: "configuracao",
                table: "fase_canonica");

            migrationBuilder.DropColumn(
                name: "resultado_definitivo",
                schema: "configuracao",
                table: "fase_canonica");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Restaura a forma das colunas, não os valores que o seed carregava: o que uma
            // fase publica passou a ser declarado na fase do cronograma do processo, e
            // reescrever esses booleanos aqui recriaria a segunda fonte de verdade que a
            // remoção elimina.
            migrationBuilder.AddColumn<bool>(
                name: "produz_resultado",
                schema: "configuracao",
                table: "fase_canonica",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "resultado_definitivo",
                schema: "configuracao",
                table: "fase_canonica",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddCheckConstraint(
                name: "ck_fase_canonica_resultado_definitivo",
                schema: "configuracao",
                table: "fase_canonica",
                sql: "resultado_definitivo = false OR produz_resultado = true");
        }
    }
}
