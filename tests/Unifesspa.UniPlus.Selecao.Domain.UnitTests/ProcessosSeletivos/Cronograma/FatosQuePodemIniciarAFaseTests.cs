namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.Cronograma;

using AwesomeAssertions;

using Unifesspa.UniPlus.Selecao.Domain.MotivosDeDecisao;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Cronograma;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

using static Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.Cronograma.VersoesPublicadasDeTeste;

/// <summary>
/// O primeiro fato que pode iniciar cada fase, para cada tipo de fato: a fase de origem que ele iniciaria, a versão
/// publicada que se aplica a ele e o instante em que ele passou a contar.
/// </summary>
public sealed class FatosQuePodemIniciarAFaseTests
{
    private static readonly DateTimeOffset Agora = Abertura.AddDays(30);

    [Fact(DisplayName = "O período da fase vale pela versão que o declara, e conta no maior instante entre a data declarada e a vigência")]
    public void PeriodoDaFase_ValePelaVersaoQueODeclara()
    {
        FaseDaVersaoPublicada fase = UmaFase(inicio: Abertura);
        FasesDaVersaoPublicada versao = Versao(fase);
        InicioDasFases inicio = Inicio([versao], SemFatos);

        inicio.FatosDaPropriaFase(Agora).Should().ContainSingle().Which.Should().Be(new FatoQuePodeIniciarAFase(fase.FaseCanonicaOrigemId, versao, Abertura));
    }

    [Fact(DisplayName = "O período declarado antes de a versão vigorar só conta a partir da vigência da versão")]
    public void PeriodoDaFase_DeclaradoAntesDaVigencia_ContaNaVigencia()
    {
        FaseDaVersaoPublicada fase = UmaFase(inicio: Abertura);
        FasesDaVersaoPublicada versao = Versao(fase, vigenteAPartirDe: Abertura.AddDays(5));

        Inicio([versao], SemFatos).FatosDaPropriaFase(Agora).Should().ContainSingle().Which.Instante.Should().Be(Abertura.AddDays(5));
    }

    [Fact(DisplayName = "O período de uma etapa vale para a fase que a contém, e não é fato da própria fase")]
    public void PeriodoDaEtapa_ValeParaAFaseQueAContem()
    {
        EtapaDaVersaoPublicada etapa = UmaEtapa(inicio: Abertura);
        FaseDaVersaoPublicada fase = UmaFase(etapas: [etapa]);
        FasesDaVersaoPublicada versao = Versao(fase);
        InicioDasFases inicio = Inicio([versao], SemFatos);

        inicio.FatosDeEtapaOuInscricao(Agora).Should().ContainSingle().Which.Should().Be(new FatoQuePodeIniciarAFase(fase.FaseCanonicaOrigemId, versao, Abertura));
    }

    [Fact(DisplayName = "O ato no lugar da fase vale pela fase de origem, sem versão aplicável, no instante da publicação")]
    public void AtoNoLugarDaFase_ValePelaFaseDeOrigem()
    {
        FaseDaVersaoPublicada fase = UmaFase();
        InicioDasFases inicio = Inicio([Versao(fase)], SemFatos with { Atos = [AtoDaFase(fase.FaseCanonicaOrigemId, Abertura)] });

        inicio.FatosDaPropriaFase(Agora).Should().ContainSingle().Which.Should().Be(new FatoQuePodeIniciarAFase(fase.FaseCanonicaOrigemId, null, Abertura));
    }

    [Fact(DisplayName = "O ato no lugar da etapa vale para a fase que a contém na versão vigente no instante da publicação")]
    public void AtoNoLugarDaEtapa_ValeParaAFaseNaVersaoVigenteNoInstante()
    {
        EtapaDaVersaoPublicada etapa = UmaEtapa();
        FaseDaVersaoPublicada fase = UmaFase(etapas: [etapa]);
        FasesDaVersaoPublicada versao = Versao(fase);
        InicioDasFases inicio = Inicio([versao], SemFatos with { Atos = [AtoDaEtapa(etapa.Id, Abertura)] });

        inicio.FatosDeEtapaOuInscricao(Agora).Should().ContainSingle().Which.Should().Be(new FatoQuePodeIniciarAFase(fase.FaseCanonicaOrigemId, versao, Abertura));
    }

    [Fact(DisplayName = "A importação de notas vale pela versão que a importação fixou, no instante da importação")]
    public void ImportacaoDeNotas_ValePelaVersaoFixadaNaImportacao()
    {
        EtapaDaVersaoPublicada etapa = UmaEtapa();
        FaseDaVersaoPublicada fase = UmaFase(etapas: [etapa]);
        FasesDaVersaoPublicada versao = Versao(fase);
        FatosDoInicioDasFases fatos = SemFatos with { Importacoes = [new ImportacaoDeNotasNaEtapa(etapa.Id, versao.VersaoId, Abertura)] };

        Inicio([versao], fatos).FatosDeEtapaOuInscricao(Agora).Should().ContainSingle()
            .Which.Should().Be(new FatoQuePodeIniciarAFase(fase.FaseCanonicaOrigemId, versao, Abertura));
    }

