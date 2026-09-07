using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ProdutosPublicadosPorFaseDoCronograma : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // As três colunas antigas são DERRUBADAS, não renomeadas: o scaffolding do
            // dotnet ef casou resultado_definitivo com emite_parecer_individual e
            // ato_produzido_codigo com fase_concluinte_codigo apenas por posição e tipo, e
            // renomear carregaria o valor antigo para um campo que significa outra coisa —
            // uma fase de resultado definitivo passaria a prometer parecer individual, e o
            // código do ato produzido viraria referência a uma fase concluinte inexistente.
            migrationBuilder.DropColumn(
                name: "produz_resultado",
                schema: "selecao",
                table: "fases_cronograma");

            migrationBuilder.DropColumn(
                name: "resultado_definitivo",
                schema: "selecao",
                table: "fases_cronograma");

            migrationBuilder.DropColumn(
                name: "ato_produzido_codigo",
                schema: "selecao",
                table: "fases_cronograma");

            migrationBuilder.AddColumn<bool>(
                name: "emite_parecer_individual",
                schema: "selecao",
                table: "fases_cronograma",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "fase_concluinte_codigo",
                schema: "selecao",
                table: "fases_cronograma",
                type: "character varying(60)",
                maxLength: 60,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "produtos_da_fase",
                schema: "selecao",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    fase_cronograma_id = table.Column<Guid>(type: "uuid", nullable: false),
                    ato_codigo = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    papel = table.Column<int>(type: "integer", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_produtos_da_fase", x => x.id);
                    table.ForeignKey(
                        name: "fk_produtos_da_fase_fases_cronograma_fase_cronograma_id",
                        column: x => x.fase_cronograma_id,
                        principalSchema: "selecao",
                        principalTable: "fases_cronograma",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ux_produtos_da_fase_ato",
                schema: "selecao",
                table: "produtos_da_fase",
                columns: new[] { "fase_cronograma_id", "ato_codigo" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "produtos_da_fase",
                schema: "selecao");

            migrationBuilder.DropColumn(
                name: "fase_concluinte_codigo",
                schema: "selecao",
                table: "fases_cronograma");

            migrationBuilder.DropColumn(
                name: "emite_parecer_individual",
                schema: "selecao",
                table: "fases_cronograma");

            migrationBuilder.AddColumn<string>(
                name: "ato_produzido_codigo",
                schema: "selecao",
                table: "fases_cronograma",
                type: "character varying(60)",
                maxLength: 60,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "resultado_definitivo",
                schema: "selecao",
                table: "fases_cronograma",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "produz_resultado",
                schema: "selecao",
                table: "fases_cronograma",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }
    }
}
