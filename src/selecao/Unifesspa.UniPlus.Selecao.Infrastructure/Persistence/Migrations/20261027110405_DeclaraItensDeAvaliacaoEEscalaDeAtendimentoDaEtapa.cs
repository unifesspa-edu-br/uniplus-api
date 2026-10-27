using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class DeclaraItensDeAvaliacaoEEscalaDeAtendimentoDaEtapa : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "escala_de_atendimento",
                schema: "selecao",
                table: "etapas_processo",
                type: "jsonb",
                nullable: true,
                comment: "Níveis de atendimento (rótulo e fração da pontuação do item), do que mais vale ao que menos vale; ausente quando a banca digita a nota.");

            migrationBuilder.CreateTable(
                name: "itens_de_avaliacao_da_etapa",
                schema: "selecao",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    etapa_processo_id = table.Column<Guid>(type: "uuid", nullable: false),
                    rotulo = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    pontuacao_maxima = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false, comment: "O quanto o item vale da nota máxima da etapa."),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_itens_de_avaliacao_da_etapa", x => x.id);
                    table.CheckConstraint("ck_itens_de_avaliacao_da_etapa_pontuacao_positiva", "pontuacao_maxima > 0");
                    table.ForeignKey(
                        name: "fk_itens_de_avaliacao_da_etapa_etapas_processo_etapa_processo_",
                        column: x => x.etapa_processo_id,
                        principalSchema: "selecao",
                        principalTable: "etapas_processo",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                },
                comment: "Itens de avaliação (critérios ou blocos) da etapa; o id é a identidade a que lançamento e recurso apontam.");

            migrationBuilder.CreateIndex(
                name: "ix_itens_de_avaliacao_da_etapa_etapa_processo_id",
                schema: "selecao",
                table: "itens_de_avaliacao_da_etapa",
                column: "etapa_processo_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "itens_de_avaliacao_da_etapa",
                schema: "selecao");

            migrationBuilder.DropColumn(
                name: "escala_de_atendimento",
                schema: "selecao",
                table: "etapas_processo");
        }
    }
}
