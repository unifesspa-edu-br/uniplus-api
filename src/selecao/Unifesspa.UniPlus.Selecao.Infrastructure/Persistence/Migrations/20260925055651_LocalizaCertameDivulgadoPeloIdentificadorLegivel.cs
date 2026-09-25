using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class LocalizaCertameDivulgadoPeloIdentificadorLegivel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // A projeção pública muda de forma (versão 2, com o identificador legível), e as linhas
            // da versão anterior não têm de onde tirá-lo: foram projetadas de envelopes congelados
            // antes de o identificador existir. Não há reprojeção a partir deles, e o sistema não
            // está em produção — as linhas saem, e o certame volta à leitura pública quando uma
            // versão nova, com identificador, for divulgada.
            migrationBuilder.Sql(
                "DELETE FROM selecao.certames_divulgados WHERE versao_projecao <> '2';");

            // Sem valor padrão: a tabela está vazia neste ponto, e um padrão vazio deixaria a coluna
            // aceitar em silêncio uma divulgação sem endereço público.
            migrationBuilder.AddColumn<string>(
                name: "identificador_legivel",
                schema: "selecao",
                table: "certames_divulgados",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false);

            migrationBuilder.CreateIndex(
                name: "ux_certames_divulgados_identificador_legivel",
                schema: "selecao",
                table: "certames_divulgados",
                column: "identificador_legivel",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ux_certames_divulgados_identificador_legivel",
                schema: "selecao",
                table: "certames_divulgados");

            migrationBuilder.DropColumn(
                name: "identificador_legivel",
                schema: "selecao",
                table: "certames_divulgados");
        }
    }
}
