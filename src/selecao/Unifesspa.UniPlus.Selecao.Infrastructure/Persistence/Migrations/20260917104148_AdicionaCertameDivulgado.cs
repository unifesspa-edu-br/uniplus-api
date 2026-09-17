using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AdicionaCertameDivulgado : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "certames_divulgados",
                schema: "selecao",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    numero_versao = table.Column<int>(type: "integer", nullable: false),
                    ato_criador_id = table.Column<Guid>(type: "uuid", nullable: false),
                    hash_configuracao = table.Column<string>(type: "character(64)", fixedLength: true, maxLength: 64, nullable: false),
                    versao_projecao = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    inscricoes_de = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    inscricoes_ate = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    certame = table.Column<string>(type: "jsonb", nullable: false),
                    divulgado_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_certames_divulgados", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_certames_divulgados_prazo",
                schema: "selecao",
                table: "certames_divulgados",
                columns: new[] { "inscricoes_ate", "id" });

            migrationBuilder.CreateIndex(
                name: "ux_certames_divulgados_ato_criador",
                schema: "selecao",
                table: "certames_divulgados",
                column: "ato_criador_id",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "certames_divulgados",
                schema: "selecao");
        }
    }
}
