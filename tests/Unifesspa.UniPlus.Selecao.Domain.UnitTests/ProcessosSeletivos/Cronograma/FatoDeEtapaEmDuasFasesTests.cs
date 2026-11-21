namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.Cronograma;

using AwesomeAssertions;

using Unifesspa.UniPlus.Selecao.Domain.MotivosDeDecisao;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Cronograma;

using static Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.Cronograma.VersoesPublicadasDeTeste;

/// <summary>
/// A etapa se liga à fase pelo código dela, e duas fases da mesma versão podem conter a mesma etapa: o fato da etapa inicia
/// cada fase que a contém, e nunca só a primeira.
/// </summary>
public sealed class FatoDeEtapaEmDuasFasesTests
{
    public enum TipoDeFato
    {
        AtoDaEtapa,
        ImportacaoDeNotas,
        EnvioAoInep,
        AvaliacaoNaEtapa,
    }

    [Theory(DisplayName = "O fato da etapa inicia as duas fases da mesma versão que a contêm")]
    [InlineData(TipoDeFato.AtoDaEtapa)]
    [InlineData(TipoDeFato.ImportacaoDeNotas)]
    [InlineData(TipoDeFato.EnvioAoInep)]
    [InlineData(TipoDeFato.AvaliacaoNaEtapa)]
    public void FatoDaEtapa_IniciaAsDuasFases(TipoDeFato tipo)
    {
        EtapaDaVersaoPublicada etapa = UmaEtapa();
        FaseDaVersaoPublicada primeira = UmaFase(etapas: [etapa]);
        FaseDaVersaoPublicada segunda = UmaFase(etapas: [etapa]);
        FasesDaVersaoPublicada versao = new(Guid.CreateVersion7(), Vigencia, null, [primeira, segunda]);
        InicioDasFases inicio = Inicio([versao], FatoDoTipo(tipo, etapa.Id, versao.VersaoId));

        inicio.FasesIniciadas(Abertura.AddDays(1)).Should().BeEquivalentTo([primeira.FaseCanonicaOrigemId, segunda.FaseCanonicaOrigemId]);
        inicio.PrimeirosFatos(Abertura.AddDays(1)).Should().HaveCount(2);
        inicio.FasesProdutoras(Abertura.AddDays(1)).Should().BeEmpty();
    }

    private static FatosDoInicioDasFases FatoDoTipo(TipoDeFato tipo, Guid etapaId, Guid versaoId) => tipo switch
    {
        TipoDeFato.AtoDaEtapa => SemFatos with { Atos = [AtoDaEtapa(etapaId, Abertura)] },
        TipoDeFato.ImportacaoDeNotas => SemFatos with { Importacoes = [new ImportacaoDeNotasNaEtapa(etapaId, versaoId, Abertura)] },
        TipoDeFato.EnvioAoInep => SemFatos with { EnviosAoInep = [new EnvioAoInepDaEtapa(etapaId, Abertura)] },
        _ => SemFatos with { AvaliacoesIniciadas = [new AvaliacaoIniciadaNoLugar(LugarDaDecisao.DaEtapa(etapaId), Abertura)] },
    };
}
