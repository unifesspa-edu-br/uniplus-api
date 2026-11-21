namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.Cronograma;

using AwesomeAssertions;

using Unifesspa.UniPlus.Selecao.Domain.MotivosDeDecisao;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Cronograma;

using static Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.Cronograma.VersoesPublicadasDeTeste;

/// <summary>
/// A etapa que aparece numa versão, mas não na versão que se aplica ao fato: a etapa começou, pela identidade dela, e
/// nenhuma fase começou, porque a versão aplicável não traz a etapa em fase nenhuma.
/// </summary>
public sealed class EtapaForaDaVersaoAplicavelTests
{
    private static readonly DateTimeOffset Substituicao = Abertura;
    private static readonly DateTimeOffset Agora = Abertura.AddDays(10);

    public enum TipoDeFato
    {
        AtoDaEtapa,
        EnvioAoInep,
        AvaliacaoNaEtapa,
    }

    [Theory(DisplayName = "O fato da etapa que a versão aplicável não traz inicia a etapa e nenhuma fase")]
    [InlineData(TipoDeFato.AtoDaEtapa, true)]
    [InlineData(TipoDeFato.AtoDaEtapa, false)]
    [InlineData(TipoDeFato.EnvioAoInep, true)]
    [InlineData(TipoDeFato.EnvioAoInep, false)]
    [InlineData(TipoDeFato.AvaliacaoNaEtapa, true)]
    [InlineData(TipoDeFato.AvaliacaoNaEtapa, false)]
    public void FatoDaEtapaForaDaVersaoAplicavel_IniciaSoAEtapa(TipoDeFato tipo, bool etapaNaVersaoAnterior)
    {
        Cenario cenario = UmCenario(etapaNaVersaoAnterior);
        InicioDasFases inicio = Inicio(cenario.Versoes, FatoDoTipo(tipo, cenario.Etapa.Id, cenario.InstanteNaVersaoSemAEtapa));

        inicio.EtapasIniciadas(Agora).Should().Contain(cenario.Etapa.Id);
        inicio.FasesIniciadas(Agora).Should().BeEmpty();
        inicio.FasesProdutoras(Agora).Should().BeEmpty();
    }

    [Theory(DisplayName = "A importação com a versão fixada sem a etapa não recai na versão vigente que a traz")]
    [InlineData(true)]
    [InlineData(false)]
    public void ImportacaoComVersaoFixadaSemAEtapa_NaoRecaiNaVigente(bool etapaNaVersaoAnterior)
    {
        Cenario cenario = UmCenario(etapaNaVersaoAnterior);
        ImportacaoDeNotasNaEtapa importacao = new(cenario.Etapa.Id, cenario.VersaoSemAEtapa.VersaoId, cenario.InstanteNaVersaoComAEtapa);
        InicioDasFases inicio = Inicio(cenario.Versoes, SemFatos with { Importacoes = [importacao] });

        inicio.EtapasIniciadas(Agora).Should().Contain(cenario.Etapa.Id);
        inicio.FasesIniciadas(Agora).Should().BeEmpty();
    }

    [Fact(DisplayName = "A importação com a versão fixada que traz a etapa inicia a fase que a contém nela")]
    public void ImportacaoComVersaoFixadaComAEtapa_IniciaAFaseDessaVersao()
    {
        Cenario cenario = UmCenario(etapaNaVersaoAnterior: true);
        ImportacaoDeNotasNaEtapa importacao = new(cenario.Etapa.Id, cenario.VersaoComAEtapa.VersaoId, cenario.InstanteNaVersaoSemAEtapa);

        Inicio(cenario.Versoes, SemFatos with { Importacoes = [importacao] }).FasesIniciadas(Agora)
            .Should().Equal(cenario.FaseComAEtapa.FaseCanonicaOrigemId);
    }

    private static FatosDoInicioDasFases FatoDoTipo(TipoDeFato tipo, Guid etapaId, DateTimeOffset instante) => tipo switch
    {
        TipoDeFato.AtoDaEtapa => SemFatos with { Atos = [AtoDaEtapa(etapaId, instante)] },
        TipoDeFato.EnvioAoInep => SemFatos with { EnviosAoInep = [new EnvioAoInepDaEtapa(etapaId, instante)] },
        _ => SemFatos with { AvaliacoesIniciadas = [new AvaliacaoIniciadaNoLugar(LugarDaDecisao.DaEtapa(etapaId), instante)] },
    };

    private sealed record Cenario(
        EtapaDaVersaoPublicada Etapa,
        FaseDaVersaoPublicada FaseComAEtapa,
        FasesDaVersaoPublicada[] Versoes,
        FasesDaVersaoPublicada VersaoComAEtapa,
        FasesDaVersaoPublicada VersaoSemAEtapa,
        DateTimeOffset InstanteNaVersaoComAEtapa,
        DateTimeOffset InstanteNaVersaoSemAEtapa);

    // A versão anterior vigora até a substituição e a posterior vigora depois dela; a etapa está só numa delas.
    private static Cenario UmCenario(bool etapaNaVersaoAnterior)
    {
        EtapaDaVersaoPublicada etapa = UmaEtapa();
        FaseDaVersaoPublicada comAEtapa = UmaFase(etapas: [etapa]);
        FaseDaVersaoPublicada semAEtapa = UmaFase();
        FasesDaVersaoPublicada anterior = new(Guid.CreateVersion7(), Vigencia, Substituicao, [etapaNaVersaoAnterior ? comAEtapa : semAEtapa]);
        FasesDaVersaoPublicada posterior = new(Guid.CreateVersion7(), Substituicao, null, [etapaNaVersaoAnterior ? semAEtapa : comAEtapa]);
        DateTimeOffset naAnterior = Substituicao.AddDays(-1);
        DateTimeOffset naPosterior = Substituicao.AddDays(1);

        return etapaNaVersaoAnterior
            ? new Cenario(etapa, comAEtapa, [posterior, anterior], anterior, posterior, naAnterior, naPosterior)
            : new Cenario(etapa, comAEtapa, [posterior, anterior], posterior, anterior, naPosterior, naAnterior);
    }
}
