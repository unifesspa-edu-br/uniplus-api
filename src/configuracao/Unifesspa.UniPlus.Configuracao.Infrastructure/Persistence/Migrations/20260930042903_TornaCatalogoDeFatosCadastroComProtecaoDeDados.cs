using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class TornaCatalogoDeFatosCadastroComProtecaoDeDados : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_rol_de_fatos_candidato_valores_dominio_coerente",
                schema: "configuracao",
                table: "rol_de_fatos_candidato");

            migrationBuilder.DropColumn(
                name: "valores_dominio",
                schema: "configuracao",
                table: "rol_de_fatos_candidato");

            migrationBuilder.AddColumn<bool>(
                name: "ativo",
                schema: "configuracao",
                table: "rol_de_fatos_candidato",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "classificacao_protecao",
                schema: "configuracao",
                table: "rol_de_fatos_candidato",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "created_by",
                schema: "configuracao",
                table: "rol_de_fatos_candidato",
                type: "character varying(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "escopo",
                schema: "configuracao",
                table: "rol_de_fatos_candidato",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "finalidade_tratamento",
                schema: "configuracao",
                table: "rol_de_fatos_candidato",
                type: "character varying(500)",
                maxLength: 500,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "hipotese_legal",
                schema: "configuracao",
                table: "rol_de_fatos_candidato",
                type: "character varying(40)",
                maxLength: 40,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<bool>(
                name: "sistema",
                schema: "configuracao",
                table: "rol_de_fatos_candidato",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "updated_by",
                schema: "configuracao",
                table: "rol_de_fatos_candidato",
                type: "character varying(255)",
                maxLength: 255,
                nullable: true);

            // `xmin` é coluna de sistema do Postgres: o token de concorrência só passa a lê-la.

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "rol_de_fatos_candidato",
                keyColumn: "id",
                keyValue: new Guid("fa700000-0000-7000-8000-000000000001"),
                columns: new[] { "ativo", "classificacao_protecao", "created_by", "escopo", "finalidade_tratamento", "hipotese_legal", "sistema", "updated_by" },
                values: new object[] { true, "SENSIVEL", null, "CANDIDATO", "Enquadramento do candidato na reserva de vagas da Lei nº 12.711/2012 e nas ações afirmativas do processo seletivo.", "CUMPRIMENTO_OBRIGACAO_LEGAL", true, null });

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "rol_de_fatos_candidato",
                keyColumn: "id",
                keyValue: new Guid("fa700000-0000-7000-8000-000000000002"),
                columns: new[] { "ativo", "classificacao_protecao", "created_by", "escopo", "finalidade_tratamento", "hipotese_legal", "sistema", "updated_by" },
                values: new object[] { true, "SENSIVEL", null, "CANDIDATO", "Enquadramento do candidato na reserva de vagas da Lei nº 12.711/2012 e nas ações afirmativas do processo seletivo.", "CUMPRIMENTO_OBRIGACAO_LEGAL", true, null });

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "rol_de_fatos_candidato",
                keyColumn: "id",
                keyValue: new Guid("fa700000-0000-7000-8000-000000000003"),
                columns: new[] { "ativo", "classificacao_protecao", "created_by", "escopo", "finalidade_tratamento", "hipotese_legal", "sistema", "updated_by" },
                values: new object[] { true, "SENSIVEL", null, "CANDIDATO", "Enquadramento do candidato na reserva de vagas da Lei nº 12.711/2012 e nas ações afirmativas do processo seletivo.", "CUMPRIMENTO_OBRIGACAO_LEGAL", true, null });

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "rol_de_fatos_candidato",
                keyColumn: "id",
                keyValue: new Guid("fa700000-0000-7000-8000-000000000004"),
                columns: new[] { "ativo", "classificacao_protecao", "created_by", "escopo", "finalidade_tratamento", "hipotese_legal", "sistema", "updated_by" },
                values: new object[] { true, "PESSOAL", null, "CANDIDATO", "Enquadramento do candidato na reserva de vagas da Lei nº 12.711/2012 e nas ações afirmativas do processo seletivo.", "CUMPRIMENTO_OBRIGACAO_LEGAL", true, null });

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "rol_de_fatos_candidato",
                keyColumn: "id",
                keyValue: new Guid("fa700000-0000-7000-8000-000000000005"),
                columns: new[] { "ativo", "classificacao_protecao", "created_by", "escopo", "finalidade_tratamento", "hipotese_legal", "sistema", "updated_by" },
                values: new object[] { true, "PESSOAL", null, "CANDIDATO", "Enquadramento do candidato na reserva de vagas da Lei nº 12.711/2012 e nas ações afirmativas do processo seletivo.", "CUMPRIMENTO_OBRIGACAO_LEGAL", true, null });

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "rol_de_fatos_candidato",
                keyColumn: "id",
                keyValue: new Guid("fa700000-0000-7000-8000-000000000006"),
                columns: new[] { "ativo", "classificacao_protecao", "created_by", "escopo", "finalidade_tratamento", "hipotese_legal", "sistema", "updated_by" },
                values: new object[] { true, "PESSOAL", null, "CANDIDATO", "Verificação dos requisitos de participação e das exigências documentais do processo seletivo.", "CUMPRIMENTO_OBRIGACAO_LEGAL", true, null });

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "rol_de_fatos_candidato",
                keyColumn: "id",
                keyValue: new Guid("fa700000-0000-7000-8000-000000000007"),
                columns: new[] { "ativo", "classificacao_protecao", "created_by", "escopo", "finalidade_tratamento", "hipotese_legal", "sistema", "updated_by" },
                values: new object[] { true, "PESSOAL", null, "CANDIDATO", "Verificação dos requisitos de participação e das exigências documentais do processo seletivo.", "CUMPRIMENTO_OBRIGACAO_LEGAL", true, null });

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "rol_de_fatos_candidato",
                keyColumn: "id",
                keyValue: new Guid("fa700000-0000-7000-8000-000000000008"),
                columns: new[] { "ativo", "classificacao_protecao", "created_by", "escopo", "finalidade_tratamento", "hipotese_legal", "sistema", "updated_by" },
                values: new object[] { true, "SENSIVEL", null, "CANDIDATO", "Enquadramento do candidato na reserva de vagas da Lei nº 12.711/2012 e nas ações afirmativas do processo seletivo.", "CUMPRIMENTO_OBRIGACAO_LEGAL", true, null });

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "rol_de_fatos_candidato",
                keyColumn: "id",
                keyValue: new Guid("fa700000-0000-7000-8000-000000000009"),
                columns: new[] { "ativo", "classificacao_protecao", "created_by", "escopo", "finalidade_tratamento", "hipotese_legal", "sistema", "updated_by" },
                values: new object[] { true, "SENSIVEL", null, "CANDIDATO", "Oferta de atendimento especializado ao candidato na realização das etapas do processo seletivo.", "CUMPRIMENTO_OBRIGACAO_LEGAL", true, null });

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "rol_de_fatos_candidato",
                keyColumn: "id",
                keyValue: new Guid("fa700000-0000-7000-8000-000000000010"),
                columns: new[] { "ativo", "classificacao_protecao", "created_by", "escopo", "finalidade_tratamento", "hipotese_legal", "sistema", "updated_by" },
                values: new object[] { true, "PESSOAL", null, "CANDIDATO", "Verificação dos requisitos de participação e das exigências documentais do processo seletivo.", "CUMPRIMENTO_OBRIGACAO_LEGAL", true, null });

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "rol_de_fatos_candidato",
                keyColumn: "id",
                keyValue: new Guid("fa700000-0000-7000-8000-000000000011"),
                columns: new[] { "ativo", "classificacao_protecao", "created_by", "escopo", "finalidade_tratamento", "hipotese_legal", "sistema", "updated_by" },
                values: new object[] { true, "SENSIVEL", null, "CANDIDATO", "Enquadramento do candidato na reserva de vagas da Lei nº 12.711/2012 e nas ações afirmativas do processo seletivo.", "CUMPRIMENTO_OBRIGACAO_LEGAL", true, null });

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "rol_de_fatos_candidato",
                keyColumn: "id",
                keyValue: new Guid("fa700000-0000-7000-8000-000000000012"),
                columns: new[] { "ativo", "classificacao_protecao", "created_by", "escopo", "finalidade_tratamento", "hipotese_legal", "sistema", "updated_by" },
                values: new object[] { true, "PESSOAL", null, "CANDIDATO", "Enquadramento do candidato na reserva de vagas da Lei nº 12.711/2012 e nas ações afirmativas do processo seletivo.", "CUMPRIMENTO_OBRIGACAO_LEGAL", true, null });

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "rol_de_fatos_candidato",
                keyColumn: "id",
                keyValue: new Guid("fa700000-0000-7000-8000-000000000013"),
                columns: new[] { "ativo", "classificacao_protecao", "created_by", "escopo", "finalidade_tratamento", "hipotese_legal", "sistema", "updated_by" },
                values: new object[] { true, "SENSIVEL", null, "CANDIDATO", "Enquadramento do candidato na reserva de vagas da Lei nº 12.711/2012 e nas ações afirmativas do processo seletivo.", "CUMPRIMENTO_OBRIGACAO_LEGAL", true, null });

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "rol_de_fatos_candidato",
                keyColumn: "id",
                keyValue: new Guid("fa700000-0000-7000-8000-000000000014"),
                columns: new[] { "ativo", "classificacao_protecao", "created_by", "escopo", "finalidade_tratamento", "hipotese_legal", "sistema", "updated_by" },
                values: new object[] { true, "PESSOAL", null, "CANDIDATO", "Enquadramento do candidato na reserva de vagas da Lei nº 12.711/2012 e nas ações afirmativas do processo seletivo.", "CUMPRIMENTO_OBRIGACAO_LEGAL", true, null });

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "rol_de_fatos_candidato",
                keyColumn: "id",
                keyValue: new Guid("fa700000-0000-7000-8000-000000000015"),
                columns: new[] { "ativo", "classificacao_protecao", "created_by", "escopo", "finalidade_tratamento", "hipotese_legal", "sistema", "updated_by" },
                values: new object[] { true, "SENSIVEL", null, "CANDIDATO", "Enquadramento do candidato na reserva de vagas da Lei nº 12.711/2012 e nas ações afirmativas do processo seletivo.", "CUMPRIMENTO_OBRIGACAO_LEGAL", true, null });

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "rol_de_fatos_candidato",
                keyColumn: "id",
                keyValue: new Guid("fa700000-0000-7000-8000-000000000016"),
                columns: new[] { "ativo", "classificacao_protecao", "created_by", "escopo", "finalidade_tratamento", "hipotese_legal", "sistema", "updated_by" },
                values: new object[] { true, "SENSIVEL", null, "CANDIDATO", "Enquadramento do candidato na reserva de vagas da Lei nº 12.711/2012 e nas ações afirmativas do processo seletivo.", "CUMPRIMENTO_OBRIGACAO_LEGAL", true, null });

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "rol_de_fatos_candidato",
                keyColumn: "id",
                keyValue: new Guid("fa700000-0000-7000-8000-000000000017"),
                columns: new[] { "ativo", "classificacao_protecao", "created_by", "escopo", "finalidade_tratamento", "hipotese_legal", "sistema", "updated_by" },
                values: new object[] { true, "PESSOAL", null, "CANDIDATO", "Enquadramento do candidato na reserva de vagas da Lei nº 12.711/2012 e nas ações afirmativas do processo seletivo.", "CUMPRIMENTO_OBRIGACAO_LEGAL", true, null });

            migrationBuilder.AddCheckConstraint(
                name: "ck_rol_de_fatos_candidato_classificacao_protecao",
                schema: "configuracao",
                table: "rol_de_fatos_candidato",
                sql: "classificacao_protecao IN ('PUBLICO', 'INTERNO', 'PESSOAL', 'SENSIVEL')");

            migrationBuilder.AddCheckConstraint(
                name: "ck_rol_de_fatos_candidato_escopo",
                schema: "configuracao",
                table: "rol_de_fatos_candidato",
                sql: "escopo IN ('CANDIDATO', 'MEMBRO_GRUPO')");

            migrationBuilder.AddCheckConstraint(
                name: "ck_rol_de_fatos_candidato_hipotese_legal",
                schema: "configuracao",
                table: "rol_de_fatos_candidato",
                sql: "hipotese_legal IN ('CONSENTIMENTO', 'CUMPRIMENTO_OBRIGACAO_LEGAL', 'EXECUCAO_POLITICAS_PUBLICAS', 'ESTUDOS_POR_ORGAO_DE_PESQUISA', 'EXECUCAO_CONTRATO', 'EXERCICIO_REGULAR_DE_DIREITOS', 'PROTECAO_DA_VIDA', 'TUTELA_DA_SAUDE', 'INTERESSE_LEGITIMO', 'PROTECAO_DO_CREDITO', 'PREVENCAO_A_FRAUDE')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_rol_de_fatos_candidato_classificacao_protecao",
                schema: "configuracao",
                table: "rol_de_fatos_candidato");

            migrationBuilder.DropCheckConstraint(
                name: "ck_rol_de_fatos_candidato_escopo",
                schema: "configuracao",
                table: "rol_de_fatos_candidato");

            migrationBuilder.DropCheckConstraint(
                name: "ck_rol_de_fatos_candidato_hipotese_legal",
                schema: "configuracao",
                table: "rol_de_fatos_candidato");

            migrationBuilder.DropColumn(
                name: "ativo",
                schema: "configuracao",
                table: "rol_de_fatos_candidato");

            migrationBuilder.DropColumn(
                name: "classificacao_protecao",
                schema: "configuracao",
                table: "rol_de_fatos_candidato");

            migrationBuilder.DropColumn(
                name: "created_by",
                schema: "configuracao",
                table: "rol_de_fatos_candidato");

            migrationBuilder.DropColumn(
                name: "escopo",
                schema: "configuracao",
                table: "rol_de_fatos_candidato");

            migrationBuilder.DropColumn(
                name: "finalidade_tratamento",
                schema: "configuracao",
                table: "rol_de_fatos_candidato");

            migrationBuilder.DropColumn(
                name: "hipotese_legal",
                schema: "configuracao",
                table: "rol_de_fatos_candidato");

            migrationBuilder.DropColumn(
                name: "sistema",
                schema: "configuracao",
                table: "rol_de_fatos_candidato");

            migrationBuilder.DropColumn(
                name: "updated_by",
                schema: "configuracao",
                table: "rol_de_fatos_candidato");


            migrationBuilder.AddColumn<string>(
                name: "valores_dominio",
                schema: "configuracao",
                table: "rol_de_fatos_candidato",
                type: "jsonb",
                nullable: true);

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "rol_de_fatos_candidato",
                keyColumn: "id",
                keyValue: new Guid("fa700000-0000-7000-8000-000000000001"),
                column: "valores_dominio",
                value: null);

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "rol_de_fatos_candidato",
                keyColumn: "id",
                keyValue: new Guid("fa700000-0000-7000-8000-000000000002"),
                column: "valores_dominio",
                value: null);

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "rol_de_fatos_candidato",
                keyColumn: "id",
                keyValue: new Guid("fa700000-0000-7000-8000-000000000003"),
                column: "valores_dominio",
                value: null);

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "rol_de_fatos_candidato",
                keyColumn: "id",
                keyValue: new Guid("fa700000-0000-7000-8000-000000000004"),
                column: "valores_dominio",
                value: null);

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "rol_de_fatos_candidato",
                keyColumn: "id",
                keyValue: new Guid("fa700000-0000-7000-8000-000000000005"),
                column: "valores_dominio",
                value: null);

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "rol_de_fatos_candidato",
                keyColumn: "id",
                keyValue: new Guid("fa700000-0000-7000-8000-000000000006"),
                column: "valores_dominio",
                value: null);

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "rol_de_fatos_candidato",
                keyColumn: "id",
                keyValue: new Guid("fa700000-0000-7000-8000-000000000007"),
                column: "valores_dominio",
                value: null);

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "rol_de_fatos_candidato",
                keyColumn: "id",
                keyValue: new Guid("fa700000-0000-7000-8000-000000000008"),
                column: "valores_dominio",
                value: null);

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "rol_de_fatos_candidato",
                keyColumn: "id",
                keyValue: new Guid("fa700000-0000-7000-8000-000000000009"),
                column: "valores_dominio",
                value: null);

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "rol_de_fatos_candidato",
                keyColumn: "id",
                keyValue: new Guid("fa700000-0000-7000-8000-000000000010"),
                column: "valores_dominio",
                value: null);

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "rol_de_fatos_candidato",
                keyColumn: "id",
                keyValue: new Guid("fa700000-0000-7000-8000-000000000011"),
                column: "valores_dominio",
                value: null);

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "rol_de_fatos_candidato",
                keyColumn: "id",
                keyValue: new Guid("fa700000-0000-7000-8000-000000000012"),
                column: "valores_dominio",
                value: null);

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "rol_de_fatos_candidato",
                keyColumn: "id",
                keyValue: new Guid("fa700000-0000-7000-8000-000000000013"),
                column: "valores_dominio",
                value: null);

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "rol_de_fatos_candidato",
                keyColumn: "id",
                keyValue: new Guid("fa700000-0000-7000-8000-000000000014"),
                column: "valores_dominio",
                value: null);

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "rol_de_fatos_candidato",
                keyColumn: "id",
                keyValue: new Guid("fa700000-0000-7000-8000-000000000015"),
                column: "valores_dominio",
                value: null);

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "rol_de_fatos_candidato",
                keyColumn: "id",
                keyValue: new Guid("fa700000-0000-7000-8000-000000000016"),
                column: "valores_dominio",
                value: null);

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "rol_de_fatos_candidato",
                keyColumn: "id",
                keyValue: new Guid("fa700000-0000-7000-8000-000000000017"),
                column: "valores_dominio",
                value: null);

            migrationBuilder.AddCheckConstraint(
                name: "ck_rol_de_fatos_candidato_valores_dominio_coerente",
                schema: "configuracao",
                table: "rol_de_fatos_candidato",
                sql: "valores_dominio IS NULL OR (\n    dominio = 'CATEGORICO'\n    AND jsonb_typeof(valores_dominio) = 'array'\n    AND valores_dominio <> '[]'::jsonb\n    AND NOT (valores_dominio @? '$[*] ? (@.type() != \"string\" || @ like_regex \"^\\\\s*$\")')\n)");
        }
    }
}
