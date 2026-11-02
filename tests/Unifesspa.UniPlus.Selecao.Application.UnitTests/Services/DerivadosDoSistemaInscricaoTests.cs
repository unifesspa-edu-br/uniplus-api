namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Services;

using System.Text.Json;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.ValueObjects;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Application.Services;

/// <summary>
/// Os derivados do sistema (issue #1859, ADR-0136) — <c>FAIXA_ETARIA</c> é calculada na data de
/// referência CONGELADA, nunca na de hoje; <c>UF_RESIDENCIA</c>/<c>MUNICIPIO_RESIDENCIA</c> só
/// entram depois de o Geo confirmar o endereço, nunca do que o candidato declarou sozinho.
/// </summary>
public sealed class DerivadosDoSistemaInscricaoTests
{
    private static Dictionary<string, JsonElement> Respostas(params (string Codigo, JsonElement Valor)[] itens) =>
        new(itens.Select(i => new KeyValuePair<string, JsonElement>(i.Codigo, i.Valor)), StringComparer.Ordinal);

    private static JsonElement Texto(string valor) => JsonSerializer.SerializeToElement(valor);

    private static JsonElement Endereco(string cep, string codigoIbge, string uf) =>
        JsonSerializer.SerializeToElement(new
        {
            cep,
            logradouro = "Rua Teste",
            numero = "123",
            complemento = (string?)null,
            bairro = "Centro",
            distrito = (string?)null,
            cidade = new { codigoIbge, nome = "Marabá", uf },
            latitude = (string?)null,
            longitude = (string?)null,
            nivelResolucao = "logradouro",
            origem = "geo-api",
        });

    private static ConferenciaDosEnderecos SemEnderecoConferido(DomainError? geo = null) =>
        new([], new Dictionary<string, CepResolvido>(StringComparer.Ordinal), geo);

    private static ConferenciaDosEnderecos ComResidenciaResolvida(string codigoIbge, string cidade, string uf) =>
        new([], new Dictionary<string, CepResolvido>(StringComparer.Ordinal) { ["ENDERECO_RESIDENCIAL"] = new(codigoIbge, cidade, uf, "logradouro") }, null);

    [Fact(DisplayName = "Sem DATA_NASCIMENTO nem ENDERECO_RESIDENCIAL, não deriva nada")]
    public void Calcular_SemRespostaNenhuma_NaoDerivaNada()
    {
        DerivadosCalculados resultado = DerivadosDoSistemaInscricao.Calcular(Respostas(), new DateOnly(2026, 12, 10), SemEnderecoConferido());

        resultado.Valores.Should().BeEmpty();
        resultado.Indeterminados.Should().BeEmpty();
    }

    [Theory(DisplayName = "FAIXA_ETARIA é a idade completa na data de referência, não na de hoje")]
    [InlineData("2008-12-11", 17)] // aniversário de 18 é um dia DEPOIS da referência
    [InlineData("2008-12-10", 18)] // aniversário de 18 é NA referência
    [InlineData("2008-12-09", 18)] // aniversário de 18 foi um dia ANTES da referência
    public void Calcular_ComDataNascimento_CalculaFaixaEtariaNaDataDeReferencia(string nascimento, int idadeEsperada)
    {
        DerivadosCalculados resultado = DerivadosDoSistemaInscricao.Calcular(
            Respostas(("DATA_NASCIMENTO", Texto(nascimento))), new DateOnly(2026, 12, 10), SemEnderecoConferido());

        resultado.Valores["FAIXA_ETARIA"].GetInt32().Should().Be(idadeEsperada);
    }

    [Fact(DisplayName = "Sem data de referência no processo ou com data de nascimento que não é data, não há faixa etária, e nunca se calcula contra hoje")]
    public void Calcular_SemReferenciaOuComDataInvalida_NaoApuraFaixaEtaria()
    {
        DerivadosDoSistemaInscricao.Calcular(Respostas(("DATA_NASCIMENTO", Texto("2008-12-10"))), dataReferenciaFatos: null, SemEnderecoConferido())
            .Valores.Should().NotContainKey("FAIXA_ETARIA");
        DerivadosDoSistemaInscricao.Calcular(Respostas(("DATA_NASCIMENTO", Texto("não é uma data"))), new DateOnly(2026, 12, 10), SemEnderecoConferido())
            .Valores.Should().NotContainKey("FAIXA_ETARIA");
    }

    [Fact(DisplayName = "A residência sai do que o Geo resolveu pelo CEP na conferência do endereço, com o nome da cidade, e não do que o candidato declarou")]
    public void Calcular_EnderecoResolvidoPeloGeo_DerivaDoQueOGeoResolveu()
    {
        DerivadosCalculados resultado = DerivadosDoSistemaInscricao.Calcular(
            Respostas(("ENDERECO_RESIDENCIAL", Endereco("40020000", "1504208", "PA"))), new DateOnly(2026, 12, 10),
            ComResidenciaResolvida("2927408", "Salvador", "BA"));

        resultado.Valores["UF_RESIDENCIA"].GetString().Should().Be("BA");
        resultado.Valores["MUNICIPIO_RESIDENCIA"].GetString().Should().Be("2927408");
        resultado.Nomes["MUNICIPIO_RESIDENCIA"].Should().Be("Salvador");
        resultado.Indeterminados.Should().BeEmpty();
    }

