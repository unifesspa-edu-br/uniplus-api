namespace Unifesspa.UniPlus.Regras.UnitTests.Formularios;

using System.Globalization;
using System.Text.Json;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Errors;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Regras.ValueObjects;

public sealed class FaixaDeDataTests
{
    private static readonly DateOnly DiaDoEnvio = new(2026, 10, 10);

    [Theory(DisplayName = "A data fica entre os limites fixos, inclusive")]
    [InlineData("1999-12-31", Ternario.Falso)]
    [InlineData("2000-01-01", Ternario.Verdadeiro)]
    [InlineData("2010-12-31", Ternario.Verdadeiro)]
    [InlineData("2011-01-01", Ternario.Falso)]
    public void Avaliar_LimitesFixos_IncluemAsPontas(string resposta, Ternario esperado)
    {
        FaixaDeData faixa = new(LimiteDeData.Fixo(new(2000, 1, 1)), LimiteDeData.Fixo(new(2010, 12, 31)));

        faixa.Avaliar(Data(resposta), Contexto(DiaDoEnvio)).Should().Be(esperado);
    }

    [Theory(DisplayName = "O limite contado do envio admite o próprio dia do envio e recusa o seguinte")]
    [InlineData("2026-10-10", Ternario.Verdadeiro)]
    [InlineData("2026-10-11", Ternario.Falso)]
    [InlineData("5244-05-04", Ternario.Falso)]
    public void Avaliar_AteOEnvio_RecusaDataFutura(string resposta, Ternario esperado)
    {
        FaixaDeData faixa = new(minima: null, LimiteDeData.NoEnvio(anos: 0, dias: 0));

        faixa.Avaliar(Data(resposta), Contexto(DiaDoEnvio)).Should().Be(esperado);
    }

    [Theory(DisplayName = "Sem envio, o limite contado dele fica indeterminado, mas o limite fixo violado viola")]
    [InlineData("2005-06-15", Ternario.Indeterminado)]
    [InlineData("1999-12-31", Ternario.Falso)]
    public void Avaliar_SemEnvio_SoOLimiteFixoDecide(string resposta, Ternario esperado)
    {
        FaixaDeData faixa = new(LimiteDeData.Fixo(new(2000, 1, 1)), LimiteDeData.NoEnvio(anos: 0, dias: 0));

        faixa.Avaliar(Data(resposta), Contexto(diaDoEnvio: null)).Should().Be(esperado);
    }

    [Theory(DisplayName = "O deslocamento soma os anos e depois os dias, e o 29 de fevereiro sem ano bissexto vira 28")]
    [InlineData(2026, 10, 10, -120, 0, 1906, 10, 10)]
    [InlineData(2026, 10, 10, -1, -10, 2025, 9, 30)]
    [InlineData(2028, 2, 29, -1, 0, 2027, 2, 28)]
    [InlineData(2028, 2, 29, -1, 1, 2027, 3, 1)]
    [InlineData(2028, 2, 29, -1, -1, 2027, 2, 27)]
    public void No_DeslocaOEnvio(int ano, int mes, int dia, int anos, int dias, int anoEsperado, int mesEsperado, int diaEsperado)
    {
        LimiteDeData.NoEnvio(anos, dias).No(new DateOnly(ano, mes, dia)).Should().Be(new DateOnly(anoEsperado, mesEsperado, diaEsperado));
    }

    [Fact(DisplayName = "Resposta que não é um dia do calendário viola a faixa")]
    public void Avaliar_RespostaForaDoFormato_Viola()
    {
        FaixaDeData faixa = new(minima: null, LimiteDeData.NoEnvio(anos: 0, dias: 0));

        faixa.Avaliar(JsonSerializer.SerializeToElement("10/01/2027"), Contexto(DiaDoEnvio)).Should().Be(Ternario.Falso);
    }

    [Theory(DisplayName = "A data mínima que cai depois da máxima, seja qual for o envio, é recusada")]
    [InlineData("DATA_FIXA", "2011-01-01", null, null, "DATA_FIXA", "2010-12-31", null, null)]
    [InlineData("ENVIO", null, 0, 1, "ENVIO", null, 0, 0)]
    [InlineData("ENVIO", null, -10, 0, "ENVIO", null, -20, 0)]
    [InlineData("ENVIO", null, 1, 1, "ENVIO", null, 0, 0)]
    public void FaixaDeData_MinimaDepoisDaMaxima_Recusa(
        string ancoraMinima, string? dataMinima, int? anosMinima, int? diasMinima,
        string ancoraMaxima, string? dataMaxima, int? anosMaxima, int? diasMaxima)
    {
        Result<RestricaoValor> faixa = RestricoesDeValor.FaixaDeData(
            Limite(ancoraMinima, dataMinima, anosMinima, diasMinima), Limite(ancoraMaxima, dataMaxima, anosMaxima, diasMaxima));

        faixa.Error!.Code.Should().Be(RestricaoValorErrorCodes.LimitesIncoerentes);
    }

