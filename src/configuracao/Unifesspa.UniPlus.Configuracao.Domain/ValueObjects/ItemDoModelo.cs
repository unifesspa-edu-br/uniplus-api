namespace Unifesspa.UniPlus.Configuracao.Domain.ValueObjects;

using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Regras.ValueObjects;

/// <summary>
/// Um item do modelo: o fato que o campo coleta, com as regras do item — exibição, obrigatoriedade,
/// restrições de valor e impedimento — sobre fatos anteriores (UNI-REQ-0145). O formato do campo de
/// texto é o do fato no catálogo; o impedimento cita também a resposta do próprio campo.
/// </summary>
public sealed record ItemDoModelo(
    string FatoCodigo,
    int Ordem,
    string? EtapaCodigo,
    string Rotulo,
    TipoRenderizacao TipoRenderizacao,
    string? Formato,
    string? Ajuda,
    Obrigatoriedade Obrigatoriedade,
    PredicadoDnf? Exibicao,
    IReadOnlyList<RestricaoValor> Restricoes,
    bool PedirConfirmacao,
    Impedimento? Impedimento = null)
{
    public IReadOnlyCollection<string> FatosCitados =>
        [.. (Exibicao?.FatosCitados ?? []).Concat(Obrigatoriedade.FatosCitados).Concat(Restricoes.SelectMany(static r => r.FatosCitados))
            .Concat((Impedimento?.FatosCitados ?? []).Where(f => !string.Equals(f, FatoCodigo, StringComparison.Ordinal)))
            .Distinct(StringComparer.Ordinal)];

    /// <summary>A exibição e a obrigatoriedade do item, cada uma com o caminho do campo dela na entrada.</summary>
    public IReadOnlyList<PredicadoNaEntrada> Predicados() =>
    [
        new("precondicao", Exibicao),
        new("predicadoObrigatoriedade", Obrigatoriedade.Predicado),
    ];
}
