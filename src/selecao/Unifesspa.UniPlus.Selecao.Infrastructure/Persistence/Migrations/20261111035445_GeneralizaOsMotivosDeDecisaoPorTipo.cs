using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class GeneralizaOsMotivosDeDecisaoPorTipo : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "motivos_decisao_isencao",
                schema: "selecao");

            migrationBuilder.CreateTable(
                name: "motivos_de_decisao",
                schema: "selecao",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tipo = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    codigo = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    descricao = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    subclassificacao = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    sentido = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    ativo = table.Column<bool>(type: "boolean", nullable: false),
                    created_by = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    updated_by = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_motivos_de_decisao", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "motivos_do_processo",
                schema: "selecao",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    processo_seletivo_id = table.Column<Guid>(type: "uuid", nullable: false),
                    motivo_de_decisao_origem_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tipo = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    codigo = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    descricao = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    subclassificacao = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    sentido = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    congelado = table.Column<bool>(type: "boolean", nullable: false),
                    justificativa_da_inclusao = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    created_by = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    updated_by = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_motivos_do_processo", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_motivos_de_decisao_tipo_subclassificacao_ativo",
                schema: "selecao",
                table: "motivos_de_decisao",
                columns: new[] { "tipo", "subclassificacao", "ativo" });

            migrationBuilder.CreateIndex(
                name: "ux_motivos_de_decisao_tipo_codigo",
                schema: "selecao",
                table: "motivos_de_decisao",
                columns: new[] { "tipo", "codigo" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_motivos_do_processo_processo_origem",
                schema: "selecao",
                table: "motivos_do_processo",
                columns: new[] { "processo_seletivo_id", "motivo_de_decisao_origem_id" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "motivos_de_decisao",
                schema: "selecao");

            migrationBuilder.DropTable(
                name: "motivos_do_processo",
                schema: "selecao");

            migrationBuilder.CreateTable(
                name: "motivos_decisao_isencao",
                schema: "selecao",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    ativo = table.Column<bool>(type: "boolean", nullable: false),
                    codigo = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    descricao = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    fundamento = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    resultado_permitido = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_motivos_decisao_isencao", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_motivos_decisao_isencao_fundamento_ativo",
                schema: "selecao",
                table: "motivos_decisao_isencao",
                columns: new[] { "fundamento", "ativo" });

            migrationBuilder.CreateIndex(
                name: "ux_motivos_decisao_isencao_codigo",
                schema: "selecao",
                table: "motivos_decisao_isencao",
                column: "codigo",
                unique: true);
        }
    }
}
