namespace Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;

using Unifesspa.UniPlus.Autorizacao.Domain.Interfaces;

/// <summary>
/// O que a migração de uma concessão avalia: a composição da versão-alvo, o que ela acrescenta à composição que a
/// concessão tem hoje e se esse acréscimo exige a proteção de dados. A classe é a do acréscimo, nunca a do perfil.
/// </summary>
/// <param name="Avaliada">Os códigos da versão-alvo, ordenados.</param>
/// <param name="Acrescimo">Os códigos da versão-alvo que a concessão ainda não dá.</param>
/// <param name="ExigeProtecaoDeDados">Algum código do acréscimo é pessoal, identificador ou sensível.</param>
public sealed record ComposicaoDoLote(IReadOnlyList<string> Avaliada, IReadOnlyList<string> Acrescimo, bool ExigeProtecaoDeDados)
{
    public static ComposicaoDoLote Calcular(
        IReadOnlyList<string> alvo, IReadOnlyList<string> daConcessao, ICatalogoDePermissoesConsultavel catalogo)
    {
        ArgumentNullException.ThrowIfNull(alvo);
        ArgumentNullException.ThrowIfNull(daConcessao);
        ArgumentNullException.ThrowIfNull(catalogo);

        DiferencaDaVersao diferenca = DiferencaDaVersao.Calcular(alvo, daConcessao, catalogo);

        return new ComposicaoDoLote(alvo, diferenca.Acrescimo, diferenca.ExigeProtecaoDeDados);
    }
}
