using System;
using Microsoft.EntityFrameworkCore.Migrations;

using Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence.Seed;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class PerguntaOrigemEscolarEmDuasQuestoesEDerivaEgresso : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // A forma de conclusão do ensino médio passa a fato de sistema, com o mesmo código. Sai antes
            // a que a semente do PSR gravava como fato do administrador, e os modelos do PSR, gravados
            // de novo no fim com as duas perguntas da origem escolar.
            foreach (string comando in SementePsrMedicina2027.ComandosQueRetiramAFormaDeConclusaoDaSemente()
                .Concat(SementePsrMedicina2027.ComandosDeRemocaoDosModelos()))
            {
                migrationBuilder.Sql(comando);
            }

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "rol_de_fatos_candidato",
                keyColumn: "id",
                keyValue: new Guid("fa700000-0000-7000-8000-000000000004"),
                columns: new[] { "binding", "origem", "regras_padrao" },
                values: new object[] { "REGRA_DERIVACAO:EGRESSO_ESCOLA_PUBLICA", "DERIVADO", "[{\"contribui\":null,\"quando\":[[{\"fato\":\"ONDE_CURSOU_EM\",\"operador\":\"EM\",\"valor\":[\"SOMENTE_PUBLICA\",\"COMUNITARIA_CAMPO\",\"PUBLICA_E_COMUNITARIA\"]}],[{\"fato\":\"ONDE_CURSOU_EM\",\"operador\":\"IGUAL\",\"valor\":\"CERTIFICACAO\"},{\"fato\":\"FORMA_CONCLUSAO_EM\",\"operador\":\"EM\",\"valor\":[\"ENCCEJA\",\"PROFICIENCIA\",\"ENEM\"]}]]}]" });

            migrationBuilder.InsertData(
                schema: "configuracao",
                table: "rol_de_fatos_candidato",
                columns: new[] { "id", "ativo", "binding", "cardinalidade", "classificacao_protecao", "codigo", "created_at", "created_by", "dependencias", "descricao", "dominio", "escopo", "finalidade_tratamento", "fonte_valores", "formato", "hipotese_legal", "nome", "origem", "ponto_resolucao", "regras_padrao", "sistema", "updated_at", "updated_by" },
                values: new object[,]
                {
                    { new Guid("fa700000-0000-7000-8000-000000000046"), true, "CAMPO_FORMULARIO:FORMA_CONCLUSAO_EM", "ESCALAR", "PESSOAL", "FORMA_CONCLUSAO_EM", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, Array.Empty<string>(), "Como o candidato concluiu o ensino médio: curso regular, Educação de Jovens e Adultos, ENCCEJA, exame de proficiência ou ENEM.", "CATEGORICO", "CANDIDATO", "Enquadramento do candidato na reserva de vagas da Lei nº 12.711/2012 e nas ações afirmativas do processo seletivo.", "GLOBAL", null, "CUMPRIMENTO_OBRIGACAO_LEGAL", "Forma de conclusão do ensino médio", "DECLARADO", "INSCRICAO", "[]", true, null, null },
                    { new Guid("fa700000-0000-7000-8000-000000000047"), true, "CAMPO_FORMULARIO:ONDE_CURSOU_EM", "ESCALAR", "PESSOAL", "ONDE_CURSOU_EM", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, Array.Empty<string>(), "Em que rede de ensino o candidato cursou o ensino médio, ou se o concluiu por certificação sem frequentá-lo.", "CATEGORICO", "CANDIDATO", "Enquadramento do candidato na reserva de vagas da Lei nº 12.711/2012 e nas ações afirmativas do processo seletivo.", "GLOBAL", null, "CUMPRIMENTO_OBRIGACAO_LEGAL", "Onde cursou o ensino médio", "DECLARADO", "INSCRICAO", "[]", true, null, null }
                });

            migrationBuilder.InsertData(
                schema: "configuracao",
                table: "fato_valor_dominio",
                columns: new[] { "id", "ativo", "codigo", "created_at", "descricao", "fato_candidato_id", "ordem", "orientacao", "updated_at" },
                values: new object[,]
                {
                    { new Guid("fa70d000-0000-7000-8000-000000000040"), true, "REGULAR", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Ensino médio regular", new Guid("fa700000-0000-7000-8000-000000000046"), 0, null, null },
                    { new Guid("fa70d000-0000-7000-8000-000000000041"), true, "EJA", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Educação de Jovens e Adultos (EJA)", new Guid("fa700000-0000-7000-8000-000000000046"), 1, null, null },
                    { new Guid("fa70d000-0000-7000-8000-000000000042"), true, "ENCCEJA", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Certificação pelo ENCCEJA", new Guid("fa700000-0000-7000-8000-000000000046"), 2, null, null },
                    { new Guid("fa70d000-0000-7000-8000-000000000043"), true, "PROFICIENCIA", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Exame de proficiência dos sistemas estaduais de ensino", new Guid("fa700000-0000-7000-8000-000000000046"), 3, null, null },
                    { new Guid("fa70d000-0000-7000-8000-000000000044"), true, "ENEM", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Certificação pelo ENEM", new Guid("fa700000-0000-7000-8000-000000000046"), 4, null, null },
                    { new Guid("fa70d000-0000-7000-8000-000000000045"), true, "SOMENTE_PUBLICA", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Somente em escola pública", new Guid("fa700000-0000-7000-8000-000000000047"), 0, "Todos os anos que você cursou, mesmo que tenha concluído por certificação sem cursar todos.", null },
                    { new Guid("fa70d000-0000-7000-8000-000000000046"), true, "COMUNITARIA_CAMPO", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Escola comunitária do campo conveniada com o poder público", new Guid("fa700000-0000-7000-8000-000000000047"), 1, null, null },
                    { new Guid("fa70d000-0000-7000-8000-000000000047"), true, "PRIVADA_BOLSA_INTEGRAL", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Escola privada com bolsa integral", new Guid("fa700000-0000-7000-8000-000000000047"), 2, null, null },
                    { new Guid("fa70d000-0000-7000-8000-000000000048"), true, "PRIVADA_BOLSA_PARCIAL", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Escola privada com bolsa parcial", new Guid("fa700000-0000-7000-8000-000000000047"), 3, null, null },
                    { new Guid("fa70d000-0000-7000-8000-000000000049"), true, "PRIVADA", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Escola privada", new Guid("fa700000-0000-7000-8000-000000000047"), 4, null, null },
                    { new Guid("fa70d000-0000-7000-8000-000000000050"), true, "PUBLICA_E_PRIVADA_BOLSA_INTEGRAL", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Parte em escola pública e parte em escola privada com bolsa integral", new Guid("fa700000-0000-7000-8000-000000000047"), 5, null, null },
                    { new Guid("fa70d000-0000-7000-8000-000000000051"), true, "PUBLICA_E_FORA_DA_REDE", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Parte na rede pública (escola pública ou comunitária do campo conveniada) e parte fora dela, nos demais casos", new Guid("fa700000-0000-7000-8000-000000000047"), 6, "A parte fora da rede pública em escola privada sem bolsa integral, no Sistema S ou no exterior.", null },
                    { new Guid("fa70d000-0000-7000-8000-000000000052"), true, "PUBLICA_E_COMUNITARIA", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Parte em escola pública e parte em escola comunitária do campo conveniada", new Guid("fa700000-0000-7000-8000-000000000047"), 7, null, null },
                    { new Guid("fa70d000-0000-7000-8000-000000000053"), true, "SISTEMA_S", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Sistema S", new Guid("fa700000-0000-7000-8000-000000000047"), 8, "Escolas do SESI, SENAI, SESC, SENAC e demais serviços sociais autônomos.", null },
                    { new Guid("fa70d000-0000-7000-8000-000000000054"), true, "EXTERIOR", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Exterior", new Guid("fa700000-0000-7000-8000-000000000047"), 9, null, null },
                    { new Guid("fa70d000-0000-7000-8000-000000000055"), true, "MAIS_DE_UM_FORA_DA_REDE", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Mais de um tipo de escola fora da rede pública", new Guid("fa700000-0000-7000-8000-000000000047"), 10, "Escola privada, Sistema S ou exterior, sem passar pela rede pública.", null },
                    { new Guid("fa70d000-0000-7000-8000-000000000056"), true, "CERTIFICACAO", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Concluí por certificação", new Guid("fa700000-0000-7000-8000-000000000047"), 11, "Para quem concluiu pelo ENCCEJA, por exame de proficiência ou pelo ENEM sem frequentar o ensino médio.", null }
                });

            foreach (string comando in SementePsrMedicina2027.ComandosDosModelos())
            {
                migrationBuilder.Sql(comando);
            }
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                schema: "configuracao",
                table: "fato_valor_dominio",
                keyColumn: "id",
                keyValue: new Guid("fa70d000-0000-7000-8000-000000000040"));

            migrationBuilder.DeleteData(
                schema: "configuracao",
                table: "fato_valor_dominio",
                keyColumn: "id",
                keyValue: new Guid("fa70d000-0000-7000-8000-000000000041"));

            migrationBuilder.DeleteData(
                schema: "configuracao",
                table: "fato_valor_dominio",
                keyColumn: "id",
                keyValue: new Guid("fa70d000-0000-7000-8000-000000000042"));

            migrationBuilder.DeleteData(
                schema: "configuracao",
                table: "fato_valor_dominio",
                keyColumn: "id",
                keyValue: new Guid("fa70d000-0000-7000-8000-000000000043"));

            migrationBuilder.DeleteData(
                schema: "configuracao",
                table: "fato_valor_dominio",
                keyColumn: "id",
                keyValue: new Guid("fa70d000-0000-7000-8000-000000000044"));

            migrationBuilder.DeleteData(
                schema: "configuracao",
                table: "fato_valor_dominio",
                keyColumn: "id",
                keyValue: new Guid("fa70d000-0000-7000-8000-000000000045"));

            migrationBuilder.DeleteData(
                schema: "configuracao",
                table: "fato_valor_dominio",
                keyColumn: "id",
                keyValue: new Guid("fa70d000-0000-7000-8000-000000000046"));

            migrationBuilder.DeleteData(
                schema: "configuracao",
                table: "fato_valor_dominio",
                keyColumn: "id",
                keyValue: new Guid("fa70d000-0000-7000-8000-000000000047"));

            migrationBuilder.DeleteData(
                schema: "configuracao",
                table: "fato_valor_dominio",
                keyColumn: "id",
                keyValue: new Guid("fa70d000-0000-7000-8000-000000000048"));

            migrationBuilder.DeleteData(
                schema: "configuracao",
                table: "fato_valor_dominio",
                keyColumn: "id",
                keyValue: new Guid("fa70d000-0000-7000-8000-000000000049"));

            migrationBuilder.DeleteData(
                schema: "configuracao",
                table: "fato_valor_dominio",
                keyColumn: "id",
                keyValue: new Guid("fa70d000-0000-7000-8000-000000000050"));

            migrationBuilder.DeleteData(
                schema: "configuracao",
                table: "fato_valor_dominio",
                keyColumn: "id",
                keyValue: new Guid("fa70d000-0000-7000-8000-000000000051"));

            migrationBuilder.DeleteData(
                schema: "configuracao",
                table: "fato_valor_dominio",
                keyColumn: "id",
                keyValue: new Guid("fa70d000-0000-7000-8000-000000000052"));

            migrationBuilder.DeleteData(
                schema: "configuracao",
                table: "fato_valor_dominio",
                keyColumn: "id",
                keyValue: new Guid("fa70d000-0000-7000-8000-000000000053"));

            migrationBuilder.DeleteData(
                schema: "configuracao",
                table: "fato_valor_dominio",
                keyColumn: "id",
                keyValue: new Guid("fa70d000-0000-7000-8000-000000000054"));

            migrationBuilder.DeleteData(
                schema: "configuracao",
                table: "fato_valor_dominio",
                keyColumn: "id",
                keyValue: new Guid("fa70d000-0000-7000-8000-000000000055"));

            migrationBuilder.DeleteData(
                schema: "configuracao",
                table: "fato_valor_dominio",
                keyColumn: "id",
                keyValue: new Guid("fa70d000-0000-7000-8000-000000000056"));

            migrationBuilder.DeleteData(
                schema: "configuracao",
                table: "rol_de_fatos_candidato",
                keyColumn: "id",
                keyValue: new Guid("fa700000-0000-7000-8000-000000000046"));

            migrationBuilder.DeleteData(
                schema: "configuracao",
                table: "rol_de_fatos_candidato",
                keyColumn: "id",
                keyValue: new Guid("fa700000-0000-7000-8000-000000000047"));

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "rol_de_fatos_candidato",
                keyColumn: "id",
                keyValue: new Guid("fa700000-0000-7000-8000-000000000004"),
                columns: new[] { "binding", "origem", "regras_padrao" },
                values: new object[] { "CAMPO_FORMULARIO:EGRESSO_ESCOLA_PUBLICA", "DECLARADO", "[]" });
        }
    }
}
