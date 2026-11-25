namespace Unifesspa.UniPlus.Configuracao.Domain.ValueObjects;

using Unifesspa.UniPlus.Regras.Entradas;
using Unifesspa.UniPlus.Regras.Formularios;

/// <summary>
/// Um item do modelo: o fato que o campo coleta, com a apresentação do campo e as regras do item —
/// exibição, obrigatoriedade, restrições de valor e impedimento — sobre fatos anteriores (UNI-REQ-0145).
/// O formato do campo de texto é o do fato no catálogo; o impedimento cita também a resposta do próprio campo.
/// </summary>
public sealed record ItemDoModelo(
    string FatoCodigo,
    int Ordem,
    string? PaginaCodigo,
    ApresentacaoDoCampo Apresentacao,
    string? Formato,
    RegrasDoCampo Regras)
{
    public IReadOnlyCollection<string> FatosCitados => Regras.FatosCitadosPor(FatoCodigo);

    /// <summary>A exibição e a obrigatoriedade do item, cada uma com o caminho do campo dela na entrada.</summary>
    public IReadOnlyList<PredicadoNaEntrada> Predicados() =>
    [
        new($"{FatoColetadoInput.CaminhoDasRegras}.exibicao", Regras.Exibicao),
        new($"{FatoColetadoInput.CaminhoDasRegras}.obrigatoriedade.predicado", Regras.Obrigatoriedade.Predicado),
    ];
}
