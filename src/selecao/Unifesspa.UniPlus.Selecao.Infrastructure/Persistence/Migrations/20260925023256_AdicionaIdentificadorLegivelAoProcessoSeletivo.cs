using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AdicionaIdentificadorLegivelAoProcessoSeletivo : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "identificador_legivel",
                schema: "selecao",
                table: "processos_seletivos",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true,
                comment: "Identificador legível escolhido no cadastro; dele derivam o endereço público do certame e a chave no acervo. Ausência = ainda não declarado.");

            migrationBuilder.CreateIndex(
                name: "ix_processos_seletivos_identificador_legivel",
                schema: "selecao",
                table: "processos_seletivos",
                column: "identificador_legivel",
                unique: true,
                filter: "identificador_legivel IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_processos_seletivos_identificador_legivel",
                schema: "selecao",
                table: "processos_seletivos");

            migrationBuilder.DropColumn(
                name: "identificador_legivel",
                schema: "selecao",
                table: "processos_seletivos");
        }
    }
}
