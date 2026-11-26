namespace Unifesspa.UniPlus.Autorizacao.Application.UnitTests;

using AwesomeAssertions;

using Unifesspa.UniPlus.Autorizacao.Application.DTOs;
using Unifesspa.UniPlus.Autorizacao.Application.Mappings;
using Unifesspa.UniPlus.Autorizacao.Domain.Entities;
using Unifesspa.UniPlus.Autorizacao.Domain.Enums;
using Unifesspa.UniPlus.Autorizacao.Domain.UnitTests;
using Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;

/// <summary>O tipo e o ator do evento que a decisão da revisão das concessões acrescenta à trilha da concessão.</summary>
public sealed class TipoDoEventoDaMigracaoTests
{
    private static readonly Identidade Gestor = PerfisDeTeste.Autor;
    private static readonly DateTimeOffset Abertura = CenarioDeMigracao.Abertura;

    private static readonly Dictionary<ResultadoDaMigracao, (string Tipo, string Ator)> DeCadaResultado = new()
    {
        [ResultadoDaMigracao.Migrar] = ("MIGRACAO_APROVADA", "HUMANO"),
        [ResultadoDaMigracao.Recusar] = ("MIGRACAO_RECUSADA", "HUMANO"),
        [ResultadoDaMigracao.Reter] = ("RETIDA_NA_MIGRACAO", "SISTEMA"),
        [ResultadoDaMigracao.NaoMigrada] = ("NAO_MIGRADA", "SISTEMA"),
        [ResultadoDaMigracao.ForaDaLista] = ("FORA_DA_LISTA_DA_MIGRACAO", "SISTEMA"),
    };

    public static TheoryData<ResultadoDaMigracao> Resultados => [.. Enum.GetValues<ResultadoDaMigracao>()];

    [Theory(DisplayName = "Cada resultado da revisão das concessões vira o seu tipo de evento, com o ator que a decisão gravou")]
    [MemberData(nameof(Resultados))]
    public void Decisao_CadaResultado_ViraOSeuTipoEOSeuAtor(ResultadoDaMigracao resultado)
    {
        DecisaoDaMigracao decisao = DecisoesDe(resultado).First(d => d.Resultado == resultado);

        EventoDaTrilhaDto evento = MigracaoMapping.ToEvento(decisao);

        (evento.Tipo.Codigo, evento.Ator.Codigo, evento.Autor?.ContaId).Should().Be((DeCadaResultado[resultado].Tipo, DeCadaResultado[resultado].Ator, decisao.DecisorContaId));
    }

    [Fact(DisplayName = "Cada resultado da revisão das concessões tem um tipo de evento diferente")]
    public void Decisao_TiposDeEvento_SaoDistintos()
    {
        DeCadaResultado.Keys.Should().BeEquivalentTo(Enum.GetValues<ResultadoDaMigracao>());
        DeCadaResultado.Values.Select(static v => v.Tipo).Should().OnlyHaveUniqueItems();
    }

    private static IReadOnlyList<DecisaoDaMigracao> DecisoesDe(ResultadoDaMigracao resultado) => resultado switch
    {
        ResultadoDaMigracao.Migrar or ResultadoDaMigracao.Recusar => DoEnvioDaGestao(),
        ResultadoDaMigracao.Reter => ComAConcessaoDoProprioGestor(),
        ResultadoDaMigracao.NaoMigrada => AoFimDoPrazoSemEnvio(),
        ResultadoDaMigracao.ForaDaLista => ComAConcessaoRevogadaDepoisDoEnvio(),
        _ => throw new ArgumentOutOfRangeException(nameof(resultado), resultado, null),
    };

    private static IReadOnlyList<DecisaoDaMigracao> DoEnvioDaGestao()
    {
        CenarioDeMigracao cenario = new(protegido: false);
        Concessao paraMigrar = cenario.Conceder(CenarioDeMigracao.Leandro);
        Concessao paraRecusar = cenario.Conceder(CenarioDeMigracao.Isabella);
        RevisaoDasConcessoesDoPerfil revisao = cenario.Abrir();
        revisao.EnviarDecisoesDaGestao(
            CenarioDeMigracao.Envio(true, (paraMigrar, ResultadoDaMigracao.Migrar), (paraRecusar, ResultadoDaMigracao.Recusar)),
            CenarioDeMigracao.Autoria(Gestor),
            cenario.Contexto(Abertura.AddHours(1)));

        return revisao.Decisoes;
    }

    private static IReadOnlyList<DecisaoDaMigracao> ComAConcessaoDoProprioGestor()
    {
        CenarioDeMigracao cenario = new(protegido: false);
        cenario.Conceder(Gestor);
        Concessao paraMigrar = cenario.Conceder(CenarioDeMigracao.Leandro);
        RevisaoDasConcessoesDoPerfil revisao = cenario.Abrir();
        revisao.EnviarDecisoesDaGestao(
            CenarioDeMigracao.Envio(false, (paraMigrar, ResultadoDaMigracao.Migrar)), CenarioDeMigracao.Autoria(Gestor), cenario.Contexto(Abertura.AddHours(1)));

        return revisao.Decisoes;
    }

    private static IReadOnlyList<DecisaoDaMigracao> AoFimDoPrazoSemEnvio()
    {
        CenarioDeMigracao cenario = new(protegido: false);
        cenario.Conceder(CenarioDeMigracao.Leandro);
        RevisaoDasConcessoesDoPerfil revisao = cenario.Abrir();
        revisao.ConferirEstado(cenario.Contexto(revisao.TerminaEm));

        return revisao.Decisoes;
    }

    private static IReadOnlyList<DecisaoDaMigracao> ComAConcessaoRevogadaDepoisDoEnvio()
    {
        CenarioDeMigracao cenario = new(protegido: true);
        Concessao revogada = cenario.Conceder(CenarioDeMigracao.Leandro);
        Concessao mantida = cenario.Conceder(CenarioDeMigracao.Isabella);
        RevisaoDasConcessoesDoPerfil revisao = cenario.Abrir();
        revisao.EnviarDecisoesDaGestao(
            CenarioDeMigracao.Envio(false, (revogada, ResultadoDaMigracao.Migrar), (mantida, ResultadoDaMigracao.Migrar)),
            CenarioDeMigracao.Autoria(Gestor),
            cenario.Contexto(Abertura.AddHours(1)));
        revogada.Revogar(new AtoDoAgente(PerfisDeTeste.Gestor, Abertura.AddHours(2)), "Revogação manual.");
        revisao.ConferirEstado(cenario.Contexto(Abertura.AddHours(3)));

        return revisao.Decisoes;
    }
}
