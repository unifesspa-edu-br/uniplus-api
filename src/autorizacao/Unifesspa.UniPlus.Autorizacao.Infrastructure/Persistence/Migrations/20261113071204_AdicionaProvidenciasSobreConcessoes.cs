using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Unifesspa.UniPlus.Autorizacao.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AdicionaProvidenciasSobreConcessoes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "providencias",
                schema: "autorizacao",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    concessao_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tipo = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    fundamento = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    estado = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    aberta_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    solicitada_por_emissor = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    solicitada_por_sub = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    solicitada_por_nome = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    solicitada_por_email = table.Column<string>(type: "character varying(254)", maxLength: 254, nullable: true),
                    desfecho_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    desfecho_por_emissor = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    desfecho_por_sub = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    desfecho_por_nome = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    motivo_do_desfecho = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_providencias", x => x.id);
                    table.CheckConstraint("ck_providencias_desfecho_completo", "(estado = 'aberta' AND desfecho_em IS NULL AND desfecho_por_emissor IS NULL AND desfecho_por_sub IS NULL AND desfecho_por_nome IS NULL AND motivo_do_desfecho IS NULL) OR (estado <> 'aberta' AND desfecho_em IS NOT NULL AND desfecho_por_emissor IS NOT NULL AND desfecho_por_sub IS NOT NULL AND motivo_do_desfecho IS NOT NULL)");
                    table.CheckConstraint("ck_providencias_estado", "estado IN ('aberta', 'respondida', 'atendida')");
                    table.CheckConstraint("ck_providencias_fundamento", "char_length(fundamento) BETWEEN 10 AND 2000");
                    table.CheckConstraint("ck_providencias_tipo", "tipo IN ('retirada', 'revisao')");
                    table.ForeignKey(
                        name: "fk_providencias_concessoes_concessao_id",
                        column: x => x.concessao_id,
                        principalSchema: "autorizacao",
                        principalTable: "concessoes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_providencias_concessao",
                schema: "autorizacao",
                table: "providencias",
                column: "concessao_id");

            migrationBuilder.CreateIndex(
                name: "ix_providencias_estado_id",
                schema: "autorizacao",
                table: "providencias",
                columns: new[] { "estado", "id" });

            migrationBuilder.CreateIndex(
                name: "ux_providencias_concessao_aberta",
                schema: "autorizacao",
                table: "providencias",
                column: "concessao_id",
                unique: true,
                filter: "estado = 'aberta'");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "providencias",
                schema: "autorizacao");
        }
    }
}
