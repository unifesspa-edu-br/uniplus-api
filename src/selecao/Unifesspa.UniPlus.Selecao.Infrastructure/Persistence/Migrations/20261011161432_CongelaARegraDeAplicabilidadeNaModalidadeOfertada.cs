using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class CongelaARegraDeAplicabilidadeNaModalidadeOfertada : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "criterios_cumulativos",
                schema: "selecao",
                table: "modalidades_selecionadas");

            // A linha já gravada recebe a regra sem cláusula, que é a da modalidade incondicional; a
            // cópia certa do cadastro só vem quando a distribuição de vagas é gravada de novo. O
            // default sai em seguida, porque toda gravação nova traz a regra.
            migrationBuilder.AddColumn<string>(
                name: "regra_de_aplicabilidade",
                schema: "selecao",
                table: "modalidades_selecionadas",
                type: "jsonb",
                nullable: false,
                defaultValueSql: "'[]'::jsonb");

            migrationBuilder.AlterColumn<string>(
                name: "regra_de_aplicabilidade",
                schema: "selecao",
                table: "modalidades_selecionadas",
                type: "jsonb",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "jsonb",
                oldDefaultValueSql: "'[]'::jsonb");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "regra_de_aplicabilidade",
                schema: "selecao",
                table: "modalidades_selecionadas");

            migrationBuilder.AddColumn<string>(
                name: "criterios_cumulativos",
                schema: "selecao",
                table: "modalidades_selecionadas",
                type: "jsonb",
                nullable: false,
                defaultValueSql: "'[]'::jsonb");
        }
    }
}
