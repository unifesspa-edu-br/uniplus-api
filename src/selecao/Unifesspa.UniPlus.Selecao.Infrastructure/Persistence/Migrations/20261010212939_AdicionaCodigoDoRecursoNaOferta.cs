using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AdicionaCodigoDoRecursoNaOferta : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // O código não é derivável do que já está gravado: o snapshot guardava só o
            // identificador de origem e o nome, e o cadastro de origem perdeu as linhas ao
            // ganhar o código. Preencher com string vazia deixaria uma opção que o candidato
            // não teria como escolher e que a própria factory recusaria ao reidratar.
            //
            // As linhas saem, e a oferta de atendimento é reconfigurada pela tela. O agregado
            // aceita oferta sem recurso, então a oferta pai permanece íntegra; o que se perde
            // é a lista de recursos, que o operador repõe escolhendo do cadastro.
            migrationBuilder.Sql("DELETE FROM selecao.ofertas_recurso;");

            // Sem defaultValue: o DELETE acima garante que não há linha a preencher, e um
            // default de string vazia ficaria permanente no schema.
            migrationBuilder.AddColumn<string>(
                name: "recurso_codigo",
                schema: "selecao",
                table: "ofertas_recurso",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false);
        }

        /// <inheritdoc />
        /// <remarks>
        /// A reversão devolve o schema ao estado anterior, mas não restaura as linhas
        /// removidas no <c>Up</c> — as ofertas voltam sem recursos.
        /// </remarks>
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "recurso_codigo",
                schema: "selecao",
                table: "ofertas_recurso");
        }
    }
}
