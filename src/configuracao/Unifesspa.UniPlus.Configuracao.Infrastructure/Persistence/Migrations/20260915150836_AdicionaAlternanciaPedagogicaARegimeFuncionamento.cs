using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Atualiza a restrição de <c>regime_de_funcionamento</c> da tabela
    /// <c>oferta_curso</c> para permitir o valor <c>ALTERNANCIA_PEDAGOGICA</c>.
    /// </summary>
    public partial class AdicionaAlternanciaPedagogicaARegimeFuncionamento : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_oferta_curso_regime_de_funcionamento",
                schema: "configuracao",
                table: "oferta_curso");

            migrationBuilder.AddCheckConstraint(
                name: "ck_oferta_curso_regime_de_funcionamento",
                schema: "configuracao",
                table: "oferta_curso",
                sql: "regime_de_funcionamento IN ('INTENSIVO', 'EXTENSIVO', 'ALTERNANCIA_PEDAGOGICA')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_oferta_curso_regime_de_funcionamento",
                schema: "configuracao",
                table: "oferta_curso");

            migrationBuilder.AddCheckConstraint(
                name: "ck_oferta_curso_regime_de_funcionamento",
                schema: "configuracao",
                table: "oferta_curso",
                sql: "regime_de_funcionamento IN ('INTENSIVO', 'EXTENSIVO')");
        }
    }
}
