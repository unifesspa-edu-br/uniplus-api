namespace Unifesspa.UniPlus.Configuracao.Domain.ValueObjects;

using Unifesspa.UniPlus.Regras.Entradas;
using Unifesspa.UniPlus.Regras.Formularios;

/// <summary>Um termo exigido pelo modelo: a versão do catálogo escolhida e as condições de exibição e de obrigatoriedade.</summary>
public sealed record TermoDoModelo(string Codigo, int Ordem, Guid TermoId, Guid VersaoId, ExibicaoEObrigatoriedade Regras)
{
    public IReadOnlyCollection<string> FatosCitados => Regras.FatosCitados;

    /// <summary>A exibição e a obrigatoriedade do termo, cada uma com o caminho do campo dela na entrada.</summary>
    public IReadOnlyList<PredicadoNaEntrada> Predicados() =>
    [
        new($"{TermoExigidoInput.CaminhoDasRegras}.exibicao", Regras.Exibicao),
        new($"{TermoExigidoInput.CaminhoDasRegras}.obrigatoriedade.predicado", Regras.Obrigatoriedade.Predicado),
    ];
}
