using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AmpliaOsDocumentosDivulgadosAoAtoSemVersao : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "editais_divulgados",
                schema: "selecao",
                table: "certames_divulgados",
                type: "jsonb",
                nullable: false,
                defaultValueSql: "'[]'::jsonb",
                comment: "Os documentos de cada ato divulgado do processo — o edital de cada versão e o ato que não cria versão —, com o ato e o endereço no acervo público.",
                oldClrType: typeof(string),
                oldType: "jsonb",
                oldDefaultValueSql: "'[]'::jsonb",
                oldComment: "O edital de cada versão divulgada, com o ato que a criou e o endereço no acervo público.");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "editais_divulgados",
                schema: "selecao",
                table: "certames_divulgados",
                type: "jsonb",
                nullable: false,
                defaultValueSql: "'[]'::jsonb",
                comment: "O edital de cada versão divulgada, com o ato que a criou e o endereço no acervo público.",
                oldClrType: typeof(string),
                oldType: "jsonb",
                oldDefaultValueSql: "'[]'::jsonb",
                oldComment: "Os documentos de cada ato divulgado do processo — o edital de cada versão e o ato que não cria versão —, com o ato e o endereço no acervo público.");
        }
    }
}
