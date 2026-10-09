using System;
using Microsoft.EntityFrameworkCore.Migrations;

using Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence.Seed;

#nullable disable

namespace Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SemeiaModeloDeIsencaoDaTaxa : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                schema: "configuracao",
                table: "rol_de_fatos_candidato",
                columns: new[] { "id", "ativo", "binding", "cardinalidade", "classificacao_protecao", "codigo", "created_at", "created_by", "dependencias", "descricao", "dominio", "escopo", "finalidade_tratamento", "fonte_valores", "formato", "hipotese_legal", "nome", "origem", "ponto_resolucao", "regras_padrao", "sistema", "updated_at", "updated_by" },
                values: new object[] { new Guid("fa700000-0000-7000-8000-000000000048"), true, "CAMPO_FORMULARIO:RENDA_ATE_UM_SALARIO_MINIMO_E_MEIO", "ESCALAR", "PESSOAL", "RENDA_ATE_UM_SALARIO_MINIMO_E_MEIO", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, Array.Empty<string>(), "Se a renda familiar per capita do candidato é igual ou inferior a um salário mínimo e meio, como ele declara no pedido de isenção da taxa.", "BOOLEANO", "CANDIDATO", "Análise do pedido de isenção da taxa de inscrição do processo seletivo.", null, null, "CUMPRIMENTO_OBRIGACAO_LEGAL", "Renda familiar per capita de até um salário mínimo e meio", "DECLARADO", "SOLICITACAO_ISENCAO", "[]", true, null, null });

            // O formulário de solicitação de isenção da taxa, ativo e sem tipo de processo, fica
            // disponível ao subir o sistema para qualquer processo que cobre taxa partir dele. O
            // conteúdo é conferido pelo domínio ao ser construído, e a inserção ignora o que já existe.
            foreach (string comando in SementeIsencaoDaTaxa.Comandos())
            {
                migrationBuilder.Sql(comando);
            }
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            foreach (string comando in SementeIsencaoDaTaxa.ComandosDeRemocao())
            {
                migrationBuilder.Sql(comando);
            }

            migrationBuilder.DeleteData(
                schema: "configuracao",
                table: "rol_de_fatos_candidato",
                keyColumn: "id",
                keyValue: new Guid("fa700000-0000-7000-8000-000000000048"));
        }
    }
}
