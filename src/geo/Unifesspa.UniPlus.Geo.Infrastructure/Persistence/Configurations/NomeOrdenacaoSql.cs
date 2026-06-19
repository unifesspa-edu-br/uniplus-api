namespace Unifesspa.UniPlus.Geo.Infrastructure.Persistence.Configurations;

/// <summary>
/// Expressão SQL compartilhada pelas colunas geradas de ordenação alfabética de
/// Estado/Cidade. Mantém busca (<c>nome_normalizado</c>) e ordenação
/// (<c>nome_ordenacao</c>) separadas: se a fonte não trouxer o texto sem acento,
/// a chave de ordenação cai para o nome público normalizado.
/// </summary>
internal static class NomeOrdenacaoSql
{
    private const string Acentos = "àáâãäçèéêëìíîïñòóôõöùúûüÀÁÂÃÄÇÈÉÊËÌÍÎÏÑÒÓÔÕÖÙÚÛÜ";
    private const string SemAcento = "aaaaaceeeeiiiinooooouuuuAAAAACEEEEIIIINOOOOOUUUU";

    public const string Expression =
        "lower(translate(coalesce(nullif(nome_normalizado, ''), nome), '" +
        Acentos + "', '" +
        SemAcento + "'))";
}
