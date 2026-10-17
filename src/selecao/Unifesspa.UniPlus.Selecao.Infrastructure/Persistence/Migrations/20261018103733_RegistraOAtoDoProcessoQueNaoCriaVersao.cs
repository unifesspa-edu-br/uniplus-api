using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RegistraOAtoDoProcessoQueNaoCriaVersao : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "atos_do_processo",
                schema: "selecao",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    processo_seletivo_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tipo_codigo = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    tipo_nome = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    numero = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    data_do_ato = table.Column<DateOnly>(type: "date", nullable: false),
                    assinante = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    unidade_administradora = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    identificador_legivel_do_processo = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    produto_do_edital_id = table.Column<Guid>(type: "uuid", nullable: true),
                    documento_principal_id = table.Column<Guid>(type: "uuid", nullable: false),
                    ato_corrigido_id = table.Column<Guid>(type: "uuid", nullable: true),
                    motivo_da_correcao = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    politica_de_divulgacao_confirmada_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    situacao = table.Column<int>(type: "integer", nullable: false),
                    registrado_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_atos_do_processo", x => x.id);
                    table.ForeignKey(
                        name: "fk_atos_do_processo_documentos_a_publicar_documento_principal_",
                        column: x => x.documento_principal_id,
                        principalSchema: "selecao",
                        principalTable: "documentos_a_publicar",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_atos_do_processo_processos_seletivos_processo_seletivo_id",
                        column: x => x.processo_seletivo_id,
                        principalSchema: "selecao",
                        principalTable: "processos_seletivos",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "anexos_do_ato_do_processo",
                schema: "selecao",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    ato_do_processo_id = table.Column<Guid>(type: "uuid", nullable: false),
                    documento_a_publicar_id = table.Column<Guid>(type: "uuid", nullable: false),
                    nome_de_exibicao = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ordem = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_anexos_do_ato_do_processo", x => x.id);
                    table.ForeignKey(
                        name: "fk_anexos_do_ato_do_processo_atos_do_processo_ato_do_processo_",
                        column: x => x.ato_do_processo_id,
                        principalSchema: "selecao",
                        principalTable: "atos_do_processo",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_anexos_do_ato_do_processo_documentos_a_publicar_documento_a",
                        column: x => x.documento_a_publicar_id,
                        principalSchema: "selecao",
                        principalTable: "documentos_a_publicar",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ux_anexos_do_ato_do_processo_documento",
                schema: "selecao",
                table: "anexos_do_ato_do_processo",
                column: "documento_a_publicar_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_anexos_do_ato_do_processo_ordem",
                schema: "selecao",
                table: "anexos_do_ato_do_processo",
                columns: new[] { "ato_do_processo_id", "ordem" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_atos_do_processo_ato_corrigido",
                schema: "selecao",
                table: "atos_do_processo",
                column: "ato_corrigido_id");

            migrationBuilder.CreateIndex(
                name: "ix_atos_do_processo_processo_recentes",
                schema: "selecao",
                table: "atos_do_processo",
                columns: new[] { "processo_seletivo_id", "created_at", "id" },
                descending: new[] { false, true, true });

            migrationBuilder.CreateIndex(
                name: "ux_atos_do_processo_documento_principal",
                schema: "selecao",
                table: "atos_do_processo",
                column: "documento_principal_id",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "anexos_do_ato_do_processo",
                schema: "selecao");

            migrationBuilder.DropTable(
                name: "atos_do_processo",
                schema: "selecao");
        }
    }
}
