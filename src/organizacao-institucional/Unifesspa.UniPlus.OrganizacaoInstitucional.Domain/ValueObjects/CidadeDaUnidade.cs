namespace Unifesspa.UniPlus.OrganizacaoInstitucional.Domain.ValueObjects;

using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Referência de cidade do Geo (ADR-0090) de uma Unidade: código IBGE mais o nome e a UF
/// guardados para exibição. Opcional all-or-nothing: ausente por completo é válido, e
/// qualquer fragmento exige o trio.
/// </summary>
public readonly record struct CidadeDaUnidade(string? CodigoIbge, string? Nome, string? Uf)
{
    public static CidadeDaUnidade Nenhuma => default;

    internal bool Informada => !string.IsNullOrWhiteSpace(CodigoIbge);

    internal IReadOnlyList<FieldError> Validar() => ConferenciaDaCidade.Conferir(CodigoIbge, Nome, Uf);
}
