namespace Unifesspa.UniPlus.Configuracao.Domain.ValueObjects;

using Unifesspa.UniPlus.Regras.Entradas;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Regras.ValueObjects;

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

    public IEnumerable<PredicadoDnf> PredicadosDasRegras() =>
        Predicados().Select(static p => p.Predicado).Append(Regras.Impedimento?.Quando)
            .Concat(Regras.Restricoes.OfType<OpcoesPermitidas>().SelectMany(static o => o.Entradas.Select(static e => e.Quando)))
            .OfType<PredicadoDnf>();

    public IEnumerable<(string Fato, string Valor)> ValoresPermitidos() =>
        Regras.Restricoes.OfType<OpcoesPermitidas>().SelectMany(static o => o.Entradas).SelectMany(e => e.Valores.Select(v => (FatoCodigo, v)));

    /// <summary>O que o item vincula no catálogo: o dado que coleta, os que as restrições e as condições citam, e os valores de cada um.</summary>
    public VinculosDeFatos Vinculos() => VinculosDeFatos.De(
        Regras.Restricoes.SelectMany(static r => r.FatosCitados).Prepend(FatoCodigo),
        RegrasLidasDoCampo.De(Regras).CondicoesDoCampo(FatoCodigo).Select(static c => (c.Fato, c.Valor)));
}
