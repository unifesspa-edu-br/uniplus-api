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
    [InlineData("2026-10-10", -120, 0, "1906-10-10")]
    [InlineData("2026-10-10", -1, -10, "2025-09-30")]
    [InlineData("2028-02-29", -1, 0, "2027-02-28")]
    [InlineData("2028-02-29", -1, 1, "2027-03-01")]
    [InlineData("2028-02-29", -1, -1, "2027-02-27")]
    public void No_DeslocaOEnvio(string envio, int anos, int dias, string esperado)
    {
        LimiteDeData.NoEnvio(anos, dias).No(Dia(envio)!.Value).Should().Be(Dia(esperado));
    }

    [Fact(DisplayName = "Resposta que não é um dia do calendário viola a faixa")]
    public void Avaliar_RespostaForaDoFormato_Viola()
    {
        FaixaDeData faixa = new(minima: null, LimiteDeData.NoEnvio(anos: 0, dias: 0));

        faixa.Avaliar(JsonSerializer.SerializeToElement("10/01/2027"), Contexto(DiaDoEnvio)).Should().Be(Ternario.Falso);
    }

    [Theory(DisplayName = "A data mínima que cai depois da máxima, seja qual for o envio, é recusada")]
    [InlineData("DATA_FIXA 2011-01-01", "DATA_FIXA 2010-12-31")]
    [InlineData("ENVIO 0 1", "ENVIO 0 0")]
    [InlineData("ENVIO -10 0", "ENVIO -20 0")]
    [InlineData("ENVIO 1 1", "ENVIO 0 0")]
    public void FaixaDeData_MinimaDepoisDaMaxima_Recusa(string minima, string maxima)
    {
        Result<RestricaoValor> faixa = RestricoesDeValor.FaixaDeData(Limite(minima), Limite(maxima));

        faixa.Error!.Code.Should().Be(RestricaoValorErrorCodes.LimitesIncoerentes);
    }

    [Theory(DisplayName = "Limites cuja ordem depende do dia do envio, ou em que a mínima vem antes, não são recusados na gravação")]
    [InlineData("DATA_FIXA 2000-01-01", "ENVIO -30 0")]
    [InlineData("ENVIO 1 -400", "ENVIO 0 0")]
    [InlineData("ENVIO -1 -1", "ENVIO 0 0")]
    public void FaixaDeData_OrdemQueDependeDoEnvio_Aceita(string minima, string maxima)
    {
        Result<RestricaoValor> faixa = RestricoesDeValor.FaixaDeData(Limite(minima), Limite(maxima));

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

        ConferirCampoDeData(TipoDeCampo.Numero, faixa).Select(static e => e.Error.Code).Should().Equal(ItemFormularioErrorCodes.RestricaoIncoerente);
        ConferirCampoDeData(TipoDeCampo.Data, faixa).Should().BeEmpty();
    }

    private static List<FieldError> ConferirCampoDeData(TipoDeCampo tipo, RestricaoValor[] restricoes) =>
        FormaDoItem.Conferir(
            "DATA_NASCIMENTO",
            1,
            new ApresentacaoDoCampo("Data de nascimento", tipo),
            formato: null,
            new RegrasDoCampo(exibicao: null, Obrigatoriedade.Sempre, restricoes));

    /// <summary>O limite descrito pelo tipo e pelo dado dele: a data, para o limite fixo, ou os anos e os dias, para o contado do envio.</summary>
    private static LimiteDeData Limite(string descricao)
    {
        ArgumentNullException.ThrowIfNull(descricao);
        string[] partes = descricao.Split(' ');
        return partes[0] == "ENVIO"
            ? RestricoesDeValor.Limite(partes[0], null, int.Parse(partes[1], CultureInfo.InvariantCulture), int.Parse(partes[2], CultureInfo.InvariantCulture)).Value!
            : RestricoesDeValor.Limite(partes[0], Dia(partes[1]), null, null).Value!;
    }

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
    private const string Pagina = "DADOS";

    private static readonly EnvioDasRespostas Envio = new(
        new DateTimeOffset(2026, 10, 10, 15, 0, 0, TimeSpan.Zero), TimeZoneInfo.FindSystemTimeZoneById("America/Belem"));

    private static readonly FaixaDeData AteOEnvio = new(LimiteDeData.NoEnvio(anos: -120, dias: 0), LimiteDeData.NoEnvio(anos: 0, dias: 0));

    [Theory(DisplayName = "A data depois do dia do envio viola a faixa do item, e o fato fica pendente")]
    [InlineData("2026-10-10", false)]
    [InlineData("2026-10-11", true)]
    public void Avaliar_ItemComFaixa_RecusaDataFutura(string nascimento, bool viola)
    {
        DefinicaoFormulario formulario = new(
            [new DefinicaoPagina(Pagina, exibicao: null, [new DefinicaoItem("DATA_NASCIMENTO", null, Obrigatoriedade.Sempre, [AteOEnvio])])],
            termos: [],
            derivacoes: []);

        AvaliacaoFormulario avaliacao = AvaliarDefinicao(formulario, new EntradaAvaliacaoFormulario(
            new Dictionary<string, JsonElement>(StringComparer.Ordinal) { ["DATA_NASCIMENTO"] = JsonSerializer.SerializeToElement(nascimento) },
            new HashSet<string>([Pagina], StringComparer.Ordinal),
            new Dictionary<string, FatoResolvido>(StringComparer.Ordinal),
            Envio));

        avaliacao.Itens.Single().RestricoesVioladas.Should().HaveCount(viola ? 1 : 0);
        avaliacao.Fatos["DATA_NASCIMENTO"].Estado.Should().Be(viola ? EstadoFato.Indeterminado : EstadoFato.Resolvido);
    }

    [Fact(DisplayName = "A faixa de data vale no subitem de cada ocorrência do grupo")]
    public void Avaliar_SubitemComFaixa_RecusaNaOcorrencia()
    {
        AvaliacaoFormulario avaliacao = AvaliarDefinicao(FormularioComGrupoDeNascimentos(), EntradaComDuasOcorrencias());

        AvaliacaoGrupo grupo = avaliacao.Grupos.Single();
        grupo.Ocorrencias.Single(static o => o.Id == "M1").Itens.Single().RestricoesVioladas.Should().BeEmpty();
        grupo.Ocorrencias.Single(static o => o.Id == "M2").Itens.Single().RestricoesVioladas.Should().ContainSingle()
            .Which.Should().BeOfType<FaixaDeData>();
    }

    private static DefinicaoFormulario FormularioComGrupoDeNascimentos() =>
        new(
            [
                new DefinicaoPagina(Pagina, exibicao: null, [],
                    [new DefinicaoGrupo("FAMILIA", null, Obrigatoriedade.Nunca, 0, null,
                        [new DefinicaoItem("NASCIMENTO_MEMBRO", null, Obrigatoriedade.Sempre, [AteOEnvio])], incluiCandidato: false)]),
            ],
            termos: [],
            derivacoes: []);

    private static EntradaAvaliacaoFormulario EntradaComDuasOcorrencias() =>
        new(
            new Dictionary<string, JsonElement>(StringComparer.Ordinal),
            new HashSet<string>([Pagina], StringComparer.Ordinal),
            new Dictionary<string, FatoResolvido>(StringComparer.Ordinal),
            Envio,
            new Dictionary<string, IReadOnlyList<OcorrenciaRespondida>>(StringComparer.Ordinal)
            {
                ["FAMILIA"] =
                [
                    Ocorrencia("M1", "2000-05-01"),
                    Ocorrencia("M2", "2030-05-01"),
                ],
            });

    private static OcorrenciaRespondida Ocorrencia(string id, string nascimento) =>
        new(id, new Dictionary<string, JsonElement>(StringComparer.Ordinal) { ["NASCIMENTO_MEMBRO"] = JsonSerializer.SerializeToElement(nascimento) });
}
