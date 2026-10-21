using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AdicionaOsDocumentosDaInscricao : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "apresentacoes_de_documento",
                schema: "selecao",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    inscricao_id = table.Column<Guid>(type: "uuid", nullable: false),
                    exigencia_id = table.Column<Guid>(type: "uuid", nullable: false, comment: "O DocumentoExigido.Id da árvore congelada da versão da inscrição. Sem chave estrangeira para a configuração viva (ADR-0070)."),
                    ocorrencia_id = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true, comment: "A ocorrência do grupo repetível a que o documento se refere; nulo quando a exigência não se repete."),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_apresentacoes_de_documento", x => x.id);
                    table.ForeignKey(
                        name: "fk_apresentacoes_de_documento_inscricoes_inscricao_id",
                        column: x => x.inscricao_id,
                        principalSchema: "selecao",
                        principalTable: "inscricoes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                },
                comment: "A apresentação de um documento exigido pela inscrição: a unidade que a exigência conta, com um ou mais arquivos.");

            migrationBuilder.CreateTable(
                name: "arquivos_da_apresentacao",
                schema: "selecao",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    apresentacao_id = table.Column<Guid>(type: "uuid", nullable: false),
                    inscricao_id = table.Column<Guid>(type: "uuid", nullable: false),
                    formato = table.Column<int>(type: "integer", nullable: false, comment: "O formato do arquivo (PDF, JPEG ou PNG), conferido contra a exigência no início do envio."),
                    tamanho_maximo_bytes = table.Column<long>(type: "bigint", nullable: false, comment: "O maior tamanho que a exigência admite para o formato; o teto do sistema (10 MB) só quando ela não define limite. Copiado da exigência no início do envio."),
                    object_key = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false, comment: "A chave do objeto do envio, em claro; só o id do arquivo a compõe. Apagada na confirmação."),
                    status = table.Column<int>(type: "integer", nullable: false),
                    expira_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    tamanho_bytes = table.Column<long>(type: "bigint", nullable: true),
                    hash_sha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true, comment: "O SHA-256 do conteúdo em claro, calculado no servidor na confirmação."),
                    confirmado_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    object_key_confirmado = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true, comment: "A chave do conteúdo selado cifrado; nula enquanto pendente."),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_arquivos_da_apresentacao", x => x.id);
                    table.ForeignKey(
                        name: "fk_arquivos_da_apresentacao_apresentacoes_de_documento_apresen",
                        column: x => x.apresentacao_id,
                        principalSchema: "selecao",
                        principalTable: "apresentacoes_de_documento",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_arquivos_da_apresentacao_inscricoes_inscricao_id",
                        column: x => x.inscricao_id,
                        principalSchema: "selecao",
                        principalTable: "inscricoes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                },
                comment: "O arquivo de uma apresentação de documento da inscrição. O conteúdo fica no armazenamento privado, cifrado; o nome original do arquivo não é guardado.");

            migrationBuilder.CreateIndex(
                name: "ix_apresentacoes_de_documento_inscricao_exigencia",
                schema: "selecao",
                table: "apresentacoes_de_documento",
                columns: new[] { "inscricao_id", "exigencia_id" });

            migrationBuilder.CreateIndex(
                name: "ix_arquivos_da_apresentacao_apresentacao",
                schema: "selecao",
                table: "arquivos_da_apresentacao",
                column: "apresentacao_id");

            migrationBuilder.CreateIndex(
                name: "ix_arquivos_da_apresentacao_inscricao",
                schema: "selecao",
                table: "arquivos_da_apresentacao",
                column: "inscricao_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "arquivos_da_apresentacao",
                schema: "selecao");

            migrationBuilder.DropTable(
                name: "apresentacoes_de_documento",
                schema: "selecao");
        }
    }
}
