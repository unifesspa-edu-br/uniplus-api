using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class GravaAsRespostasDaInscricaoEmTabela : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "aceites",
                schema: "selecao",
                table: "inscricoes");

            migrationBuilder.DropColumn(
                name: "documentos_cifrados",
                schema: "selecao",
                table: "inscricoes");

            migrationBuilder.DropColumn(
                name: "fatos_identificador_declarados",
                schema: "selecao",
                table: "inscricoes");

            migrationBuilder.DropColumn(
                name: "grupos",
                schema: "selecao",
                table: "inscricoes");

            migrationBuilder.DropColumn(
                name: "respostas",
                schema: "selecao",
                table: "inscricoes");

            migrationBuilder.AlterTable(
                name: "inscricoes",
                schema: "selecao",
                comment: "A inscrição de um candidato num processo seletivo (issue #1858) — uma por CPF e processo. As respostas ficam em respostas_da_inscricao e nas tabelas vizinhas.",
                oldComment: "A inscrição de um candidato num processo seletivo (issue #1858) — uma por CPF e processo. Números de documento vivem cifrados aqui ou em documentos_cifrados, nunca em respostas_json.");

            migrationBuilder.AddColumn<string>(
                name: "derivados",
                schema: "selecao",
                table: "comprovantes_inscricao",
                type: "jsonb",
                nullable: false,
                defaultValue: "{}",
                comment: "O que o sistema derivou das respostas nesta submissão, como a UF a partir do endereço.");

            migrationBuilder.CreateTable(
                name: "aceites_da_inscricao",
                schema: "selecao",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    inscricao_id = table.Column<Guid>(type: "uuid", nullable: false),
                    ordem = table.Column<int>(type: "integer", nullable: false),
                    termo_codigo = table.Column<string>(type: "text", nullable: false),
                    versao_id = table.Column<Guid>(type: "uuid", nullable: false),
                    hash_versao = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_aceites_da_inscricao", x => x.id);
                    table.ForeignKey(
                        name: "fk_aceites_da_inscricao_inscricoes_inscricao_id",
                        column: x => x.inscricao_id,
                        principalSchema: "selecao",
                        principalTable: "inscricoes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                },
                comment: "O aceite de um termo na inscrição, com a versão e o hash da versão fixada.");

            migrationBuilder.CreateTable(
                name: "ocorrencias_de_grupo_da_inscricao",
                schema: "selecao",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    inscricao_id = table.Column<Guid>(type: "uuid", nullable: false),
                    grupo_codigo = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    membro_id = table.Column<string>(type: "text", nullable: false, comment: "O id estável que o formulário dá ao membro — reordenar a lista não troca a identidade dele."),
                    ordem = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_ocorrencias_de_grupo_da_inscricao", x => x.id);
                    table.ForeignKey(
                        name: "fk_ocorrencias_de_grupo_da_inscricao_inscricoes_inscricao_id",
                        column: x => x.inscricao_id,
                        principalSchema: "selecao",
                        principalTable: "inscricoes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                },
                comment: "Uma ocorrência de grupo repetível da inscrição, como cada dependente de um grupo de dependentes.");

            migrationBuilder.CreateTable(
                name: "enderecos_da_inscricao",
                schema: "selecao",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    inscricao_id = table.Column<Guid>(type: "uuid", nullable: false),
                    ocorrencia_id = table.Column<Guid>(type: "uuid", nullable: true),
                    fato_codigo = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    origem = table.Column<int>(type: "integer", nullable: false),
                    cep = table.Column<string>(type: "text", nullable: true),
                    logradouro = table.Column<string>(type: "text", nullable: true),
                    numero = table.Column<string>(type: "text", nullable: true),
                    complemento = table.Column<string>(type: "text", nullable: true),
                    bairro = table.Column<string>(type: "text", nullable: true),
                    distrito = table.Column<string>(type: "text", nullable: true),
                    municipio_codigo_ibge = table.Column<string>(type: "text", nullable: true),
                    municipio_nome = table.Column<string>(type: "text", nullable: true),
                    uf = table.Column<string>(type: "text", nullable: true),
                    latitude = table.Column<string>(type: "text", nullable: true),
                    longitude = table.Column<string>(type: "text", nullable: true),
                    nivel_resolucao = table.Column<string>(type: "text", nullable: true),
                    procedencia = table.Column<string>(type: "text", nullable: true, comment: "Como o endereço foi preenchido no formulário: pela busca do CEP no Geo ou à mão.")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_enderecos_da_inscricao", x => x.id);
                    table.CheckConstraint("ck_enderecos_da_inscricao_origem", "origem IN (1, 2)");
                    table.ForeignKey(
                        name: "fk_enderecos_da_inscricao_inscricoes_inscricao_id",
                        column: x => x.inscricao_id,
                        principalSchema: "selecao",
                        principalTable: "inscricoes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_enderecos_da_inscricao_ocorrencias_de_grupo_da_inscricao_oc",
                        column: x => x.ocorrencia_id,
                        principalSchema: "selecao",
                        principalTable: "ocorrencias_de_grupo_da_inscricao",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                },
                comment: "A resposta de um campo de endereço da inscrição, um componente por coluna.");

            migrationBuilder.CreateTable(
                name: "respostas_da_inscricao",
                schema: "selecao",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    inscricao_id = table.Column<Guid>(type: "uuid", nullable: false),
                    ocorrencia_id = table.Column<Guid>(type: "uuid", nullable: true),
                    fato_codigo = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    posicao = table.Column<int>(type: "integer", nullable: true),
                    origem = table.Column<int>(type: "integer", nullable: false),
                    tipo = table.Column<int>(type: "integer", nullable: false),
                    valor_booleano = table.Column<bool>(type: "boolean", nullable: true),
                    valor_numero = table.Column<decimal>(type: "numeric", nullable: true),
                    valor_codigo = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    valor_texto = table.Column<string>(type: "text", nullable: true),
                    valor_data = table.Column<DateOnly>(type: "date", nullable: true),
                    valor_cifrado = table.Column<byte[]>(type: "bytea", nullable: true, comment: "Número de documento cifrado (AES-GCM, ADR-0121) — nunca texto claro.")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_respostas_da_inscricao", x => x.id);
                    table.CheckConstraint("ck_respostas_da_inscricao_origem", "origem IN (1, 2)");
                    table.CheckConstraint("ck_respostas_da_inscricao_valor_do_tipo", "num_nonnulls(valor_booleano, valor_numero, valor_codigo, valor_texto, valor_data, valor_cifrado) = 1 AND tipo BETWEEN 1 AND 5 AND (valor_cifrado IS NOT NULL OR (tipo = 1 AND valor_booleano IS NOT NULL) OR (tipo = 2 AND valor_numero IS NOT NULL) OR (tipo = 3 AND valor_codigo IS NOT NULL) OR (tipo = 4 AND valor_texto IS NOT NULL) OR (tipo = 5 AND valor_data IS NOT NULL))");
                    table.ForeignKey(
                        name: "fk_respostas_da_inscricao_inscricoes_inscricao_id",
                        column: x => x.inscricao_id,
                        principalSchema: "selecao",
                        principalTable: "inscricoes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_respostas_da_inscricao_ocorrencias_de_grupo_da_inscricao_oc",
                        column: x => x.ocorrencia_id,
                        principalSchema: "selecao",
                        principalTable: "ocorrencias_de_grupo_da_inscricao",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                },
                comment: "Um valor respondido na inscrição, na coluna do tipo do fato, com a origem: informado pelo candidato ou derivado de outra resposta.");

            migrationBuilder.CreateIndex(
                name: "ux_aceites_da_inscricao_ordem",
                schema: "selecao",
                table: "aceites_da_inscricao",
                columns: new[] { "inscricao_id", "ordem" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_enderecos_da_inscricao_ocorrencia_id",
                schema: "selecao",
                table: "enderecos_da_inscricao",
                column: "ocorrencia_id");

            migrationBuilder.CreateIndex(
                name: "ux_enderecos_da_inscricao_fato",
                schema: "selecao",
                table: "enderecos_da_inscricao",
                columns: new[] { "inscricao_id", "ocorrencia_id", "fato_codigo" },
                unique: true)
                .Annotation("Npgsql:NullsDistinct", false);

            migrationBuilder.CreateIndex(
                name: "ux_ocorrencias_de_grupo_da_inscricao_membro",
                schema: "selecao",
                table: "ocorrencias_de_grupo_da_inscricao",
                columns: new[] { "inscricao_id", "grupo_codigo", "membro_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_respostas_da_inscricao_fato_booleano",
                schema: "selecao",
                table: "respostas_da_inscricao",
                columns: new[] { "fato_codigo", "valor_booleano" },
                filter: "valor_booleano IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_respostas_da_inscricao_fato_codigo",
                schema: "selecao",
                table: "respostas_da_inscricao",
                columns: new[] { "fato_codigo", "valor_codigo" },
                filter: "valor_codigo IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_respostas_da_inscricao_fato_data",
                schema: "selecao",
                table: "respostas_da_inscricao",
                columns: new[] { "fato_codigo", "valor_data" },
                filter: "valor_data IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_respostas_da_inscricao_fato_numero",
                schema: "selecao",
                table: "respostas_da_inscricao",
                columns: new[] { "fato_codigo", "valor_numero" },
                filter: "valor_numero IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_respostas_da_inscricao_ocorrencia_id",
                schema: "selecao",
                table: "respostas_da_inscricao",
                column: "ocorrencia_id");

            migrationBuilder.CreateIndex(
                name: "ux_respostas_da_inscricao_valor",
                schema: "selecao",
                table: "respostas_da_inscricao",
                columns: new[] { "inscricao_id", "ocorrencia_id", "fato_codigo", "posicao" },
                unique: true)
                .Annotation("Npgsql:NullsDistinct", false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "aceites_da_inscricao",
                schema: "selecao");

            migrationBuilder.DropTable(
                name: "enderecos_da_inscricao",
                schema: "selecao");

            migrationBuilder.DropTable(
                name: "respostas_da_inscricao",
                schema: "selecao");

            migrationBuilder.DropTable(
                name: "ocorrencias_de_grupo_da_inscricao",
                schema: "selecao");

            migrationBuilder.DropColumn(
                name: "derivados",
                schema: "selecao",
                table: "comprovantes_inscricao");

            migrationBuilder.AlterTable(
                name: "inscricoes",
                schema: "selecao",
                comment: "A inscrição de um candidato num processo seletivo (issue #1858) — uma por CPF e processo. Números de documento vivem cifrados aqui ou em documentos_cifrados, nunca em respostas_json.",
                oldComment: "A inscrição de um candidato num processo seletivo (issue #1858) — uma por CPF e processo. As respostas ficam em respostas_da_inscricao e nas tabelas vizinhas.");

            migrationBuilder.AddColumn<string>(
                name: "aceites",
                schema: "selecao",
                table: "inscricoes",
                type: "jsonb",
                nullable: false,
                defaultValue: "[]",
                comment: "Aceites de termo — código, versão e hash da versão fixada.");

            migrationBuilder.AddColumn<byte[]>(
                name: "documentos_cifrados",
                schema: "selecao",
                table: "inscricoes",
                type: "bytea",
                nullable: false,
                defaultValue: Array.Empty<byte>(),
                comment: "Envelope AEAD com o mapa {código do fato: valor} dos números de documento — nunca em claro.");

            migrationBuilder.AddColumn<string>(
                name: "fatos_identificador_declarados",
                schema: "selecao",
                table: "inscricoes",
                type: "jsonb",
                nullable: false,
                defaultValue: "[]",
                comment: "Códigos de fato cuja classificação congelada é IDENTIFICADOR, fixados ao iniciar.");

            migrationBuilder.AddColumn<string>(
                name: "grupos",
                schema: "selecao",
                table: "inscricoes",
                type: "jsonb",
                nullable: false,
                defaultValue: "{}",
                comment: "Ocorrências de grupo repetível, mesma exclusão de documento das respostas.");

            migrationBuilder.AddColumn<string>(
                name: "respostas",
                schema: "selecao",
                table: "inscricoes",
                type: "jsonb",
                nullable: false,
                defaultValue: "{}",
                comment: "Respostas por fato (código → valor), sem números de documento.");
        }
    }
}
