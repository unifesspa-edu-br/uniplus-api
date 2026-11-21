namespace Unifesspa.UniPlus.Configuracao.Domain.ValueObjects;

using Unifesspa.UniPlus.Regras.Entradas;
using Unifesspa.UniPlus.Regras.Formularios;

/// <summary>
/// Um grupo repetível do modelo (ADR-0138, UNI-REQ-0146): a lista de ocorrências de um mesmo conjunto
/// de campos de fatos de membro, como a composição familiar, com as ocorrências admitidas — mínimo, máximo
/// e se o próprio candidato é um dos membros — e a exibição e a obrigatoriedade do grupo. A seção é a do
/// grupo; os campos não declaram seção própria.
/// </summary>
public sealed record GrupoDoModelo(
    string Codigo,
    int Ordem,
    string? EtapaCodigo,
    string Rotulo,
    OcorrenciasDoGrupo Ocorrencias,
    ExibicaoEObrigatoriedade Regras,
    IReadOnlyList<ItemDoModelo> Subitens)
{
    /// <summary>Os fatos que as regras do próprio grupo citam — a exibição e a obrigatoriedade.</summary>
    public IReadOnlyCollection<string> FatosCitados => Regras.FatosCitados;

    /// <summary>A exibição e a obrigatoriedade do grupo, cada uma com o caminho do campo dela na entrada.</summary>
    public IReadOnlyList<PredicadoNaEntrada> Predicados() =>
    [
        new($"{GrupoColetadoInput.CaminhoDasRegras}.exibicao", Regras.Exibicao),
        new($"{GrupoColetadoInput.CaminhoDasRegras}.obrigatoriedade.predicado", Regras.Obrigatoriedade.Predicado),
    ];
}