    [Fact(DisplayName = "O envio ao INEP vale para a fase que contém a etapa na versão vigente no instante da geração")]
    public void EnvioAoInep_ValeParaAFaseNaVersaoVigenteNoInstante()
    {
        EtapaDaVersaoPublicada etapa = UmaEtapa();
        FaseDaVersaoPublicada fase = UmaFase(etapas: [etapa]);
        FasesDaVersaoPublicada versao = Versao(fase);
        InicioDasFases inicio = Inicio([versao], SemFatos with { EnviosAoInep = [new EnvioAoInepDaEtapa(etapa.Id, Abertura)] });

        inicio.FatosDeEtapaOuInscricao(Agora).Should().ContainSingle().Which.Should().Be(new FatoQuePodeIniciarAFase(fase.FaseCanonicaOrigemId, versao, Abertura));
    }

    [Fact(DisplayName = "A inscrição recebida vale para a fase que coleta com o período aberto no instante dela")]
    public void InscricaoRecebida_ValeParaAFaseComPeriodoAberto()
    {
        FaseDaVersaoPublicada anterior = UmaFase(inicio: Vigencia, coletaInscricao: true);
        FaseDaVersaoPublicada comPeriodoAberto = UmaFase(inicio: Abertura.AddDays(-1), coletaInscricao: true);
        FasesDaVersaoPublicada versao = new(Guid.CreateVersion7(), Vigencia, null, [anterior with { Periodo = new JanelaDeTempo(anterior.Periodo.Inicio, Vigencia.AddDays(1)) }, comPeriodoAberto]);
        InicioDasFases inicio = Inicio([versao], SemFatos with { InscricoesRecebidas = [Abertura] });

        inicio.FatosDeEtapaOuInscricao(Agora).Should().ContainSingle()
            .Which.Should().Be(new FatoQuePodeIniciarAFase(comPeriodoAberto.FaseCanonicaOrigemId, versao, Abertura));
    }

    [Fact(DisplayName = "A inscrição recebida sem período que a contenha vale para a primeira fase que coleta")]
    public void InscricaoRecebida_SemPeriodoAberto_ValeParaAPrimeiraQueColeta()
    {
        FaseDaVersaoPublicada primeira = UmaFase(coletaInscricao: true);
        FaseDaVersaoPublicada segunda = UmaFase(coletaInscricao: true);
        FasesDaVersaoPublicada versao = new(Guid.CreateVersion7(), Vigencia, null, [primeira, segunda]);

        Inicio([versao], SemFatos with { InscricoesRecebidas = [Abertura] }).FatosDeEtapaOuInscricao(Agora).Should().ContainSingle()
            .Which.FaseCanonicaOrigemId.Should().Be(primeira.FaseCanonicaOrigemId);
    }

    [Fact(DisplayName = "A avaliação iniciada na fase vale pela fase, sem versão aplicável, no instante do registro")]
    public void AvaliacaoNaFase_ValePelaFase()
    {
        FaseDaVersaoPublicada fase = UmaFase();
        AvaliacaoIniciadaNoLugar avaliacao = new(LugarDaDecisao.DaFase(fase.FaseCanonicaOrigemId), Abertura);
        InicioDasFases inicio = Inicio([Versao(fase)], SemFatos with { AvaliacoesIniciadas = [avaliacao] });

        inicio.FatosDaPropriaFase(Agora).Should().ContainSingle().Which.Should().Be(new FatoQuePodeIniciarAFase(fase.FaseCanonicaOrigemId, null, Abertura));
    }

    [Fact(DisplayName = "A avaliação iniciada na etapa vale para a fase que a contém na versão vigente no instante do registro")]
    public void AvaliacaoNaEtapa_ValeParaAFaseNaVersaoVigenteNoInstante()
    {
        EtapaDaVersaoPublicada etapa = UmaEtapa();
        FaseDaVersaoPublicada fase = UmaFase(etapas: [etapa]);
        FasesDaVersaoPublicada versao = Versao(fase);
        AvaliacaoIniciadaNoLugar avaliacao = new(LugarDaDecisao.DaEtapa(etapa.Id), Abertura);
        InicioDasFases inicio = Inicio([versao], SemFatos with { AvaliacoesIniciadas = [avaliacao] });

        inicio.FatosDeEtapaOuInscricao(Agora).Should().ContainSingle().Which.Should().Be(new FatoQuePodeIniciarAFase(fase.FaseCanonicaOrigemId, versao, Abertura));
    }

    [Fact(DisplayName = "O período de uma versão que não vigorou não gera fato")]
    public void PeriodoDeVersaoQueNaoVigorou_NaoGeraFato()
    {
        FasesDaVersaoPublicada versao = Versao(UmaFase(inicio: Abertura), vigenteAPartirDe: Abertura.AddDays(2), substituidaEm: Abertura.AddDays(2));

        Inicio([versao], SemFatos).PrimeirosFatos(Agora).Should().BeEmpty();
    }

    [Fact(DisplayName = "A importação com a versão fixada fora das vigentes vale pela versão vigente no instante da importação")]
    public void ImportacaoDeNotas_VersaoFixadaForaDasVigentes_ValePelaVigenteNoInstante()
    {
        EtapaDaVersaoPublicada etapa = UmaEtapa();
        FaseDaVersaoPublicada fase = UmaFase(etapas: [etapa]);
        FasesDaVersaoPublicada versao = Versao(fase);
        FatosDoInicioDasFases fatos = SemFatos with { Importacoes = [new ImportacaoDeNotasNaEtapa(etapa.Id, Guid.CreateVersion7(), Abertura)] };

        Inicio([versao], fatos).FatosDeEtapaOuInscricao(Agora).Should().ContainSingle()
            .Which.Should().Be(new FatoQuePodeIniciarAFase(fase.FaseCanonicaOrigemId, versao, Abertura));
    }
}
