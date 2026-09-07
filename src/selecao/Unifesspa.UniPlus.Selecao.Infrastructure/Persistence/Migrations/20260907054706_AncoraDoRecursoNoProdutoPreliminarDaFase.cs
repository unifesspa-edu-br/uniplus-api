using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AncoraDoRecursoNoProdutoPreliminarDaFase : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // A âncora deixa de ser o código do tipo de ato e passa a ser a linha do produto
            // preliminar da fase. As regras já gravadas não têm como ser convertidas: a
            // migration anterior criou produtos_da_fase vazia, e nenhuma fase publica produto
            // a que apontar. Sem produção, a regra de recurso é reescrita pela configuração
            // do certame; deixá-las com âncora vazia faria a fase deixar de publicar e de ser
            // republicável sem que ninguém soubesse por quê.
            migrationBuilder.Sql("DELETE FROM selecao.regras_recurso_fase;");

            migrationBuilder.DropColumn(
                name: "ato_ancora_codigo",
                schema: "selecao",
                table: "regras_recurso_fase");

            migrationBuilder.AddColumn<Guid>(
                name: "produto_ancora_id",
                schema: "selecao",
                table: "regras_recurso_fase",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "produto_ancora_id",
                schema: "selecao",
                table: "regras_recurso_fase");

            migrationBuilder.AddColumn<string>(
                name: "ato_ancora_codigo",
                schema: "selecao",
                table: "regras_recurso_fase",
                type: "character varying(60)",
                maxLength: 60,
                nullable: false,
                defaultValue: "");
        }
    }
}
