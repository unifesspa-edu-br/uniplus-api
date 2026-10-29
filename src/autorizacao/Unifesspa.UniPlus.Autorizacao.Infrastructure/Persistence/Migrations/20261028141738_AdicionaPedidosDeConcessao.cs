using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Unifesspa.UniPlus.Autorizacao.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AdicionaPedidosDeConcessao : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "pedidos_de_concessao",
                schema: "autorizacao",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    solicitante_emissor = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    solicitante_sub = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    beneficiario_email = table.Column<string>(type: "character varying(254)", maxLength: 254, nullable: false),
                    beneficiario_emissor = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    beneficiario_sub = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    beneficiario_nome = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    beneficiario_aceitou_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    perfil_id = table.Column<Guid>(type: "uuid", nullable: false),
                    unidade_id = table.Column<Guid>(type: "uuid", nullable: true),
                    justificativa = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    situacao = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    criado_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    encerrado_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    concessao_id = table.Column<Guid>(type: "uuid", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_pedidos_de_concessao", x => x.id);
                    table.CheckConstraint("ck_pedidos_beneficiario_completo", "(beneficiario_emissor IS NULL) = (beneficiario_sub IS NULL)");
                    table.CheckConstraint("ck_pedidos_concessao_so_aprovado", "concessao_id IS NULL OR situacao = 'aprovado'");
                    table.CheckConstraint("ck_pedidos_decisao_com_beneficiario", "situacao IN ('aguardando_beneficiario', 'cancelado') OR beneficiario_sub IS NOT NULL");
                    table.CheckConstraint("ck_pedidos_encerramento", "(situacao IN ('aprovado', 'recusado', 'cancelado')) = (encerrado_em IS NOT NULL)");
                    table.ForeignKey(
                        name: "fk_pedidos_de_concessao_perfis_perfil_id",
                        column: x => x.perfil_id,
                        principalSchema: "autorizacao",
                        principalTable: "perfis",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "pedido_anexos",
                schema: "autorizacao",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    pedido_id = table.Column<Guid>(type: "uuid", nullable: false),
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
                    table.PrimaryKey("pk_pedido_anexos", x => x.id);
                    table.ForeignKey(
                        name: "fk_pedido_anexos_pedidos_de_concessao_pedido_id",
                        column: x => x.pedido_id,
                        principalSchema: "autorizacao",
                        principalTable: "pedidos_de_concessao",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "pedido_decisoes",
                schema: "autorizacao",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    pedido_id = table.Column<Guid>(type: "uuid", nullable: false),
                    etapa = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    decisor_emissor = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    decisor_sub = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    resultado = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    motivo = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    instante = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    permissoes_avaliadas = table.Column<string[]>(type: "text[]", nullable: false),
                    sensibilidade_avaliada = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    dispensa = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_pedido_decisoes", x => x.id);
                    table.CheckConstraint("ck_pedido_decisoes_motivo_na_recusa", "resultado <> 'recusado' OR motivo IS NOT NULL");
                    table.ForeignKey(
                        name: "fk_pedido_decisoes_pedidos_de_concessao_pedido_id",
                        column: x => x.pedido_id,
                        principalSchema: "autorizacao",
                        principalTable: "pedidos_de_concessao",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "pedido_anexo_leituras",
                schema: "autorizacao",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    pedido_id = table.Column<Guid>(type: "uuid", nullable: false),
                    anexo_id = table.Column<Guid>(type: "uuid", nullable: false),
                    leitor_emissor = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    leitor_sub = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    instante = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_pedido_anexo_leituras", x => x.id);
                    table.ForeignKey(
                        name: "fk_pedido_anexo_leituras_pedido_anexos_anexo_id",
                        column: x => x.anexo_id,
                        principalSchema: "autorizacao",
                        principalTable: "pedido_anexos",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_pedido_anexo_leituras_pedidos_pedido_id",
                        column: x => x.pedido_id,
                        principalSchema: "autorizacao",
                        principalTable: "pedidos_de_concessao",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                schema: "autorizacao",
                table: "perfil_permissoes",
                columns: new[] { "codigo", "perfil_id" },
                values: new object[,]
                {
                    { "autorizacao:pedidos:aprovar-funcional", new Guid("019a0000-0000-7000-8000-000000000001") },
                    { "autorizacao:pedidos:aprovar-protecao-de-dados", new Guid("019a0000-0000-7000-8000-000000000002") }
                });

            migrationBuilder.CreateIndex(
                name: "ix_pedido_anexo_leituras_anexo_id",
                schema: "autorizacao",
                table: "pedido_anexo_leituras",
                column: "anexo_id");

            migrationBuilder.CreateIndex(
                name: "ix_pedido_anexo_leituras_pedido",
                schema: "autorizacao",
                table: "pedido_anexo_leituras",
                column: "pedido_id");

            migrationBuilder.CreateIndex(
                name: "ix_pedido_anexos_pedido",
                schema: "autorizacao",
                table: "pedido_anexos",
                column: "pedido_id");

            migrationBuilder.CreateIndex(
                name: "ix_pedido_decisoes_pedido",
                schema: "autorizacao",
                table: "pedido_decisoes",
                column: "pedido_id");

            migrationBuilder.CreateIndex(
                name: "ix_pedidos_de_concessao_perfil_id",
                schema: "autorizacao",
                table: "pedidos_de_concessao",
                column: "perfil_id");

            migrationBuilder.CreateIndex(
                name: "ix_pedidos_situacao",
                schema: "autorizacao",
                table: "pedidos_de_concessao",
                column: "situacao");

            migrationBuilder.CreateIndex(
                name: "ix_pedidos_solicitante",
                schema: "autorizacao",
                table: "pedidos_de_concessao",
                columns: new[] { "solicitante_emissor", "solicitante_sub" });

            migrationBuilder.CreateIndex(
                name: "ux_pedidos_ativo_unico",
                schema: "autorizacao",
                table: "pedidos_de_concessao",
                columns: new[] { "beneficiario_email", "perfil_id", "unidade_id" },
                unique: true,
                filter: "situacao IN ('aguardando_beneficiario', 'aguardando_gestor', 'aguardando_encarregada')")
                .Annotation("Npgsql:NullsDistinct", false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "pedido_anexo_leituras",
                schema: "autorizacao");

            migrationBuilder.DropTable(
                name: "pedido_decisoes",
                schema: "autorizacao");

            migrationBuilder.DropTable(
                name: "pedido_anexos",
                schema: "autorizacao");

            migrationBuilder.DropTable(
                name: "pedidos_de_concessao",
                schema: "autorizacao");

            migrationBuilder.DeleteData(
                schema: "autorizacao",
                table: "perfil_permissoes",
                keyColumns: new[] { "codigo", "perfil_id" },
                keyValues: new object[] { "autorizacao:pedidos:aprovar-funcional", new Guid("019a0000-0000-7000-8000-000000000001") });

            migrationBuilder.DeleteData(
                schema: "autorizacao",
                table: "perfil_permissoes",
                keyColumns: new[] { "codigo", "perfil_id" },
                keyValues: new object[] { "autorizacao:pedidos:aprovar-protecao-de-dados", new Guid("019a0000-0000-7000-8000-000000000002") });
        }
    }
}
