using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SemeiaAOpcaoPelasCotasDaLeiEAVagaPorAcrescimo : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                schema: "configuracao",
                table: "rol_de_fatos_candidato",
                columns: new[] { "id", "ativo", "binding", "cardinalidade", "classificacao_protecao", "codigo", "created_at", "created_by", "dependencias", "descricao", "dominio", "escopo", "finalidade_tratamento", "fonte_valores", "formato", "hipotese_legal", "nome", "origem", "ponto_resolucao", "regras_padrao", "sistema", "updated_at", "updated_by" },
                values: new object[,]
                {
                    { new Guid("fa700000-0000-7000-8000-000000000054"), true, "REGRA_DERIVACAO:OPTOU_PELAS_COTAS_DA_LEI", "ESCALAR", "PESSOAL", "OPTOU_PELAS_COTAS_DA_LEI", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, Array.Empty<string>(), "Se o candidato é egresso de escola pública e optou por concorrer às vagas de escola pública.", "BOOLEANO", "CANDIDATO", "Enquadramento do candidato na reserva de vagas da Lei nº 12.711/2012 e nas ações afirmativas do processo seletivo.", null, null, "CUMPRIMENTO_OBRIGACAO_LEGAL", "Optou pelas cotas da Lei nº 12.711/2012", "DERIVADO", "INSCRICAO", "[{\"contribui\":null,\"quando\":[[{\"fato\":\"EGRESSO_ESCOLA_PUBLICA\",\"operador\":\"IGUAL\",\"valor\":true},{\"fato\":\"CONCORRER_EP\",\"operador\":\"IGUAL\",\"valor\":true}]]}]", true, null, null },
                    { new Guid("fa700000-0000-7000-8000-000000000055"), true, "CAMPO_FORMULARIO:CONCORRER_VAGA_POR_ACRESCIMO", "ESCALAR", "SENSIVEL", "CONCORRER_VAGA_POR_ACRESCIMO", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, Array.Empty<string>(), null, "BOOLEANO", "CANDIDATO", "Enquadramento do candidato na reserva de vagas da Lei nº 12.711/2012 e nas ações afirmativas do processo seletivo.", null, null, "CUMPRIMENTO_OBRIGACAO_LEGAL", "Deseja concorrer às vagas por acréscimo para indígenas e quilombolas", "DECLARADO", "INSCRICAO", "[]", true, null, null }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                schema: "configuracao",
                table: "rol_de_fatos_candidato",
                keyColumn: "id",
                keyValue: new Guid("fa700000-0000-7000-8000-000000000054"));

            migrationBuilder.DeleteData(
                schema: "configuracao",
                table: "rol_de_fatos_candidato",
                keyColumn: "id",
                keyValue: new Guid("fa700000-0000-7000-8000-000000000055"));
        }
    }
}
