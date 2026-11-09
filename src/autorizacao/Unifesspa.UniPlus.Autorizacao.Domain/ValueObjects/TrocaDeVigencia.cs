namespace Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;

using Unifesspa.UniPlus.Autorizacao.Domain.Entities;

/// <summary>
/// A troca da composição em vigor de um perfil: a versão que deixou de valer e a que passou a valer. A primeira
/// vigência do perfil não tem versão anterior, e por isso não tem efeitos sobre concessões nem pedidos.
/// </summary>
/// <param name="Anterior">A versão que deixou de ser a em vigor; nula na primeira vigência.</param>
/// <param name="Nova">A versão que passou a ser a em vigor.</param>
public sealed record TrocaDeVigencia(VersaoDoPerfil? Anterior, VersaoDoPerfil Nova)
{
    /// <summary>A versão nova reúne alguma permissão que a anterior não reunia.</summary>
    public bool TemAcrescimo => Anterior is { } anterior && Nova.AcrescimosSobre(anterior).Count > 0;

    /// <summary>Os códigos que a anterior reunia e a nova deixou de reunir, ordenados.</summary>
    public IReadOnlyList<string> Removidas =>
        Anterior is { } anterior ? [.. anterior.Codigos().Except(Nova.Codigos(), StringComparer.Ordinal)] : [];
}
