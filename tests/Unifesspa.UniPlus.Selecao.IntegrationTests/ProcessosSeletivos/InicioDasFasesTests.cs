namespace Unifesspa.UniPlus.Selecao.IntegrationTests.ProcessosSeletivos;

using AwesomeAssertions;

using Unifesspa.UniPlus.Selecao.Domain.MotivosDeDecisao;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Repositories;

using static Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Repositories.FasesNoEnvelope;
using static Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Repositories.InicioDasFases;

/// <summary>
/// A regra de quais fases e etapas começaram, sobre versões e fatos montados na memória, com instantes fixos: a fase
/// produtora conta só o que é da própria fase, e o que começou nunca deixa de ter começado.
/// </summary>
public sealed class InicioDasFasesTests
{
    private static readonly DateTimeOffset Vigencia = new(2040, 1, 1, 0, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Abertura = new(2040, 3, 10, 0, 0, 0, TimeSpan.Zero);
    private static readonly Fatos SemFatos = new([], [], [], [], []);

    [Fact(DisplayName = "A janela da fase que abre no instante da consulta já a iniciou")]
    public void Janela_NoInstanteDaConsulta_Iniciou()
    {
        Fase fase = UmaFase(inicio: Abertura);

        FasesProdutoras([Versao(fase)], Abertura, SemFatos).Should().Contain(fase.OrigemId);
        Origens([Versao(fase)], Abertura, SemFatos).Should().Contain(fase.OrigemId);
    }

    [Fact(DisplayName = "Um tick antes de a janela abrir a fase ainda não começou")]
    public void Janela_UmTickAntes_NaoIniciou()
    {
        Fase fase = UmaFase(inicio: Abertura);

        FasesProdutoras([Versao(fase)], Abertura.AddTicks(-1), SemFatos).Should().BeEmpty();
        Origens([Versao(fase)], Abertura.AddTicks(-1), SemFatos).Should().BeEmpty();
    }

    [Fact(DisplayName = "A janela que coincide com o instante em que a versão foi substituída não conta")]
    public void Janela_IgualASubstituidaEm_NaoConta()
    {
        Guid origem = Guid.CreateVersion7();
        VersaoDasFases substituida = Versao(UmaFase(origem, inicio: Abertura), vigenteAPartirDe: Vigencia, substituidaEm: Abertura);
        VersaoDasFases sucessora = Versao(UmaFase(origem), vigenteAPartirDe: Abertura);

        FasesProdutoras([sucessora, substituida], Abertura.AddDays(1), SemFatos).Should().BeEmpty();
    }

    [Fact(DisplayName = "A janela que abriu um tick antes de a versão ser substituída conta")]
    public void Janela_UmTickAntesDeSubstituidaEm_Conta()
    {
        Guid origem = Guid.CreateVersion7();
        VersaoDasFases substituida = Versao(UmaFase(origem, inicio: Abertura.AddTicks(-1)), vigenteAPartirDe: Vigencia, substituidaEm: Abertura);
        VersaoDasFases sucessora = Versao(UmaFase(origem), vigenteAPartirDe: Abertura);

        FasesProdutoras([sucessora, substituida], Abertura.AddDays(1), SemFatos).Should().Contain(origem);
    }

    [Fact(DisplayName = "Adiar a janela numa retificação depois de ela abrir não desfaz o início")]
    public void Janela_AdiadaDepoisDeAbrir_ContinuaIniciada()
    {
        Guid origem = Guid.CreateVersion7();
        VersaoDasFases substituida = Versao(UmaFase(origem, inicio: Abertura), vigenteAPartirDe: Vigencia, substituidaEm: Abertura.AddDays(2));
        VersaoDasFases adiada = Versao(UmaFase(origem, inicio: Abertura.AddYears(5)), vigenteAPartirDe: Abertura.AddDays(2));

        FasesProdutoras([adiada, substituida], Abertura.AddDays(5), SemFatos).Should().Contain(origem);
        Origens([adiada, substituida], Abertura.AddDays(5), SemFatos).Should().Contain(origem);
    }

    [Fact(DisplayName = "A janela de uma etapa inicia a fase para a autorização, mas não a fase produtora")]
    public void JanelaDaEtapa_NaoIniciaAFaseProdutora()
    {
        EtapaDaFase etapa = UmaEtapa(inicio: Abertura);
        Fase fase = UmaFase(etapas: [etapa]);

        FasesProdutoras([Versao(fase)], Abertura, SemFatos).Should().BeEmpty();
        Origens([Versao(fase)], Abertura, SemFatos).Should().Contain(fase.OrigemId);
        Etapas([Versao(fase)], Abertura, SemFatos).Should().Contain(etapa.Id);
    }

    [Fact(DisplayName = "A inscrição recebida inicia a fase para a autorização, mas não a fase produtora")]
    public void Inscricao_NaoIniciaAFaseProdutora()
    {
        Fase fase = UmaFase(coletaInscricao: true);
        Fatos fatos = SemFatos with { Inscricoes = [Abertura] };

        FasesProdutoras([Versao(fase)], Abertura.AddDays(1), fatos).Should().BeEmpty();
        Origens([Versao(fase)], Abertura.AddDays(1), fatos).Should().Contain(fase.OrigemId);
    }

    [Fact(DisplayName = "O ato do item da fase inicia a fase produtora, no instante fixo da divulgação do ato")]
    public void AtoDaFase_IniciaAFaseProdutora()
    {
        Fase fase = UmaFase(inicio: Abertura.AddYears(5));
        Fatos fatos = SemFatos with { Atos = [($"fase:{fase.OrigemId:D}|RESULTADO", Abertura)] };

        FasesProdutoras([Versao(fase)], Abertura.AddDays(30), fatos).Should().Contain(fase.OrigemId);
        FasesProdutoras([Versao(fase)], Abertura.AddDays(90), fatos).Should().Contain(fase.OrigemId);
    }

    [Fact(DisplayName = "O ato do item da etapa inicia a etapa e a fase de então, e não a fase produtora")]
    public void AtoDaEtapa_IniciaAEtapaENaoAFaseProdutora()
    {
        (VersaoDasFases[] versoes, Guid primeira, Guid segunda, Guid etapa) = EtapaMovidaDeFase();
        Fatos fatos = SemFatos with { Atos = [($"etapa:{etapa:D}|RESULTADO", Abertura)] };

        Etapas(versoes, Abertura.AddDays(10), fatos).Should().Contain(etapa);
        Origens(versoes, Abertura.AddDays(10), fatos).Should().Contain(primeira).And.NotContain(segunda);
        FasesProdutoras(versoes, Abertura.AddDays(10), fatos).Should().BeEmpty();
    }

    [Fact(DisplayName = "O início no lugar da fase inicia a fase produtora e a fase para a autorização")]
    public void InicioNoLugarDaFase_IniciaAFaseProdutora()
    {
        Fase fase = UmaFase(inicio: Abertura.AddYears(5));
        Fatos fatos = SemFatos with { IniciosNoLugar = [new InicioNoLugar(LugarDaDecisao.DaFase(fase.OrigemId), Abertura)] };

        FasesProdutoras([Versao(fase)], Abertura.AddDays(1), fatos).Should().Contain(fase.OrigemId);
        Origens([Versao(fase)], Abertura.AddDays(1), fatos).Should().Contain(fase.OrigemId);
    }

    [Fact(DisplayName = "A etapa sem janela só começa pelo início no lugar dela")]
    public void EtapaSemJanela_ComecaSoPeloInicioNoLugar()
    {
        EtapaDaFase etapa = UmaEtapa();
        VersaoDasFases[] versoes = [Versao(UmaFase(etapas: [etapa]))];
        Fatos fatos = SemFatos with { IniciosNoLugar = [new InicioNoLugar(LugarDaDecisao.DaEtapa(etapa.Id), Abertura)] };

        Etapas(versoes, Abertura.AddDays(1), SemFatos).Should().BeEmpty();
        Etapas(versoes, Abertura.AddDays(1), fatos).Should().Contain(etapa.Id);
    }

    [Fact(DisplayName = "O início no lugar da etapa vale para a fase a que a etapa pertencia no instante do fato")]
    public void InicioNoLugarDaEtapa_ValeParaAFaseDaEpoca()
    {
        (VersaoDasFases[] versoes, Guid primeira, Guid segunda, Guid etapa) = EtapaMovidaDeFase();
        Fatos fatos = SemFatos with { IniciosNoLugar = [new InicioNoLugar(LugarDaDecisao.DaEtapa(etapa), Abertura)] };

        Origens(versoes, Abertura.AddDays(10), fatos).Should().Contain(primeira).And.NotContain(segunda);
        FasesProdutoras(versoes, Abertura.AddDays(10), fatos).Should().BeEmpty();
    }

    public enum TipoDeFato
    {
        AtoDaFase,
        AtoDaEtapa,
        ImportacaoDeNotas,
        EnvioAoInep,
        InscricaoRecebida,
        InicioNoLugarDaFase,
        InicioNoLugarDaEtapa,
    }

    [Theory(DisplayName = "O fato que aconteceu antes do instante consultado, ou nele, inicia a fase")]
    [InlineData(TipoDeFato.AtoDaFase)]
    [InlineData(TipoDeFato.AtoDaEtapa)]
    [InlineData(TipoDeFato.ImportacaoDeNotas)]
    [InlineData(TipoDeFato.EnvioAoInep)]
    [InlineData(TipoDeFato.InscricaoRecebida)]
    [InlineData(TipoDeFato.InicioNoLugarDaFase)]
    [InlineData(TipoDeFato.InicioNoLugarDaEtapa)]
    public void Fato_AteOInstanteConsultado_IniciaAFase(TipoDeFato tipo)
    {
        ProcessoComUmaEtapa processo = UmProcessoComUmaEtapa();

        Origens(processo.Versoes, Abertura, FatoDoTipo(tipo, processo, Abertura)).Should().Contain(processo.Fase.OrigemId);
        Origens(processo.Versoes, Abertura, FatoDoTipo(tipo, processo, Abertura.AddTicks(-1))).Should().Contain(processo.Fase.OrigemId);
    }

    [Theory(DisplayName = "O fato que aconteceu um tick depois do instante consultado não inicia fase nem etapa")]
    [InlineData(TipoDeFato.AtoDaFase)]
    [InlineData(TipoDeFato.AtoDaEtapa)]
    [InlineData(TipoDeFato.ImportacaoDeNotas)]
    [InlineData(TipoDeFato.EnvioAoInep)]
    [InlineData(TipoDeFato.InscricaoRecebida)]
    [InlineData(TipoDeFato.InicioNoLugarDaFase)]
    [InlineData(TipoDeFato.InicioNoLugarDaEtapa)]
    public void Fato_DepoisDoInstanteConsultado_NaoIniciaNada(TipoDeFato tipo)
    {
        ProcessoComUmaEtapa processo = UmProcessoComUmaEtapa();
        Fatos fatos = FatoDoTipo(tipo, processo, Abertura.AddTicks(1));

        Origens(processo.Versoes, Abertura, fatos).Should().BeEmpty();
        Etapas(processo.Versoes, Abertura, fatos).Should().BeEmpty();
        FasesProdutoras(processo.Versoes, Abertura, fatos).Should().BeEmpty();
    }

    [Fact(DisplayName = "Os inícios no lugar da etapa antes e depois de ela mudar de fase iniciam cada fase que a continha")]
    public void IniciosNoLugarDaEtapa_AntesEDepoisDaMudancaDeFase_IniciamCadaFase()
    {
        (VersaoDasFases[] versoes, Guid primeira, Guid segunda, Guid etapa) = EtapaMovidaDeFase();
        InicioNoLugar naPrimeira = new(LugarDaDecisao.DaEtapa(etapa), Abertura);
        InicioNoLugar naSegunda = new(LugarDaDecisao.DaEtapa(etapa), Abertura.AddDays(2));

        Origens(versoes, Abertura.AddDays(10), SemFatos with { IniciosNoLugar = [naPrimeira] }).Should().Contain(primeira).And.NotContain(segunda);
        Origens(versoes, Abertura.AddDays(10), SemFatos with { IniciosNoLugar = [naSegunda] }).Should().Contain(segunda).And.NotContain(primeira);
        Origens(versoes, Abertura.AddDays(10), SemFatos with { IniciosNoLugar = [naPrimeira, naSegunda] }).Should().Contain([primeira, segunda]);
    }

    private sealed record ProcessoComUmaEtapa(VersaoDasFases[] Versoes, Fase Fase, EtapaDaFase Etapa);

    // A fase tem a janela longe no futuro, e a etapa não tem janela: só o fato inicia.
    private static ProcessoComUmaEtapa UmProcessoComUmaEtapa()
    {
        EtapaDaFase etapa = UmaEtapa();
        Fase fase = UmaFase(inicio: Abertura.AddYears(5), coletaInscricao: true, etapas: [etapa]);
        return new ProcessoComUmaEtapa([Versao(fase)], fase, etapa);
    }

    private static Fatos FatoDoTipo(TipoDeFato tipo, ProcessoComUmaEtapa processo, DateTimeOffset instante) => tipo switch
    {
        TipoDeFato.AtoDaFase => SemFatos with { Atos = [($"fase:{processo.Fase.OrigemId:D}|RESULTADO", instante)] },
        TipoDeFato.AtoDaEtapa => SemFatos with { Atos = [($"etapa:{processo.Etapa.Id:D}|RESULTADO", instante)] },
        TipoDeFato.ImportacaoDeNotas => SemFatos with { Importacoes = [new ImportacaoNaEtapa(processo.Etapa.Id, processo.Versoes[0].Id, instante)] },
        TipoDeFato.EnvioAoInep => SemFatos with { Envios = [new EnvioAoInepDaEtapa(processo.Etapa.Id, instante)] },
        TipoDeFato.InscricaoRecebida => SemFatos with { Inscricoes = [instante] },
        TipoDeFato.InicioNoLugarDaFase => SemFatos with { IniciosNoLugar = [new InicioNoLugar(LugarDaDecisao.DaFase(processo.Fase.OrigemId), instante)] },
        _ => SemFatos with { IniciosNoLugar = [new InicioNoLugar(LugarDaDecisao.DaEtapa(processo.Etapa.Id), instante)] },
    };

    // A etapa é da primeira fase na versão que vigia na abertura e passa à segunda na que a sucede um dia depois.
    private static (VersaoDasFases[] Versoes, Guid Primeira, Guid Segunda, Guid Etapa) EtapaMovidaDeFase()
    {
        EtapaDaFase etapa = UmaEtapa();
        Fase primeira = UmaFase(etapas: [etapa]);
        Fase segunda = UmaFase(etapas: [etapa]);
        VersaoDasFases anterior = Versao(primeira, vigenteAPartirDe: Vigencia, substituidaEm: Abertura.AddDays(1));
        VersaoDasFases posterior = Versao(segunda, vigenteAPartirDe: Abertura.AddDays(1));

        return ([posterior, anterior], primeira.OrigemId, segunda.OrigemId, etapa.Id);
    }

    private static VersaoDasFases Versao(Fase fase, DateTimeOffset? vigenteAPartirDe = null, DateTimeOffset? substituidaEm = null) =>
        new(Guid.CreateVersion7(), vigenteAPartirDe ?? Vigencia, substituidaEm, [fase]);

    private static Fase UmaFase(Guid? origem = null, DateTimeOffset? inicio = null, bool coletaInscricao = false, params EtapaDaFase[] etapas) =>
        new(Guid.CreateVersion7(), origem ?? Guid.CreateVersion7(), "FASE", null, inicio, null, coletaInscricao, etapas);

    private static EtapaDaFase UmaEtapa(DateTimeOffset? inicio = null) =>
        new(Guid.CreateVersion7(), "Etapa", inicio, null);
}
