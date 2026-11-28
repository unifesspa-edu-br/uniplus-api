namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.ValueObjects;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

public sealed class RegraDeDeliberacaoObrigatoriosTests
{
    private static DeclaracaoDaDeliberacao Colegiada(int? integrantes, int? suplentes) => new(
        ModoDeDeliberacao.ColegiadaComVotacao.Codigo(),
        integrantes,
        suplentes,
        new(2, CriterioDeVotacao.MaioriaSimples.Codigo(), DesempateDaVotacao.VotoDaPresidencia.Codigo(), EncerramentoDaVotacao.PresidenciaNoQuorum.Codigo(), null));

    private static (string? Campo, string Codigo)[] Recusas(DeclaracaoDaDeliberacao declarada) =>
        [.. RegraDeDeliberacao.Criar(declarada).Errors.Select(static erro => (erro.Field, erro.Error.Code))];

    [Fact(DisplayName = "Integrantes ausentes são recusados com o código genérico de campo obrigatório")]
    public void Criar_SemIntegrantes_Recusa()
    {
        Recusas(Colegiada(integrantes: null, suplentes: 0))
            .Should().Equal(("integrantes", CodigosDeValidacao.Obrigatorio));
    }

    [Fact(DisplayName = "Suplentes ausentes são recusados com o código genérico de campo obrigatório")]
    public void Criar_SemSuplentes_Recusa()
    {
        Recusas(Colegiada(integrantes: 3, suplentes: null))
            .Should().Equal(("suplentes", CodigosDeValidacao.Obrigatorio));
    }

    [Fact(DisplayName = "Integrantes e suplentes ausentes são recusados juntos, e o quórum e o desempate, que dependem dos integrantes, não viram outra recusa")]
    public void Criar_SemIntegrantesESuplentes_AcumulaSoOsDoisCampos()
    {
        Recusas(Colegiada(integrantes: null, suplentes: null)).Should().Equal(
            ("integrantes", CodigosDeValidacao.Obrigatorio),
            ("suplentes", CodigosDeValidacao.Obrigatorio));
    }

    [Fact(DisplayName = "O modo desconhecido acumula com os integrantes e os suplentes ausentes, na ordem dos campos")]
    public void Criar_ModoDesconhecidoSemComposicao_Acumula()
    {
        DeclaracaoDaDeliberacao declarada = Colegiada(null, null) with { Modo = "outro" };

        Recusas(declarada).Select(static recusa => recusa.Campo).Should().Equal("modo", "integrantes", "suplentes");
    }

    [Fact(DisplayName = "Zero suplentes declara nenhum e é aceito, e zero integrantes segue recusado pelo mínimo")]
    public void Criar_ZeroSuplentesEZeroIntegrantes_DiferenciaNenhumDeAusente()
    {
        RegraDeDeliberacao.Criar(Colegiada(integrantes: 3, suplentes: 0)).IsSuccess.Should().BeTrue();
        Recusas(Colegiada(integrantes: 0, suplentes: 0)).Select(static recusa => recusa.Campo).Should().Contain("integrantes")
            .And.NotContain(recusa => recusa == "suplentes");
    }
}
