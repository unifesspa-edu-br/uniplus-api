using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Unifesspa.UniPlus.Geo.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddGeoImportacaoExecucao : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "geo_importacao_execucao",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    versao_dataset = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    status = table.Column<int>(type: "integer", nullable: false),
                    iniciado_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    concluido_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    disparado_por = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    relatorio_json = table.Column<string>(type: "jsonb", nullable: true),
                    mensagem = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_geo_importacao_execucao", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_geo_importacao_execucao_iniciado_em",
                table: "geo_importacao_execucao",
                column: "iniciado_em");

            migrationBuilder.CreateIndex(
                name: "ux_geo_importacao_execucao_em_andamento",
                table: "geo_importacao_execucao",
                column: "status",
                unique: true,
                filter: "status = 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "geo_importacao_execucao");
        }
    }
}
