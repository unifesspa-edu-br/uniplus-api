using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class VerificacaoDosArquivosDoCandidato : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Os arquivos confirmados na versão anterior foram selados pela API e nunca passaram pela verificação:
            // não há dado a preservar (os selados são descartados e reenviados). Sem esta limpeza o valor 1, que
            // era "confirmado", passaria a significar "em verificação" sem ETag nem pedido.
            migrationBuilder.Sql("DELETE FROM selecao.arquivos_da_apresentacao WHERE status = 1;");
            migrationBuilder.Sql(
                "DELETE FROM selecao.apresentacoes_de_documento p WHERE NOT EXISTS " +
                "(SELECT 1 FROM selecao.arquivos_da_apresentacao a WHERE a.apresentacao_id = p.id);");

            migrationBuilder.DropColumn(
                name: "object_key_confirmado",
                schema: "selecao",
                table: "arquivos_da_apresentacao");

            migrationBuilder.RenameColumn(
                name: "status",
                schema: "selecao",
                table: "arquivos_da_apresentacao",
                newName: "situacao");

            migrationBuilder.AlterTable(
                name: "arquivos_da_apresentacao",
                schema: "selecao",
                comment: "O arquivo de uma apresentação de documento da inscrição. O conteúdo fica no armazenamento privado, cifrado pelo próprio armazenamento; o nome original do arquivo não é guardado.",
                oldComment: "O arquivo de uma apresentação de documento da inscrição. O conteúdo fica no armazenamento privado, cifrado; o nome original do arquivo não é guardado.");

            migrationBuilder.AlterColumn<long>(
                name: "tamanho_maximo_bytes",
                schema: "selecao",
                table: "arquivos_da_apresentacao",
                type: "bigint",
                nullable: false,
                comment: "O maior tamanho que a exigência admite para o formato; o teto do sistema (10 MB) só quando ela não define limite, e nunca acima de 20 MB. Copiado da exigência no início do envio.",
                oldClrType: typeof(long),
                oldType: "bigint",
                oldComment: "O maior tamanho que a exigência admite para o formato; o teto do sistema (10 MB) só quando ela não define limite. Copiado da exigência no início do envio.");

            migrationBuilder.AlterColumn<string>(
                name: "object_key",
                schema: "selecao",
                table: "arquivos_da_apresentacao",
                type: "character varying(500)",
                maxLength: 500,
                nullable: false,
                comment: "A chave do objeto no bucket de envio (quarentena); só o id do arquivo a compõe.",
                oldClrType: typeof(string),
                oldType: "character varying(500)",
                oldMaxLength: 500,
                oldComment: "A chave do objeto do envio, em claro; só o id do arquivo a compõe. Apagada na confirmação.");

            migrationBuilder.AlterColumn<string>(
                name: "hash_sha256",
                schema: "selecao",
                table: "arquivos_da_apresentacao",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true,
                comment: "O SHA-256 do conteúdo, calculado pelo verificador; nulo enquanto não verificado.",
                oldClrType: typeof(string),
                oldType: "character varying(64)",
                oldMaxLength: 64,
                oldNullable: true,
                oldComment: "O SHA-256 do conteúdo em claro, calculado no servidor na confirmação.");

            migrationBuilder.AlterColumn<bool>(
                name: "constou_de_envio",
                schema: "selecao",
                table: "arquivos_da_apresentacao",
                type: "boolean",
                nullable: false,
                comment: "O arquivo verificado já constou de um envio da inscrição; a limpeza dos envios seguintes nunca o apaga (UNI-REQ-0168).",
                oldClrType: typeof(bool),
                oldType: "boolean",
                oldComment: "O arquivo confirmado já constou de um envio da inscrição; a limpeza dos envios seguintes nunca o apaga (UNI-REQ-0168).");

            migrationBuilder.AddColumn<string>(
                name: "chave_definitiva",
                schema: "selecao",
                table: "arquivos_da_apresentacao",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true,
                comment: "A chave do objeto promovido ao bucket definitivo; nula enquanto não verificado.");

            migrationBuilder.AddColumn<string>(
                name: "etag",
                schema: "selecao",
                table: "arquivos_da_apresentacao",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true,
                comment: "O ETag que o PUT devolveu ao candidato, enviado na confirmação; fixa a versão do objeto que o verificador confere.");

            migrationBuilder.AddColumn<int>(
                name: "motivo_da_recusa",
                schema: "selecao",
                table: "arquivos_da_apresentacao",
                type: "integer",
                nullable: true,
                comment: "Por que a verificação recusou o arquivo; nulo fora da recusa.");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "proxima_publicacao_em",
                schema: "selecao",
                table: "arquivos_da_apresentacao",
                type: "timestamp with time zone",
                nullable: true,
                comment: "Quando a reconciliação pode republicar o pedido: a última publicação mais o prazo, que cresce a cada publicação.");

            migrationBuilder.AddColumn<int>(
                name: "publicacoes",
                schema: "selecao",
                table: "arquivos_da_apresentacao",
                type: "integer",
                nullable: false,
                defaultValue: 0,
                comment: "Quantas vezes o pedido de verificação foi publicado; o prazo da reconciliação dobra a cada uma.");

            migrationBuilder.Sql("ALTER TABLE selecao.arquivos_da_apresentacao ALTER COLUMN publicacoes DROP DEFAULT;");

            migrationBuilder.AlterColumn<int>(
                name: "situacao",
                schema: "selecao",
                table: "arquivos_da_apresentacao",
                type: "integer",
                nullable: false,
                comment: "Pendente (0), em verificação (1), verificado (2), recusado (3) ou reenvio necessário (4). Só o verificado atende a exigência.",
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.CreateIndex(
                name: "ix_arquivos_da_apresentacao_em_verificacao",
                schema: "selecao",
                table: "arquivos_da_apresentacao",
                column: "proxima_publicacao_em",
                filter: "situacao = 1");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_arquivos_da_apresentacao_em_verificacao",
                schema: "selecao",
                table: "arquivos_da_apresentacao");

            migrationBuilder.DropColumn(
                name: "chave_definitiva",
                schema: "selecao",
                table: "arquivos_da_apresentacao");

            migrationBuilder.DropColumn(
                name: "etag",
                schema: "selecao",
                table: "arquivos_da_apresentacao");

            migrationBuilder.DropColumn(
                name: "motivo_da_recusa",
                schema: "selecao",
                table: "arquivos_da_apresentacao");

            migrationBuilder.DropColumn(
                name: "proxima_publicacao_em",
                schema: "selecao",
                table: "arquivos_da_apresentacao");

            migrationBuilder.DropColumn(
                name: "publicacoes",
                schema: "selecao",
                table: "arquivos_da_apresentacao");

            migrationBuilder.AlterColumn<int>(
                name: "situacao",
                schema: "selecao",
                table: "arquivos_da_apresentacao",
                type: "integer",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer",
                oldComment: "Pendente (0), em verificação (1), verificado (2), recusado (3) ou reenvio necessário (4). Só o verificado atende a exigência.");

            migrationBuilder.RenameColumn(
                name: "situacao",
                schema: "selecao",
                table: "arquivos_da_apresentacao",
                newName: "status");

            migrationBuilder.AlterTable(
                name: "arquivos_da_apresentacao",
                schema: "selecao",
                comment: "O arquivo de uma apresentação de documento da inscrição. O conteúdo fica no armazenamento privado, cifrado; o nome original do arquivo não é guardado.",
                oldComment: "O arquivo de uma apresentação de documento da inscrição. O conteúdo fica no armazenamento privado, cifrado pelo próprio armazenamento; o nome original do arquivo não é guardado.");

            migrationBuilder.AlterColumn<long>(
                name: "tamanho_maximo_bytes",
                schema: "selecao",
                table: "arquivos_da_apresentacao",
                type: "bigint",
                nullable: false,
                comment: "O maior tamanho que a exigência admite para o formato; o teto do sistema (10 MB) só quando ela não define limite. Copiado da exigência no início do envio.",
                oldClrType: typeof(long),
                oldType: "bigint",
                oldComment: "O maior tamanho que a exigência admite para o formato; o teto do sistema (10 MB) só quando ela não define limite, e nunca acima de 20 MB. Copiado da exigência no início do envio.");

            migrationBuilder.AlterColumn<string>(
                name: "object_key",
                schema: "selecao",
                table: "arquivos_da_apresentacao",
                type: "character varying(500)",
                maxLength: 500,
                nullable: false,
                comment: "A chave do objeto do envio, em claro; só o id do arquivo a compõe. Apagada na confirmação.",
                oldClrType: typeof(string),
                oldType: "character varying(500)",
                oldMaxLength: 500,
                oldComment: "A chave do objeto no bucket de envio (quarentena); só o id do arquivo a compõe.");

            migrationBuilder.AlterColumn<string>(
                name: "hash_sha256",
                schema: "selecao",
                table: "arquivos_da_apresentacao",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true,
                comment: "O SHA-256 do conteúdo em claro, calculado no servidor na confirmação.",
                oldClrType: typeof(string),
                oldType: "character varying(64)",
                oldMaxLength: 64,
                oldNullable: true,
                oldComment: "O SHA-256 do conteúdo, calculado pelo verificador; nulo enquanto não verificado.");

            migrationBuilder.AlterColumn<bool>(
                name: "constou_de_envio",
                schema: "selecao",
                table: "arquivos_da_apresentacao",
                type: "boolean",
                nullable: false,
                comment: "O arquivo confirmado já constou de um envio da inscrição; a limpeza dos envios seguintes nunca o apaga (UNI-REQ-0168).",
                oldClrType: typeof(bool),
                oldType: "boolean",
                oldComment: "O arquivo verificado já constou de um envio da inscrição; a limpeza dos envios seguintes nunca o apaga (UNI-REQ-0168).");

            migrationBuilder.AddColumn<string>(
                name: "object_key_confirmado",
                schema: "selecao",
                table: "arquivos_da_apresentacao",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true,
                comment: "A chave do conteúdo selado cifrado; nula enquanto pendente.");
        }
    }
}
