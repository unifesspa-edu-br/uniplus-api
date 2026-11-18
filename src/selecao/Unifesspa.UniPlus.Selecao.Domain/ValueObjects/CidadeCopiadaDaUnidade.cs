namespace Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

using Unifesspa.UniPlus.Kernel.Domain.Cidades;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// A cidade da Unidade, copiada como o cadastro a informa: código IBGE, nome e UF, todos presentes ou nenhum (issue #1114).
/// Mesmo padrão de <c>Unidade.ValidarReferenciaCidade</c> e <c>Instituicao</c>.
/// </summary>
public sealed record CidadeCopiadaDaUnidade(string? CodigoIbge, string? Nome, string? Uf)
{
    /// <summary>A referência que o cadastro não informa: sem nenhum dos três.</summary>
    public static CidadeCopiadaDaUnidade Ausente { get; } = new(null, null, null);

    /// <summary>Recusa a referência parcial ou malformada; sem nenhum dos três campos, a cidade é aceita como ausente.</summary>
    public Result Validar()
    {
        bool algumPresente = !string.IsNullOrWhiteSpace(CodigoIbge)
            || !string.IsNullOrWhiteSpace(Nome)
            || !string.IsNullOrWhiteSpace(Uf);

        return algumPresente
            ? ReferenciaCidadeGeo.Validar(CodigoIbge, Nome, Uf)
            : Result.Success();
    }

    /// <summary>Normaliza o trio já validado por <see cref="Validar"/>: ou os três presentes, ou nenhum.</summary>
    public CidadeCopiadaDaUnidade Normalizada() =>
        string.IsNullOrWhiteSpace(CodigoIbge)
            ? Ausente
            : new(CodigoIbge.Trim(), Nome?.Trim(), Uf?.Trim().ToUpperInvariant());
}
