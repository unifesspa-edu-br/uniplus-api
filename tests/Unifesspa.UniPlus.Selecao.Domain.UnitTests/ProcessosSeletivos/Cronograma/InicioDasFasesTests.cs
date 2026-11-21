namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.Cronograma;

using AwesomeAssertions;

using Unifesspa.UniPlus.Selecao.Domain.MotivosDeDecisao;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Cronograma;

using static Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.Cronograma.VersoesPublicadasDeTeste;

/// <summary>
/// A regra de quais fases e etapas começaram, sobre versões e fatos montados na memória, com instantes fixos: a fase
/// produtora conta só o que é da própria fase, e o que começou nunca deixa de ter começado.
/// </summary>
public sealed class InicioDasFasesTests
{
    public enum TipoDeFato
    {
        AtoDaFase,
        AtoDaEtapa,
        ImportacaoDeNotas,
        EnvioAoInep,
        InscricaoRecebida,
        AvaliacaoNaFase,
        AvaliacaoNaEtapa,
    }

    [Fact(DisplayName = "O período da fase que abre no instante da consulta já a iniciou")]
    public void Periodo_NoInstanteDaConsulta_Iniciou()
    {
        FaseDaVersaoPublicada fase = UmaFase(inicio: Abertura);
        InicioDasFases inicio = Inicio([Versao(fase)], SemFatos);

        inicio.FasesProdutoras(Abertura).Should().Contain(fase.FaseCanonicaOrigemId);
        inicio.FasesIniciadas(Abertura).Should().Contain(fase.FaseCanonicaOrigemId);
    }

    [Fact(DisplayName = "Um tick antes de o período abrir a fase ainda não começou")]
    public void Periodo_UmTickAntes_NaoIniciou()
    {
        InicioDasFases inicio = Inicio([Versao(UmaFase(inicio: Abertura))], SemFatos);

        inicio.FasesProdutoras(Abertura.AddTicks(-1)).Should().BeEmpty();
        inicio.FasesIniciadas(Abertura.AddTicks(-1)).Should().BeEmpty();
    }

    [Fact(DisplayName = "O período que coincide com o instante em que a versão foi substituída não conta")]
    public void Periodo_IgualASubstituidaEm_NaoConta()
    {
        Guid origem = Guid.CreateVersion7();
        FasesDaVersaoPublicada substituida = Versao(UmaFase(origem, inicio: Abertura), vigenteAPartirDe: Vigencia, substituidaEm: Abertura);
        FasesDaVersaoPublicada sucessora = Versao(UmaFase(origem), vigenteAPartirDe: Abertura);

        Inicio([sucessora, substituida], SemFatos).FasesProdutoras(Abertura.AddDays(1)).Should().BeEmpty();
    }

    [Fact(DisplayName = "O período que abriu um tick antes de a versão ser substituída conta")]
    public void Periodo_UmTickAntesDeSubstituidaEm_Conta()
    {
        Guid origem = Guid.CreateVersion7();
        FasesDaVersaoPublicada substituida = Versao(UmaFase(origem, inicio: Abertura.AddTicks(-1)), vigenteAPartirDe: Vigencia, substituidaEm: Abertura);
        FasesDaVersaoPublicada sucessora = Versao(UmaFase(origem), vigenteAPartirDe: Abertura);

        Inicio([sucessora, substituida], SemFatos).FasesProdutoras(Abertura.AddDays(1)).Should().Contain(origem);
    }

    [Fact(DisplayName = "Adiar o período numa retificação depois de ele abrir não desfaz o início")]
    public void Periodo_AdiadoDepoisDeAbrir_ContinuaIniciado()
    {
        Guid origem = Guid.CreateVersion7();
        FasesDaVersaoPublicada substituida = Versao(UmaFase(origem, inicio: Abertura), vigenteAPartirDe: Vigencia, substituidaEm: Abertura.AddDays(2));
        FasesDaVersaoPublicada adiada = Versao(UmaFase(origem, inicio: Abertura.AddYears(5)), vigenteAPartirDe: Abertura.AddDays(2));
        InicioDasFases inicio = Inicio([adiada, substituida], SemFatos);

        inicio.FasesProdutoras(Abertura.AddDays(5)).Should().Contain(origem);
        inicio.FasesIniciadas(Abertura.AddDays(5)).Should().Contain(origem);
    }

