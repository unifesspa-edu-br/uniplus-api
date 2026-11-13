namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.ValueObjects;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

using Xunit;

public sealed class RegraDeDeliberacaoTests
{
    private static DeclaracaoDaDeliberacao Votacao(
        int integrantes = 3,
        int suplentes = 0,
        int? quorum = 2,
        CriterioDeVotacao? criterio = CriterioDeVotacao.MaioriaSimples,
        DesempateDaVotacao? desempate = DesempateDaVotacao.VotoDaPresidencia,
        EncerramentoDaVotacao? encerramento = EncerramentoDaVotacao.PresidenciaNoQuorum,
        ResultadoSemUnanimidade? semUnanimidade = null) =>
        new(ModoDeDeliberacao.ColegiadaComVotacao, integrantes, suplentes, quorum, criterio, desempate, encerramento, semUnanimidade);

    private static void DeveRecusar(DeclaracaoDaDeliberacao declaracao, string campo, string codigo)
    {
        Result<RegraDeDeliberacao> regra = RegraDeDeliberacao.Criar(declaracao);

        regra.IsFailure.Should().BeTrue();
        regra.Errors.Should().Contain(erro => erro.Field == campo && erro.Error.Code == codigo);
    }

    [Fact(DisplayName = "A banca de recursos da Medicina, com 3 titulares e quórum 2, é aceita")]
    public void Votacao_DaMedicina_Aceita()
    {
        Result<RegraDeDeliberacao> regra = RegraDeDeliberacao.Criar(Votacao());

        regra.IsSuccess.Should().BeTrue();
        regra.Value!.Declaracao().Should().Be(Votacao());
        regra.Value.QuorumExigido.Should().Be(2);
    }

    [Fact(DisplayName = "Ato individual é aceito sem quórum, critério nem desempate")]
    public void AtoIndividual_SemVotacao_Aceita()
    {
        Result<RegraDeDeliberacao> regra = RegraDeDeliberacao.Criar(
            new(ModoDeDeliberacao.AtoIndividual, 2, 1, null, null, null, null, null));

        regra.IsSuccess.Should().BeTrue();
        regra.Value!.QuorumExigido.Should().Be(1);
    }

    [Fact(DisplayName = "Ato individual com campo de votação é recusado em cada campo declarado")]
    public void AtoIndividual_ComCamposDeVotacao_RecusaCadaCampo()
    {
        Result<RegraDeDeliberacao> regra = RegraDeDeliberacao.Criar(
            new(ModoDeDeliberacao.AtoIndividual, 1, 0, 1, CriterioDeVotacao.MaioriaSimples, null, null, null));

        regra.Errors.Select(static e => e.Field).Should().BeEquivalentTo("quorum", "criterio");
        regra.Errors.Should().OnlyContain(static e => e.Error.Code == "Banca.AtoIndividualSemVotacao");
    }

    [Fact(DisplayName = "Modo fora do vocabulário é recusado")]
    public void Modo_Invalido_Recusa()
    {
        DeveRecusar(Votacao() with { Modo = (ModoDeDeliberacao)99 }, "modo", "Banca.ModoDeDeliberacaoInvalido");
    }

    [Theory(DisplayName = "Integrantes menor que 1 é recusado")]
    [InlineData(0)]
    [InlineData(-2)]
    public void Integrantes_MenorQueUm_Recusa(int integrantes)
    {
        DeveRecusar(Votacao(integrantes: integrantes), "integrantes", "Banca.IntegrantesInvalidos");
    }

    [Fact(DisplayName = "Suplentes menor que 0 é recusado")]
    public void Suplentes_Negativo_Recusa()
    {
        DeveRecusar(Votacao(suplentes: -1), "suplentes", "Banca.SuplentesInvalidos");
    }

    [Theory(DisplayName = "Quórum fora de 1 até o número de integrantes é recusado")]
    [InlineData(0)]
    [InlineData(4)]
    [InlineData(null)]
    public void Quorum_ForaDoIntervalo_Recusa(int? quorum)
    {
        DeveRecusar(Votacao(quorum: quorum), "quorum", "Banca.QuorumForaDoIntervalo");
    }

