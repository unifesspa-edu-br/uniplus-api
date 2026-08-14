using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class LocalidadeRegenteNoProcessoSeletivo : Migration
    {
        /// <inheritdoc />
        // O EF exige valor de preenchimento ao adicionar coluna NOT NULL, e o DEFAULT que ele
        // gera permanece na coluna depois do ALTER TABLE. Por isso o valor é a sede da
        // instituição (Marabá/PA), que é localidade de domínio válida, e não string vazia:
        // uma linha inserida por SQL cru sem citar a coluna fica com um município real em vez
        // de um sentinela que o domínio recusaria. Isto é preenchimento de schema, não dedução
        // em tempo de requisição — o servidor continua recusando pedido sem localidade.
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "localidade_codigo_ibge",
                schema: "selecao",
                table: "processos_seletivos",
                type: "character(7)",
                fixedLength: true,
                maxLength: 7,
                nullable: false,
                defaultValue: "1504208",
                comment: "Código IBGE do município cujo calendário rege a contagem dos prazos — o único valor normativo da localidade.");

            migrationBuilder.AddColumn<string>(
                name: "localidade_nome",
                schema: "selecao",
                table: "processos_seletivos",
                type: "character varying(150)",
                maxLength: 150,
                nullable: false,
                defaultValue: "Marabá",
                comment: "Nome do município da localidade regente — cache de exibição, não entra em cálculo de prazo.");

            migrationBuilder.AddColumn<string>(
                name: "localidade_uf",
                schema: "selecao",
                table: "processos_seletivos",
                type: "character(2)",
                fixedLength: true,
                maxLength: 2,
                nullable: false,
                defaultValue: "PA",
                comment: "UF da localidade regente — cache de exibição; a UF que vale é a derivada do prefixo do código.");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "localidade_codigo_ibge",
                schema: "selecao",
                table: "processos_seletivos");

            migrationBuilder.DropColumn(
                name: "localidade_nome",
                schema: "selecao",
                table: "processos_seletivos");

            migrationBuilder.DropColumn(
                name: "localidade_uf",
                schema: "selecao",
                table: "processos_seletivos");
        }
    }
}