    [Fact(DisplayName = "O período de uma etapa inicia a fase para a autorização, mas não a fase produtora")]
    public void PeriodoDaEtapa_NaoIniciaAFaseProdutora()
    {
        EtapaDaVersaoPublicada etapa = UmaEtapa(inicio: Abertura);
        FaseDaVersaoPublicada fase = UmaFase(etapas: [etapa]);
        InicioDasFases inicio = Inicio([Versao(fase)], SemFatos);

        inicio.FasesProdutoras(Abertura).Should().BeEmpty();
        inicio.FasesIniciadas(Abertura).Should().Contain(fase.FaseCanonicaOrigemId);
        inicio.EtapasIniciadas(Abertura).Should().Contain(etapa.Id);
    }

    [Fact(DisplayName = "A inscrição recebida inicia a fase para a autorização, mas não a fase produtora")]
    public void Inscricao_NaoIniciaAFaseProdutora()
    {
        FaseDaVersaoPublicada fase = UmaFase(coletaInscricao: true);
        InicioDasFases inicio = Inicio([Versao(fase)], SemFatos with { InscricoesRecebidas = [Abertura] });

        inicio.FasesProdutoras(Abertura.AddDays(1)).Should().BeEmpty();
        inicio.FasesIniciadas(Abertura.AddDays(1)).Should().Contain(fase.FaseCanonicaOrigemId);
    }

    [Fact(DisplayName = "O ato do item da fase inicia a fase produtora, no instante fixo da divulgação do ato")]
    public void AtoDaFase_IniciaAFaseProdutora()
    {
        FaseDaVersaoPublicada fase = UmaFase(inicio: Abertura.AddYears(5));
        InicioDasFases inicio = Inicio([Versao(fase)], SemFatos with { Atos = [AtoDaFase(fase.FaseCanonicaOrigemId, Abertura)] });

        inicio.FasesProdutoras(Abertura.AddDays(30)).Should().Contain(fase.FaseCanonicaOrigemId);
        inicio.FasesProdutoras(Abertura.AddDays(90)).Should().Contain(fase.FaseCanonicaOrigemId);
    }

    [Fact(DisplayName = "O ato do item da etapa inicia a etapa e a fase de então, e não a fase produtora")]
    public void AtoDaEtapa_IniciaAEtapaENaoAFaseProdutora()
    {
        (FasesDaVersaoPublicada[] versoes, Guid primeira, Guid segunda, Guid etapa) = EtapaMovidaDeFase();
        InicioDasFases inicio = Inicio(versoes, SemFatos with { Atos = [AtoDaEtapa(etapa, Abertura)] });

        inicio.EtapasIniciadas(Abertura.AddDays(10)).Should().Contain(etapa);
        inicio.FasesIniciadas(Abertura.AddDays(10)).Should().Contain(primeira).And.NotContain(segunda);
        inicio.FasesProdutoras(Abertura.AddDays(10)).Should().BeEmpty();
    }

    [Fact(DisplayName = "A avaliação iniciada na fase inicia a fase produtora e a fase para a autorização")]
    public void AvaliacaoNaFase_IniciaAFaseProdutora()
    {
        FaseDaVersaoPublicada fase = UmaFase(inicio: Abertura.AddYears(5));
        FatosDoInicioDasFases fatos = SemFatos with { AvaliacoesIniciadas = [new AvaliacaoIniciadaNoLugar(LugarDaDecisao.DaFase(fase.FaseCanonicaOrigemId), Abertura)] };
        InicioDasFases inicio = Inicio([Versao(fase)], fatos);

        inicio.FasesProdutoras(Abertura.AddDays(1)).Should().Contain(fase.FaseCanonicaOrigemId);
        inicio.FasesIniciadas(Abertura.AddDays(1)).Should().Contain(fase.FaseCanonicaOrigemId);
    }

