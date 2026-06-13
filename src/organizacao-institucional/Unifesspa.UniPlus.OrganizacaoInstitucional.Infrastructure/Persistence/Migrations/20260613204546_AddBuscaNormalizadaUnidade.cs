using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Unifesspa.UniPlus.OrganizacaoInstitucional.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddBuscaNormalizadaUnidade : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "busca_normalizada",
                table: "unidade",
                type: "text",
                nullable: false,
                defaultValue: "");

            // Backfill das linhas pré-existentes (issue #640). Reproduz a semântica
            // do NormalizadorTermoBusca (remove diacríticos + dobra a caixa para
            // maiúsculas) usando apenas funções IMMUTABLE nativas — normalize +
            // translate + upper — sem depender da extensão unaccent. O
            // normalize(..., NFC) compõe eventuais marcas combinantes em precompostos
            // antes do translate, casando o resultado do NFD-strip feito em C# tanto
            // para texto composto quanto decomposto. Cobre o conjunto de acentos do
            // pt-BR (á/à/â/ã/ä, é/ê/è/ë, í/ì/î/ï, ó/ò/ô/õ/ö, ú/ù/û/ü, ç, ñ) em ambas
            // as caixas. Em ambientes recém-criados (incl. Testcontainers) a tabela
            // está vazia e este UPDATE é no-op; linhas novas/editadas são preenchidas
            // pelo agregado. Concatena os campos pesquisáveis na mesma ordem do índice.
            migrationBuilder.Sql(
                """
                UPDATE unidade
                SET busca_normalizada = upper(translate(
                    normalize(
                        trim(
                            coalesce(nome, '') || ' ' ||
                            coalesce(sigla, '') || ' ' ||
                            coalesce(codigo, '') || ' ' ||
                            coalesce(slug, '') || ' ' ||
                            coalesce(alias, '')),
                        NFC),
                    'áàâãäéêèëíìîïóòôõöúùûüçñÁÀÂÃÄÉÊÈËÍÌÎÏÓÒÔÕÖÚÙÛÜÇÑ',
                    'aaaaaeeeeiiiiooooouuuucnaaaaaeeeeiiiiooooouuuucn'));
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Forward-only per ADR-0054 §J: nova migration "Reverte..." é o
            // mecanismo canônico de revert. Dropar a coluna aqui via
            // `database update <baseline>` removeria silenciosamente o índice de
            // busca em staging/prod — caminho proibido.
            throw new System.NotSupportedException("Forward-only migration per ADR-0054 §J.");
        }
    }
}
