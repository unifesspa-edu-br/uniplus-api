namespace Unifesspa.UniPlus.OrganizacaoInstitucional.Domain.ValueObjects;

using Unifesspa.UniPlus.Kernel.Domain.Cidades;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Referência de cidade do Geo (ADR-0090) da sede da Instituição: o trio código IBGE, nome e UF
/// mais a proveniência e o instante do display cache. Opcional all-or-nothing: ausente por
/// completo é válido, e qualquer fragmento exige o trio.
/// </summary>
public readonly record struct CidadeDaSede(
    string? CodigoIbge,
    string? Nome,
    string? Uf,
    string? Origem,
    DateTimeOffset? DisplayAtualizadoEm)
{
    public static CidadeDaSede Nenhuma => default;

    internal bool Informada => !string.IsNullOrWhiteSpace(CodigoIbge);

    /// <summary>
    /// Carimba a proveniência do Geo e o instante só quando o trio informado muda em relação ao
    /// <paramref name="anterior"/>, comparando os valores normalizados (código e nome aparados, UF
    /// em caixa alta); assim o instante rastreia a última reconciliação da cidade, não qualquer
    /// edição de outro campo. Sem cidade, proveniência e instante ficam nulos.
    /// </summary>
    public CidadeDaSede Carimbada(CidadeDaSede anterior, DateTimeOffset agora)
    {
        CidadeDaSede informada = Normalizada();
        if (!informada.Informada)
        {
            return Nenhuma;
        }

        return informada.MesmoTrioQue(anterior.Normalizada())
            ? informada with { Origem = anterior.Origem, DisplayAtualizadoEm = anterior.DisplayAtualizadoEm }
            : informada with { Origem = ReferenciaCidadeGeo.OrigemGeoApi, DisplayAtualizadoEm = agora };
    }

    internal IReadOnlyList<FieldError> Validar() => ConferenciaDaCidade.Conferir(CodigoIbge, Nome, Uf);

    /// <summary>
    /// A cidade como se grava: com cidade informada, o trio aparado (UF em caixa alta) e o display
    /// cache; sem cidade, nada. O código IBGE é quem diz se há cidade, porque a validação garante
    /// que, se qualquer fragmento veio, o trio completo veio.
    /// </summary>
    internal CidadeDaSede Normalizada() => Informada
        ? new(CodigoIbge?.Trim(), Nome?.Trim(), Uf?.Trim().ToUpperInvariant(), NormalizarOpcional(Origem), DisplayAtualizadoEm)
        : Nenhuma;

    private bool MesmoTrioQue(CidadeDaSede outra) =>
        string.Equals(CodigoIbge, outra.CodigoIbge, StringComparison.Ordinal)
        && string.Equals(Nome, outra.Nome, StringComparison.Ordinal)
        && string.Equals(Uf, outra.Uf, StringComparison.Ordinal);

    private static string? NormalizarOpcional(string? valor) =>
        string.IsNullOrWhiteSpace(valor) ? null : valor.Trim();
}
