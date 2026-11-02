using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class GravaONomeDoMunicipioConferidoNaResposta : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_respostas_da_inscricao_valor_do_tipo",
                schema: "selecao",
                table: "respostas_da_inscricao");

            migrationBuilder.AddColumn<string>(
                name: "valor_rotulo",
                schema: "selecao",
                table: "respostas_da_inscricao",
                type: "text",
                nullable: true,
                comment: "O nome que o Geo conferiu para o município de valor_codigo; nulo no rascunho e preenchido na inscrição enviada.");

            migrationBuilder.AddCheckConstraint(
                name: "ck_respostas_da_inscricao_valor_do_tipo",
                schema: "selecao",
                table: "respostas_da_inscricao",
                sql: "num_nonnulls(valor_booleano, valor_numero, valor_codigo, valor_texto, valor_data, valor_cifrado) = 1 AND tipo IN (1, 2, 3, 4, 5, 7) AND (valor_cifrado IS NOT NULL OR (tipo = 1 AND valor_booleano IS NOT NULL) OR (tipo = 2 AND valor_numero IS NOT NULL) OR (tipo = 3 AND valor_codigo IS NOT NULL) OR (tipo = 4 AND valor_texto IS NOT NULL) OR (tipo = 5 AND valor_data IS NOT NULL) OR (tipo = 7 AND valor_codigo IS NOT NULL)) AND (valor_rotulo IS NULL OR (tipo = 7 AND valor_codigo IS NOT NULL))");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_respostas_da_inscricao_valor_do_tipo",
                schema: "selecao",
                table: "respostas_da_inscricao");

            migrationBuilder.DropColumn(
                name: "valor_rotulo",
                schema: "selecao",
                table: "respostas_da_inscricao");

            migrationBuilder.AddCheckConstraint(
                name: "ck_respostas_da_inscricao_valor_do_tipo",
                schema: "selecao",
                table: "respostas_da_inscricao",
                sql: "num_nonnulls(valor_booleano, valor_numero, valor_codigo, valor_texto, valor_data, valor_cifrado) = 1 AND tipo BETWEEN 1 AND 5 AND (valor_cifrado IS NOT NULL OR (tipo = 1 AND valor_booleano IS NOT NULL) OR (tipo = 2 AND valor_numero IS NOT NULL) OR (tipo = 3 AND valor_codigo IS NOT NULL) OR (tipo = 4 AND valor_texto IS NOT NULL) OR (tipo = 5 AND valor_data IS NOT NULL))");
        }
    }
}
