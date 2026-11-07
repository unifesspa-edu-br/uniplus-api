using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AtualizaORotuloDaFaixaEtaria : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "rol_de_fatos_candidato",
                keyColumn: "id",
                keyValue: new Guid("fa700000-0000-7000-8000-000000000006"),
                columns: new[] { "descricao", "nome" },
                values: new object[] { "Idade em anos completos do candidato na data de referência dos fatos do processo, calculada pelo sistema a partir da data de nascimento, para verificar os requisitos de participação.", "Idade" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "rol_de_fatos_candidato",
                keyColumn: "id",
                keyValue: new Guid("fa700000-0000-7000-8000-000000000006"),
                columns: new[] { "descricao", "nome" },
                values: new object[] { "Faixa de idade em que o candidato se enquadra, calculada pelo sistema a partir da data de nascimento, para verificar os requisitos de participação.", "Faixa etária" });
        }
    }
}
