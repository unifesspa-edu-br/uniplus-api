namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.Cronograma;

using AwesomeAssertions;

using Unifesspa.UniPlus.Selecao.Domain.MotivosDeDecisao;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Cronograma;

using static Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.Cronograma.VersoesPublicadasDeTeste;

/// <summary>
/// O fato que não se amarra a versão nenhuma, ou que acontece antes de a primeira versão vigorar: o que a regra decide
/// sem versão, e a versão mais antiga para o fato anterior à primeira vigência.
/// </summary>
public sealed class FatosSemVersaoAplicavelTests
{
    public enum TipoDeFato
    {
        AtoDaEtapa,
        EnvioAoInep,
        AvaliacaoNaEtapa,
        InscricaoRecebida,
        ImportacaoComVersaoFixadaForaDasVigentes,
    }

    [Fact(DisplayName = "Sem versões, a avaliação iniciada na fase inicia a fase e a fase produtora, e nenhuma etapa")]
    public void SemVersoes_AvaliacaoNaFase_IniciaAFase()
    {
        Guid fase = Guid.CreateVersion7();
        InicioDasFases inicio = Inicio([], SemFatos with { AvaliacoesIniciadas = [new AvaliacaoIniciadaNoLugar(LugarDaDecisao.DaFase(fase), Abertura)] });

        inicio.FasesIniciadas(Abertura).Should().Equal(fase);
        inicio.FasesProdutoras(Abertura).Should().Equal(fase);
        inicio.EtapasIniciadas(Abertura).Should().BeEmpty();
    }

    [Fact(DisplayName = "Sem versões, a avaliação iniciada na etapa inicia a etapa e nenhuma fase")]
    public void SemVersoes_AvaliacaoNaEtapa_IniciaSoAEtapa()
    {
        Guid etapa = Guid.CreateVersion7();
        InicioDasFases inicio = Inicio([], SemFatos with { AvaliacoesIniciadas = [new AvaliacaoIniciadaNoLugar(LugarDaDecisao.DaEtapa(etapa), Abertura)] });

        inicio.EtapasIniciadas(Abertura).Should().Equal(etapa);
        inicio.FasesIniciadas(Abertura).Should().BeEmpty();
        inicio.FasesProdutoras(Abertura).Should().BeEmpty();
    }

    [Fact(DisplayName = "Sem versões, o ato da fase e o período não iniciam nada")]
    public void SemVersoes_AtoDaFaseEPeriodo_NaoIniciamNada()
    {
        InicioDasFases inicio = Inicio([], SemFatos with { Atos = [AtoDaFase(Guid.CreateVersion7(), Abertura), AtoDaEtapa(Guid.CreateVersion7(), Abertura)] });

        inicio.FasesIniciadas(Abertura).Should().BeEmpty();
        inicio.FasesProdutoras(Abertura).Should().BeEmpty();
        inicio.EtapasIniciadas(Abertura).Should().BeEmpty();
    }

    [Fact(DisplayName = "A fase ausente de todas as versões começa pela avaliação iniciada nela, e não pelo ato")]
    public void FaseAusenteDeTodasAsVersoes_ComecaSoPelaAvaliacaoIniciada()
    {
        Guid ausente = Guid.CreateVersion7();
        FasesDaVersaoPublicada[] versoes = [Versao(UmaFase())];
        InicioDasFases comAto = Inicio(versoes, SemFatos with { Atos = [AtoDaFase(ausente, Abertura)] });
        InicioDasFases comAvaliacao = Inicio(versoes, SemFatos with { AvaliacoesIniciadas = [new AvaliacaoIniciadaNoLugar(LugarDaDecisao.DaFase(ausente), Abertura)] });

        comAto.FasesIniciadas(Abertura).Should().BeEmpty();
        comAto.FasesProdutoras(Abertura).Should().BeEmpty();
        comAvaliacao.FasesProdutoras(Abertura).Should().Equal(ausente);
        comAvaliacao.FasesIniciadas(Abertura).Should().Equal(ausente);
    }

    [Theory(DisplayName = "O fato anterior à primeira vigência vale pela versão mais antiga")]
    [InlineData(TipoDeFato.AtoDaEtapa)]
    [InlineData(TipoDeFato.EnvioAoInep)]
    [InlineData(TipoDeFato.AvaliacaoNaEtapa)]
    [InlineData(TipoDeFato.InscricaoRecebida)]
    [InlineData(TipoDeFato.ImportacaoComVersaoFixadaForaDasVigentes)]
    public void FatoAnteriorAPrimeiraVigencia_ValePelaVersaoMaisAntiga(TipoDeFato tipo)
    {
        EtapaDaVersaoPublicada etapa = UmaEtapa();
        FaseDaVersaoPublicada faseDaMaisAntiga = UmaFase(coletaInscricao: true, etapas: [etapa]);
        FaseDaVersaoPublicada faseDaMaisRecente = UmaFase(coletaInscricao: true, etapas: [etapa]);
        FasesDaVersaoPublicada maisAntiga = Versao(faseDaMaisAntiga, vigenteAPartirDe: Vigencia, substituidaEm: Vigencia.AddDays(30));
        FasesDaVersaoPublicada maisRecente = Versao(faseDaMaisRecente, vigenteAPartirDe: Vigencia.AddDays(30));
        InicioDasFases inicio = Inicio([maisRecente, maisAntiga], FatoDoTipo(tipo, etapa.Id, Vigencia.AddDays(-10)));

        inicio.FasesIniciadas(Abertura).Should().Equal(faseDaMaisAntiga.FaseCanonicaOrigemId);
    }

    private static FatosDoInicioDasFases FatoDoTipo(TipoDeFato tipo, Guid etapaId, DateTimeOffset instante) => tipo switch
    {
        TipoDeFato.AtoDaEtapa => SemFatos with { Atos = [AtoDaEtapa(etapaId, instante)] },
        TipoDeFato.EnvioAoInep => SemFatos with { EnviosAoInep = [new EnvioAoInepDaEtapa(etapaId, instante)] },
        TipoDeFato.AvaliacaoNaEtapa => SemFatos with { AvaliacoesIniciadas = [new AvaliacaoIniciadaNoLugar(LugarDaDecisao.DaEtapa(etapaId), instante)] },
        TipoDeFato.InscricaoRecebida => SemFatos with { InscricoesRecebidas = [instante] },
        _ => SemFatos with { Importacoes = [new ImportacaoDeNotasNaEtapa(etapaId, Guid.CreateVersion7(), instante)] },
    };
}