    [Fact(DisplayName = "A etapa sem período só começa pela avaliação iniciada nela")]
    public void EtapaSemPeriodo_ComecaSoPelaAvaliacaoIniciada()
    {
        EtapaDaVersaoPublicada etapa = UmaEtapa();
        FasesDaVersaoPublicada[] versoes = [Versao(UmaFase(etapas: [etapa]))];
        FatosDoInicioDasFases fatos = SemFatos with { AvaliacoesIniciadas = [new AvaliacaoIniciadaNoLugar(LugarDaDecisao.DaEtapa(etapa.Id), Abertura)] };

        Inicio(versoes, SemFatos).EtapasIniciadas(Abertura.AddDays(1)).Should().BeEmpty();
        Inicio(versoes, fatos).EtapasIniciadas(Abertura.AddDays(1)).Should().Contain(etapa.Id);
    }

    [Fact(DisplayName = "A avaliação iniciada na etapa vale para a fase a que a etapa pertencia no instante do fato")]
    public void AvaliacaoNaEtapa_ValeParaAFaseDaEpoca()
    {
        (FasesDaVersaoPublicada[] versoes, Guid primeira, Guid segunda, Guid etapa) = EtapaMovidaDeFase();
        FatosDoInicioDasFases fatos = SemFatos with { AvaliacoesIniciadas = [new AvaliacaoIniciadaNoLugar(LugarDaDecisao.DaEtapa(etapa), Abertura)] };
        InicioDasFases inicio = Inicio(versoes, fatos);

        inicio.FasesIniciadas(Abertura.AddDays(10)).Should().Contain(primeira).And.NotContain(segunda);
        inicio.FasesProdutoras(Abertura.AddDays(10)).Should().BeEmpty();
    }

    [Theory(DisplayName = "O fato que aconteceu antes do instante consultado, ou nele, inicia a fase")]
    [InlineData(TipoDeFato.AtoDaFase)]
    [InlineData(TipoDeFato.AtoDaEtapa)]
    [InlineData(TipoDeFato.ImportacaoDeNotas)]
    [InlineData(TipoDeFato.EnvioAoInep)]
    [InlineData(TipoDeFato.InscricaoRecebida)]
    [InlineData(TipoDeFato.AvaliacaoNaFase)]
    [InlineData(TipoDeFato.AvaliacaoNaEtapa)]
    public void Fato_AteOInstanteConsultado_IniciaAFase(TipoDeFato tipo)
    {
        ProcessoComUmaEtapa processo = UmProcessoComUmaEtapa();

        Inicio(processo.Versoes, FatoDoTipo(tipo, processo, Abertura)).FasesIniciadas(Abertura).Should().Contain(processo.Fase.FaseCanonicaOrigemId);
        Inicio(processo.Versoes, FatoDoTipo(tipo, processo, Abertura.AddTicks(-1))).FasesIniciadas(Abertura).Should().Contain(processo.Fase.FaseCanonicaOrigemId);
    }

    [Theory(DisplayName = "O fato que aconteceu um tick depois do instante consultado não inicia fase nem etapa")]
    [InlineData(TipoDeFato.AtoDaFase)]
    [InlineData(TipoDeFato.AtoDaEtapa)]
    [InlineData(TipoDeFato.ImportacaoDeNotas)]
    [InlineData(TipoDeFato.EnvioAoInep)]
    [InlineData(TipoDeFato.InscricaoRecebida)]
    [InlineData(TipoDeFato.AvaliacaoNaFase)]
    [InlineData(TipoDeFato.AvaliacaoNaEtapa)]
    public void Fato_DepoisDoInstanteConsultado_NaoIniciaNada(TipoDeFato tipo)
    {
        ProcessoComUmaEtapa processo = UmProcessoComUmaEtapa();
        InicioDasFases inicio = Inicio(processo.Versoes, FatoDoTipo(tipo, processo, Abertura.AddTicks(1)));

        inicio.FasesIniciadas(Abertura).Should().BeEmpty();
        inicio.EtapasIniciadas(Abertura).Should().BeEmpty();
        inicio.FasesProdutoras(Abertura).Should().BeEmpty();
    }

