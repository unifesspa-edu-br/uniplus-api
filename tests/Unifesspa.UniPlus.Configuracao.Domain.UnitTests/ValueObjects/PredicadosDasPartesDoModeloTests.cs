namespace Unifesspa.UniPlus.Configuracao.Domain.UnitTests.ValueObjects;

using AwesomeAssertions;

using Unifesspa.UniPlus.Configuracao.Domain.ValueObjects;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Regras.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

using static Unifesspa.UniPlus.Configuracao.Domain.UnitTests.TestSupport.ConteudosDoModeloDeTeste;

/// <summary>As regras de cada parte do conteúdo do modelo, na ordem, com o caminho do campo de cada uma na entrada.</summary>
public sealed class PredicadosDasPartesDoModeloTests
{
    private static readonly PredicadoDnf Exibicao = Quando("DA_EXIBICAO");
    private static readonly PredicadoDnf DaObrigatoriedade = Quando("DA_OBRIGATORIEDADE");
    private static readonly PredicadoDnf DoImpedimento = Quando("DO_IMPEDIMENTO");

    [Fact(DisplayName = "O item enumera, no caminho das regras, a exibição e o predicado de obrigatoriedade, nessa ordem")]
    public void Item_ExibicaoEObrigatoriedade_NaOrdem()
    {
        ItemDoModelo item = Item("CERTIFICADO", exibicao: Exibicao)
            .ComObrigatoriedade(Obrigatoriedade.Quando(DaObrigatoriedade))
            .ComImpedimento(new Impedimento(DoImpedimento, "Já possui o certificado."));

        item.Predicados().Should().Equal(
            new PredicadoNaEntrada("regras.exibicao", Exibicao),
            new PredicadoNaEntrada("regras.obrigatoriedade.predicado", DaObrigatoriedade));
    }

    [Fact(DisplayName = "O item sem regras enumera os mesmos caminhos, sem predicado")]
    public void Item_SemRegras_MesmosCaminhosSemPredicado()
    {
        Item("RENDA").Predicados().Should().Equal(
            new PredicadoNaEntrada("regras.exibicao", null),
            new PredicadoNaEntrada("regras.obrigatoriedade.predicado", null));
    }

    [Fact(DisplayName = "O grupo enumera a exibição e o predicado de obrigatoriedade, nessa ordem")]
    public void Grupo_ExibicaoEObrigatoriedade_NaOrdem()
    {
        GrupoDoModelo grupo = Grupo("COMPOSICAO", 0, Exibicao).ComObrigatoriedade(Obrigatoriedade.Quando(DaObrigatoriedade));

        grupo.Predicados().Should().Equal(
            new PredicadoNaEntrada("regras.exibicao", Exibicao),
            new PredicadoNaEntrada("regras.obrigatoriedade.predicado", DaObrigatoriedade));
    }

    [Fact(DisplayName = "O grupo sem regras enumera os mesmos caminhos, sem predicado")]
    public void Grupo_SemRegras_MesmosCaminhosSemPredicado()
    {
        Grupo("COMPOSICAO").Predicados().Should().Equal(
            new PredicadoNaEntrada("regras.exibicao", null),
            new PredicadoNaEntrada("regras.obrigatoriedade.predicado", null));
    }

    [Fact(DisplayName = "A pagina enumera a exibição")]
    public void Pagina_Exibicao()
    {
        Pagina(exibicao: Exibicao).Predicados().Should().Equal(new PredicadoNaEntrada("exibicao", Exibicao));
    }

    [Fact(DisplayName = "A pagina sem exibição enumera o caminho, sem predicado")]
    public void Pagina_SemExibicao_CaminhoSemPredicado()
    {
        Pagina().Predicados().Should().Equal(new PredicadoNaEntrada("exibicao", null));
    }

    [Fact(DisplayName = "O termo enumera a exibição e o predicado de obrigatoriedade, nessa ordem")]
    public void Termo_ExibicaoEObrigatoriedade_NaOrdem()
    {
        TermoDoModelo termo = Termo("LGPD", exibicao: Exibicao).ComObrigatoriedade(Obrigatoriedade.Quando(DaObrigatoriedade));

        termo.Predicados().Should().Equal(
            new PredicadoNaEntrada("regras.exibicao", Exibicao),
            new PredicadoNaEntrada("regras.obrigatoriedade.predicado", DaObrigatoriedade));
    }

    [Fact(DisplayName = "O termo sem regras enumera os mesmos caminhos, sem predicado")]
    public void Termo_SemRegras_MesmosCaminhosSemPredicado()
    {
        Termo("LGPD").Predicados().Should().Equal(
            new PredicadoNaEntrada("regras.exibicao", null),
            new PredicadoNaEntrada("regras.obrigatoriedade.predicado", null));
    }

    [Fact(DisplayName = "O documento enumera a condição que o torna exigido")]
    public void Documento_Quando()
    {
        Documento("LAUDO", Exibicao).Predicados().Should().Equal(new PredicadoNaEntrada("quando", Exibicao));
    }

    [Fact(DisplayName = "O documento sem condição enumera o caminho, sem predicado")]
    public void Documento_SemCondicao_CaminhoSemPredicado()
    {
        Documento("LAUDO").Predicados().Should().Equal(new PredicadoNaEntrada("quando", null));
    }
}
