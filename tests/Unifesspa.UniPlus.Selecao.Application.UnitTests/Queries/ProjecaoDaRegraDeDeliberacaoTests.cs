namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Queries;

using AwesomeAssertions;

using Unifesspa.UniPlus.Application.Abstractions.Respostas;
using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
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
            ModoDeDeliberacao.ColegiadaComVotacao.Codigo(), Integrantes: 3, Suplentes: 1, new(Quorum: 2,
            CriterioDeVotacao.MaioriaSimples.Codigo(), DesempateDaVotacao.VotoDaPresidencia.Codigo(),
            EncerramentoDaVotacao.PresidenciaNoQuorum.Codigo(), SemUnanimidade: null))).Value!;

        RegraDeDeliberacaoDto projetada = ProjecaoDaRegraDeDeliberacao.De(regra)!;
        projetada.Modo.Should().Be(CodigoComRotulo<ModoDeDeliberacao>.De(ModoDeDeliberacao.ColegiadaComVotacao));
        projetada.Integrantes.Should().Be(3);
        projetada.Suplentes.Should().Be(1);
        projetada.Quorum.Should().Be(2);
        projetada.Criterio.Should().Be(CodigoComRotulo<CriterioDeVotacao>.De(CriterioDeVotacao.MaioriaSimples));
        projetada.Desempate.Should().Be(CodigoComRotulo<DesempateDaVotacao>.De(DesempateDaVotacao.VotoDaPresidencia));
        projetada.Encerramento.Should().Be(CodigoComRotulo<EncerramentoDaVotacao>.De(EncerramentoDaVotacao.PresidenciaNoQuorum));
        projetada.SemUnanimidade.Should().BeNull();
    }

    [Fact(DisplayName = "A regra do ato individual projeta só a composição, sem campos de votação")]
    public void De_AtoIndividual_ProjetaSoAComposicao()
    {
        RegraDeDeliberacao regra = RegraDeDeliberacao.Criar(new DeclaracaoDaDeliberacao(
            ModoDeDeliberacao.AtoIndividual.Codigo(), Integrantes: 1, Suplentes: 0, new(
            Quorum: null, Criterio: null, Desempate: null, Encerramento: null, SemUnanimidade: null))).Value!;

        RegraDeDeliberacaoDto projetada = ProjecaoDaRegraDeDeliberacao.De(regra)!;

        projetada.Modo.Should().Be(CodigoComRotulo<ModoDeDeliberacao>.De(ModoDeDeliberacao.AtoIndividual));
        projetada.Integrantes.Should().Be(1);
        projetada.Suplentes.Should().Be(0);
        projetada.Quorum.Should().BeNull();
        projetada.Criterio.Should().BeNull();
        projetada.Desempate.Should().BeNull();
        projetada.Encerramento.Should().BeNull();
        projetada.SemUnanimidade.Should().BeNull();
    }

    [Fact(DisplayName = "A regra do ato individual projeta a votação declarada para o recurso")]
    public void De_AtoIndividualComVotacao_ProjetaCamposDaVotacao()
    {
        RegraDeDeliberacao regra = RegraDeDeliberacao.Criar(new DeclaracaoDaDeliberacao(
            ModoDeDeliberacao.AtoIndividual.Codigo(), 3, 0, new(2,
            CriterioDeVotacao.MaioriaSimples.Codigo(), DesempateDaVotacao.VotoDaPresidencia.Codigo(),
            EncerramentoDaVotacao.PresidenciaNoQuorum.Codigo(), null))).Value!;

        RegraDeDeliberacaoDto projetada = ProjecaoDaRegraDeDeliberacao.De(regra)!;

        projetada.Quorum.Should().Be(2);
        projetada.Criterio.Should().Be(CodigoComRotulo<CriterioDeVotacao>.De(CriterioDeVotacao.MaioriaSimples));
        projetada.Encerramento.Should().Be(CodigoComRotulo<EncerramentoDaVotacao>.De(EncerramentoDaVotacao.PresidenciaNoQuorum));
    }

    [Fact(DisplayName = "A regra de unanimidade projeta o resultado sem unanimidade")]
    public void De_Unanimidade_ProjetaResultadoSemUnanimidade()
    {
        RegraDeDeliberacao regra = RegraDeDeliberacao.Criar(new DeclaracaoDaDeliberacao(
            ModoDeDeliberacao.ColegiadaComVotacao.Codigo(), 3, 0, new(3,
            CriterioDeVotacao.Unanimidade.Codigo(), null, EncerramentoDaVotacao.TodosOsTitulares.Codigo(),
            ResultadoSemUnanimidade.NovaVotacao.Codigo()))).Value!;

        RegraDeDeliberacaoDto projetada = ProjecaoDaRegraDeDeliberacao.De(regra)!;

        projetada.SemUnanimidade.Should().Be(CodigoComRotulo<ResultadoSemUnanimidade>.De(ResultadoSemUnanimidade.NovaVotacao));
        projetada.Desempate.Should().BeNull();
    }
}
