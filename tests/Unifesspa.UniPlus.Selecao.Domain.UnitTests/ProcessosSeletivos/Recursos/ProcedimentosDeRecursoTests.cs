namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.Recursos;

using AwesomeAssertions;

using Unifesspa.UniPlus.Regras.Services;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Recursos;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

using Xunit;

using static Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.Recursos.ProcedimentosDeRecursoParaTestes;

/// <summary>A coleção de procedimentos guarda a ordem canônica, que não depende da ordem de entrada.</summary>
public sealed class ProcedimentosDeRecursoTests
{
    private static readonly ProcedimentoDeRecursoDeclarado Classificacao = Procedimento();
    private static readonly ProcedimentoDeRecursoDeclarado HomologacaoSemAlvo = Procedimento(TipoMotivoDeDecisao.HomologacaoDasInscricoes);
    private static readonly ProcedimentoDeRecursoDeclarado AnaliseDoItem = Procedimento(TipoMotivoDeDecisao.AnaliseDocumental, AlvoItem());
    private static readonly ProcedimentoDeRecursoDeclarado AnaliseDaArea = Procedimento(TipoMotivoDeDecisao.AnaliseDocumental, AlvoArea(AreaDoEnem.Redacao));

    [Fact(DisplayName = "A ordem canônica é tipo de decisão, tipo de alvo e valor, qualquer que seja a ordem de entrada")]
    public void Ordem_CanonicaIndependeDaEntrada()
    {
        ProcedimentosDeRecurso porAUmaOrdem = ProcedimentosDeRecurso.De([Classificacao, AnaliseDaArea, HomologacaoSemAlvo, AnaliseDoItem]);
        ProcedimentosDeRecurso porOutra = ProcedimentosDeRecurso.De([AnaliseDoItem, HomologacaoSemAlvo, Classificacao, AnaliseDaArea]);

        porAUmaOrdem.Itens.Should().Equal(AnaliseDoItem, AnaliseDaArea, Classificacao, HomologacaoSemAlvo);
        porAUmaOrdem.MesmoConteudo(porOutra).Should().BeTrue();
    }

    [Fact(DisplayName = "Trocar a regra, o alvo ou o tipo de um procedimento distingue as coleções")]
    public void MesmoConteudo_DistingueOQueMudou()
    {
        ProcedimentosDeRecurso base1 = ProcedimentosDeRecurso.De([Classificacao]);

        base1.MesmoConteudo(ProcedimentosDeRecurso.De([Procedimento(codigoDaRegra: RegraProcedimentoRecursoCodigo.Conferencia)])).Should().BeFalse();
        base1.MesmoConteudo(ProcedimentosDeRecurso.De([Procedimento(alvo: AlvoItem())])).Should().BeFalse();
        base1.MesmoConteudo(ProcedimentosDeRecurso.De([HomologacaoSemAlvo])).Should().BeFalse();
        base1.MesmoConteudo(ProcedimentosDeRecurso.Nenhum).Should().BeFalse();
    }

    [Fact(DisplayName = "Tipo de decisão sentinela não monta procedimento: o tipo inválido é recusado antes")]
    public void TipoNenhum_NaoMonta()
    {
        Action montar = () => ProcedimentoDeRecursoDeclarado.De(TipoMotivoDeDecisao.Nenhum, null, Regra());

        montar.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact(DisplayName = "O alvo guarda o item pelo identificador canônico e a área pelo código; a igualdade é pelo valor")]
    public void Alvo_IgualdadePeloValor()
    {
        AlvoItem().Should().Be(AlvoItem());
        AlvoItem().Valor.Should().Be("11111111-1111-1111-1111-111111111111");
        AlvoArea().Should().Be(AlvoArea(" MATEMATICA "));
        AlvoItem().Should().NotBe(AlvoItem(OutroItem));
    }

    [Fact(DisplayName = "Área em branco não monta alvo")]
    public void AlvoDeArea_EmBranco_NaoMonta()
    {
        Action montar = () => AlvoDoProcedimentoDeRecurso.DaArea(" ");

        montar.Should().Throw<ArgumentException>();
    }
}
