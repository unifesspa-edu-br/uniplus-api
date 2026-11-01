using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class DeclaraComponenteDeExibicaoEDescricaoObrigatoriaDoFato : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "descricao",
                schema: "configuracao",
                table: "rol_de_fatos_candidato",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "character varying(1000)",
                oldMaxLength: 1000,
                oldNullable: true);

            migrationBuilder.AddColumn<string>(
                name: "componente_exibicao",
                schema: "configuracao",
                table: "rol_de_fatos_candidato",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "rol_de_fatos_candidato",
                keyColumn: "id",
                keyValue: new Guid("fa700000-0000-7000-8000-000000000001"),
                columns: new[] { "componente_exibicao", "descricao" },
                values: new object[] { null, "Cor ou raça que o candidato declara. É o dado de que depende a concorrência às vagas reservadas a pretos, pardos e indígenas." });

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "rol_de_fatos_candidato",
                keyColumn: "id",
                keyValue: new Guid("fa700000-0000-7000-8000-000000000002"),
                columns: new[] { "componente_exibicao", "descricao" },
                values: new object[] { null, "Se o candidato se declara quilombola. É o dado de que depende a concorrência às vagas reservadas a quilombolas." });

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "rol_de_fatos_candidato",
                keyColumn: "id",
                keyValue: new Guid("fa700000-0000-7000-8000-000000000003"),
                columns: new[] { "componente_exibicao", "descricao" },
                values: new object[] { null, "Se o candidato declara ser pessoa com deficiência. É o dado de que depende a concorrência às vagas reservadas a pessoas com deficiência." });

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "rol_de_fatos_candidato",
                keyColumn: "id",
                keyValue: new Guid("fa700000-0000-7000-8000-000000000004"),
                columns: new[] { "componente_exibicao", "descricao" },
                values: new object[] { null, "Se o candidato cursou o ensino médio em escola pública. O sistema o calcula a partir das duas respostas sobre a origem escolar, pela regra da Lei nº 12.711/2012." });

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "rol_de_fatos_candidato",
                keyColumn: "id",
                keyValue: new Guid("fa700000-0000-7000-8000-000000000005"),
                columns: new[] { "componente_exibicao", "descricao" },
                values: new object[] { "MOEDA", "Renda mensal da família do candidato dividida pelo número de integrantes da composição familiar, em reais. Define o enquadramento nas vagas reservadas por renda." });

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "rol_de_fatos_candidato",
                keyColumn: "id",
                keyValue: new Guid("fa700000-0000-7000-8000-000000000006"),
                columns: new[] { "componente_exibicao", "descricao" },
                values: new object[] { null, "Faixa de idade em que o candidato se enquadra, calculada pelo sistema a partir da data de nascimento, para verificar os requisitos de participação." });

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "rol_de_fatos_candidato",
                keyColumn: "id",
                keyValue: new Guid("fa700000-0000-7000-8000-000000000007"),
                columns: new[] { "componente_exibicao", "descricao" },
                values: new object[] { null, "Sexo biológico do candidato, conforme ele declara na inscrição." });

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "rol_de_fatos_candidato",
                keyColumn: "id",
                keyValue: new Guid("fa700000-0000-7000-8000-000000000008"),
                columns: new[] { "componente_exibicao", "descricao" },
                values: new object[] { null, "Modalidades de concorrência em que o candidato concorre, resultantes das regras de reserva de vagas do processo aplicadas às respostas dele." });

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "rol_de_fatos_candidato",
                keyColumn: "id",
                keyValue: new Guid("fa700000-0000-7000-8000-000000000009"),
                columns: new[] { "componente_exibicao", "descricao" },
                values: new object[] { null, "Condições do candidato pelas quais ele pede atendimento especializado nas etapas do processo." });

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "rol_de_fatos_candidato",
                keyColumn: "id",
                keyValue: new Guid("fa700000-0000-7000-8000-000000000010"),
                columns: new[] { "componente_exibicao", "descricao" },
                values: new object[] { null, "Nacionalidade do candidato, que define os documentos de identificação que ele informa." });

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "rol_de_fatos_candidato",
                keyColumn: "id",
                keyValue: new Guid("fa700000-0000-7000-8000-000000000011"),
                columns: new[] { "componente_exibicao", "descricao" },
                values: new object[] { null, "Tipo de deficiência do candidato com deficiência, entre os do cadastro de tipos de deficiência." });

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "rol_de_fatos_candidato",
                keyColumn: "id",
                keyValue: new Guid("fa700000-0000-7000-8000-000000000012"),
                columns: new[] { "componente_exibicao", "descricao" },
                values: new object[] { null, "Autodeclaração de que a renda familiar per capita é igual ou inferior a um salário mínimo, feita na inscrição para as vagas reservadas por renda." });

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "rol_de_fatos_candidato",
                keyColumn: "id",
                keyValue: new Guid("fa700000-0000-7000-8000-000000000013"),
                columns: new[] { "componente_exibicao", "descricao" },
                values: new object[] { null, "Opção do candidato por concorrer às vagas reservadas a pessoas com deficiência." });

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "rol_de_fatos_candidato",
                keyColumn: "id",
                keyValue: new Guid("fa700000-0000-7000-8000-000000000014"),
                columns: new[] { "componente_exibicao", "descricao" },
                values: new object[] { null, "Opção do candidato por concorrer às vagas reservadas a egressos de escola pública." });

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "rol_de_fatos_candidato",
                keyColumn: "id",
                keyValue: new Guid("fa700000-0000-7000-8000-000000000015"),
                columns: new[] { "componente_exibicao", "descricao" },
                values: new object[] { null, "Opção do candidato por concorrer às vagas reservadas a pretos, pardos e indígenas." });

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "rol_de_fatos_candidato",
                keyColumn: "id",
                keyValue: new Guid("fa700000-0000-7000-8000-000000000016"),
                columns: new[] { "componente_exibicao", "descricao" },
                values: new object[] { null, "Opção do candidato por concorrer às vagas reservadas a quilombolas." });

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "rol_de_fatos_candidato",
                keyColumn: "id",
                keyValue: new Guid("fa700000-0000-7000-8000-000000000017"),
                columns: new[] { "componente_exibicao", "descricao" },
                values: new object[] { null, "Opção do candidato por concorrer às vagas reservadas por renda familiar per capita." });

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "rol_de_fatos_candidato",
                keyColumn: "id",
                keyValue: new Guid("fa700000-0000-7000-8000-000000000018"),
                columns: new[] { "componente_exibicao", "descricao" },
                values: new object[] { null, "Endereço em que o candidato reside, usado para verificar os requisitos regionais do processo." });

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "rol_de_fatos_candidato",
                keyColumn: "id",
                keyValue: new Guid("fa700000-0000-7000-8000-000000000019"),
                columns: new[] { "componente_exibicao", "descricao" },
                values: new object[] { null, "Data de nascimento do candidato, usada para verificar a idade e os requisitos de participação." });

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "rol_de_fatos_candidato",
                keyColumn: "id",
                keyValue: new Guid("fa700000-0000-7000-8000-000000000020"),
                columns: new[] { "componente_exibicao", "descricao" },
                values: new object[] { null, "Unidade da Federação em que o candidato reside, derivada do endereço residencial." });

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "rol_de_fatos_candidato",
                keyColumn: "id",
                keyValue: new Guid("fa700000-0000-7000-8000-000000000021"),
                columns: new[] { "componente_exibicao", "descricao" },
                values: new object[] { null, "Município em que o candidato reside, derivado do endereço residencial." });

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "rol_de_fatos_candidato",
                keyColumn: "id",
                keyValue: new Guid("fa700000-0000-7000-8000-000000000022"),
                columns: new[] { "componente_exibicao", "descricao" },
                values: new object[] { null, "Grupo de vagas em que o candidato foi convocado, conhecido só depois do resultado final. Os documentos da habilitação seguem a convocação." });

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "rol_de_fatos_candidato",
                keyColumn: "id",
                keyValue: new Guid("fa700000-0000-7000-8000-000000000023"),
                columns: new[] { "componente_exibicao", "descricao" },
                values: new object[] { null, "Se o membro da composição familiar é maior de idade." });

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "rol_de_fatos_candidato",
                keyColumn: "id",
                keyValue: new Guid("fa700000-0000-7000-8000-000000000024"),
                columns: new[] { "componente_exibicao", "descricao" },
                values: new object[] { null, "Se o membro da composição familiar não tem renda." });

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "rol_de_fatos_candidato",
                keyColumn: "id",
                keyValue: new Guid("fa700000-0000-7000-8000-000000000025"),
                columns: new[] { "componente_exibicao", "descricao" },
                values: new object[] { null, "Se o membro da composição familiar está sob guarda." });

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "rol_de_fatos_candidato",
                keyColumn: "id",
                keyValue: new Guid("fa700000-0000-7000-8000-000000000026"),
                columns: new[] { "componente_exibicao", "descricao" },
                values: new object[] { null, "Parentesco do membro da composição familiar com o candidato." });

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "rol_de_fatos_candidato",
                keyColumn: "id",
                keyValue: new Guid("fa700000-0000-7000-8000-000000000027"),
                columns: new[] { "componente_exibicao", "descricao" },
                values: new object[] { null, "Nome civil do candidato, como vem do login gov.br." });

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "rol_de_fatos_candidato",
                keyColumn: "id",
                keyValue: new Guid("fa700000-0000-7000-8000-000000000028"),
                columns: new[] { "componente_exibicao", "descricao" },
                values: new object[] { null, "Se o candidato deseja ser identificado pelo nome social no processo seletivo." });

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "rol_de_fatos_candidato",
                keyColumn: "id",
                keyValue: new Guid("fa700000-0000-7000-8000-000000000029"),
                columns: new[] { "componente_exibicao", "descricao" },
                values: new object[] { null, "Nome pelo qual o candidato prefere ser identificado e que aparece no lugar do nome civil." });

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "rol_de_fatos_candidato",
                keyColumn: "id",
                keyValue: new Guid("fa700000-0000-7000-8000-000000000030"),
                columns: new[] { "componente_exibicao", "descricao" },
                values: new object[] { null, "Número do CPF do candidato." });

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "rol_de_fatos_candidato",
                keyColumn: "id",
                keyValue: new Guid("fa700000-0000-7000-8000-000000000031"),
                columns: new[] { "componente_exibicao", "descricao" },
                values: new object[] { null, "Número do RG do candidato." });

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "rol_de_fatos_candidato",
                keyColumn: "id",
                keyValue: new Guid("fa700000-0000-7000-8000-000000000032"),
                columns: new[] { "componente_exibicao", "descricao" },
                values: new object[] { null, "Órgão que emitiu o RG do candidato, entre os de uma lista fechada." });

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "rol_de_fatos_candidato",
                keyColumn: "id",
                keyValue: new Guid("fa700000-0000-7000-8000-000000000033"),
                columns: new[] { "componente_exibicao", "descricao" },
                values: new object[] { null, "Data em que o RG do candidato foi emitido." });

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "rol_de_fatos_candidato",
                keyColumn: "id",
                keyValue: new Guid("fa700000-0000-7000-8000-000000000036"),
                columns: new[] { "componente_exibicao", "descricao" },
                values: new object[] { null, "Nome da mãe do candidato." });

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "rol_de_fatos_candidato",
                keyColumn: "id",
                keyValue: new Guid("fa700000-0000-7000-8000-000000000037"),
                columns: new[] { "componente_exibicao", "descricao" },
                values: new object[] { null, "Nome do pai do candidato." });

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "rol_de_fatos_candidato",
                keyColumn: "id",
                keyValue: new Guid("fa700000-0000-7000-8000-000000000038"),
                columns: new[] { "componente_exibicao", "descricao" },
                values: new object[] { null, "Estado civil do candidato." });

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "rol_de_fatos_candidato",
                keyColumn: "id",
                keyValue: new Guid("fa700000-0000-7000-8000-000000000039"),
                columns: new[] { "componente_exibicao", "descricao" },
                values: new object[] { null, "Endereço de e-mail para comunicação com o candidato." });

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "rol_de_fatos_candidato",
                keyColumn: "id",
                keyValue: new Guid("fa700000-0000-7000-8000-000000000040"),
                columns: new[] { "componente_exibicao", "descricao" },
                values: new object[] { null, "Telefone, com DDD, para comunicação com o candidato." });

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "rol_de_fatos_candidato",
                keyColumn: "id",
                keyValue: new Guid("fa700000-0000-7000-8000-000000000041"),
                columns: new[] { "componente_exibicao", "descricao" },
                values: new object[] { null, "Unidade da Federação que emitiu o RG do candidato." });

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "rol_de_fatos_candidato",
                keyColumn: "id",
                keyValue: new Guid("fa700000-0000-7000-8000-000000000042"),
                columns: new[] { "componente_exibicao", "descricao" },
                values: new object[] { null, "Unidade da Federação em que o candidato nasceu." });

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "rol_de_fatos_candidato",
                keyColumn: "id",
                keyValue: new Guid("fa700000-0000-7000-8000-000000000043"),
                columns: new[] { "componente_exibicao", "descricao" },
                values: new object[] { null, "Município em que o candidato nasceu." });

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "rol_de_fatos_candidato",
                keyColumn: "id",
                keyValue: new Guid("fa700000-0000-7000-8000-000000000044"),
                columns: new[] { "componente_exibicao", "descricao" },
                values: new object[] { null, "Tipo de localidade em que o candidato reside, perguntado antes do endereço." });

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "rol_de_fatos_candidato",
                keyColumn: "id",
                keyValue: new Guid("fa700000-0000-7000-8000-000000000045"),
                columns: new[] { "componente_exibicao", "descricao" },
                values: new object[] { null, "Nome da aldeia, da comunidade tradicional ou do quilombo em que o candidato reside." });

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "rol_de_fatos_candidato",
                keyColumn: "id",
                keyValue: new Guid("fa700000-0000-7000-8000-000000000046"),
                column: "componente_exibicao",
                value: null);

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "rol_de_fatos_candidato",
                keyColumn: "id",
                keyValue: new Guid("fa700000-0000-7000-8000-000000000047"),
                column: "componente_exibicao",
                value: null);

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "rol_de_fatos_candidato",
                keyColumn: "id",
                keyValue: new Guid("fa700000-0000-7000-8000-000000000048"),
                column: "componente_exibicao",
                value: null);

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "rol_de_fatos_candidato",
                keyColumn: "id",
                keyValue: new Guid("fa700000-0000-7000-8000-000000000049"),
                columns: new[] { "componente_exibicao", "descricao" },
                values: new object[] { null, "Edição do ENEM cujas notas são importadas para a classificação do candidato." });

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "rol_de_fatos_candidato",
                keyColumn: "id",
                keyValue: new Guid("fa700000-0000-7000-8000-000000000050"),
                columns: new[] { "componente_exibicao", "descricao" },
                values: new object[] { null, "Se o candidato se declara indígena. A condição exige declaração de pertencimento e passa só por análise de documentos." });

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "rol_de_fatos_candidato",
                keyColumn: "id",
                keyValue: new Guid("fa700000-0000-7000-8000-000000000051"),
                columns: new[] { "componente_exibicao", "descricao" },
                values: new object[] { null, "Número do passaporte do candidato." });

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "rol_de_fatos_candidato",
                keyColumn: "id",
                keyValue: new Guid("fa700000-0000-7000-8000-000000000052"),
                columns: new[] { "componente_exibicao", "descricao" },
                values: new object[] { null, "Número do Registro Nacional Migratório (RNM) do candidato." });

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "rol_de_fatos_candidato",
                keyColumn: "id",
                keyValue: new Guid("fa700000-0000-7000-8000-000000000053"),
                columns: new[] { "componente_exibicao", "descricao" },
                values: new object[] { null, "Recursos de acessibilidade de que o candidato precisa nas etapas, entre os que o processo oferece." });

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "rol_de_fatos_candidato",
                keyColumn: "id",
                keyValue: new Guid("fa700000-0000-7000-8000-000000000054"),
                column: "componente_exibicao",
                value: null);

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "rol_de_fatos_candidato",
                keyColumn: "id",
                keyValue: new Guid("fa700000-0000-7000-8000-000000000055"),
                columns: new[] { "componente_exibicao", "descricao" },
                values: new object[] { null, "Opção do candidato por concorrer às vagas por acréscimo para indígenas e quilombolas." });

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "rol_de_fatos_candidato",
                keyColumn: "id",
                keyValue: new Guid("fa700000-0000-7000-8000-000000000056"),
                column: "componente_exibicao",
                value: null);

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "rol_de_fatos_candidato",
                keyColumn: "id",
                keyValue: new Guid("fa700000-0000-7000-8000-000000000057"),
                columns: new[] { "componente_exibicao", "descricao" },
                values: new object[] { null, "Sexo que consta do registro civil do candidato." });

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "rol_de_fatos_candidato",
                keyColumn: "id",
                keyValue: new Guid("fa700000-0000-7000-8000-000000000058"),
                columns: new[] { "componente_exibicao", "descricao" },
                values: new object[] { null, "Identidade de gênero que o candidato declara." });

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "rol_de_fatos_candidato",
                keyColumn: "id",
                keyValue: new Guid("fa700000-0000-7000-8000-000000000059"),
                columns: new[] { "componente_exibicao", "descricao" },
                values: new object[] { null, "Orientação sexual que o candidato declara." });

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "rol_de_fatos_candidato",
                keyColumn: "id",
                keyValue: new Guid("fa700000-0000-7000-8000-000000000060"),
                columns: new[] { "componente_exibicao", "descricao" },
                values: new object[] { null, "Modalidades de concorrência do candidato na segunda opção de curso, que tem distribuição de vagas própria." });

            migrationBuilder.AddCheckConstraint(
                name: "ck_rol_de_fatos_candidato_componente_exibicao",
                schema: "configuracao",
                table: "rol_de_fatos_candidato",
                sql: "componente_exibicao IS NULL OR componente_exibicao IN ('MOEDA', 'SITUACAO')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_rol_de_fatos_candidato_componente_exibicao",
                schema: "configuracao",
                table: "rol_de_fatos_candidato");

            migrationBuilder.DropColumn(
                name: "componente_exibicao",
                schema: "configuracao",
                table: "rol_de_fatos_candidato");

            migrationBuilder.AlterColumn<string>(
                name: "descricao",
                schema: "configuracao",
                table: "rol_de_fatos_candidato",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(1000)",
                oldMaxLength: 1000);

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "rol_de_fatos_candidato",
                keyColumn: "id",
                keyValue: new Guid("fa700000-0000-7000-8000-000000000001"),
                column: "descricao",
                value: null);

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "rol_de_fatos_candidato",
                keyColumn: "id",
                keyValue: new Guid("fa700000-0000-7000-8000-000000000002"),
                column: "descricao",
                value: null);

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "rol_de_fatos_candidato",
                keyColumn: "id",
                keyValue: new Guid("fa700000-0000-7000-8000-000000000003"),
                column: "descricao",
                value: null);

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "rol_de_fatos_candidato",
                keyColumn: "id",
                keyValue: new Guid("fa700000-0000-7000-8000-000000000004"),
                column: "descricao",
                value: null);

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "rol_de_fatos_candidato",
                keyColumn: "id",
                keyValue: new Guid("fa700000-0000-7000-8000-000000000005"),
                column: "descricao",
                value: null);

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "rol_de_fatos_candidato",
                keyColumn: "id",
                keyValue: new Guid("fa700000-0000-7000-8000-000000000006"),
                column: "descricao",
                value: null);

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "rol_de_fatos_candidato",
                keyColumn: "id",
                keyValue: new Guid("fa700000-0000-7000-8000-000000000007"),
                column: "descricao",
                value: null);

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "rol_de_fatos_candidato",
                keyColumn: "id",
                keyValue: new Guid("fa700000-0000-7000-8000-000000000008"),
                column: "descricao",
                value: null);

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "rol_de_fatos_candidato",
                keyColumn: "id",
                keyValue: new Guid("fa700000-0000-7000-8000-000000000009"),
                column: "descricao",
                value: null);

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "rol_de_fatos_candidato",
                keyColumn: "id",
                keyValue: new Guid("fa700000-0000-7000-8000-000000000010"),
                column: "descricao",
                value: null);

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "rol_de_fatos_candidato",
                keyColumn: "id",
                keyValue: new Guid("fa700000-0000-7000-8000-000000000011"),
                column: "descricao",
                value: null);

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "rol_de_fatos_candidato",
                keyColumn: "id",
                keyValue: new Guid("fa700000-0000-7000-8000-000000000012"),
                column: "descricao",
                value: null);

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "rol_de_fatos_candidato",
                keyColumn: "id",
                keyValue: new Guid("fa700000-0000-7000-8000-000000000013"),
                column: "descricao",
                value: null);

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "rol_de_fatos_candidato",
                keyColumn: "id",
                keyValue: new Guid("fa700000-0000-7000-8000-000000000014"),
                column: "descricao",
                value: null);

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "rol_de_fatos_candidato",
                keyColumn: "id",
                keyValue: new Guid("fa700000-0000-7000-8000-000000000015"),
                column: "descricao",
                value: null);

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "rol_de_fatos_candidato",
                keyColumn: "id",
                keyValue: new Guid("fa700000-0000-7000-8000-000000000016"),
                column: "descricao",
                value: null);

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "rol_de_fatos_candidato",
                keyColumn: "id",
                keyValue: new Guid("fa700000-0000-7000-8000-000000000017"),
                column: "descricao",
                value: null);

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "rol_de_fatos_candidato",
                keyColumn: "id",
                keyValue: new Guid("fa700000-0000-7000-8000-000000000018"),
                column: "descricao",
                value: null);

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "rol_de_fatos_candidato",
                keyColumn: "id",
                keyValue: new Guid("fa700000-0000-7000-8000-000000000019"),
                column: "descricao",
                value: null);

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "rol_de_fatos_candidato",
                keyColumn: "id",
                keyValue: new Guid("fa700000-0000-7000-8000-000000000020"),
                column: "descricao",
                value: null);

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "rol_de_fatos_candidato",
                keyColumn: "id",
                keyValue: new Guid("fa700000-0000-7000-8000-000000000021"),
                column: "descricao",
                value: null);

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "rol_de_fatos_candidato",
                keyColumn: "id",
                keyValue: new Guid("fa700000-0000-7000-8000-000000000022"),
                column: "descricao",
                value: null);

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "rol_de_fatos_candidato",
                keyColumn: "id",
                keyValue: new Guid("fa700000-0000-7000-8000-000000000023"),
                column: "descricao",
                value: null);

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "rol_de_fatos_candidato",
                keyColumn: "id",
                keyValue: new Guid("fa700000-0000-7000-8000-000000000024"),
                column: "descricao",
                value: null);

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "rol_de_fatos_candidato",
                keyColumn: "id",
                keyValue: new Guid("fa700000-0000-7000-8000-000000000025"),
                column: "descricao",
                value: null);

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "rol_de_fatos_candidato",
                keyColumn: "id",
                keyValue: new Guid("fa700000-0000-7000-8000-000000000026"),
                column: "descricao",
                value: null);

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "rol_de_fatos_candidato",
                keyColumn: "id",
                keyValue: new Guid("fa700000-0000-7000-8000-000000000027"),
                column: "descricao",
                value: null);

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "rol_de_fatos_candidato",
                keyColumn: "id",
                keyValue: new Guid("fa700000-0000-7000-8000-000000000028"),
                column: "descricao",
                value: null);

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "rol_de_fatos_candidato",
                keyColumn: "id",
                keyValue: new Guid("fa700000-0000-7000-8000-000000000029"),
                column: "descricao",
                value: null);

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "rol_de_fatos_candidato",
                keyColumn: "id",
                keyValue: new Guid("fa700000-0000-7000-8000-000000000030"),
                column: "descricao",
                value: null);

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "rol_de_fatos_candidato",
                keyColumn: "id",
                keyValue: new Guid("fa700000-0000-7000-8000-000000000031"),
                column: "descricao",
                value: null);

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "rol_de_fatos_candidato",
                keyColumn: "id",
                keyValue: new Guid("fa700000-0000-7000-8000-000000000032"),
                column: "descricao",
                value: null);

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "rol_de_fatos_candidato",
                keyColumn: "id",
                keyValue: new Guid("fa700000-0000-7000-8000-000000000033"),
                column: "descricao",
                value: null);

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "rol_de_fatos_candidato",
                keyColumn: "id",
                keyValue: new Guid("fa700000-0000-7000-8000-000000000036"),
                column: "descricao",
                value: null);

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "rol_de_fatos_candidato",
                keyColumn: "id",
                keyValue: new Guid("fa700000-0000-7000-8000-000000000037"),
                column: "descricao",
                value: null);

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "rol_de_fatos_candidato",
                keyColumn: "id",
                keyValue: new Guid("fa700000-0000-7000-8000-000000000038"),
                column: "descricao",
                value: null);

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "rol_de_fatos_candidato",
                keyColumn: "id",
                keyValue: new Guid("fa700000-0000-7000-8000-000000000039"),
                column: "descricao",
                value: null);

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "rol_de_fatos_candidato",
                keyColumn: "id",
                keyValue: new Guid("fa700000-0000-7000-8000-000000000040"),
                column: "descricao",
                value: null);

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "rol_de_fatos_candidato",
                keyColumn: "id",
                keyValue: new Guid("fa700000-0000-7000-8000-000000000041"),
                column: "descricao",
                value: null);

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "rol_de_fatos_candidato",
                keyColumn: "id",
                keyValue: new Guid("fa700000-0000-7000-8000-000000000042"),
                column: "descricao",
                value: null);

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "rol_de_fatos_candidato",
                keyColumn: "id",
                keyValue: new Guid("fa700000-0000-7000-8000-000000000043"),
                column: "descricao",
                value: null);

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "rol_de_fatos_candidato",
                keyColumn: "id",
                keyValue: new Guid("fa700000-0000-7000-8000-000000000044"),
                column: "descricao",
                value: null);

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "rol_de_fatos_candidato",
                keyColumn: "id",
                keyValue: new Guid("fa700000-0000-7000-8000-000000000045"),
                column: "descricao",
                value: null);

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "rol_de_fatos_candidato",
                keyColumn: "id",
                keyValue: new Guid("fa700000-0000-7000-8000-000000000049"),
                column: "descricao",
                value: null);

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "rol_de_fatos_candidato",
                keyColumn: "id",
                keyValue: new Guid("fa700000-0000-7000-8000-000000000050"),
                column: "descricao",
                value: null);

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "rol_de_fatos_candidato",
                keyColumn: "id",
                keyValue: new Guid("fa700000-0000-7000-8000-000000000051"),
                column: "descricao",
                value: null);

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "rol_de_fatos_candidato",
                keyColumn: "id",
                keyValue: new Guid("fa700000-0000-7000-8000-000000000052"),
                column: "descricao",
                value: null);

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "rol_de_fatos_candidato",
                keyColumn: "id",
                keyValue: new Guid("fa700000-0000-7000-8000-000000000053"),
                column: "descricao",
                value: null);

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "rol_de_fatos_candidato",
                keyColumn: "id",
                keyValue: new Guid("fa700000-0000-7000-8000-000000000055"),
                column: "descricao",
                value: null);

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "rol_de_fatos_candidato",
                keyColumn: "id",
                keyValue: new Guid("fa700000-0000-7000-8000-000000000057"),
                column: "descricao",
                value: null);

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "rol_de_fatos_candidato",
                keyColumn: "id",
                keyValue: new Guid("fa700000-0000-7000-8000-000000000058"),
                column: "descricao",
                value: null);

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "rol_de_fatos_candidato",
                keyColumn: "id",
                keyValue: new Guid("fa700000-0000-7000-8000-000000000059"),
                column: "descricao",
                value: null);

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "rol_de_fatos_candidato",
                keyColumn: "id",
                keyValue: new Guid("fa700000-0000-7000-8000-000000000060"),
                column: "descricao",
                value: null);
        }
    }
}
