using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Unifesspa.UniPlus.Autorizacao.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AdicionaVersaoEVigenciaDoPerfil : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "versoes_do_perfil",
                schema: "autorizacao",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    perfil_id = table.Column<Guid>(type: "uuid", nullable: false),
                    numero = table.Column<int>(type: "integer", nullable: false),
                    nome = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    descricao = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    protocolo = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    situacao = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    motivo_da_recusa = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    recusada_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    criada_por_emissor = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    criada_por_sub = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    criada_por_nome = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    criada_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    cancelada_por_emissor = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    cancelada_por_sub = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    cancelada_por_nome = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    cancelada_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_versoes_do_perfil", x => x.id);
                    table.UniqueConstraint("ak_versoes_do_perfil_perfil_versao", x => new { x.perfil_id, x.id });
                    table.ForeignKey(
                        name: "fk_versoes_do_perfil_perfis_perfil_id",
                        column: x => x.perfil_id,
                        principalSchema: "autorizacao",
                        principalTable: "perfis",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "documentos_da_versao",
                schema: "autorizacao",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    versao_id = table.Column<Guid>(type: "uuid", nullable: false),
                    perfil_id = table.Column<Guid>(type: "uuid", nullable: false),
                    situacao = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    tamanho_declarado = table.Column<long>(type: "bigint", nullable: false),
                    etag = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    sha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    enviado_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    verificado_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    recusado_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_documentos_da_versao", x => x.id);
                    table.ForeignKey(
                        name: "fk_documentos_da_versao_versoes_do_perfil_perfil_id_versao_id",
                        columns: x => new { x.perfil_id, x.versao_id },
                        principalSchema: "autorizacao",
                        principalTable: "versoes_do_perfil",
                        principalColumns: new[] { "perfil_id", "id" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "versao_do_perfil_permissoes",
                schema: "autorizacao",
                columns: table => new
                {
                    codigo = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    versao_id = table.Column<Guid>(type: "uuid", nullable: false),
                    classificacao = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    de_unidade = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_versao_do_perfil_permissoes", x => new { x.versao_id, x.codigo });
                    table.ForeignKey(
                        name: "fk_versao_do_perfil_permissoes_versoes_do_perfil_versao_id",
                        column: x => x.versao_id,
                        principalSchema: "autorizacao",
                        principalTable: "versoes_do_perfil",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "vigencias_do_perfil",
                schema: "autorizacao",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    perfil_id = table.Column<Guid>(type: "uuid", nullable: false),
                    versao_id = table.Column<Guid>(type: "uuid", nullable: false),
                    desde = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ate = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_vigencias_do_perfil", x => x.id);
                    table.ForeignKey(
                        name: "fk_vigencias_do_perfil_perfis_perfil_id",
                        column: x => x.perfil_id,
                        principalSchema: "autorizacao",
                        principalTable: "perfis",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_vigencias_do_perfil_versoes_do_perfil_perfil_id_versao_id",
                        columns: x => new { x.perfil_id, x.versao_id },
                        principalSchema: "autorizacao",
                        principalTable: "versoes_do_perfil",
                        principalColumns: new[] { "perfil_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "leituras_do_documento_da_versao",
                schema: "autorizacao",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    documento_id = table.Column<Guid>(type: "uuid", nullable: false),
                    leitor_emissor = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    leitor_sub = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    leitor_nome = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    instante = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_leituras_do_documento_da_versao", x => x.id);
                    table.ForeignKey(
                        name: "fk_leituras_do_documento_da_versao_documentos_da_versao_docume",
                        column: x => x.documento_id,
                        principalSchema: "autorizacao",
                        principalTable: "documentos_da_versao",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_documentos_da_versao_perfil_id_versao_id",
                schema: "autorizacao",
                table: "documentos_da_versao",
                columns: new[] { "perfil_id", "versao_id" });

            migrationBuilder.CreateIndex(
                name: "ux_documentos_da_versao_vivo",
                schema: "autorizacao",
                table: "documentos_da_versao",
                column: "versao_id",
                unique: true,
                filter: "situacao <> 'recusado'");

            migrationBuilder.CreateIndex(
                name: "ix_leituras_do_documento_da_versao_documento",
                schema: "autorizacao",
                table: "leituras_do_documento_da_versao",
                column: "documento_id");

            migrationBuilder.CreateIndex(
                name: "ux_versoes_do_perfil_numero",
                schema: "autorizacao",
                table: "versoes_do_perfil",
                columns: new[] { "perfil_id", "numero" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_versoes_do_perfil_pendente",
                schema: "autorizacao",
                table: "versoes_do_perfil",
                column: "perfil_id",
                unique: true,
                filter: "situacao = 'pendente'");

            migrationBuilder.CreateIndex(
                name: "ux_versoes_do_perfil_vigente",
                schema: "autorizacao",
                table: "versoes_do_perfil",
                column: "perfil_id",
                unique: true,
                filter: "situacao = 'vigente'");

            migrationBuilder.CreateIndex(
                name: "ix_vigencias_do_perfil_perfil_id_versao_id",
                schema: "autorizacao",
                table: "vigencias_do_perfil",
                columns: new[] { "perfil_id", "versao_id" });

            migrationBuilder.CreateIndex(
                name: "ux_vigencias_do_perfil_aberta",
                schema: "autorizacao",
                table: "vigencias_do_perfil",
                column: "perfil_id",
                unique: true,
                filter: "ate IS NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "leituras_do_documento_da_versao",
                schema: "autorizacao");

            migrationBuilder.DropTable(
                name: "versao_do_perfil_permissoes",
                schema: "autorizacao");

            migrationBuilder.DropTable(
                name: "vigencias_do_perfil",
                schema: "autorizacao");

            migrationBuilder.DropTable(
                name: "documentos_da_versao",
                schema: "autorizacao");

            migrationBuilder.DropTable(
                name: "versoes_do_perfil",
                schema: "autorizacao");
        }
    }
}
