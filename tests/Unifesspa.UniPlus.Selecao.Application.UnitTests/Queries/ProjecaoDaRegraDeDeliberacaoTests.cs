namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Queries;

using AwesomeAssertions;

using Unifesspa.UniPlus.Selecao.Application.DTOs;
using Unifesspa.UniPlus.Selecao.Application.Queries.ProcessosSeletivos;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

public sealed class ProjecaoDaRegraDeDeliberacaoTests
{
    [Fact(DisplayName = "Banca sem regra declarada projeta regra ausente")]
    public void De_SemRegra_ProjetaAusencia() =>
        ProjecaoDaRegraDeDeliberacao.De(null).Should().BeNull();

    [Fact(DisplayName = "A regra da votação projeta cada campo declarado")]
    public void De_ColegiadaComVotacao_ProjetaTodosOsCampos()
    {
        RegraDeDeliberacao regra = RegraDeDeliberacao.Criar(new DeclaracaoDaDeliberacao(
            ModoDeDeliberacao.ColegiadaComVotacao, Integrantes: 3, Suplentes: 1, Quorum: 2,
            CriterioDeVotacao.MaioriaSimples, DesempateDaVotacao.VotoDaPresidencia,
            EncerramentoDaVotacao.PresidenciaNoQuorum, SemUnanimidade: null)).Value!;

        RegraDeDeliberacaoDto projetada = ProjecaoDaRegraDeDeliberacao.De(regra)!;

        projetada.Should().Be(new RegraDeDeliberacaoDto(
            ModoDeDeliberacao.ColegiadaComVotacao, 3, 1, 2,
            CriterioDeVotacao.MaioriaSimples, DesempateDaVotacao.VotoDaPresidencia,
            EncerramentoDaVotacao.PresidenciaNoQuorum, null));
    }

    [Fact(DisplayName = "A regra do ato individual projeta só a composição, sem campos de votação")]
    public void De_AtoIndividual_ProjetaSoAComposicao()
    {
        RegraDeDeliberacao regra = RegraDeDeliberacao.Criar(new DeclaracaoDaDeliberacao(
            ModoDeDeliberacao.AtoIndividual, Integrantes: 1, Suplentes: 0,
            Quorum: null, Criterio: null, Desempate: null, Encerramento: null, SemUnanimidade: null)).Value!;

        RegraDeDeliberacaoDto projetada = ProjecaoDaRegraDeDeliberacao.De(regra)!;

        projetada.Should().Be(new RegraDeDeliberacaoDto(ModoDeDeliberacao.AtoIndividual, 1, 0, null, null, null, null, null));
    }
}
