namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.Etapas;

using AwesomeAssertions;

using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Edicao;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Etapas;
using Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.Edicao;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

public sealed class CitacoesDasEtapasTests
{
    private readonly EtapaProcesso _citada = EtapasDeTeste.Etapa("Redação", 1);
    private readonly EtapaProcesso _outra = EtapasDeTeste.Etapa("Prova", 2);

    [Fact(DisplayName = "Sem desempate nem classificação, nenhuma etapa é citada")]
    public void SemConfiguracao_NinguemCitaAEtapa()
    {
        EstadoDaConfiguracao estado = new EstadoDaConfiguracaoBuilder().ComEtapas(_citada, _outra).Construir();

        CitacoesDasEtapas.EtapaCitadaPorDesempate(estado, _citada.Id).Should().BeFalse();
        CitacoesDasEtapas.EtapaCitadaPorDesempateOuClassificacao(estado, _citada.Id).Should().BeFalse();
        CitacoesDasEtapas.EliminacoesPorNotaMinimaQueCitam(estado, _citada.Id).Should().BeEmpty();
    }

    [Fact(DisplayName = "O desempate pela maior nota da etapa cita só a etapa dele")]
    public void Desempate_CitaSoAEtapaDele()
    {
        EstadoDaConfiguracao estado = new EstadoDaConfiguracaoBuilder()
            .ComEtapas(_citada, _outra)
            .ComCriteriosDeDesempate(EtapasDeTeste.DesempatePelaNotaDa(_citada, 1))
            .Construir();

        CitacoesDasEtapas.EtapaCitadaPorDesempate(estado, _citada.Id).Should().BeTrue();
        CitacoesDasEtapas.EtapaCitadaPorDesempate(estado, _outra.Id).Should().BeFalse();
        CitacoesDasEtapas.EtapaCitadaPorDesempateOuClassificacao(estado, _citada.Id).Should().BeTrue();
    }

    [Fact(DisplayName = "A regra de eliminação, de qualquer tipo, conta como citação da classificação, mas não do desempate")]
    public void Eliminacao_CitaPelaClassificacaoENaoPeloDesempate()
    {
        EstadoDaConfiguracao estado = new EstadoDaConfiguracaoBuilder()
            .ComEtapas(_citada, _outra)
            .ComClassificacao(EtapasDeTeste.ClassificacaoSemEnem(
                EtapasDeTeste.Eliminacao(RegraEliminacaoCodigo.ElimAusenciaNaEtapa, new ArgsElimAusenciaNaEtapa(_citada.Id))))
            .Construir();

        CitacoesDasEtapas.EtapaCitadaPorDesempateOuClassificacao(estado, _citada.Id).Should().BeTrue();
        CitacoesDasEtapas.EtapaCitadaPorDesempateOuClassificacao(estado, _outra.Id).Should().BeFalse();
        CitacoesDasEtapas.EtapaCitadaPorDesempate(estado, _citada.Id).Should().BeFalse();
    }

    [Fact(DisplayName = "As eliminações por nota mínima que citam a etapa não incluem a ausência nem a nota mínima de outra etapa")]
    public void NotaMinima_DevolveSoAsDaEtapa()
    {
        ArgsElimNotaMinimaEtapa daEtapa = new(_citada.Id, 5m);
        EstadoDaConfiguracao estado = new EstadoDaConfiguracaoBuilder()
            .ComEtapas(_citada, _outra)
            .ComClassificacao(EtapasDeTeste.ClassificacaoSemEnem(
                EtapasDeTeste.Eliminacao(RegraEliminacaoCodigo.ElimNotaMinimaEtapa, daEtapa),
                EtapasDeTeste.Eliminacao(RegraEliminacaoCodigo.ElimNotaMinimaEtapa, new ArgsElimNotaMinimaEtapa(_outra.Id, 7m)),
                EtapasDeTeste.Eliminacao(RegraEliminacaoCodigo.ElimAusenciaNaEtapa, new ArgsElimAusenciaNaEtapa(_citada.Id))))
            .Construir();

        CitacoesDasEtapas.EliminacoesPorNotaMinimaQueCitam(estado, _citada.Id).Should().ContainSingle().Which.Should().Be(daEtapa);
    }
}
