using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AdicionaOrientacaoAoValorDeDominio : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "orientacao",
                schema: "configuracao",
                table: "fato_valor_dominio",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "fato_valor_dominio",
                keyColumn: "id",
                keyValue: new Guid("fa70d000-0000-7000-8000-000000000001"),
                columns: new[] { "descricao", "orientacao" },
                values: new object[] { "Branca", null });

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "fato_valor_dominio",
                keyColumn: "id",
                keyValue: new Guid("fa70d000-0000-7000-8000-000000000002"),
                columns: new[] { "descricao", "orientacao" },
                values: new object[] { "Preta", "Quem concorre às vagas para pretos e pardos passa pela heteroidentificação da Unifesspa (Lei 12.711/2012)." });

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "fato_valor_dominio",
                keyColumn: "id",
                keyValue: new Guid("fa70d000-0000-7000-8000-000000000003"),
                columns: new[] { "descricao", "orientacao" },
                values: new object[] { "Parda", "Quem concorre às vagas para pretos e pardos passa pela heteroidentificação da Unifesspa (Lei 12.711/2012)." });

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "fato_valor_dominio",
                keyColumn: "id",
                keyValue: new Guid("fa70d000-0000-7000-8000-000000000004"),
                columns: new[] { "descricao", "orientacao" },
                values: new object[] { "Amarela", "Pessoa de origem asiática." });

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "fato_valor_dominio",
                keyColumn: "id",
                keyValue: new Guid("fa70d000-0000-7000-8000-000000000005"),
                columns: new[] { "descricao", "orientacao" },
                values: new object[] { "Indígena", "Pessoa que se reconhece como pertencente a um povo indígena." });

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "fato_valor_dominio",
                keyColumn: "id",
                keyValue: new Guid("fa70d000-0000-7000-8000-000000000006"),
                columns: new[] { "descricao", "orientacao" },
                values: new object[] { "Prefiro não informar", "Sem essa informação, você não concorre às vagas reservadas por cor ou raça." });

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "fato_valor_dominio",
                keyColumn: "id",
                keyValue: new Guid("fa70d000-0000-7000-8000-000000000007"),
                columns: new[] { "descricao", "orientacao" },
                values: new object[] { "Feminino", null });

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "fato_valor_dominio",
                keyColumn: "id",
                keyValue: new Guid("fa70d000-0000-7000-8000-000000000008"),
                columns: new[] { "descricao", "orientacao" },
                values: new object[] { "Masculino", null });

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "fato_valor_dominio",
                keyColumn: "id",
                keyValue: new Guid("fa70d000-0000-7000-8000-000000000009"),
                columns: new[] { "descricao", "orientacao" },
                values: new object[] { "Intersexo", "Pessoa com variação natural das características sexuais." });

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "fato_valor_dominio",
                keyColumn: "id",
                keyValue: new Guid("fa70d000-0000-7000-8000-000000000010"),
                columns: new[] { "descricao", "orientacao" },
                values: new object[] { "Brasileiro nato", "Nascido no Brasil ou nas condições previstas pela Constituição." });

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "fato_valor_dominio",
                keyColumn: "id",
                keyValue: new Guid("fa70d000-0000-7000-8000-000000000011"),
                columns: new[] { "descricao", "orientacao" },
                values: new object[] { "Brasileiro naturalizado", "Com processo de naturalização reconhecido." });

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "fato_valor_dominio",
                keyColumn: "id",
                keyValue: new Guid("fa70d000-0000-7000-8000-000000000012"),
                columns: new[] { "descricao", "orientacao" },
                values: new object[] { "Estrangeiro", null });

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "fato_valor_dominio",
                keyColumn: "id",
                keyValue: new Guid("fa70d000-0000-7000-8000-000000000013"),
                columns: new[] { "descricao", "orientacao" },
                values: new object[] { "O próprio candidato", null });

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "fato_valor_dominio",
                keyColumn: "id",
                keyValue: new Guid("fa70d000-0000-7000-8000-000000000014"),
                columns: new[] { "descricao", "orientacao" },
                values: new object[] { "Cônjuge ou companheiro(a)", null });

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "fato_valor_dominio",
                keyColumn: "id",
                keyValue: new Guid("fa70d000-0000-7000-8000-000000000015"),
                columns: new[] { "descricao", "orientacao" },
                values: new object[] { "Filho(a) ou enteado(a)", null });

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "fato_valor_dominio",
                keyColumn: "id",
                keyValue: new Guid("fa70d000-0000-7000-8000-000000000016"),
                columns: new[] { "descricao", "orientacao" },
                values: new object[] { "Pai ou mãe", null });

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "fato_valor_dominio",
                keyColumn: "id",
                keyValue: new Guid("fa70d000-0000-7000-8000-000000000017"),
                columns: new[] { "descricao", "orientacao" },
                values: new object[] { "Padrasto ou madrasta", null });

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "fato_valor_dominio",
                keyColumn: "id",
                keyValue: new Guid("fa70d000-0000-7000-8000-000000000018"),
                columns: new[] { "descricao", "orientacao" },
                values: new object[] { "Irmão ou irmã", null });

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "fato_valor_dominio",
                keyColumn: "id",
                keyValue: new Guid("fa70d000-0000-7000-8000-000000000019"),
                columns: new[] { "descricao", "orientacao" },
                values: new object[] { "Avô ou avó", null });

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "fato_valor_dominio",
                keyColumn: "id",
                keyValue: new Guid("fa70d000-0000-7000-8000-000000000020"),
                columns: new[] { "descricao", "orientacao" },
                values: new object[] { "Neto(a)", null });

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "fato_valor_dominio",
                keyColumn: "id",
                keyValue: new Guid("fa70d000-0000-7000-8000-000000000021"),
                columns: new[] { "descricao", "orientacao" },
                values: new object[] { "Sogro ou sogra", null });

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "fato_valor_dominio",
                keyColumn: "id",
                keyValue: new Guid("fa70d000-0000-7000-8000-000000000022"),
                columns: new[] { "descricao", "orientacao" },
                values: new object[] { "Genro ou nora", null });

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "fato_valor_dominio",
                keyColumn: "id",
                keyValue: new Guid("fa70d000-0000-7000-8000-000000000023"),
                columns: new[] { "descricao", "orientacao" },
                values: new object[] { "Outro parente", null });

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "fato_valor_dominio",
                keyColumn: "id",
                keyValue: new Guid("fa70d000-0000-7000-8000-000000000024"),
                columns: new[] { "descricao", "orientacao" },
                values: new object[] { "Sem parentesco", "Pessoa sem parentesco com o candidato que integra o grupo familiar." });

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "fato_valor_dominio",
                keyColumn: "id",
                keyValue: new Guid("fa70d000-0000-7000-8000-000000000025"),
                columns: new[] { "descricao", "orientacao" },
                values: new object[] { "Passaporte", null });

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "fato_valor_dominio",
                keyColumn: "id",
                keyValue: new Guid("fa70d000-0000-7000-8000-000000000026"),
                columns: new[] { "descricao", "orientacao" },
                values: new object[] { "Registro Nacional Migratório (RNM)", null });

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "fato_valor_dominio",
                keyColumn: "id",
                keyValue: new Guid("fa70d000-0000-7000-8000-000000000027"),
                columns: new[] { "descricao", "orientacao" },
                values: new object[] { "Solteiro(a)", null });

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "fato_valor_dominio",
                keyColumn: "id",
                keyValue: new Guid("fa70d000-0000-7000-8000-000000000028"),
                columns: new[] { "descricao", "orientacao" },
                values: new object[] { "Casado(a)", null });

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "fato_valor_dominio",
                keyColumn: "id",
                keyValue: new Guid("fa70d000-0000-7000-8000-000000000029"),
                columns: new[] { "descricao", "orientacao" },
                values: new object[] { "Em união estável", null });

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "fato_valor_dominio",
                keyColumn: "id",
                keyValue: new Guid("fa70d000-0000-7000-8000-000000000030"),
                columns: new[] { "descricao", "orientacao" },
                values: new object[] { "Separado(a) judicialmente", null });

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "fato_valor_dominio",
                keyColumn: "id",
                keyValue: new Guid("fa70d000-0000-7000-8000-000000000031"),
                columns: new[] { "descricao", "orientacao" },
                values: new object[] { "Divorciado(a)", null });

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "fato_valor_dominio",
                keyColumn: "id",
                keyValue: new Guid("fa70d000-0000-7000-8000-000000000032"),
                columns: new[] { "descricao", "orientacao" },
                values: new object[] { "Viúvo(a)", null });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "orientacao",
                schema: "configuracao",
                table: "fato_valor_dominio");

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "fato_valor_dominio",
                keyColumn: "id",
                keyValue: new Guid("fa70d000-0000-7000-8000-000000000001"),
                column: "descricao",
                value: "Autodeclaração de cor/raça branca.");

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "fato_valor_dominio",
                keyColumn: "id",
                keyValue: new Guid("fa70d000-0000-7000-8000-000000000002"),
                column: "descricao",
                value: "Autodeclaração de cor/raça preta, conforme Lei 12.711/2012 e resoluções da Unifesspa sobre heteroidentificação.");

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "fato_valor_dominio",
                keyColumn: "id",
                keyValue: new Guid("fa70d000-0000-7000-8000-000000000003"),
                column: "descricao",
                value: "Autodeclaração de cor/raça parda, conforme Lei 12.711/2012 e resoluções da Unifesspa sobre heteroidentificação.");

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "fato_valor_dominio",
                keyColumn: "id",
                keyValue: new Guid("fa70d000-0000-7000-8000-000000000004"),
                column: "descricao",
                value: "Autodeclaração de cor/raça amarela (ascendência asiática).");

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "fato_valor_dominio",
                keyColumn: "id",
                keyValue: new Guid("fa70d000-0000-7000-8000-000000000005"),
                column: "descricao",
                value: "Autodeclaração de povo indígena.");

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "fato_valor_dominio",
                keyColumn: "id",
                keyValue: new Guid("fa70d000-0000-7000-8000-000000000006"),
                column: "descricao",
                value: "Candidato optou por não informar cor ou raça.");

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "fato_valor_dominio",
                keyColumn: "id",
                keyValue: new Guid("fa70d000-0000-7000-8000-000000000007"),
                column: "descricao",
                value: "Sexo feminino.");

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "fato_valor_dominio",
                keyColumn: "id",
                keyValue: new Guid("fa70d000-0000-7000-8000-000000000008"),
                column: "descricao",
                value: "Sexo masculino.");

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "fato_valor_dominio",
                keyColumn: "id",
                keyValue: new Guid("fa70d000-0000-7000-8000-000000000009"),
                column: "descricao",
                value: "Pessoa intersexo — variação natural das características sexuais.");

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "fato_valor_dominio",
                keyColumn: "id",
                keyValue: new Guid("fa70d000-0000-7000-8000-000000000010"),
                column: "descricao",
                value: "Brasileiro nato, nascido no Brasil ou nas condições previstas pela Constituição.");

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "fato_valor_dominio",
                keyColumn: "id",
                keyValue: new Guid("fa70d000-0000-7000-8000-000000000011"),
                column: "descricao",
                value: "Brasileiro naturalizado, conforme processo de naturalização reconhecido.");

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "fato_valor_dominio",
                keyColumn: "id",
                keyValue: new Guid("fa70d000-0000-7000-8000-000000000012"),
                column: "descricao",
                value: "Cidadão estrangeiro, não brasileiro.");

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "fato_valor_dominio",
                keyColumn: "id",
                keyValue: new Guid("fa70d000-0000-7000-8000-000000000013"),
                column: "descricao",
                value: "O próprio candidato.");

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "fato_valor_dominio",
                keyColumn: "id",
                keyValue: new Guid("fa70d000-0000-7000-8000-000000000014"),
                column: "descricao",
                value: "Cônjuge ou companheiro(a) do candidato.");

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "fato_valor_dominio",
                keyColumn: "id",
                keyValue: new Guid("fa70d000-0000-7000-8000-000000000015"),
                column: "descricao",
                value: "Filho(a) ou enteado(a) do candidato.");

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "fato_valor_dominio",
                keyColumn: "id",
                keyValue: new Guid("fa70d000-0000-7000-8000-000000000016"),
                column: "descricao",
                value: "Pai ou mãe do candidato.");

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "fato_valor_dominio",
                keyColumn: "id",
                keyValue: new Guid("fa70d000-0000-7000-8000-000000000017"),
                column: "descricao",
                value: "Padrasto ou madrasta do candidato.");

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "fato_valor_dominio",
                keyColumn: "id",
                keyValue: new Guid("fa70d000-0000-7000-8000-000000000018"),
                column: "descricao",
                value: "Irmão ou irmã do candidato.");

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "fato_valor_dominio",
                keyColumn: "id",
                keyValue: new Guid("fa70d000-0000-7000-8000-000000000019"),
                column: "descricao",
                value: "Avô ou avó do candidato.");

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "fato_valor_dominio",
                keyColumn: "id",
                keyValue: new Guid("fa70d000-0000-7000-8000-000000000020"),
                column: "descricao",
                value: "Neto(a) do candidato.");

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "fato_valor_dominio",
                keyColumn: "id",
                keyValue: new Guid("fa70d000-0000-7000-8000-000000000021"),
                column: "descricao",
                value: "Sogro ou sogra do candidato.");

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "fato_valor_dominio",
                keyColumn: "id",
                keyValue: new Guid("fa70d000-0000-7000-8000-000000000022"),
                column: "descricao",
                value: "Genro ou nora do candidato.");

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "fato_valor_dominio",
                keyColumn: "id",
                keyValue: new Guid("fa70d000-0000-7000-8000-000000000023"),
                column: "descricao",
                value: "Outro parente do candidato.");

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "fato_valor_dominio",
                keyColumn: "id",
                keyValue: new Guid("fa70d000-0000-7000-8000-000000000024"),
                column: "descricao",
                value: "Pessoa sem parentesco com o candidato que integra o grupo familiar.");

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "fato_valor_dominio",
                keyColumn: "id",
                keyValue: new Guid("fa70d000-0000-7000-8000-000000000025"),
                column: "descricao",
                value: "Passaporte.");

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "fato_valor_dominio",
                keyColumn: "id",
                keyValue: new Guid("fa70d000-0000-7000-8000-000000000026"),
                column: "descricao",
                value: "Registro Nacional Migratório.");

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "fato_valor_dominio",
                keyColumn: "id",
                keyValue: new Guid("fa70d000-0000-7000-8000-000000000027"),
                column: "descricao",
                value: "Solteiro(a).");

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "fato_valor_dominio",
                keyColumn: "id",
                keyValue: new Guid("fa70d000-0000-7000-8000-000000000028"),
                column: "descricao",
                value: "Casado(a).");

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "fato_valor_dominio",
                keyColumn: "id",
                keyValue: new Guid("fa70d000-0000-7000-8000-000000000029"),
                column: "descricao",
                value: "Em união estável.");

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "fato_valor_dominio",
                keyColumn: "id",
                keyValue: new Guid("fa70d000-0000-7000-8000-000000000030"),
                column: "descricao",
                value: "Separado(a) judicialmente.");

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "fato_valor_dominio",
                keyColumn: "id",
                keyValue: new Guid("fa70d000-0000-7000-8000-000000000031"),
                column: "descricao",
                value: "Divorciado(a).");

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "fato_valor_dominio",
                keyColumn: "id",
                keyValue: new Guid("fa70d000-0000-7000-8000-000000000032"),
                column: "descricao",
                value: "Viúvo(a).");
        }
    }
}