    [Fact(DisplayName = "As avaliações iniciadas na etapa antes e depois de ela mudar de fase iniciam cada fase que a continha")]
    public void AvaliacoesNaEtapa_AntesEDepoisDaMudancaDeFase_IniciamCadaFase()
    {
        (FasesDaVersaoPublicada[] versoes, Guid primeira, Guid segunda, Guid etapa) = EtapaMovidaDeFase();
        AvaliacaoIniciadaNoLugar naPrimeira = new(LugarDaDecisao.DaEtapa(etapa), Abertura);
        AvaliacaoIniciadaNoLugar naSegunda = new(LugarDaDecisao.DaEtapa(etapa), Abertura.AddDays(2));

        FasesIniciadasCom(versoes, naPrimeira).Should().Contain(primeira).And.NotContain(segunda);
        FasesIniciadasCom(versoes, naSegunda).Should().Contain(segunda).And.NotContain(primeira);
        FasesIniciadasCom(versoes, naPrimeira, naSegunda).Should().Contain([primeira, segunda]);
    }

    private static HashSet<Guid> FasesIniciadasCom(FasesDaVersaoPublicada[] versoes, params AvaliacaoIniciadaNoLugar[] avaliacoes) =>
        Inicio(versoes, SemFatos with { AvaliacoesIniciadas = avaliacoes }).FasesIniciadas(Abertura.AddDays(10));

    private sealed record ProcessoComUmaEtapa(FasesDaVersaoPublicada[] Versoes, FaseDaVersaoPublicada Fase, EtapaDaVersaoPublicada Etapa);

    // A fase tem o período longe no futuro, e a etapa não tem período: só o fato inicia.
    private static ProcessoComUmaEtapa UmProcessoComUmaEtapa()
    {
        EtapaDaVersaoPublicada etapa = UmaEtapa();
        FaseDaVersaoPublicada fase = UmaFase(inicio: Abertura.AddYears(5), coletaInscricao: true, etapas: [etapa]);
        return new ProcessoComUmaEtapa([Versao(fase)], fase, etapa);
    }

    private static FatosDoInicioDasFases FatoDoTipo(TipoDeFato tipo, ProcessoComUmaEtapa processo, DateTimeOffset instante) => tipo switch
    {
        TipoDeFato.AtoDaFase => SemFatos with { Atos = [AtoDaFase(processo.Fase.FaseCanonicaOrigemId, instante)] },
        TipoDeFato.AtoDaEtapa => SemFatos with { Atos = [AtoDaEtapa(processo.Etapa.Id, instante)] },
        TipoDeFato.ImportacaoDeNotas => SemFatos with { Importacoes = [new ImportacaoDeNotasNaEtapa(processo.Etapa.Id, processo.Versoes[0].VersaoId, instante)] },
        TipoDeFato.EnvioAoInep => SemFatos with { EnviosAoInep = [new EnvioAoInepDaEtapa(processo.Etapa.Id, instante)] },
        TipoDeFato.InscricaoRecebida => SemFatos with { InscricoesRecebidas = [instante] },
        TipoDeFato.AvaliacaoNaFase => SemFatos with { AvaliacoesIniciadas = [new AvaliacaoIniciadaNoLugar(LugarDaDecisao.DaFase(processo.Fase.FaseCanonicaOrigemId), instante)] },
        _ => SemFatos with { AvaliacoesIniciadas = [new AvaliacaoIniciadaNoLugar(LugarDaDecisao.DaEtapa(processo.Etapa.Id), instante)] },
    };

    // A etapa é da primeira fase na versão que vigia na abertura e passa à segunda na que a sucede um dia depois.
    private static (FasesDaVersaoPublicada[] Versoes, Guid Primeira, Guid Segunda, Guid Etapa) EtapaMovidaDeFase()
    {
        EtapaDaVersaoPublicada etapa = UmaEtapa();
        FaseDaVersaoPublicada primeira = UmaFase(etapas: [etapa]);
        FaseDaVersaoPublicada segunda = UmaFase(etapas: [etapa]);
        FasesDaVersaoPublicada anterior = Versao(primeira, vigenteAPartirDe: Vigencia, substituidaEm: Abertura.AddDays(1));
        FasesDaVersaoPublicada posterior = Versao(segunda, vigenteAPartirDe: Abertura.AddDays(1));

        return ([posterior, anterior], primeira.FaseCanonicaOrigemId, segunda.FaseCanonicaOrigemId, etapa.Id);
    }
}
