using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class LimpaOEnvioDoArquivoConfirmado : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "envio_limpo_em",
                schema: "selecao",
                table: "arquivos_da_apresentacao",
                type: "timestamp with time zone",
                nullable: true,
                comment: "Quando a chave de envio, em claro, foi limpa depois de vencida a URL de envio; nulo no confirmado = ainda a limpar.");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "envio_limpo_em",
                schema: "selecao",
                table: "arquivos_da_apresentacao");
        }
    }
}
