namespace Unifesspa.UniPlus.Authorization.UnitTests.Projecao;

using AwesomeAssertions;

using Unifesspa.UniPlus.Authorization.Contracts;
using Unifesspa.UniPlus.Authorization.Projecao;
using Unifesspa.UniPlus.Authorization.UnitTests.Decisao;
using Unifesspa.UniPlus.Kernel.Domain.Enums;
using Unifesspa.UniPlus.Testes.Compartilhado;

/// <summary>O item só sai com a decisão permitida do par e, sendo sensível, com a base legal congelada.</summary>
public sealed class DecisoesDaProjecaoTests
{
    private static readonly DecisaoPermitida Permitida = DecisaoDeAcessoDeTeste.Permitida(CenariosDeDecisao.Requisito());

    private static DecisoesDaProjecao Com(params (NaturezaDoItem, ClassificacaoDeProtecao)[] pares) =>
        new(pares.ToDictionary(static p => p, static _ => Permitida));

    [Fact(DisplayName = "Sem a decisão permitida do par o item não sai, e a permissão de dado não abre arquivo")]
    public void Permite_ParSemDecisao_Omite()
    {
        DecisoesDaProjecao decisoes = Com((NaturezaDoItem.Dado, ClassificacaoDeProtecao.Pessoal));

        decisoes.ItemSai(new Item(NaturezaDoItem.Dado, ClassificacaoDeProtecao.Pessoal, null)).Should().BeTrue();
        decisoes.ItemSai(new Item(NaturezaDoItem.Dado, ClassificacaoDeProtecao.Interno, null)).Should().BeFalse();
        decisoes.ItemSai(new Item(NaturezaDoItem.Arquivo, ClassificacaoDeProtecao.Pessoal, null)).Should().BeFalse();
    }

    [Fact(DisplayName = "O item sensível sem base legal congelada não sai, mesmo para quem tem a permissão")]
    public void Permite_SensivelSemBaseLegal_Omite()
    {
        DecisoesDaProjecao decisoes = Com((NaturezaDoItem.Dado, ClassificacaoDeProtecao.Sensivel));

        decisoes.ItemSai(new Item(NaturezaDoItem.Dado, ClassificacaoDeProtecao.Sensivel, " ")).Should().BeFalse();
        decisoes.ItemSai(new Item(NaturezaDoItem.Dado, ClassificacaoDeProtecao.Sensivel, "CONSENTIMENTO")).Should().BeTrue();
    }

    [Fact(DisplayName = "A classificação não resolvida vale como sensível")]
    public void Permite_ClassificacaoNaoResolvida_ValeSensivel()
    {
        Item naoResolvido = new(NaturezaDoItem.Dado, ClassificacaoDeProtecao.Nenhuma, "CONSENTIMENTO");

        Com((NaturezaDoItem.Dado, ClassificacaoDeProtecao.Pessoal)).ItemSai(naoResolvido).Should().BeFalse();
        Com((NaturezaDoItem.Dado, ClassificacaoDeProtecao.Sensivel)).ItemSai(naoResolvido).Should().BeTrue();
    }

    private sealed record Item(NaturezaDoItem Natureza, ClassificacaoDeProtecao Classificacao, string? BaseLegal) : IItemClassificado;
}
