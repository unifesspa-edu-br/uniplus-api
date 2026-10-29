using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Unifesspa.UniPlus.Autorizacao.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AdicionaInstalacoes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "instalacoes",
                schema: "autorizacao",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    executor_emissor = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    executor_sub = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    numero_do_documento = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    tamanho_do_pdf = table.Column<long>(type: "bigint", nullable: false),
                    situacao = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    documento_etag = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    documento_sha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    documento_verificado_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    documento_recusado_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    codigo_resumo = table.Column<byte[]>(type: "bytea", nullable: true),
                    codigo_enviado_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    codigo_expira_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    tentativas = table.Column<int>(type: "integer", nullable: false),
                    iniciada_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    confirmada_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    cancelada_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_instalacoes", x => x.id);
                    table.CheckConstraint("ck_instalacoes_codigo_completo", "(codigo_enviado_em IS NULL) = (codigo_expira_em IS NULL)");
                    table.CheckConstraint("ck_instalacoes_tentativas", "tentativas >= 0 AND tentativas <= 5");
                });

            migrationBuilder.CreateTable(
                name: "instalacao_gestores",
                schema: "autorizacao",
                columns: table => new
                {
                    emissor = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    sub = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    instalacao_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_instalacao_gestores", x => new { x.instalacao_id, x.emissor, x.sub });
                    table.ForeignKey(
                        name: "fk_instalacao_gestores_instalacoes_instalacao_id",
                        column: x => x.instalacao_id,
                        principalSchema: "autorizacao",
                        principalTable: "instalacoes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "instalacao_tentativas",
                schema: "autorizacao",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    instalacao_id = table.Column<Guid>(type: "uuid", nullable: true),
                    autor_emissor = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    autor_sub = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    instante = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    resultado = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_instalacao_tentativas", x => x.id);
                    table.ForeignKey(
                        name: "fk_instalacao_tentativas_instalacoes_instalacao_id",
                        column: x => x.instalacao_id,
                        principalSchema: "autorizacao",
                        principalTable: "instalacoes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_instalacao_tentativas_instalacao",
                schema: "autorizacao",
                table: "instalacao_tentativas",
                column: "instalacao_id");

            migrationBuilder.CreateIndex(
                name: "ux_instalacoes_executor_pendente",
                schema: "autorizacao",
                table: "instalacoes",
                columns: new[] { "executor_emissor", "executor_sub" },
                unique: true,
                filter: "situacao IN ('aguardando_documento', 'documento_em_verificacao', 'documento_verificado', 'aguardando_codigo')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "instalacao_gestores",
                schema: "autorizacao");

            migrationBuilder.DropTable(
                name: "instalacao_tentativas",
                schema: "autorizacao");

            migrationBuilder.DropTable(
                name: "instalacoes",
                schema: "autorizacao");
        }
    }
}
