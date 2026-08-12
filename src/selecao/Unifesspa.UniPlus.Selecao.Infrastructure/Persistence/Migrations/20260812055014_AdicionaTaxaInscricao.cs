using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AdicionaTaxaInscricao : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "configuracoes_taxa_inscricao",
                schema: "selecao",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, comment: "Identificador interno (UUIDv7) — não confundir com o Id do processo seletivo, a FK."),
                    processo_seletivo_id = table.Column<Guid>(type: "uuid", nullable: false, comment: "Id do processo seletivo dono desta configuração (FK 1:1, cascade delete)."),
                    cobra = table.Column<bool>(type: "boolean", nullable: false, comment: "Declaração explícita de cobrança de taxa — nunca inferida pela ausência da linha (CA-01)."),
                    valor = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: true, comment: "Valor da taxa em reais, positivo quando cobra=true; sempre nulo quando cobra=false (CA-02/CA-03)."),
                    fundamentos = table.Column<string>(type: "jsonb", nullable: false, comment: "Fundamentos de isenção referenciados (tokens de FundamentoIsencaoCodigo), deduplicados e em ordem canônica; vazio é estado válido (CA-04)."),
                    confirmacao_fundamentos = table.Column<bool>(type: "boolean", nullable: false, comment: "Confirmação explícita do administrador ao referenciar fundamentos de isenção (CA-06) — irrelevante quando fundamentos é vazio."),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, comment: "Instante de criação do registro (auditoria, carimbado pelo AuditableInterceptor)."),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true, comment: "Instante da última atualização do registro (auditoria, carimbado pelo AuditableInterceptor).")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_configuracoes_taxa_inscricao", x => x.id);
                    table.ForeignKey(
                        name: "fk_configuracoes_taxa_inscricao_processos_seletivos_processo_s",
                        column: x => x.processo_seletivo_id,
                        principalSchema: "selecao",
                        principalTable: "processos_seletivos",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                },
                comment: "Configuração de taxa de inscrição e fundamentos de isenção do processo seletivo (issue #1112) — entidade dependente 1:1 de processos_seletivos.");

            migrationBuilder.CreateIndex(
                name: "ix_configuracoes_taxa_inscricao_processo_seletivo_id",
                schema: "selecao",
                table: "configuracoes_taxa_inscricao",
                column: "processo_seletivo_id",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "configuracoes_taxa_inscricao",
                schema: "selecao");
        }
    }
}
