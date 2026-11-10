using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Unifesspa.UniPlus.Autorizacao.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AdicionaReclassificacaoDoCatalogo : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                CREATE FUNCTION autorizacao.ordem_da_classificacao(classificacao text) RETURNS integer
                LANGUAGE sql IMMUTABLE STRICT PARALLEL SAFE
                RETURN CASE classificacao
                    WHEN 'nenhuma' THEN 0
                    WHEN 'publico' THEN 1
                    WHEN 'interno' THEN 2
                    WHEN 'pessoal' THEN 3
                    WHEN 'identificador' THEN 4
                    WHEN 'sensivel' THEN 5
                END;");

            migrationBuilder.CreateTable(
                name: "avisos_da_reclassificacao",
                schema: "autorizacao",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    origem = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    origem_id = table.Column<Guid>(type: "uuid", nullable: false),
                    enviado_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_avisos_da_reclassificacao", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "bases_da_permissao_na_concessao",
                schema: "autorizacao",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    perfil_id = table.Column<Guid>(type: "uuid", nullable: false),
                    concessao_id = table.Column<Guid>(type: "uuid", nullable: false),
                    codigo = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    classificacao = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    de_unidade = table.Column<bool>(type: "boolean", nullable: true),
                    origem = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    referencia_id = table.Column<Guid>(type: "uuid", nullable: false),
                    ordem_na_trilha = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_bases_da_permissao_na_concessao", x => x.id);
                    table.CheckConstraint("ck_bases_da_permissao_origem", "origem IN ('migracao', 'saida', 'liberacao', 'conciliacao')");
                    table.CheckConstraint("ck_bases_da_permissao_saida", "(origem = 'saida') = (classificacao IS NULL AND de_unidade IS NULL)");
                    table.ForeignKey(
                        name: "fk_bases_da_permissao_na_concessao_concessoes_perfil_id_conces",
                        columns: x => new { x.perfil_id, x.concessao_id },
                        principalSchema: "autorizacao",
                        principalTable: "concessoes",
                        principalColumns: new[] { "perfil_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "implantacoes_das_permissoes",
                schema: "autorizacao",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    sequencia = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    impressao = table.Column<string>(type: "char(64)", nullable: false),
                    implantada_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_implantacoes_das_permissoes", x => x.id);
                    table.CheckConstraint("ck_implantacoes_das_permissoes_impressao", "impressao ~ '^[0-9a-f]{64}$'");
                });

            migrationBuilder.CreateTable(
                name: "divergencias_da_governanca",
                schema: "autorizacao",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    implantacao_id = table.Column<Guid>(type: "uuid", nullable: false),
                    perfil_id = table.Column<Guid>(type: "uuid", nullable: false),
                    codigo = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    tipo = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    de = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    para = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    instante = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ordem_na_trilha = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_divergencias_da_governanca", x => x.id);
                    table.CheckConstraint("ck_divergencias_da_governanca_tipo", "tipo IN ('composicao_faltante', 'composicao_excedente', 'classificacao', 'escopo')");
                    table.ForeignKey(
                        name: "fk_divergencias_da_governanca_implantacao",
                        column: x => x.implantacao_id,
                        principalSchema: "autorizacao",
                        principalTable: "implantacoes_das_permissoes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_divergencias_da_governanca_perfis_perfil_id",
                        column: x => x.perfil_id,
                        principalSchema: "autorizacao",
                        principalTable: "perfis",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "permissoes_implantadas",
                schema: "autorizacao",
                columns: table => new
                {
                    implantacao_id = table.Column<Guid>(type: "uuid", nullable: false),
                    codigo = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    modulo = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    classificacao = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    de_unidade = table.Column<bool>(type: "boolean", nullable: false),
                    reservada = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_permissoes_implantadas", x => new { x.implantacao_id, x.codigo });
                    table.ForeignKey(
                        name: "fk_permissoes_implantadas_implantacao",
                        column: x => x.implantacao_id,
                        principalSchema: "autorizacao",
                        principalTable: "implantacoes_das_permissoes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "reclassificacoes_da_permissao",
                schema: "autorizacao",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    implantacao_id = table.Column<Guid>(type: "uuid", nullable: false),
                    perfil_id = table.Column<Guid>(type: "uuid", nullable: false),
                    codigo = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    classificacao_anterior = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    classificacao_nova = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    de_unidade_anterior = table.Column<bool>(type: "boolean", nullable: false),
                    de_unidade_novo = table.Column<bool>(type: "boolean", nullable: false),
                    instante = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ordem_na_trilha = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_reclassificacoes_da_permissao", x => x.id);
                    table.CheckConstraint("ck_reclassificacoes_da_permissao_mudanca", "classificacao_anterior <> classificacao_nova OR de_unidade_anterior <> de_unidade_novo");
                    table.ForeignKey(
                        name: "fk_reclassificacoes_da_permissao_implantacao",
                        column: x => x.implantacao_id,
                        principalSchema: "autorizacao",
                        principalTable: "implantacoes_das_permissoes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_reclassificacoes_da_permissao_perfis_perfil_id",
                        column: x => x.perfil_id,
                        principalSchema: "autorizacao",
                        principalTable: "perfis",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "liberacoes_da_reclassificacao",
                schema: "autorizacao",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    reclassificacao_id = table.Column<Guid>(type: "uuid", nullable: false),
                    perfil_id = table.Column<Guid>(type: "uuid", nullable: false),
                    concessao_id = table.Column<Guid>(type: "uuid", nullable: false),
                    etapa = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    resultado = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    motivo = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    decisor_emissor = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    decisor_sub = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    decisor_nome = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    decisor_tem_concessao = table.Column<bool>(type: "boolean", nullable: false),
                    lote_id = table.Column<Guid>(type: "uuid", nullable: false),
                    instante = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ordem_na_trilha = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_liberacoes_da_reclassificacao", x => x.id);
                    table.CheckConstraint("ck_liberacoes_da_reclassificacao_etapa", "etapa IN ('gestao_de_acessos', 'protecao_de_dados')");
                    table.CheckConstraint("ck_liberacoes_da_reclassificacao_motivo", "(resultado = 'mantida') = (motivo IS NOT NULL) AND resultado IN ('liberada', 'mantida')");
                    table.ForeignKey(
                        name: "fk_liberacoes_da_reclassificacao_concessoes_perfil_id_concessa",
                        columns: x => new { x.perfil_id, x.concessao_id },
                        principalSchema: "autorizacao",
                        principalTable: "concessoes",
                        principalColumns: new[] { "perfil_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_liberacoes_da_reclassificacao_reclassificacoes_reclassifica",
                        column: x => x.reclassificacao_id,
                        principalSchema: "autorizacao",
                        principalTable: "reclassificacoes_da_permissao",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ux_avisos_da_reclassificacao_fato",
                schema: "autorizacao",
                table: "avisos_da_reclassificacao",
                columns: new[] { "origem", "origem_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_bases_da_permissao_concessao_codigo",
                schema: "autorizacao",
                table: "bases_da_permissao_na_concessao",
                columns: new[] { "concessao_id", "codigo", "ordem_na_trilha" },
                descending: new[] { false, false, true });

            migrationBuilder.CreateIndex(
                name: "ix_bases_da_permissao_na_concessao_perfil_id_concessao_id",
                schema: "autorizacao",
                table: "bases_da_permissao_na_concessao",
                columns: new[] { "perfil_id", "concessao_id" });

            migrationBuilder.CreateIndex(
                name: "ux_bases_da_permissao_ordem",
                schema: "autorizacao",
                table: "bases_da_permissao_na_concessao",
                column: "ordem_na_trilha",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_divergencias_da_governanca_implantacao_id",
                schema: "autorizacao",
                table: "divergencias_da_governanca",
                column: "implantacao_id");

            migrationBuilder.CreateIndex(
                name: "ix_divergencias_da_governanca_perfil_id",
                schema: "autorizacao",
                table: "divergencias_da_governanca",
                column: "perfil_id");

            migrationBuilder.CreateIndex(
                name: "ux_divergencias_da_governanca_ordem",
                schema: "autorizacao",
                table: "divergencias_da_governanca",
                column: "ordem_na_trilha",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_implantacoes_das_permissoes_sequencia",
                schema: "autorizacao",
                table: "implantacoes_das_permissoes",
                column: "sequencia",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_liberacoes_da_reclassificacao_perfil_id_concessao_id",
                schema: "autorizacao",
                table: "liberacoes_da_reclassificacao",
                columns: new[] { "perfil_id", "concessao_id" });

            migrationBuilder.CreateIndex(
                name: "ux_liberacoes_da_reclassificacao_etapa",
                schema: "autorizacao",
                table: "liberacoes_da_reclassificacao",
                columns: new[] { "reclassificacao_id", "concessao_id", "etapa" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_liberacoes_da_reclassificacao_ordem",
                schema: "autorizacao",
                table: "liberacoes_da_reclassificacao",
                column: "ordem_na_trilha",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_reclassificacoes_da_permissao_perfil_codigo",
                schema: "autorizacao",
                table: "reclassificacoes_da_permissao",
                columns: new[] { "perfil_id", "codigo", "ordem_na_trilha" },
                descending: new[] { false, false, true });

            migrationBuilder.CreateIndex(
                name: "ux_reclassificacoes_da_permissao_fato",
                schema: "autorizacao",
                table: "reclassificacoes_da_permissao",
                columns: new[] { "implantacao_id", "perfil_id", "codigo" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_reclassificacoes_da_permissao_ordem",
                schema: "autorizacao",
                table: "reclassificacoes_da_permissao",
                column: "ordem_na_trilha",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP FUNCTION autorizacao.ordem_da_classificacao(text);");

            migrationBuilder.DropTable(
                name: "avisos_da_reclassificacao",
                schema: "autorizacao");

            migrationBuilder.DropTable(
                name: "bases_da_permissao_na_concessao",
                schema: "autorizacao");

            migrationBuilder.DropTable(
                name: "divergencias_da_governanca",
                schema: "autorizacao");

            migrationBuilder.DropTable(
                name: "liberacoes_da_reclassificacao",
                schema: "autorizacao");

            migrationBuilder.DropTable(
                name: "permissoes_implantadas",
                schema: "autorizacao");

            migrationBuilder.DropTable(
                name: "reclassificacoes_da_permissao",
                schema: "autorizacao");

            migrationBuilder.DropTable(
                name: "implantacoes_das_permissoes",
                schema: "autorizacao");
        }
    }
}