    [Theory(DisplayName = "Limites cuja ordem depende do dia do envio, ou em que a mínima vem antes, não são recusados na gravação")]
    [InlineData("DATA_FIXA", "2000-01-01", null, null, "ENVIO", null, -30, 0)]
    [InlineData("ENVIO", null, 1, -400, "ENVIO", null, 0, 0)]
    [InlineData("ENVIO", null, -1, -1, "ENVIO", null, 0, 0)]
    public void FaixaDeData_OrdemQueDependeDoEnvio_Aceita(
        string ancoraMinima, string? dataMinima, int? anosMinima, int? diasMinima,
        string ancoraMaxima, string? dataMaxima, int? anosMaxima, int? diasMaxima)
    {
        Result<RestricaoValor> faixa = RestricoesDeValor.FaixaDeData(
            Limite(ancoraMinima, dataMinima, anosMinima, diasMinima), Limite(ancoraMaxima, dataMaxima, anosMaxima, diasMaxima));

        faixa.IsSuccess.Should().BeTrue();
    }

    [Fact(DisplayName = "A faixa de data sem nenhum limite é recusada")]
    public void FaixaDeData_SemLimite_Recusa()
    {
        RestricoesDeValor.FaixaDeData(null, null).Error!.Code.Should().Be(RestricaoValorErrorCodes.LimitesIncoerentes);
    }

    [Theory(DisplayName = "O limite que não forma data fixa nem envio deslocado é recusado")]
    [InlineData("DATA_FIXA", null, null, null)]
    [InlineData("DATA_FIXA", "2000-01-01", 0, null)]
    [InlineData("DATA_FIXA", "0001-01-01", null, null)]
    [InlineData("ENVIO", "2000-01-01", null, null)]
    [InlineData("ENVIO", null, 151, 0)]
    [InlineData("ENVIO", null, 0, -54901)]
    [InlineData("ENVIO", null, int.MinValue, 0)]
    [InlineData("ENVIO", null, 0, int.MinValue)]
    [InlineData("HOJE", null, null, null)]
    public void Limite_FormaInvalida_Recusa(string ancora, string? data, int? anos, int? dias)
    {
        RestricoesDeValor.Limite(ancora, Dia(data), anos, dias).Error!.Code.Should().Be(RestricaoValorErrorCodes.LimiteDeDataInvalido);
    }

    [Theory(DisplayName = "O dia do envio é o do calendário no fuso, dos dois lados da meia-noite")]
    [InlineData("2026-10-11T02:59:59Z", 2026, 10, 10)]
    [InlineData("2026-10-11T03:00:00Z", 2026, 10, 11)]
    public void Dia_ContaNoFuso(string instante, int ano, int mes, int dia)
    {
        EnvioDasRespostas envio = new(
            DateTimeOffset.Parse(instante, CultureInfo.InvariantCulture), TimeZoneInfo.FindSystemTimeZoneById("America/Belem"));

        envio.Dia.Should().Be(new DateOnly(ano, mes, dia));
    }

    [Fact(DisplayName = "A faixa de data só cabe no campo de data")]
    public void Forma_FaixaDeDataForaDoCampoDeData_Recusa()
    {
        RestricaoValor[] faixa = [new FaixaDeData(minima: null, LimiteDeData.NoEnvio(anos: 0, dias: 0))];

        FormaDoItem.Conferir("DATA_NASCIMENTO", 1, "Data de nascimento", TipoRenderizacao.Numero, null, null, [], Obrigatoriedade.Sempre, faixa)
            .Select(static e => e.Error.Code).Should().Equal(ItemFormularioErrorCodes.RestricaoIncoerente);
        FormaDoItem.Conferir("DATA_NASCIMENTO", 1, "Data de nascimento", TipoRenderizacao.Data, null, null, [], Obrigatoriedade.Sempre, faixa)
            .Should().BeEmpty();
    }

    private static LimiteDeData Limite(string ancora, string? data, int? anos, int? dias) =>
        RestricoesDeValor.Limite(ancora, Dia(data), anos, dias).Value!;

