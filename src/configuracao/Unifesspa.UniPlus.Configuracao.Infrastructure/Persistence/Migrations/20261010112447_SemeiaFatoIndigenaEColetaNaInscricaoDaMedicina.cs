using System;
using Microsoft.EntityFrameworkCore.Migrations;

using Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence.Seed;

#nullable disable

namespace Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SemeiaFatoIndigenaEColetaNaInscricaoDaMedicina : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                schema: "configuracao",
                table: "rol_de_fatos_candidato",
                columns: new[] { "id", "ativo", "binding", "cardinalidade", "classificacao_protecao", "codigo", "created_at", "created_by", "dependencias", "descricao", "dominio", "escopo", "finalidade_tratamento", "fonte_valores", "formato", "hipotese_legal", "nome", "origem", "ponto_resolucao", "regras_padrao", "sistema", "updated_at", "updated_by" },
                values: new object[] { new Guid("fa700000-0000-7000-8000-000000000050"), true, "CAMPO_FORMULARIO:INDIGENA", "ESCALAR", "SENSIVEL", "INDIGENA", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, Array.Empty<string>(), null, "BOOLEANO", "CANDIDATO", "Enquadramento do candidato na reserva de vagas da Lei nº 12.711/2012 e nas ações afirmativas do processo seletivo.", null, null, "CUMPRIMENTO_OBRIGACAO_LEGAL", "Indígena", "DECLARADO", "INSCRICAO", "[]", true, null, null });

            // O modelo de inscrição passa a coletar o fato; a inserção da semente ignora o modelo que
            // já existe, então ele sai antes de ser gravado de novo.
            foreach (string comando in SementePsrMedicina2027.ComandosDeRemocaoDosModelos().Concat(SementePsrMedicina2027.ComandosDosModelos()))
            {
                migrationBuilder.Sql(comando);
            }
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                schema: "configuracao",
                table: "rol_de_fatos_candidato",
                keyColumn: "id",
                keyValue: new Guid("fa700000-0000-7000-8000-000000000050"));
        }
    }
}
