using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AdicionaIdentificacaoNoLoteDeNotasDoEnem : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "cpf_mascarado",
                schema: "selecao",
                table: "ocorrencias_do_lote_de_notas",
                type: "character varying(14)",
                maxLength: 14,
                nullable: true,
                comment: "O CPF mascarado da linha sem inscrito (***.999.999-**); nunca o CPF puro.");

            migrationBuilder.AddColumn<string>(
                name: "nome",
                schema: "selecao",
                table: "ocorrencias_do_lote_de_notas",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true,
                comment: "O nome que o arquivo traz para a linha sem inscrito.");

            migrationBuilder.AddColumn<string>(
                name: "sexo",
                schema: "selecao",
                table: "ocorrencias_do_lote_de_notas",
                type: "character varying(1)",
                maxLength: 1,
                nullable: true,
                comment: "O sexo (F ou M) que o arquivo traz para a linha sem inscrito.");

            migrationBuilder.AddColumn<string>(
                name: "importado_por_nome",
                schema: "selecao",
                table: "lotes_de_notas_do_enem",
                type: "character varying(255)",
                maxLength: 255,
                nullable: true,
                comment: "O nome de quem importou, gravado no momento da ação.");

            migrationBuilder.AddColumn<string>(
                name: "gerado_por_nome",
                schema: "selecao",
                table: "geracoes_de_envio_ao_inep",
                type: "character varying(255)",
                maxLength: 255,
                nullable: true,
                comment: "O nome de quem gerou, gravado no momento da ação.");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "cpf_mascarado",
                schema: "selecao",
                table: "ocorrencias_do_lote_de_notas");

            migrationBuilder.DropColumn(
                name: "nome",
                schema: "selecao",
                table: "ocorrencias_do_lote_de_notas");

            migrationBuilder.DropColumn(
                name: "sexo",
                schema: "selecao",
                table: "ocorrencias_do_lote_de_notas");

            migrationBuilder.DropColumn(
                name: "importado_por_nome",
                schema: "selecao",
                table: "lotes_de_notas_do_enem");

            migrationBuilder.DropColumn(
                name: "gerado_por_nome",
                schema: "selecao",
                table: "geracoes_de_envio_ao_inep");
        }
    }
}
