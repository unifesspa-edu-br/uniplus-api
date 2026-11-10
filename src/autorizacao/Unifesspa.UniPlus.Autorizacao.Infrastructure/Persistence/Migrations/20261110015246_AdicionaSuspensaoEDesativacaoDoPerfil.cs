using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Unifesspa.UniPlus.Autorizacao.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AdicionaSuspensaoEDesativacaoDoPerfil : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateSequence(
                name: "ordem_da_trilha_de_contencao",
                schema: "autorizacao");

            migrationBuilder.CreateTable(
                name: "desativacoes_do_perfil",
                schema: "autorizacao",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    perfil_id = table.Column<Guid>(type: "uuid", nullable: false),
                    desativada_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    desativada_por_emissor = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    desativada_por_sub = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    desativada_por_nome = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    motivo = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    reativada_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    reativada_por_emissor = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    reativada_por_sub = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    reativada_por_nome = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    motivo_da_reativacao = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    ordem_na_trilha = table.Column<long>(type: "bigint", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_desativacoes_do_perfil", x => x.id);
                    table.CheckConstraint("ck_desativacoes_do_perfil_reativacao", "(reativada_em IS NULL AND reativada_por_emissor IS NULL AND reativada_por_sub IS NULL AND reativada_por_nome IS NULL AND motivo_da_reativacao IS NULL) OR (reativada_em IS NOT NULL AND reativada_por_emissor IS NOT NULL AND reativada_por_sub IS NOT NULL AND motivo_da_reativacao IS NOT NULL)");
                    table.ForeignKey(
                        name: "fk_desativacoes_do_perfil_perfis_perfil_id",
                        column: x => x.perfil_id,
                        principalSchema: "autorizacao",
                        principalTable: "perfis",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "suspensoes_do_perfil",
                schema: "autorizacao",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    perfil_id = table.Column<Guid>(type: "uuid", nullable: false),
                    suspensa_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    suspensa_por_emissor = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    suspensa_por_sub = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    suspensa_por_nome = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    papel_de_quem_suspendeu = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    motivo = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    retomada_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ordem_na_trilha = table.Column<long>(type: "bigint", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_suspensoes_do_perfil", x => x.id);
                    table.CheckConstraint("ck_suspensoes_do_perfil_papel", "papel_de_quem_suspendeu IN ('gestao_de_acessos', 'protecao_de_dados')");
                    table.ForeignKey(
                        name: "fk_suspensoes_do_perfil_perfis_perfil_id",
                        column: x => x.perfil_id,
                        principalSchema: "autorizacao",
                        principalTable: "perfis",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "decisoes_da_retomada",
                schema: "autorizacao",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    suspensao_id = table.Column<Guid>(type: "uuid", nullable: false),
                    etapa = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    decisor_emissor = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    decisor_sub = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    decisor_nome = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    decisor_tem_concessao = table.Column<bool>(type: "boolean", nullable: false),
                    motivo = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    perfil_protegido_na_decisao = table.Column<bool>(type: "boolean", nullable: false),
                    instante = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ordem_na_trilha = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_decisoes_da_retomada", x => x.id);
                    table.CheckConstraint("ck_decisoes_da_retomada_etapa", "etapa IN ('gestao_de_acessos', 'protecao_de_dados')");
                    table.ForeignKey(
                        name: "fk_decisoes_da_retomada_suspensoes_do_perfil_suspensao_id",
                        column: x => x.suspensao_id,
                        principalSchema: "autorizacao",
                        principalTable: "suspensoes_do_perfil",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "eventos_da_contencao",
                schema: "autorizacao",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    perfil_id = table.Column<Guid>(type: "uuid", nullable: false),
                    instante = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    tipo = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    suspensao_id = table.Column<Guid>(type: "uuid", nullable: true),
                    desativacao_id = table.Column<Guid>(type: "uuid", nullable: true),
                    ator = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    origem = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    decisao_id = table.Column<Guid>(type: "uuid", nullable: true),
                    versao_que_entrou_id = table.Column<Guid>(type: "uuid", nullable: true),
                    permissao_codigo = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    ator_emissor = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    ator_sub = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    ator_nome = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    papel = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    motivo = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    versao_vigente_id = table.Column<Guid>(type: "uuid", nullable: true),
                    perfil_protegido = table.Column<bool>(type: "boolean", nullable: false),
                    ordem_na_trilha = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_eventos_da_contencao", x => x.id);
                    table.CheckConstraint("ck_eventos_da_contencao_desativacao", "tipo <> 'desativacao' OR (ator = 'humano' AND ator_emissor IS NOT NULL AND ator_sub IS NOT NULL AND motivo IS NOT NULL AND desativacao_id IS NOT NULL AND suspensao_id IS NULL AND papel IS NULL AND origem IS NULL AND decisao_id IS NULL AND versao_que_entrou_id IS NULL AND permissao_codigo IS NULL)");
                    table.CheckConstraint("ck_eventos_da_contencao_reativacao", "tipo <> 'reativacao' OR (ator = 'humano' AND ator_emissor IS NOT NULL AND ator_sub IS NOT NULL AND motivo IS NOT NULL AND desativacao_id IS NOT NULL AND suspensao_id IS NULL AND papel IS NULL AND origem IS NULL AND decisao_id IS NULL AND versao_que_entrou_id IS NULL AND permissao_codigo IS NULL)");
                    table.CheckConstraint("ck_eventos_da_contencao_retomada", "tipo <> 'retomada' OR (ator = 'sistema' AND suspensao_id IS NOT NULL AND desativacao_id IS NULL AND ator_emissor IS NULL AND ator_sub IS NULL AND ator_nome IS NULL AND papel IS NULL AND motivo IS NULL AND origem IS NOT NULL AND ((origem = 'decisao_da_etapa' AND decisao_id IS NOT NULL AND versao_que_entrou_id IS NULL AND permissao_codigo IS NULL) OR (origem = 'troca_de_vigencia' AND versao_que_entrou_id IS NOT NULL AND decisao_id IS NULL AND permissao_codigo IS NULL) OR (origem = 'reclassificacao' AND permissao_codigo IS NOT NULL AND decisao_id IS NULL AND versao_que_entrou_id IS NULL)))");
                    table.CheckConstraint("ck_eventos_da_contencao_suspensao", "tipo <> 'suspensao' OR (ator = 'humano' AND ator_emissor IS NOT NULL AND ator_sub IS NOT NULL AND motivo IS NOT NULL AND suspensao_id IS NOT NULL AND desativacao_id IS NULL AND papel IS NOT NULL AND origem IS NULL AND decisao_id IS NULL AND versao_que_entrou_id IS NULL AND permissao_codigo IS NULL)");
                    table.CheckConstraint("ck_eventos_da_contencao_tipo", "tipo IN ('suspensao', 'retomada', 'desativacao', 'reativacao')");
                    table.ForeignKey(
                        name: "fk_eventos_da_contencao_decisoes_da_retomada_decisao_id",
                        column: x => x.decisao_id,
                        principalSchema: "autorizacao",
                        principalTable: "decisoes_da_retomada",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_eventos_da_contencao_desativacoes_do_perfil_desativacao_id",
                        column: x => x.desativacao_id,
                        principalSchema: "autorizacao",
                        principalTable: "desativacoes_do_perfil",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_eventos_da_contencao_perfis_perfil_id",
                        column: x => x.perfil_id,
                        principalSchema: "autorizacao",
                        principalTable: "perfis",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_eventos_da_contencao_suspensoes_dos_perfis_suspensao_id",
                        column: x => x.suspensao_id,
                        principalSchema: "autorizacao",
                        principalTable: "suspensoes_do_perfil",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_eventos_da_contencao_versoes_dos_perfis_versao_que_entrou_id",
                        column: x => x.versao_que_entrou_id,
                        principalSchema: "autorizacao",
                        principalTable: "versoes_do_perfil",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_eventos_da_contencao_versoes_dos_perfis_versao_vigente_id",
                        column: x => x.versao_vigente_id,
                        principalSchema: "autorizacao",
                        principalTable: "versoes_do_perfil",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ux_decisoes_da_retomada_etapa",
                schema: "autorizacao",
                table: "decisoes_da_retomada",
                columns: new[] { "suspensao_id", "etapa" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_decisoes_da_retomada_ordem",
                schema: "autorizacao",
                table: "decisoes_da_retomada",
                column: "ordem_na_trilha",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_desativacoes_do_perfil_aberta",
                schema: "autorizacao",
                table: "desativacoes_do_perfil",
                column: "perfil_id",
                unique: true,
                filter: "reativada_em IS NULL");

            migrationBuilder.CreateIndex(
                name: "ux_desativacoes_do_perfil_ordem",
                schema: "autorizacao",
                table: "desativacoes_do_perfil",
                column: "ordem_na_trilha",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_eventos_da_contencao_decisao_id",
                schema: "autorizacao",
                table: "eventos_da_contencao",
                column: "decisao_id");

            migrationBuilder.CreateIndex(
                name: "ix_eventos_da_contencao_desativacao_id",
                schema: "autorizacao",
                table: "eventos_da_contencao",
                column: "desativacao_id");

            migrationBuilder.CreateIndex(
                name: "ix_eventos_da_contencao_perfil",
                schema: "autorizacao",
                table: "eventos_da_contencao",
                columns: new[] { "perfil_id", "instante" });

            migrationBuilder.CreateIndex(
                name: "ix_eventos_da_contencao_suspensao_id",
                schema: "autorizacao",
                table: "eventos_da_contencao",
                column: "suspensao_id");

            migrationBuilder.CreateIndex(
                name: "ix_eventos_da_contencao_versao_que_entrou_id",
                schema: "autorizacao",
                table: "eventos_da_contencao",
                column: "versao_que_entrou_id");

            migrationBuilder.CreateIndex(
                name: "ix_eventos_da_contencao_versao_vigente_id",
                schema: "autorizacao",
                table: "eventos_da_contencao",
                column: "versao_vigente_id");

            migrationBuilder.CreateIndex(
                name: "ux_eventos_da_contencao_ordem",
                schema: "autorizacao",
                table: "eventos_da_contencao",
                column: "ordem_na_trilha",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_suspensoes_do_perfil_aberta",
                schema: "autorizacao",
                table: "suspensoes_do_perfil",
                column: "perfil_id",
                unique: true,
                filter: "retomada_em IS NULL");

            migrationBuilder.CreateIndex(
                name: "ux_suspensoes_do_perfil_ordem",
                schema: "autorizacao",
                table: "suspensoes_do_perfil",
                column: "ordem_na_trilha",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "eventos_da_contencao",
                schema: "autorizacao");

            migrationBuilder.DropTable(
                name: "decisoes_da_retomada",
                schema: "autorizacao");

            migrationBuilder.DropTable(
                name: "desativacoes_do_perfil",
                schema: "autorizacao");

            migrationBuilder.DropTable(
                name: "suspensoes_do_perfil",
                schema: "autorizacao");

            migrationBuilder.DropSequence(
                name: "ordem_da_trilha_de_contencao",
                schema: "autorizacao");
        }
    }
}
