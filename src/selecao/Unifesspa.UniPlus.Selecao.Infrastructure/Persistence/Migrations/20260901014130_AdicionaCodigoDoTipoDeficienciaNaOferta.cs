using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AdicionaCodigoDoTipoDeficienciaNaOferta : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // O código não é derivável do que já está gravado: o snapshot guardava só o
            // identificador de origem e o nome, e nome não é código. Preencher as linhas
            // existentes com string vazia — o default que o EF geraria sozinho — deixaria
            // um snapshot que nenhuma regra jamais casaria, e que a própria factory do
            // agregado recusaria ao reidratar.
            //
            // As linhas saem, e a oferta de atendimento é reconfigurada pela tela. O
            // agregado aceita oferta sem tipo de deficiência, então a oferta pai
            // permanece íntegra; o que se perde é a lista de tipos, que o operador
            // repõe escolhendo do cadastro — agora com o código junto.
            migrationBuilder.Sql("DELETE FROM selecao.ofertas_tipo_deficiencia;");

            // Sem defaultValue: o DELETE acima garante que não há linha a preencher, e um
            // default de string vazia ficaria permanente no schema, disponível para
            // gravar snapshot inválido em qualquer insert futuro que omitisse a coluna.
            migrationBuilder.AddColumn<string>(
                name: "tipo_deficiencia_codigo",
                schema: "selecao",
                table: "ofertas_tipo_deficiencia",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false);
        }

        /// <inheritdoc />
        /// <remarks>
        /// A reversão devolve o schema ao estado anterior, mas não restaura as linhas
        /// removidas no <c>Up</c> — as ofertas voltam sem tipos de deficiência.
        /// </remarks>
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "tipo_deficiencia_codigo",
                schema: "selecao",
                table: "ofertas_tipo_deficiencia");
        }
    }
}
