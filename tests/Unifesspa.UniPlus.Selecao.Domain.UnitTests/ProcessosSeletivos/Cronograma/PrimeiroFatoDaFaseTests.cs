namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.Cronograma;

using AwesomeAssertions;

using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Cronograma;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

using static Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.Cronograma.VersoesPublicadasDeTeste;

/// <summary>
/// A escolha do primeiro fato de cada fase: o de menor instante, e no empate o da própria fase antes dos demais. Escolher
/// o primeiro fato não muda quais fases começaram.
/// </summary>
public sealed class PrimeiroFatoDaFaseTests
{
    private static readonly DateTimeOffset Agora = Abertura.AddDays(30);

    [Fact(DisplayName = "No empate entre o período e o ato da fase no mesmo instante, vale o período, que traz a versão")]
    public void Empate_PeriodoEAtoDaFase_ValeOPeriodo()
    {
        FaseDaVersaoPublicada fase = UmaFase(inicio: Abertura);
        FasesDaVersaoPublicada versao = Versao(fase);
        InicioDasFases inicio = Inicio([versao], SemFatos with { Atos = [AtoDaFase(fase.FaseCanonicaOrigemId, Abertura)] });

        inicio.PrimeirosFatos(Agora).Should().ContainSingle().Which.Should().Be(new FatoQuePodeIniciarAFase(fase.FaseCanonicaOrigemId, versao, Abertura));
        inicio.FasesIniciadas(Agora).Should().Equal(fase.FaseCanonicaOrigemId);
        inicio.FasesProdutoras(Agora).Should().Equal(fase.FaseCanonicaOrigemId);
    }

    [Fact(DisplayName = "No empate entre o ato da fase e a inscrição recebida, vale o ato, que é da própria fase")]
    public void Empate_AtoDaFaseEInscricao_ValeOAto()
    {
        FaseDaVersaoPublicada fase = UmaFase(coletaInscricao: true);
        FatosDoInicioDasFases fatos = SemFatos with { Atos = [AtoDaFase(fase.FaseCanonicaOrigemId, Abertura)], InscricoesRecebidas = [Abertura] };

        Inicio([Versao(fase)], fatos).PrimeirosFatos(Agora).Should().ContainSingle()
            .Which.Should().Be(new FatoQuePodeIniciarAFase(fase.FaseCanonicaOrigemId, null, Abertura));
    }

    [Fact(DisplayName = "O fato mais antigo vale mesmo quando listado depois dos demais")]
    public void FatoMaisAntigo_ValeMesmoListadoDepois()
    {
        FaseDaVersaoPublicada fase = UmaFase(coletaInscricao: true);
        FatosDoInicioDasFases fatos = SemFatos with { Atos = [AtoDaFase(fase.FaseCanonicaOrigemId, Abertura.AddDays(3))], InscricoesRecebidas = [Abertura] };

        Inicio([Versao(fase)], fatos).PrimeirosFatos(Agora).Should().ContainSingle().Which.Instante.Should().Be(Abertura);
    }

    [Fact(DisplayName = "A versão substituída no mesmo instante em que passou a vigir não dá fato, e a fase só começa pela versão que a sucede")]
    public void VersaoSubstituidaNoInstanteDaVigencia_NaoDaFato()
    {
        Guid origem = Guid.CreateVersion7();
        FasesDaVersaoPublicada semVigorar = Versao(UmaFase(origem, inicio: Abertura.AddDays(-5)), vigenteAPartirDe: Abertura, substituidaEm: Abertura);
        FasesDaVersaoPublicada sucessora = Versao(UmaFase(origem, inicio: Abertura.AddDays(20)), vigenteAPartirDe: Abertura);
        InicioDasFases inicio = Inicio([sucessora, semVigorar], SemFatos);

        inicio.PrimeirosFatos(Abertura.AddDays(10)).Should().BeEmpty();
        inicio.FasesIniciadas(Abertura.AddDays(10)).Should().BeEmpty();
        inicio.FasesIniciadas(Agora).Should().Equal(origem);
    }

    [Fact(DisplayName = "A data do período anterior ao instante da substituição conta para a versão substituída")]
    public void DataAnteriorASubstituicao_Conta()
    {
        Guid origem = Guid.CreateVersion7();
        FasesDaVersaoPublicada substituida = Versao(UmaFase(origem, inicio: Abertura.AddDays(-2)), vigenteAPartirDe: Vigencia, substituidaEm: Abertura);
        InicioDasFases inicio = Inicio([Versao(UmaFase(origem), vigenteAPartirDe: Abertura), substituida], SemFatos);

        inicio.PrimeirosFatos(Agora).Should().ContainSingle().Which.Instante.Should().Be(Abertura.AddDays(-2));
        inicio.FasesIniciadas(Agora).Should().Equal(origem);
    }

    [Fact(DisplayName = "O período que a versão nova declara antes de vigir conta na vigência dela")]
    public void PeriodoDeVersaoNovaDeclaradoAntesDaVigencia_ContaNaVigencia()
    {
        Guid origem = Guid.CreateVersion7();
        InicioDasFases inicio = Inicio(VersoesComPeriodoDaNovaNoDia5(origem), SemFatos);

        inicio.PrimeirosFatos(Agora).Should().ContainSingle().Which.Instante.Should().Be(Abertura);
        inicio.FasesIniciadas(Agora).Should().Equal(origem);
    }

    [Fact(DisplayName = "O ato publicado antes da vigência da versão nova é o primeiro fato da fase, e não o período dessa versão")]
    public void AtoDaVersaoAnterior_AntesDaVigenciaDaNova_EOPrimeiroFato()
    {
        Guid origem = Guid.CreateVersion7();
        FatosDoInicioDasFases fatos = SemFatos with { Atos = [AtoDaFase(origem, Abertura.AddDays(-3))] };
        InicioDasFases inicio = Inicio(VersoesComPeriodoDaNovaNoDia5(origem), fatos);

        inicio.PrimeirosFatos(Agora).Should().ContainSingle().Which.Should().Be(new FatoQuePodeIniciarAFase(origem, null, Abertura.AddDays(-3)));
        inicio.FasesIniciadas(Agora).Should().Equal(origem);
    }

    // A primeira versão vige até a abertura; a segunda vige a partir dela e declara o período iniciado cinco dias antes.
    private static FasesDaVersaoPublicada[] VersoesComPeriodoDaNovaNoDia5(Guid origem) =>
    [
        Versao(UmaFase(origem, inicio: Abertura.AddDays(-5)), vigenteAPartirDe: Abertura),
        Versao(UmaFase(origem), vigenteAPartirDe: Vigencia, substituidaEm: Abertura),
    ];
}
