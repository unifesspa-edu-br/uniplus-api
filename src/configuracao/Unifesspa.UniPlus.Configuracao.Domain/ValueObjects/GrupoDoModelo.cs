namespace Unifesspa.UniPlus.Configuracao.Domain.ValueObjects;

using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Regras.ValueObjects;

/// <summary>
/// Um grupo repetível do modelo (ADR-0138, UNI-REQ-0146): a lista de ocorrências de um mesmo conjunto
/// de campos de fatos de membro, como a composição familiar, com mínimo e, quando declarado, máximo
/// de ocorrências, e se o próprio candidato é um dos membros. A seção é a do grupo; os campos não
/// declaram seção própria.
/// </summary>
public sealed record GrupoDoModelo(
    string Codigo,
    int Ordem,
    string? EtapaCodigo,
    string Rotulo,
    int Minimo,
    int? Maximo,
    PredicadoDnf? Exibicao,
    Obrigatoriedade Obrigatoriedade,
    IReadOnlyList<ItemDoModelo> Subitens,
    bool IncluiCandidato = false)
{
    /// <summary>Os fatos que as regras do próprio grupo citam — a exibição e a obrigatoriedade.</summary>
    public IReadOnlyCollection<string> FatosCitados =>
        [.. (Exibicao?.FatosCitados ?? []).Concat(Obrigatoriedade.FatosCitados).Distinct(StringComparer.Ordinal)];

    /// <summary>A exibição e a obrigatoriedade do grupo, cada uma com o caminho do campo dela na entrada.</summary>
    public IReadOnlyList<PredicadoNaEntrada> Predicados() =>
    [
        new("exibicao", Exibicao),
        new("predicadoObrigatoriedade", Obrigatoriedade.Predicado),
    ];
}
