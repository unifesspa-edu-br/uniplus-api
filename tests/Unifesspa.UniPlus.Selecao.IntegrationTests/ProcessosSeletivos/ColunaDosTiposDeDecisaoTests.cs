namespace Unifesspa.UniPlus.Selecao.IntegrationTests.ProcessosSeletivos;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore.ChangeTracking;

using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Configurations;

using Xunit;

/// <summary>
/// O comparador que o rastreamento usa para perceber que a lista de tipos de decisão mudou no lugar.
/// Não toca em banco.
/// </summary>
public sealed class ColunaDosTiposDeDecisaoTests
{
    private static readonly string[] Classificacao = ["CLASSIFICACAO"];

    [Fact(DisplayName = "Comparador — listas com os mesmos tipos na mesma ordem são iguais")]
    public void ListasComOsMesmosTiposNaMesmaOrdem_SaoIguais()
    {
        ColunaDosTiposDeDecisao.SaoIguais(["ANALISE_DOCUMENTAL", "CLASSIFICACAO"], ["ANALISE_DOCUMENTAL", "CLASSIFICACAO"])
            .Should().BeTrue();
        ColunaDosTiposDeDecisao.SaoIguais([], []).Should().BeTrue("duas declarações vazias são a mesma declaração");
    }

    [Fact(DisplayName = "Comparador — a ordem, o conteúdo e o tamanho distinguem as listas")]
    public void OrdemConteudoETamanho_DistinguemAsListas()
    {
        ColunaDosTiposDeDecisao.SaoIguais(["ANALISE_DOCUMENTAL", "CLASSIFICACAO"], ["CLASSIFICACAO", "ANALISE_DOCUMENTAL"])
            .Should().BeFalse("a lista guardada é canônica, e a ordem diferente é mudança");
        ColunaDosTiposDeDecisao.SaoIguais(["CLASSIFICACAO"], ["HETEROIDENTIFICACAO"]).Should().BeFalse();
        ColunaDosTiposDeDecisao.SaoIguais(["CLASSIFICACAO"], ["CLASSIFICACAO", "HETEROIDENTIFICACAO"]).Should().BeFalse();
        ColunaDosTiposDeDecisao.SaoIguais([], ["CLASSIFICACAO"]).Should().BeFalse();
    }

    [Fact(DisplayName = "Comparador — a comparação é ordinal e diferencia caixa")]
    public void Comparacao_EOrdinalEDiferenciaCaixa()
    {
        ColunaDosTiposDeDecisao.SaoIguais(["CLASSIFICACAO"], ["classificacao"]).Should().BeFalse();
    }

    [Fact(DisplayName = "Comparador — a mesma instância é igual a si, e o nulo só é igual ao nulo")]
    public void MesmaInstanciaENulos()
    {
        List<string> lista = [.. Classificacao];

        ColunaDosTiposDeDecisao.SaoIguais(lista, lista).Should().BeTrue();
        ColunaDosTiposDeDecisao.SaoIguais(null, null).Should().BeTrue();
        ColunaDosTiposDeDecisao.SaoIguais(null, lista).Should().BeFalse();
        ColunaDosTiposDeDecisao.SaoIguais(lista, null).Should().BeFalse();
    }

    [Fact(DisplayName = "Código — listas iguais têm o mesmo código, e a ordem muda o código")]
    public void CodigoAcompanhaAIgualdade()
    {
        ColunaDosTiposDeDecisao.Codigo(["ANALISE_DOCUMENTAL", "CLASSIFICACAO"])
            .Should().Be(ColunaDosTiposDeDecisao.Codigo(["ANALISE_DOCUMENTAL", "CLASSIFICACAO"]));
        ColunaDosTiposDeDecisao.Codigo(["ANALISE_DOCUMENTAL", "CLASSIFICACAO"])
            .Should().NotBe(ColunaDosTiposDeDecisao.Codigo(["CLASSIFICACAO", "ANALISE_DOCUMENTAL"]));
        ColunaDosTiposDeDecisao.Codigo([]).Should().Be(ColunaDosTiposDeDecisao.Codigo([]));
    }

    [Fact(DisplayName = "Cópia — é uma lista nova, e mudar o original não muda a cópia")]
    public void Copia_EIndependenteDoOriginal()
    {
        List<string> original = ["ANALISE_DOCUMENTAL"];

        List<string> copia = ColunaDosTiposDeDecisao.Copiar(original);
        original.Clear();
        original.Add("CLASSIFICACAO");

        copia.Should().NotBeSameAs(original);
        copia.Should().Equal("ANALISE_DOCUMENTAL");
    }

    [Fact(DisplayName = "Cópia — a lista vazia gera uma lista vazia nova")]
    public void Copia_DaListaVazia()
    {
        List<string> original = [];

        List<string> copia = ColunaDosTiposDeDecisao.Copiar(original);

        copia.Should().BeEmpty();
        copia.Should().NotBeSameAs(original);
    }

    [Fact(DisplayName = "Comparador — o snapshot do rastreamento percebe a alteração no lugar")]
    public void SnapshotDoComparador_PercebeAlteracaoNoLugar()
    {
        ValueComparer<List<string>> comparador = ColunaDosTiposDeDecisao.Comparador;
        List<string> viva = ["ANALISE_DOCUMENTAL"];
        List<string> snapshot = comparador.Snapshot(viva);

        comparador.Equals(viva, snapshot).Should().BeTrue();

        viva.Clear();
        viva.Add("CLASSIFICACAO");

        comparador.Equals(viva, snapshot).Should().BeFalse("a mesma instância alterada tem de diferir do que foi lido");
    }
}
