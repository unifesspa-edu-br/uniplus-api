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

    [Fact(DisplayName = "Sem DATA_NASCIMENTO nem ENDERECO_RESIDENCIAL, não deriva nada e não chama o Geo")]
    public async Task CalcularAsync_SemRespostaNenhuma_NaoDerivaNada()
    {
        IConferenciaEnderecoGeo conferencia = Substitute.For<IConferenciaEnderecoGeo>();

        Result<IReadOnlyDictionary<string, JsonElement>> resultado = await DerivadosDoSistemaInscricao.CalcularAsync(
            Respostas(), new DateOnly(2026, 12, 10), conferencia);

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        resultado.Value!.Should().BeEmpty();
        await conferencia.DidNotReceive().ConferirCepAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Theory(DisplayName = "FAIXA_ETARIA é a idade completa na data de referência, não na de hoje")]
    [InlineData("2008-12-11", 17)] // aniversário de 18 é um dia DEPOIS da referência
    [InlineData("2008-12-10", 18)] // aniversário de 18 é NA referência
    [InlineData("2008-12-09", 18)] // aniversário de 18 foi um dia ANTES da referência
    public async Task CalcularAsync_ComDataNascimento_CalculaFaixaEtariaNaDataDeReferencia(string nascimento, int idadeEsperada)
    {
        IConferenciaEnderecoGeo conferencia = Substitute.For<IConferenciaEnderecoGeo>();

        Result<IReadOnlyDictionary<string, JsonElement>> resultado = await DerivadosDoSistemaInscricao.CalcularAsync(
            Respostas(("DATA_NASCIMENTO", Texto(nascimento))), new DateOnly(2026, 12, 10), conferencia);

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        resultado.Value!["FAIXA_ETARIA"].GetInt32().Should().Be(idadeEsperada);
    }

    [Fact(DisplayName = "Sem data de referência no processo, a data de nascimento não vira faixa etária nem recusa, e nunca se calcula contra hoje")]
    public async Task CalcularAsync_DataNascimentoSemReferenciaDoProcesso_NaoApuraFaixaEtaria()
    {
        IConferenciaEnderecoGeo conferencia = Substitute.For<IConferenciaEnderecoGeo>();

        Result<IReadOnlyDictionary<string, JsonElement>> resultado = await DerivadosDoSistemaInscricao.CalcularAsync(
            Respostas(("DATA_NASCIMENTO", Texto("2008-12-10"))), dataReferenciaFatos: null, conferencia);

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        resultado.Value!.Should().NotContainKey("FAIXA_ETARIA");
    }

    [Fact(DisplayName = "Data de nascimento que não é uma data válida recusa")]
    public async Task CalcularAsync_DataNascimentoInvalida_Recusa()
    {
        IConferenciaEnderecoGeo conferencia = Substitute.For<IConferenciaEnderecoGeo>();

        Result<IReadOnlyDictionary<string, JsonElement>> resultado = await DerivadosDoSistemaInscricao.CalcularAsync(
            Respostas(("DATA_NASCIMENTO", Texto("não é uma data"))), new DateOnly(2026, 12, 10), conferencia);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("Inscricao.DataNascimentoInvalida");
    }

    [Fact(DisplayName = "Endereço confirmado pelo Geo deriva UF e município de residência do que o Geo confirmou")]
    public async Task CalcularAsync_EnderecoConfirmadoPeloGeo_DerivaUfEMunicipio()
    {
        IConferenciaEnderecoGeo conferencia = Substitute.For<IConferenciaEnderecoGeo>();
        conferencia.ConferirCepAsync("68500000", "1504208", "PA", Arg.Any<CancellationToken>()).Returns(Result.Success());

        Result<IReadOnlyDictionary<string, JsonElement>> resultado = await DerivadosDoSistemaInscricao.CalcularAsync(
            Respostas(("ENDERECO_RESIDENCIAL", Endereco("68500000", "1504208", "PA"))), new DateOnly(2026, 12, 10), conferencia);

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        resultado.Value!["UF_RESIDENCIA"].GetString().Should().Be("PA");
        resultado.Value!["MUNICIPIO_RESIDENCIA"].GetString().Should().Be("1504208");
    }

    [Fact(DisplayName = "Geo indisponível ou endereço divergente recusa a derivação, nunca aceita o que o candidato declarou")]
    public async Task CalcularAsync_GeoRecusaOEndereco_Recusa()
    {
        IConferenciaEnderecoGeo conferencia = Substitute.For<IConferenciaEnderecoGeo>();
        conferencia.ConferirCepAsync("68500000", "1504208", "PA", Arg.Any<CancellationToken>())
            .Returns(Result.Failure(new DomainError("ConferenciaEnderecoGeo.CidadeDivergente", "O CEP informado resolve para uma cidade diferente da declarada.")));

        Result<IReadOnlyDictionary<string, JsonElement>> resultado = await DerivadosDoSistemaInscricao.CalcularAsync(
            Respostas(("ENDERECO_RESIDENCIAL", Endereco("68500000", "1504208", "PA"))), new DateOnly(2026, 12, 10), conferencia);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("ConferenciaEnderecoGeo.CidadeDivergente");
    }

    [Fact(DisplayName = "Endereço sem CEP, com a cidade escolhida manualmente, confere pelo município — nunca recusa por falta de CEP")]
    public async Task CalcularAsync_EnderecoSemCepComCidade_ConfereProMunicipioEDeriva()
    {
        IConferenciaEnderecoGeo conferencia = Substitute.For<IConferenciaEnderecoGeo>();
        conferencia.ConferirMunicipioAsync("1504208", "PA", Arg.Any<CancellationToken>()).Returns(Result.Success());
        JsonElement enderecoSemCep = JsonSerializer.SerializeToElement(new { cep = (string?)null, cidade = new { codigoIbge = "1504208", uf = "PA" } });

        Result<IReadOnlyDictionary<string, JsonElement>> resultado = await DerivadosDoSistemaInscricao.CalcularAsync(
            Respostas(("ENDERECO_RESIDENCIAL", enderecoSemCep)), new DateOnly(2026, 12, 10), conferencia);

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        resultado.Value!["UF_RESIDENCIA"].GetString().Should().Be("PA");
        resultado.Value!["MUNICIPIO_RESIDENCIA"].GetString().Should().Be("1504208");
        await conferencia.DidNotReceive().ConferirCepAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Endereço sem cidade nenhuma recusa sem nem chamar o Geo")]
    public async Task CalcularAsync_EnderecoSemCidade_RecusaSemChamarOGeo()
    {
        IConferenciaEnderecoGeo conferencia = Substitute.For<IConferenciaEnderecoGeo>();
        JsonElement enderecoSemCidade = JsonSerializer.SerializeToElement(new { cep = "68500000" });

        Result<IReadOnlyDictionary<string, JsonElement>> resultado = await DerivadosDoSistemaInscricao.CalcularAsync(
            Respostas(("ENDERECO_RESIDENCIAL", enderecoSemCidade)), new DateOnly(2026, 12, 10), conferencia);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("Inscricao.EnderecoResidencialInvalido");
        await conferencia.DidNotReceive().ConferirCepAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
        await conferencia.DidNotReceive().ConferirMunicipioAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Fora do envio, o endereço que o Geo recusa deixa a residência indeterminada, sem recusar")]
    public async Task ApurarAsync_GeoRecusaOEndereco_FicaIndeterminado()
    {
        IConferenciaEnderecoGeo conferencia = Substitute.For<IConferenciaEnderecoGeo>();
        conferencia.ConferirCepAsync("68500000", "1504208", "PA", Arg.Any<CancellationToken>())
            .Returns(Result.Failure(new DomainError("ConferenciaEnderecoGeo.GeoIndisponivel", "O Geo não respondeu.")));

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
