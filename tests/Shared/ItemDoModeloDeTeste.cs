namespace Unifesspa.UniPlus.Testes.Compartilhado;

using Unifesspa.UniPlus.Configuracao.Domain.ValueObjects;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Regras.ValueObjects;

/// <summary>
/// Ajusta um item do modelo de partida, que o teste monta já válido, em só uma parte da apresentação ou
/// das regras do campo: o item devolvido é novo e o de partida não muda.
/// </summary>
internal static class ItemDoModeloDeTeste
{
    public static ItemDoModelo ComRotulo(this ItemDoModelo item, string rotulo) =>
        item.ComApresentacao(a => new ApresentacaoDoCampo(rotulo, a.TipoDeCampo, a.Ajuda, a.PedirConfirmacao));

    public static ItemDoModelo ComAjuda(this ItemDoModelo item, string? ajuda) =>
        item.ComApresentacao(a => new ApresentacaoDoCampo(a.Rotulo, a.TipoDeCampo, ajuda, a.PedirConfirmacao));

    public static ItemDoModelo ComTipoDeCampo(this ItemDoModelo item, TipoDeCampo tipo) =>
        item.ComApresentacao(a => new ApresentacaoDoCampo(a.Rotulo, tipo, a.Ajuda, a.PedirConfirmacao));

    public static ItemDoModelo ComExibicao(this ItemDoModelo item, PredicadoDnf? exibicao) =>
        item.ComRegras(r => new RegrasDoCampo(exibicao, r.Obrigatoriedade, r.Restricoes, r.Impedimento));

    public static ItemDoModelo ComObrigatoriedade(this ItemDoModelo item, Obrigatoriedade obrigatoriedade) =>
        item.ComRegras(r => new RegrasDoCampo(r.Exibicao, obrigatoriedade, r.Restricoes, r.Impedimento));

    public static ItemDoModelo ComRestricoes(this ItemDoModelo item, IReadOnlyList<RestricaoValor> restricoes) =>
        item.ComRegras(r => new RegrasDoCampo(r.Exibicao, r.Obrigatoriedade, restricoes, r.Impedimento));

    public static ItemDoModelo ComImpedimento(this ItemDoModelo item, Impedimento? impedimento) =>
        item.ComRegras(r => new RegrasDoCampo(r.Exibicao, r.Obrigatoriedade, r.Restricoes, impedimento));

    private static ItemDoModelo ComApresentacao(this ItemDoModelo item, Func<ApresentacaoDoCampo, ApresentacaoDoCampo> ajustar) =>
        item with { Apresentacao = ajustar(item.Apresentacao) };

    private static ItemDoModelo ComRegras(this ItemDoModelo item, Func<RegrasDoCampo, RegrasDoCampo> ajustar) =>
        item with { Regras = ajustar(item.Regras) };
}
