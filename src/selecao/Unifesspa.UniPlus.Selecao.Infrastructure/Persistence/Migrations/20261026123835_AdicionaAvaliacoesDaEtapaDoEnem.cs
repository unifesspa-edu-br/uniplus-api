using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AdicionaAvaliacoesDaEtapaDoEnem : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "versao_configuracao_id",
                schema: "selecao",
                table: "lotes_de_notas_do_enem",
                type: "uuid",
                nullable: false,
                comment: "A versão divulgada do certame cujos pesos apuraram as avaliações do lote.");

            migrationBuilder.CreateTable(
                name: "avaliacoes_da_etapa",
                schema: "selecao",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    lote_id = table.Column<Guid>(type: "uuid", nullable: false),
                    etapa_processo_id = table.Column<Guid>(type: "uuid", nullable: false, comment: "A etapa do lote, repetida para ler a avaliação sem o lote; a chave estrangeira composta com o lote impede que divirja."),
                    inscricao_id = table.Column<Guid>(type: "uuid", nullable: false, comment: "A inscrição avaliada, guardada por valor: quem não tem linha no arquivo do lote também tem avaliação, sem nota."),
                    oferta_curso_id = table.Column<Guid>(type: "uuid", nullable: false, comment: "A oferta de curso concorrida (origem da oferta no edital divulgado), guardada por valor e sem chave estrangeira: pertence à configuração do processo."),
                    grupo_area_enem_codigo = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false, comment: "O código do grupo de área do ENEM cujos pesos apuraram a nota."),
                    situacao = table.Column<short>(type: "smallint", nullable: false, comment: "1 = apurada, 2 = inscrição sem linha no arquivo, 3 = inscrição com linhas em conflito. Só a apurada tem nota; a falta de nota nunca vira zero."),
                    nota = table.Column<decimal>(type: "numeric", nullable: true, comment: "Σ(nota × peso) ÷ Σ(pesos) das cinco áreas, sem arredondar. Nulo quando não há nota. Não vai para log.")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_avaliacoes_da_etapa", x => x.id);
                    table.CheckConstraint("ck_avaliacoes_da_etapa_nota", "(situacao = 1) = (nota IS NOT NULL) AND (nota IS NULL OR nota BETWEEN 0 AND 1000)");
                    table.CheckConstraint("ck_avaliacoes_da_etapa_situacao", "situacao IN (1, 2, 3)");
                    table.ForeignKey(
                        name: "fk_avaliacoes_da_etapa_lotes_de_notas_do_enem_lote_id_etapa_pr",
                        columns: x => new { x.lote_id, x.etapa_processo_id },
                        principalSchema: "selecao",
                        principalTable: "lotes_de_notas_do_enem",
                        principalColumns: new[] { "id", "etapa_processo_id" },
                        onDelete: ReferentialAction.Restrict);
                },
                comment: "A nota da etapa de uma inscrição numa oferta de curso concorrida (1ª ou 2ª opção), apurada pelos pesos do grupo de área do curso quando o lote foi importado. Append-only; vale com o lote. A nota por área está nos lançamentos do mesmo lote.");

            migrationBuilder.CreateIndex(
                name: "ix_lotes_de_notas_do_enem_versao_configuracao_id",
                schema: "selecao",
                table: "lotes_de_notas_do_enem",
                column: "versao_configuracao_id");

            migrationBuilder.CreateIndex(
                name: "ix_avaliacoes_da_etapa_inscricao_etapa",
                schema: "selecao",
                table: "avaliacoes_da_etapa",
                columns: new[] { "inscricao_id", "etapa_processo_id" });

            migrationBuilder.CreateIndex(
                name: "ix_avaliacoes_da_etapa_lote_id_etapa_processo_id",
                schema: "selecao",
                table: "avaliacoes_da_etapa",
                columns: new[] { "lote_id", "etapa_processo_id" });

            migrationBuilder.CreateIndex(
                name: "ux_avaliacoes_da_etapa_lote_inscricao_oferta",
                schema: "selecao",
                table: "avaliacoes_da_etapa",
                columns: new[] { "lote_id", "inscricao_id", "oferta_curso_id" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "fk_lotes_de_notas_do_enem_versoes_configuracao_versao_configur",
                schema: "selecao",
                table: "lotes_de_notas_do_enem",
                column: "versao_configuracao_id",
                principalSchema: "selecao",
                principalTable: "versoes_configuracao",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_lotes_de_notas_do_enem_versoes_configuracao_versao_configur",
                schema: "selecao",
                table: "lotes_de_notas_do_enem");

            migrationBuilder.DropTable(
                name: "avaliacoes_da_etapa",
                schema: "selecao");

            migrationBuilder.DropIndex(
                name: "ix_lotes_de_notas_do_enem_versao_configuracao_id",
                schema: "selecao",
                table: "lotes_de_notas_do_enem");

            migrationBuilder.DropColumn(
                name: "versao_configuracao_id",
                schema: "selecao",
                table: "lotes_de_notas_do_enem");
        }
    }
}
