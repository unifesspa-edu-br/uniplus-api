using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class GravaCifradosEVinculadosAoComprovanteOsDadosSensiveis : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<byte[]>(
                name: "valor_cifrado",
                schema: "selecao",
                table: "respostas_da_inscricao",
                type: "bytea",
                nullable: true,
                comment: "Resposta cifrada (AES-GCM, ADR-0121) — nunca texto claro: o número de documento, com a chave dos identificadores, ou o dado sensível de cifra individual, com a chave dos sensíveis.",
                oldClrType: typeof(byte[]),
                oldType: "bytea",
                oldNullable: true,
                oldComment: "Número de documento cifrado (AES-GCM, ADR-0121) — nunca texto claro.");

            migrationBuilder.AddColumn<byte[]>(
                name: "sensiveis_cifrados",
                schema: "selecao",
                table: "comprovantes_inscricao",
                type: "bytea",
                nullable: true,
                comment: "Respostas de dado pessoal sensível com cifra individual (identidade de gênero, orientação sexual), cifradas (AES-GCM, ADR-0121, chave dos sensíveis) — nunca texto claro.");

            migrationBuilder.AddColumn<byte[]>(
                name: "sensiveis_hmac",
                schema: "selecao",
                table: "comprovantes_inscricao",
                type: "bytea",
                nullable: true,
                comment: "HMAC do id do comprovante e das respostas sensíveis, que prende o envelope cifrado a este comprovante.");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "sensiveis_cifrados",
                schema: "selecao",
                table: "comprovantes_inscricao");

            migrationBuilder.DropColumn(
                name: "sensiveis_hmac",
                schema: "selecao",
                table: "comprovantes_inscricao");

            migrationBuilder.AlterColumn<byte[]>(
                name: "valor_cifrado",
                schema: "selecao",
                table: "respostas_da_inscricao",
                type: "bytea",
                nullable: true,
                comment: "Número de documento cifrado (AES-GCM, ADR-0121) — nunca texto claro.",
                oldClrType: typeof(byte[]),
                oldType: "bytea",
                oldNullable: true,
                oldComment: "Resposta cifrada (AES-GCM, ADR-0121) — nunca texto claro: o número de documento, com a chave dos identificadores, ou o dado sensível de cifra individual, com a chave dos sensíveis.");
        }
    }
}
