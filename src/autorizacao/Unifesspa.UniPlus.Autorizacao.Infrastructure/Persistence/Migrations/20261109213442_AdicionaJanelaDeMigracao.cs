using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Unifesspa.UniPlus.Autorizacao.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AdicionaJanelaDeMigracao : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_concessoes_revogacao_completa",
                schema: "autorizacao",
                table: "concessoes");

            migrationBuilder.AddColumn<Guid>(
                name: "revogada_pela_janela_id",
                schema: "autorizacao",
                table: "concessoes",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddUniqueConstraint(
                name: "ak_concessoes_perfil_id_id",
                schema: "autorizacao",
                table: "concessoes",
                columns: new[] { "perfil_id", "id" });

            migrationBuilder.CreateTable(
                name: "janelas_de_migracao",
                schema: "autorizacao",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    perfil_id = table.Column<Guid>(type: "uuid", nullable: false),
                    versao_nova_id = table.Column<Guid>(type: "uuid", nullable: false),
                    numero_da_versao_nova = table.Column<int>(type: "integer", nullable: false),
                    versao_alvo_id = table.Column<Guid>(type: "uuid", nullable: false),
                    aberta_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    prazo = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    encerrada_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    forma_do_encerramento = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    motivo_do_encerramento = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    encerrada_por_emissor = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    encerrada_por_sub = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    encerrada_por_nome = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    aviso_48h_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    aviso_24h_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_janelas_de_migracao", x => x.id);
                    table.UniqueConstraint("ak_janelas_de_migracao_id_perfil_id", x => new { x.id, x.perfil_id });
                    table.CheckConstraint("ck_janelas_de_migracao_encerramento", "(encerrada_em IS NULL) = (forma_do_encerramento IS NULL)");
                    table.CheckConstraint("ck_janelas_de_migracao_forma", "forma_do_encerramento IS NULL OR forma_do_encerramento IN ('concluida', 'prazo', 'manual')");
                    table.CheckConstraint("ck_janelas_de_migracao_manual", "COALESCE(forma_do_encerramento = 'manual', false) = (motivo_do_encerramento IS NOT NULL AND encerrada_por_sub IS NOT NULL)");
                    table.CheckConstraint("ck_janelas_de_migracao_prazo", "prazo = aberta_em + interval '72 hours'");
                    table.ForeignKey(
                        name: "fk_janelas_de_migracao_perfis_perfil_id",
                        column: x => x.perfil_id,
                        principalSchema: "autorizacao",
                        principalTable: "perfis",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_janelas_de_migracao_versoes_dos_perfis_perfil_id_versao_alv",
                        columns: x => new { x.perfil_id, x.versao_alvo_id },
                        principalSchema: "autorizacao",
                        principalTable: "versoes_do_perfil",
                        principalColumns: new[] { "perfil_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_janelas_de_migracao_versoes_dos_perfis_perfil_id_versao_nov",
                        columns: x => new { x.perfil_id, x.versao_nova_id },
                        principalSchema: "autorizacao",
                        principalTable: "versoes_do_perfil",
                        principalColumns: new[] { "perfil_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "lotes_da_migracao",
                schema: "autorizacao",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    janela_id = table.Column<Guid>(type: "uuid", nullable: false),
                    perfil_id = table.Column<Guid>(type: "uuid", nullable: false),
                    etapa = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    enviado_por_emissor = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    enviado_por_sub = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    enviado_por_nome = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    enviado_por_tem_concessao = table.Column<bool>(type: "boolean", nullable: false),
                    confirmou_exclusao = table.Column<bool>(type: "boolean", nullable: false),
                    quantidade = table.Column<int>(type: "integer", nullable: false),
                    instante = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_lotes_da_migracao", x => x.id);
                    table.UniqueConstraint("ak_lotes_da_migracao_janela_id_id_etapa", x => new { x.janela_id, x.id, x.etapa });
                    table.CheckConstraint("ck_lotes_da_migracao_etapa", "etapa IN ('gestao_de_acessos', 'protecao_de_dados')");
                    table.ForeignKey(
                        name: "fk_lotes_da_migracao_janelas_de_migracao_janela_id_perfil_id",
                        columns: x => new { x.janela_id, x.perfil_id },
                        principalSchema: "autorizacao",
                        principalTable: "janelas_de_migracao",
                        principalColumns: new[] { "id", "perfil_id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "migracoes_da_concessao",
                schema: "autorizacao",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    janela_id = table.Column<Guid>(type: "uuid", nullable: false),
                    perfil_id = table.Column<Guid>(type: "uuid", nullable: false),
                    concessao_id = table.Column<Guid>(type: "uuid", nullable: false),
                    situacao = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    exige_protecao_de_dados = table.Column<bool>(type: "boolean", nullable: false),
                    dispensa = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    atualizada_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_migracoes_da_concessao", x => x.id);
                    table.CheckConstraint("ck_migracoes_da_concessao_dispensa", "dispensa IS NULL OR dispensa = 'beneficiario_ja_tem_protecao_de_dados'");
                    table.CheckConstraint("ck_migracoes_da_concessao_situacao", "situacao IN ('retida', 'aguardando_protecao_de_dados', 'migrada', 'recusada', 'nao_migrada', 'fora_da_lista')");
                    table.ForeignKey(
                        name: "fk_migracoes_da_concessao_concessoes_perfil_id_concessao_id",
                        columns: x => new { x.perfil_id, x.concessao_id },
                        principalSchema: "autorizacao",
                        principalTable: "concessoes",
                        principalColumns: new[] { "perfil_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_migracoes_da_concessao_janelas_de_migracao_janela_id_perfil",
                        columns: x => new { x.janela_id, x.perfil_id },
                        principalSchema: "autorizacao",
                        principalTable: "janelas_de_migracao",
                        principalColumns: new[] { "id", "perfil_id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "decisoes_da_migracao",
                schema: "autorizacao",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    janela_id = table.Column<Guid>(type: "uuid", nullable: false),
                    perfil_id = table.Column<Guid>(type: "uuid", nullable: false),
                    concessao_id = table.Column<Guid>(type: "uuid", nullable: false),
                    lote_de_origem_id = table.Column<Guid>(type: "uuid", nullable: true),
                    lote_de_origem_etapa = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    envio_id = table.Column<Guid>(type: "uuid", nullable: true),
                    envio_etapa = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    etapa = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    decisao = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    ator = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    decisor_emissor = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    decisor_sub = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    decisor_nome = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    decisor_tem_concessao = table.Column<bool>(type: "boolean", nullable: true),
                    dispensa = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    versao_avaliada_id = table.Column<Guid>(type: "uuid", nullable: false),
                    composicao_avaliada = table.Column<string[]>(type: "text[]", nullable: false),
                    acrescimo_avaliado = table.Column<string[]>(type: "text[]", nullable: false),
                    prazo = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    instante = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_decisoes_da_migracao", x => x.id);
                    table.CheckConstraint("ck_decisoes_da_migracao_dispensa", "dispensa IS NULL OR dispensa = 'beneficiario_ja_tem_protecao_de_dados'");
                    table.CheckConstraint("ck_decisoes_da_migracao_envio", "(envio_id IS NULL) = (envio_etapa IS NULL) AND (envio_etapa IS NULL OR envio_etapa IN ('gestao_de_acessos', 'protecao_de_dados'))");
                    table.CheckConstraint("ck_decisoes_da_migracao_lote_de_origem", "(lote_de_origem_id IS NULL) = (lote_de_origem_etapa IS NULL) AND (lote_de_origem_etapa IS NULL OR lote_de_origem_etapa = 'gestao_de_acessos')");
                    table.CheckConstraint("ck_decisoes_da_migracao_ramo", "(ator = 'humano' AND etapa IN ('gestao_de_acessos', 'protecao_de_dados') AND decisao IN ('migrar', 'recusar') AND decisor_emissor IS NOT NULL AND decisor_sub IS NOT NULL AND decisor_tem_concessao IS NOT NULL AND lote_de_origem_id IS NOT NULL AND (envio_etapa IS NULL OR envio_etapa = etapa) AND (etapa <> 'protecao_de_dados' OR envio_id IS NOT NULL) AND (etapa <> 'gestao_de_acessos' OR envio_id IS NULL OR envio_id = lote_de_origem_id)) OR (ator = 'sistema' AND decisor_emissor IS NULL AND decisor_sub IS NULL AND decisor_nome IS NULL AND decisor_tem_concessao IS NULL AND envio_id IS NULL AND ((etapa = 'gestao_de_acessos' AND decisao = 'reter' AND lote_de_origem_id IS NOT NULL) OR (etapa = 'sistema' AND decisao IN ('nao_migrada', 'fora_da_lista'))))");
                    table.ForeignKey(
                        name: "fk_decisoes_da_migracao_concessoes_perfil_id_concessao_id",
                        columns: x => new { x.perfil_id, x.concessao_id },
                        principalSchema: "autorizacao",
                        principalTable: "concessoes",
                        principalColumns: new[] { "perfil_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_decisoes_da_migracao_janelas_de_migracao_janela_id_perfil_id",
                        columns: x => new { x.janela_id, x.perfil_id },
                        principalSchema: "autorizacao",
                        principalTable: "janelas_de_migracao",
                        principalColumns: new[] { "id", "perfil_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_decisoes_da_migracao_lotes_da_migracao_janela_id_envio_id_e",
                        columns: x => new { x.janela_id, x.envio_id, x.envio_etapa },
                        principalSchema: "autorizacao",
                        principalTable: "lotes_da_migracao",
                        principalColumns: new[] { "janela_id", "id", "etapa" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_decisoes_da_migracao_lotes_da_migracao_janela_id_lote_de_or",
                        columns: x => new { x.janela_id, x.lote_de_origem_id, x.lote_de_origem_etapa },
                        principalSchema: "autorizacao",
                        principalTable: "lotes_da_migracao",
                        principalColumns: new[] { "janela_id", "id", "etapa" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_decisoes_da_migracao_versoes_dos_perfis_perfil_id_versao_av",
                        columns: x => new { x.perfil_id, x.versao_avaliada_id },
                        principalSchema: "autorizacao",
                        principalTable: "versoes_do_perfil",
                        principalColumns: new[] { "perfil_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_concessoes_revogada_pela_janela_id_perfil_id",
                schema: "autorizacao",
                table: "concessoes",
                columns: new[] { "revogada_pela_janela_id", "perfil_id" });

            migrationBuilder.AddCheckConstraint(
                name: "ck_concessoes_revogacao_completa",
                schema: "autorizacao",
                table: "concessoes",
                sql: "(revogada_em IS NULL AND revogada_por_emissor IS NULL AND revogada_por_sub IS NULL AND motivo_revogacao IS NULL AND revogada_pela_janela_id IS NULL) OR (revogada_em IS NOT NULL AND revogada_por_emissor IS NOT NULL AND revogada_por_sub IS NOT NULL AND motivo_revogacao IS NOT NULL AND revogada_pela_janela_id IS NULL) OR (revogada_em IS NOT NULL AND revogada_por_emissor IS NULL AND revogada_por_sub IS NULL AND motivo_revogacao IS NOT NULL AND revogada_pela_janela_id IS NOT NULL)");

            migrationBuilder.CreateIndex(
                name: "ix_decisoes_da_migracao_janela_id_envio_id_envio_etapa",
                schema: "autorizacao",
                table: "decisoes_da_migracao",
                columns: new[] { "janela_id", "envio_id", "envio_etapa" });

            migrationBuilder.CreateIndex(
                name: "ix_decisoes_da_migracao_janela_id_lote_de_origem_id_lote_de_or",
                schema: "autorizacao",
                table: "decisoes_da_migracao",
                columns: new[] { "janela_id", "lote_de_origem_id", "lote_de_origem_etapa" });

            migrationBuilder.CreateIndex(
                name: "ix_decisoes_da_migracao_janela_id_perfil_id",
                schema: "autorizacao",
                table: "decisoes_da_migracao",
                columns: new[] { "janela_id", "perfil_id" });

            migrationBuilder.CreateIndex(
                name: "ix_decisoes_da_migracao_perfil_id_concessao_id",
                schema: "autorizacao",
                table: "decisoes_da_migracao",
                columns: new[] { "perfil_id", "concessao_id" });

            migrationBuilder.CreateIndex(
                name: "ix_decisoes_da_migracao_perfil_id_versao_avaliada_id",
                schema: "autorizacao",
                table: "decisoes_da_migracao",
                columns: new[] { "perfil_id", "versao_avaliada_id" });

            migrationBuilder.CreateIndex(
                name: "ix_janelas_de_migracao_perfil_id_versao_alvo_id",
                schema: "autorizacao",
                table: "janelas_de_migracao",
                columns: new[] { "perfil_id", "versao_alvo_id" });

            migrationBuilder.CreateIndex(
                name: "ix_janelas_de_migracao_perfil_id_versao_nova_id",
                schema: "autorizacao",
                table: "janelas_de_migracao",
                columns: new[] { "perfil_id", "versao_nova_id" });

            migrationBuilder.CreateIndex(
                name: "ux_janelas_de_migracao_aberta",
                schema: "autorizacao",
                table: "janelas_de_migracao",
                column: "perfil_id",
                unique: true,
                filter: "encerrada_em IS NULL");

            migrationBuilder.CreateIndex(
                name: "ix_lotes_da_migracao_janela_id_perfil_id",
                schema: "autorizacao",
                table: "lotes_da_migracao",
                columns: new[] { "janela_id", "perfil_id" });

            migrationBuilder.CreateIndex(
                name: "ux_lotes_da_migracao_gestao",
                schema: "autorizacao",
                table: "lotes_da_migracao",
                column: "janela_id",
                unique: true,
                filter: "etapa = 'gestao_de_acessos'");

            migrationBuilder.CreateIndex(
                name: "ix_migracoes_da_concessao_janela_id_perfil_id",
                schema: "autorizacao",
                table: "migracoes_da_concessao",
                columns: new[] { "janela_id", "perfil_id" });

            migrationBuilder.CreateIndex(
                name: "ix_migracoes_da_concessao_perfil_id_concessao_id",
                schema: "autorizacao",
                table: "migracoes_da_concessao",
                columns: new[] { "perfil_id", "concessao_id" });

            migrationBuilder.CreateIndex(
                name: "ux_migracoes_da_concessao_janela_concessao",
                schema: "autorizacao",
                table: "migracoes_da_concessao",
                columns: new[] { "janela_id", "concessao_id" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "fk_concessoes_janelas_de_migracao_revogada_pela_janela_id_perf",
                schema: "autorizacao",
                table: "concessoes",
                columns: new[] { "revogada_pela_janela_id", "perfil_id" },
                principalSchema: "autorizacao",
                principalTable: "janelas_de_migracao",
                principalColumns: new[] { "id", "perfil_id" },
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_concessoes_janelas_de_migracao_revogada_pela_janela_id_perf",
                schema: "autorizacao",
                table: "concessoes");

            migrationBuilder.DropTable(
                name: "decisoes_da_migracao",
                schema: "autorizacao");

            migrationBuilder.DropTable(
                name: "migracoes_da_concessao",
                schema: "autorizacao");

            migrationBuilder.DropTable(
                name: "lotes_da_migracao",
                schema: "autorizacao");

            migrationBuilder.DropTable(
                name: "janelas_de_migracao",
                schema: "autorizacao");

            migrationBuilder.DropUniqueConstraint(
                name: "ak_concessoes_perfil_id_id",
                schema: "autorizacao",
                table: "concessoes");

            migrationBuilder.DropIndex(
                name: "ix_concessoes_revogada_pela_janela_id_perfil_id",
                schema: "autorizacao",
                table: "concessoes");

            migrationBuilder.DropCheckConstraint(
                name: "ck_concessoes_revogacao_completa",
                schema: "autorizacao",
                table: "concessoes");

            migrationBuilder.DropColumn(
                name: "revogada_pela_janela_id",
                schema: "autorizacao",
                table: "concessoes");

            migrationBuilder.AddCheckConstraint(
                name: "ck_concessoes_revogacao_completa",
                schema: "autorizacao",
                table: "concessoes",
                sql: "(revogada_em IS NULL AND revogada_por_emissor IS NULL AND revogada_por_sub IS NULL AND motivo_revogacao IS NULL) OR (revogada_em IS NOT NULL AND revogada_por_emissor IS NOT NULL AND revogada_por_sub IS NOT NULL AND motivo_revogacao IS NOT NULL)");
        }
    }
}
