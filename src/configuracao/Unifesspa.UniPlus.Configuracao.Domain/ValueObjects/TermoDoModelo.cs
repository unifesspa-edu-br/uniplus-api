namespace Unifesspa.UniPlus.Configuracao.Domain.ValueObjects;

using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Regras.ValueObjects;

/// <summary>Um termo exigido pelo modelo: a versão do catálogo escolhida e as condições de exibição e de obrigatoriedade.</summary>
public sealed record TermoDoModelo(string Codigo, int Ordem, Guid TermoId, Guid VersaoId, PredicadoDnf? Exibicao, Obrigatoriedade Obrigatoriedade)
{
    public IReadOnlyCollection<string> FatosCitados =>
        [.. (Exibicao?.FatosCitados ?? []).Concat(Obrigatoriedade.FatosCitados).Distinct(StringComparer.Ordinal)];

    /// <summary>A exibição e a obrigatoriedade do termo, cada uma com o caminho do campo dela na entrada.</summary>
    public IReadOnlyList<PredicadoNaEntrada> Predicados() =>
    [
        new("exibicao", Exibicao),
        new("predicadoObrigatoriedade", Obrigatoriedade.Predicado),
    ];
}