    private static DateOnly? Dia(string? data) => data is null ? null : DateOnly.Parse(data, CultureInfo.InvariantCulture);

    private static JsonElement Data(string dia) => JsonSerializer.SerializeToElement(dia);

    private static ContextoDaResposta Contexto(DateOnly? diaDoEnvio) =>
        new(new Dictionary<string, FatoResolvido>(StringComparer.Ordinal), diaDoEnvio);
}

/// <summary>
/// A faixa de data no avaliador: o envio da entrada é o dia de que os limites se contam, no item e no
/// subitem de grupo, e o portável a carrega pelo fio sem perda.
/// </summary>
public class FaixaDeDataNaAvaliacaoTests : TestesDeAvaliacao
{
    private const string Etapa = "DADOS";

    private static readonly EnvioDasRespostas Envio = new(
        new DateTimeOffset(2026, 10, 10, 15, 0, 0, TimeSpan.Zero), TimeZoneInfo.FindSystemTimeZoneById("America/Belem"));

    private static readonly FaixaDeData AteOEnvio = new(LimiteDeData.NoEnvio(anos: -120, dias: 0), LimiteDeData.NoEnvio(anos: 0, dias: 0));

    [Theory(DisplayName = "A data depois do dia do envio viola a faixa do item, e o fato fica pendente")]
    [InlineData("2026-10-10", false)]
    [InlineData("2026-10-11", true)]
    public void Avaliar_ItemComFaixa_RecusaDataFutura(string nascimento, bool viola)
    {
        DefinicaoFormulario formulario = new(
            [new DefinicaoEtapa(Etapa, exibicao: null, [new DefinicaoItem("DATA_NASCIMENTO", null, Obrigatoriedade.Sempre, [AteOEnvio])])],
            termos: [],
            derivacoes: []);

        AvaliacaoFormulario avaliacao = AvaliarDefinicao(formulario, new EntradaAvaliacaoFormulario(
            new Dictionary<string, JsonElement>(StringComparer.Ordinal) { ["DATA_NASCIMENTO"] = JsonSerializer.SerializeToElement(nascimento) },
            new HashSet<string>([Etapa], StringComparer.Ordinal),
            new Dictionary<string, FatoResolvido>(StringComparer.Ordinal),
            Envio));

        avaliacao.Itens.Single().RestricoesVioladas.Should().HaveCount(viola ? 1 : 0);
        avaliacao.Fatos["DATA_NASCIMENTO"].Estado.Should().Be(viola ? EstadoFato.Indeterminado : EstadoFato.Resolvido);
    }

    [Fact(DisplayName = "A faixa de data vale no subitem de cada ocorrência do grupo")]
    public void Avaliar_SubitemComFaixa_RecusaNaOcorrencia()
    {
        DefinicaoFormulario formulario = new(
            [
                new DefinicaoEtapa(Etapa, exibicao: null, [],
                    [new DefinicaoGrupo("FAMILIA", null, Obrigatoriedade.Nunca, 0, null,
                        [new DefinicaoItem("NASCIMENTO_MEMBRO", null, Obrigatoriedade.Sempre, [AteOEnvio])])]),
            ],
            termos: [],
            derivacoes: []);

        AvaliacaoFormulario avaliacao = AvaliarDefinicao(formulario, new EntradaAvaliacaoFormulario(
            new Dictionary<string, JsonElement>(StringComparer.Ordinal),
            new HashSet<string>([Etapa], StringComparer.Ordinal),
            new Dictionary<string, FatoResolvido>(StringComparer.Ordinal),
            Envio,
            new Dictionary<string, IReadOnlyList<OcorrenciaRespondida>>(StringComparer.Ordinal)
            {
                ["FAMILIA"] =
                [
                    Ocorrencia("M1", "2000-05-01"),
                    Ocorrencia("M2", "2030-05-01"),
                ],
            }));

        AvaliacaoGrupo grupo = avaliacao.Grupos.Single();
        grupo.Ocorrencias.Single(static o => o.Id == "M1").Itens.Single().RestricoesVioladas.Should().BeEmpty();
        grupo.Ocorrencias.Single(static o => o.Id == "M2").Itens.Single().RestricoesVioladas.Should().ContainSingle()
            .Which.Should().BeOfType<FaixaDeData>();
    }

    private static OcorrenciaRespondida Ocorrencia(string id, string nascimento) =>
        new(id, new Dictionary<string, JsonElement>(StringComparer.Ordinal) { ["NASCIMENTO_MEMBRO"] = JsonSerializer.SerializeToElement(nascimento) });
}