    [Fact(DisplayName = "Endereço que a conferência não resolveu deixa a residência indeterminada, sem Geo próprio: a recusa é da conferência do endereço")]
    public void Calcular_EnderecoNaoResolvido_DeixaIndeterminado()
    {
        DerivadosCalculados resultado = DerivadosDoSistemaInscricao.Calcular(
            Respostas(("ENDERECO_RESIDENCIAL", Endereco("68500000", "1504208", "PA"))), new DateOnly(2026, 12, 10), SemEnderecoConferido());

        resultado.Valores.Should().NotContainKey("UF_RESIDENCIA").And.NotContainKey("MUNICIPIO_RESIDENCIA");
        resultado.Indeterminados.Should().BeEquivalentTo(["UF_RESIDENCIA", "MUNICIPIO_RESIDENCIA"]);
        resultado.GeoIndisponivel.Should().BeNull();
    }

    [Fact(DisplayName = "Geo fora do ar na conferência do endereço deixa a residência indeterminada e diz que foi o Geo")]
    public void Calcular_GeoForaDoAr_DizQueFoiOGeo()
    {
        DomainError fora = new(ConferenciaEnderecoGeoErrorCodes.GeoIndisponivel, "O Geo não respondeu.");

        DerivadosCalculados resultado = DerivadosDoSistemaInscricao.Calcular(
            Respostas(("ENDERECO_RESIDENCIAL", Endereco("68500000", "1504208", "PA"))), new DateOnly(2026, 12, 10), SemEnderecoConferido(fora));

        resultado.Indeterminados.Should().BeEquivalentTo(["UF_RESIDENCIA", "MUNICIPIO_RESIDENCIA"]);
        resultado.GeoIndisponivel.Should().Be(fora);
    }

    [Fact(DisplayName = "A residência da inscrição enviada vem dos derivados gravados no envio, sem Geo; o que não foi gravado fica indeterminado e a modalidade não entra")]
    public void ResidenciaGravada_VemDoEnvio()
    {
        IReadOnlyDictionary<string, FatoResolvido> fatos = DerivadosDoSistemaInscricao.ResidenciaGravada(
            Respostas(("UF_RESIDENCIA", Texto("BA")), ("MODALIDADE", Texto("AC")), ("FAIXA_ETARIA", JsonSerializer.SerializeToElement(26))),
            new HashSet<string>(["UF_RESIDENCIA", "MUNICIPIO_RESIDENCIA", "MODALIDADE", "FAIXA_ETARIA"], StringComparer.Ordinal));

        fatos.Keys.Should().BeEquivalentTo(["UF_RESIDENCIA", "MUNICIPIO_RESIDENCIA"]);
        fatos["UF_RESIDENCIA"].Valor!.Value.GetString().Should().Be("BA");
        fatos["MUNICIPIO_RESIDENCIA"].Estado.Should().Be(EstadoFato.Indeterminado);
    }

    [Fact(DisplayName = "Fora do envio, o endereço que o Geo recusa deixa a residência indeterminada, sem recusar")]
    public async Task ApurarAsync_GeoRecusaOEndereco_FicaIndeterminado()
    {
        IConferenciaEnderecoGeo conferencia = Substitute.For<IConferenciaEnderecoGeo>();
        conferencia.ConferirCepAsync("68500000", "1504208", "PA", Arg.Any<CancellationToken>())
            .Returns(Result<string>.Failure(new DomainError("ConferenciaEnderecoGeo.GeoIndisponivel", "O Geo não respondeu.")));

        IReadOnlyDictionary<string, FatoResolvido> apurados = await DerivadosDoSistemaInscricao.ApurarAsync(
            Respostas(("ENDERECO_RESIDENCIAL", Endereco("68500000", "1504208", "PA"))), new DateOnly(2026, 12, 10), conferencia,
            new HashSet<string>(["UF_RESIDENCIA"], StringComparer.Ordinal));

        apurados["UF_RESIDENCIA"].Estado.Should().Be(EstadoFato.Indeterminado);
        apurados["MUNICIPIO_RESIDENCIA"].Estado.Should().Be(EstadoFato.Indeterminado);
    }

    [Fact(DisplayName = "Fora do envio, só se apura o derivado de que a avaliação depende: sem pedido, o Geo não é consultado")]
    public async Task ApurarAsync_SemPedido_NaoConsultaOGeo()
    {
        IConferenciaEnderecoGeo conferencia = Substitute.For<IConferenciaEnderecoGeo>();

        IReadOnlyDictionary<string, FatoResolvido> apurados = await DerivadosDoSistemaInscricao.ApurarAsync(
            Respostas(("ENDERECO_RESIDENCIAL", Endereco("68500000", "1504208", "PA")), ("DATA_NASCIMENTO", Texto("2008-12-10"))),
            new DateOnly(2026, 12, 10), conferencia, new HashSet<string>(["FAIXA_ETARIA"], StringComparer.Ordinal));

        apurados.Should().ContainKey("FAIXA_ETARIA").And.NotContainKey("UF_RESIDENCIA");
        await conferencia.DidNotReceiveWithAnyArgs().ConferirCepAsync(default!, default!, default!, default);
    }
}