    [Fact(DisplayName = "Todos os titulares com quórum menor que os integrantes é recusado como quórum incoerente")]
    public void TodosOsTitulares_ComQuorumMenor_Recusa()
    {
        DeveRecusar(
            Votacao(quorum: 2, encerramento: EncerramentoDaVotacao.TodosOsTitulares), "quorum", "Banca.QuorumIncoerente");
    }

    [Fact(DisplayName = "Todos os titulares com quórum igual aos integrantes é aceito")]
    public void TodosOsTitulares_ComQuorumIgual_Aceita()
    {
        RegraDeDeliberacao.Criar(Votacao(quorum: 3, encerramento: EncerramentoDaVotacao.TodosOsTitulares))
            .IsSuccess.Should().BeTrue();
    }

    [Fact(DisplayName = "Critério e encerramento ausentes na votação são recusados juntos")]
    public void Votacao_SemCriterioNemEncerramento_RecusaOsDois()
    {
        Result<RegraDeDeliberacao> regra = RegraDeDeliberacao.Criar(Votacao(criterio: null, encerramento: null));

        regra.Errors.Select(static e => e.Error.Code)
            .Should().Contain(["Banca.CriterioObrigatorio", "Banca.EncerramentoObrigatorio"]);
    }

    [Theory(DisplayName = "Desempate ausente em maioria com 2 ou mais integrantes é recusado")]
    [InlineData(CriterioDeVotacao.MaioriaSimples)]
    [InlineData(CriterioDeVotacao.MaioriaAbsoluta)]
    public void Maioria_SemDesempate_Recusa(CriterioDeVotacao criterio)
    {
        DeveRecusar(Votacao(integrantes: 4, criterio: criterio, desempate: null), "desempate", "Banca.DesempateObrigatorio");
    }

    [Fact(DisplayName = "Desempate declarado com 1 integrante não se aplica")]
    public void Desempate_ComUmIntegrante_Recusa()
    {
        DeveRecusar(Votacao(integrantes: 1, quorum: 1), "desempate", "Banca.DesempateNaoSeAplica");
    }

    [Fact(DisplayName = "Desempate declarado na unanimidade não se aplica")]
    public void Desempate_NaUnanimidade_Recusa()
    {
        DeveRecusar(
            Votacao(criterio: CriterioDeVotacao.Unanimidade, semUnanimidade: ResultadoSemUnanimidade.Desfavoravel),
            "desempate", "Banca.DesempateNaoSeAplica");
    }

    [Fact(DisplayName = "Unanimidade com 2 ou mais integrantes sem o resultado quando não alcançada é recusada")]
    public void Unanimidade_SemResultadoSemUnanimidade_Recusa()
    {
        DeveRecusar(
            Votacao(criterio: CriterioDeVotacao.Unanimidade, desempate: null),
            "semUnanimidade", "Banca.SemUnanimidadeObrigatoria");
    }

    [Fact(DisplayName = "Resultado sem unanimidade fora da unanimidade não se aplica")]
    public void SemUnanimidade_ForaDaUnanimidade_Recusa()
    {
        DeveRecusar(
            Votacao(semUnanimidade: ResultadoSemUnanimidade.NovaVotacao), "semUnanimidade", "Banca.SemUnanimidadeNaoSeAplica");
    }

    [Fact(DisplayName = "Unanimidade com resultado declarado é aceita")]
    public void Unanimidade_ComResultado_Aceita()
    {
        RegraDeDeliberacao.Criar(Votacao(
            criterio: CriterioDeVotacao.Unanimidade, desempate: null, semUnanimidade: ResultadoSemUnanimidade.NovaVotacao))
            .IsSuccess.Should().BeTrue();
    }

    [Fact(DisplayName = "Banca de 1 integrante com maioria e sem desempate é aceita")]
    public void Maioria_ComUmIntegranteSemDesempate_Aceita()
    {
        RegraDeDeliberacao.Criar(Votacao(integrantes: 1, quorum: 1, desempate: null)).IsSuccess.Should().BeTrue();
    }

    [Fact(DisplayName = "A fábrica acumula todas as violações da declaração")]
    public void Criar_AcumulaAsViolacoes()
    {
        Result<RegraDeDeliberacao> regra = RegraDeDeliberacao.Criar(Votacao(integrantes: 4, quorum: 9, suplentes: -1, desempate: null));

        regra.Errors.Select(static e => e.Field).Should().BeEquivalentTo("suplentes", "quorum", "desempate");
    }
}
