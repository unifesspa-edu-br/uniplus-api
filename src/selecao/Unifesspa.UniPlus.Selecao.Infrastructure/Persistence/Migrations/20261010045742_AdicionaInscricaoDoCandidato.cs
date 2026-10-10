using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AdicionaInscricaoDoCandidato : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "inscricoes",
                schema: "selecao",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    processo_seletivo_id = table.Column<Guid>(type: "uuid", nullable: false, comment: "O processo seletivo desta inscrição."),
                    cpf_ciphertext = table.Column<byte[]>(type: "bytea", nullable: false, comment: "CPF cifrado em repouso (AES-GCM, ADR-0121) — envelope autenticado; nunca texto claro."),
                    cpf_indice = table.Column<byte[]>(type: "bytea", nullable: false, comment: "Índice cego (HMAC-SHA256) do CPF — sustenta a busca pelo dono e o índice único por processo, sem decifrar a tabela."),
                    sub = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false, comment: "O sub do token de quem iniciou a inscrição — nunca troca de dono."),
                    emissor_token = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false, comment: "O emissor (issuer) do token de quem iniciou a inscrição."),
                    situacao = table.Column<int>(type: "integer", nullable: false),
                    versao_formulario_id = table.Column<Guid>(type: "uuid", nullable: false, comment: "A versão do formulário divulgada no instante do início (UNI-REQ-0084) — fixa, nunca recalculada."),
                    oferta_curso_id = table.Column<Guid>(type: "uuid", nullable: true),
                    numero_inscricao = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true, comment: "Gerado na primeira submissão (sequência do banco, formatada); nulo em rascunho."),
                    respostas = table.Column<string>(type: "jsonb", nullable: false, comment: "Respostas por fato (código → valor), sem números de documento."),
                    grupos = table.Column<string>(type: "jsonb", nullable: false, comment: "Ocorrências de grupo repetível, mesma exclusão de documento das respostas."),
                    aceites = table.Column<string>(type: "jsonb", nullable: false, comment: "Aceites de termo — código, versão e hash da versão fixada."),
                    fatos_identificador_declarados = table.Column<string>(type: "jsonb", nullable: false, comment: "Códigos de fato cuja classificação congelada é IDENTIFICADOR, fixados ao iniciar."),
                    documentos_cifrados = table.Column<byte[]>(type: "bytea", nullable: false, comment: "Envelope AEAD com o mapa {código do fato: valor} dos números de documento — nunca em claro."),
                    revisao = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_inscricoes", x => x.id);
                },
                comment: "A inscrição de um candidato num processo seletivo (issue #1858) — uma por CPF e processo. Números de documento vivem cifrados aqui ou em documentos_cifrados, nunca em respostas_json.");

            migrationBuilder.CreateTable(
                name: "comprovantes_inscricao",
                schema: "selecao",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    inscricao_id = table.Column<Guid>(type: "uuid", nullable: false),
                    numero_inscricao = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    instante = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    hash_integridade = table.Column<string>(type: "character(64)", fixedLength: true, maxLength: 64, nullable: false),
                    oferta_curso_id = table.Column<Guid>(type: "uuid", nullable: true),
                    respostas = table.Column<string>(type: "jsonb", nullable: false),
                    grupos = table.Column<string>(type: "jsonb", nullable: false),
                    aceites = table.Column<string>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_comprovantes_inscricao", x => x.id);
                    table.ForeignKey(
                        name: "fk_comprovantes_inscricao_inscricoes",
                        column: x => x.inscricao_id,
                        principalSchema: "selecao",
                        principalTable: "inscricoes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                },
                comment: "Comprovante de uma submissão da inscrição (issue #1858) — append-only, sem nenhum número de documento; uma correção soma uma linha nova, nunca muda a anterior.");

            migrationBuilder.CreateIndex(
                name: "ix_comprovantes_inscricao_inscricao_numero",
                schema: "selecao",
                table: "comprovantes_inscricao",
                columns: new[] { "inscricao_id", "numero_inscricao" });

            migrationBuilder.CreateIndex(
                name: "ux_inscricoes_processo_cpf_indice",
                schema: "selecao",
                table: "inscricoes",
                columns: new[] { "processo_seletivo_id", "cpf_indice" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "comprovantes_inscricao",
                schema: "selecao");

            migrationBuilder.DropTable(
                name: "inscricoes",
                schema: "selecao");
        }
    }
}
