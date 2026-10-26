using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AdicionaLotesELancamentosDeNotasDoEnem : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "geracoes_de_envio_ao_inep",
                schema: "selecao",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    processo_seletivo_id = table.Column<Guid>(type: "uuid", nullable: false),
                    etapa_processo_id = table.Column<Guid>(type: "uuid", nullable: false),
                    edicao_codigo = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    leiaute_codigo = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    leiaute_versao = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    leiaute_hash = table.Column<string>(type: "character(64)", fixedLength: true, maxLength: 64, nullable: false),
                    gerado_por = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false, comment: "O sub do token de quem gerou."),
                    gerado_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    total_de_cpfs = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_geracoes_de_envio_ao_inep", x => x.id);
                    table.CheckConstraint("ck_geracoes_de_envio_ao_inep_leiaute_hash", "leiaute_hash ~ '^[0-9a-f]{64}$'");
                    table.CheckConstraint("ck_geracoes_de_envio_ao_inep_total", "total_de_cpfs >= 0");
                    table.ForeignKey(
                        name: "fk_geracoes_de_envio_ao_inep_processos_seletivos_processo_sele",
                        column: x => x.processo_seletivo_id,
                        principalSchema: "selecao",
                        principalTable: "processos_seletivos",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                },
                comment: "O registro de que o arquivo de envio ao INEP foi gerado para uma edição do ENEM. Append-only; o arquivo e os CPFs não são guardados.");

            migrationBuilder.CreateTable(
                name: "lotes_de_notas_do_enem",
                schema: "selecao",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    processo_seletivo_id = table.Column<Guid>(type: "uuid", nullable: false),
                    etapa_processo_id = table.Column<Guid>(type: "uuid", nullable: false, comment: "A etapa de nota do ENEM, guardada por valor e sem chave estrangeira: pertence à configuração do processo, que a retificação recria."),
                    edicao_codigo = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false, comment: "O código da opção do fato EDICAO_ENEM, como o processo a declara."),
                    leiaute_codigo = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    leiaute_versao = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    leiaute_hash = table.Column<string>(type: "character(64)", fixedLength: true, maxLength: 64, nullable: false, comment: "SHA-256 da definição do leiaute com que o arquivo foi lido."),
                    arquivo_hash = table.Column<string>(type: "character(64)", fixedLength: true, maxLength: 64, nullable: false, comment: "SHA-256 dos bytes do arquivo importado."),
                    importado_por = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false, comment: "O sub do token de quem importou."),
                    importado_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    total_de_linhas = table.Column<int>(type: "integer", nullable: false),
                    total_de_inscritos_com_nota = table.Column<int>(type: "integer", nullable: false),
                    total_de_lancamentos = table.Column<int>(type: "integer", nullable: false),
                    total_de_inscritos_sem_linha = table.Column<int>(type: "integer", nullable: false),
                    total_de_linhas_sem_inscrito = table.Column<int>(type: "integer", nullable: false),
                    total_de_inscritos_em_conflito = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_lotes_de_notas_do_enem", x => x.id);
                    table.UniqueConstraint("ak_lotes_de_notas_do_enem_id_etapa", x => new { x.id, x.etapa_processo_id });
                    table.CheckConstraint("ck_lotes_de_notas_do_enem_edicao", "edicao_codigo <> ''");
                    table.CheckConstraint("ck_lotes_de_notas_do_enem_hashes", "arquivo_hash ~ '^[0-9a-f]{64}$' AND leiaute_hash ~ '^[0-9a-f]{64}$'");
                    table.CheckConstraint("ck_lotes_de_notas_do_enem_totais", "total_de_linhas >= 0 AND total_de_inscritos_com_nota >= 0 AND total_de_lancamentos >= 0 AND total_de_inscritos_sem_linha >= 0 AND total_de_linhas_sem_inscrito >= 0 AND total_de_inscritos_em_conflito >= 0");
                    table.ForeignKey(
                        name: "fk_lotes_de_notas_do_enem_processos_seletivos_processo_seletiv",
                        column: x => x.processo_seletivo_id,
                        principalSchema: "selecao",
                        principalTable: "processos_seletivos",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                },
                comment: "Uma importação do retorno do INEP para um processo, uma etapa e uma edição do ENEM. Append-only; vale o lote mais recente do escopo, por inteiro. O arquivo não é guardado, só o hash.");

            migrationBuilder.CreateTable(
                name: "inscritos_do_lote_de_notas",
                schema: "selecao",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    lote_id = table.Column<Guid>(type: "uuid", nullable: false),
                    inscricao_id = table.Column<Guid>(type: "uuid", nullable: false),
                    numero_de_inscricao_no_enem = table.Column<string>(type: "character varying(12)", maxLength: 12, nullable: false, comment: "O número de inscrição do INEP, guardado para a conferência de um recurso.")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_inscritos_do_lote_de_notas", x => x.id);
                    table.UniqueConstraint("ak_inscritos_do_lote_de_notas_lote_inscricao", x => new { x.lote_id, x.inscricao_id });
                    table.CheckConstraint("ck_inscritos_do_lote_de_notas_numero_enem", "numero_de_inscricao_no_enem ~ '^[0-9]{12}$'");
                    table.ForeignKey(
                        name: "fk_inscritos_do_lote_de_notas_inscricoes_inscricao_id",
                        column: x => x.inscricao_id,
                        principalSchema: "selecao",
                        principalTable: "inscricoes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_inscritos_do_lote_de_notas_lotes_de_notas_do_enem_lote_id",
                        column: x => x.lote_id,
                        principalSchema: "selecao",
                        principalTable: "lotes_de_notas_do_enem",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                },
                comment: "A inscrição com linha válida em um lote de notas, com o número de inscrição que o INEP lhe deu. Append-only.");

            migrationBuilder.CreateTable(
                name: "ocorrencias_do_lote_de_notas",
                schema: "selecao",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    lote_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tipo = table.Column<short>(type: "smallint", nullable: false, comment: "1 = inscrito sem linha, 2 = linha sem inscrito, 3 = conflito."),
                    inscricao_id = table.Column<Guid>(type: "uuid", nullable: true),
                    numeros_das_linhas = table.Column<int[]>(type: "integer[]", nullable: true, comment: "Os números das linhas do arquivo, a partir de 1.")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_ocorrencias_do_lote_de_notas", x => x.id);
                    table.CheckConstraint("ck_ocorrencias_do_lote_de_notas_forma", "(tipo = 1 AND inscricao_id IS NOT NULL AND numeros_das_linhas IS NULL) OR (tipo = 2 AND inscricao_id IS NULL AND numeros_das_linhas IS NOT NULL AND cardinality(numeros_das_linhas) = 1) OR (tipo = 3 AND inscricao_id IS NOT NULL AND numeros_das_linhas IS NOT NULL AND cardinality(numeros_das_linhas) >= 2)");
                    table.ForeignKey(
                        name: "fk_ocorrencias_do_lote_de_notas_inscricoes_inscricao_id",
                        column: x => x.inscricao_id,
                        principalSchema: "selecao",
                        principalTable: "inscricoes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_ocorrencias_do_lote_de_notas_lotes_de_notas_do_enem_lote_id",
                        column: x => x.lote_id,
                        principalSchema: "selecao",
                        principalTable: "lotes_de_notas_do_enem",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                },
                comment: "O que não virou nota ao importar um lote: inscrito sem linha, linha de CPF sem inscrito e inscrito em conflito. Nunca guarda o CPF.");

            migrationBuilder.CreateTable(
                name: "lancamentos_de_nota",
                schema: "selecao",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    lote_id = table.Column<Guid>(type: "uuid", nullable: false),
                    inscricao_id = table.Column<Guid>(type: "uuid", nullable: false),
                    etapa_processo_id = table.Column<Guid>(type: "uuid", nullable: false, comment: "A etapa do lote, repetida para ler o lançamento sem o lote; a chave estrangeira composta com o lote impede que divirja."),
                    item_codigo = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false, comment: "A área do ENEM. Só o domínio enumera as áreas: o banco não carrega o vocabulário, para que acrescentar um item não exija migration."),
                    nota = table.Column<decimal>(type: "numeric(7,3)", precision: 7, scale: 3, nullable: false),
                    situacao = table.Column<short>(type: "smallint", nullable: false, comment: "1 = presente, 2 = ausente. Quem faltou tem nota zero, que não vale como nota obtida.")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_lancamentos_de_nota", x => x.id);
                    table.CheckConstraint("ck_lancamentos_de_nota_nota", "nota BETWEEN 0 AND 1000");
                    table.CheckConstraint("ck_lancamentos_de_nota_situacao", "situacao IN (1, 2)");
                    table.ForeignKey(
                        name: "fk_lancamentos_de_nota_inscritos_do_lote_de_notas_lote_id_insc",
                        columns: x => new { x.lote_id, x.inscricao_id },
                        principalSchema: "selecao",
                        principalTable: "inscritos_do_lote_de_notas",
                        principalColumns: new[] { "lote_id", "inscricao_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_lancamentos_de_nota_lotes_de_notas_do_enem_lote_id_etapa_pr",
                        columns: x => new { x.lote_id, x.etapa_processo_id },
                        principalSchema: "selecao",
                        principalTable: "lotes_de_notas_do_enem",
                        principalColumns: new[] { "id", "etapa_processo_id" },
                        onDelete: ReferentialAction.Restrict);
                },
                comment: "Uma nota individual de uma inscrição numa etapa: uma área do ENEM, com a nota e a situação. Append-only; corrigir é importar outro lote.");

            migrationBuilder.CreateIndex(
                name: "ix_geracoes_de_envio_ao_inep_escopo",
                schema: "selecao",
                table: "geracoes_de_envio_ao_inep",
                columns: new[] { "processo_seletivo_id", "etapa_processo_id", "edicao_codigo", "gerado_em" },
                descending: new[] { false, false, false, true });

            migrationBuilder.CreateIndex(
                name: "ix_inscritos_do_lote_de_notas_inscricao",
                schema: "selecao",
                table: "inscritos_do_lote_de_notas",
                column: "inscricao_id");

            migrationBuilder.CreateIndex(
                name: "ix_lancamentos_de_nota_inscricao_etapa",
                schema: "selecao",
                table: "lancamentos_de_nota",
                columns: new[] { "inscricao_id", "etapa_processo_id" });

            migrationBuilder.CreateIndex(
                name: "ix_lancamentos_de_nota_lote_id_etapa_processo_id",
                schema: "selecao",
                table: "lancamentos_de_nota",
                columns: new[] { "lote_id", "etapa_processo_id" });

            migrationBuilder.CreateIndex(
                name: "ux_lancamentos_de_nota_lote_inscricao_item",
                schema: "selecao",
                table: "lancamentos_de_nota",
                columns: new[] { "lote_id", "inscricao_id", "item_codigo" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_lotes_de_notas_do_enem_escopo",
                schema: "selecao",
                table: "lotes_de_notas_do_enem",
                columns: new[] { "processo_seletivo_id", "etapa_processo_id", "edicao_codigo", "importado_em", "id" },
                descending: new[] { false, false, false, true, true });

            migrationBuilder.CreateIndex(
                name: "ix_ocorrencias_do_lote_de_notas_inscricao_id",
                schema: "selecao",
                table: "ocorrencias_do_lote_de_notas",
                column: "inscricao_id");

            migrationBuilder.CreateIndex(
                name: "ix_ocorrencias_do_lote_de_notas_lote",
                schema: "selecao",
                table: "ocorrencias_do_lote_de_notas",
                column: "lote_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "geracoes_de_envio_ao_inep",
                schema: "selecao");

            migrationBuilder.DropTable(
                name: "lancamentos_de_nota",
                schema: "selecao");

            migrationBuilder.DropTable(
                name: "ocorrencias_do_lote_de_notas",
                schema: "selecao");

            migrationBuilder.DropTable(
                name: "inscritos_do_lote_de_notas",
                schema: "selecao");

            migrationBuilder.DropTable(
                name: "lotes_de_notas_do_enem",
                schema: "selecao");
        }
    }
}
