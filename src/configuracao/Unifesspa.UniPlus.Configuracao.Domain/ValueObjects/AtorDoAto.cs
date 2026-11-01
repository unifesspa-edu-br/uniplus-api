namespace Unifesspa.UniPlus.Configuracao.Domain.ValueObjects;

/// <summary>
/// Quem praticou um ato do circuito de aprovação da proteção de dados: o emissor e o <c>sub</c> do token
/// (ADR-0033), que identificam a pessoa, e o nome de exibição que o token trazia no instante do ato.
/// </summary>
/// <param name="Emissor">O emissor (<c>iss</c>) do token.</param>
/// <param name="Sub">O identificador (<c>sub</c>) da pessoa no emissor.</param>
/// <param name="Nome">O nome de exibição no instante do ato; nulo quando o token não trazia nome.</param>
public sealed record AtorDoAto(string Emissor, string Sub, string? Nome)
{
    /// <summary>Tamanho máximo do nome, também o da coluna que o guarda.</summary>
    public const int NomeMaxLength = 255;

    /// <summary>Tamanho máximo do emissor e do sub, também o das colunas que os guardam.</summary>
    public const int IdentidadeMaxLength = 255;

    /// <summary>
    /// O nome aparado; nulo quando vazio ou com caractere nulo, que a coluna textual do banco não armazena. Vindo
    /// longo demais, perde o excedente em vez de impedir o ato.
    /// </summary>
    public static string? NormalizarNome(string? nome)
    {
        if (string.IsNullOrWhiteSpace(nome) || nome.Contains('\0', StringComparison.Ordinal))
        {
            return null;
        }

        string aparado = nome.Trim();

        return aparado.Length > NomeMaxLength ? aparado[..NomeMaxLength] : aparado;
    }
}
