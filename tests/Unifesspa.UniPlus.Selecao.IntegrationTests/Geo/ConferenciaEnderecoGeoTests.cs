namespace Unifesspa.UniPlus.Selecao.IntegrationTests.Geo;

using System.Net;
using System.Net.Http;

using AwesomeAssertions;

using Microsoft.Extensions.Logging.Abstractions;

using NSubstitute;

using Polly.CircuitBreaker;
using Polly.Timeout;

using Refit;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Infrastructure.Geo;

/// <summary>
/// <see cref="ConferenciaEnderecoGeo"/> (issue #1865): a tradução de status HTTP e corpo em
/// <see cref="Result"/>, falha fechada em erro de rede. O cliente HTTP real (Refit + resiliência +
/// autenticação M2M) é o mesmo molde já provado pelo cliente SIGAA — aqui só a lógica de negócio
/// desta classe, com <see cref="IGeoApi"/> dublado diretamente via NSubstitute.
/// </summary>
public sealed class ConferenciaEnderecoGeoTests
{
    private static ApiResponse<T> Resposta<T>(HttpStatusCode status, T? conteudo)
    {
        HttpResponseMessage resposta = new(status) { RequestMessage = new HttpRequestMessage() };
        return new(resposta, conteudo!, new RefitSettings());
    }

    [Fact(DisplayName = "A conferência descarta a resposta do Geo, no CEP e no município")]
    public async Task Conferir_DescartaAResposta()
    {
        ConteudoRastreado conteudoDoCep = new();
        ConteudoRastreado conteudoDoMunicipio = new();
        IGeoApi geo = Substitute.For<IGeoApi>();
        geo.ResolverCepAsync("68500000", Arg.Any<CancellationToken>())
            .Returns(RespostaCom(conteudoDoCep, new CepResolvidoResponse("Marabá", "1504208", "PA")));
        geo.ObterCidadeAsync("1504208", Arg.Any<CancellationToken>())
            .Returns(RespostaCom(conteudoDoMunicipio, new CidadeDetalheResponse("1504208", "Marabá", "PA")));
        ConferenciaEnderecoGeo conferencia = new(geo, NullLogger<ConferenciaEnderecoGeo>.Instance);

        await conferencia.ConferirCepAsync("68500000", "1504208", "PA");
        await conferencia.ConferirMunicipioAsync("1504208", "PA");

        conteudoDoCep.Descartado.Should().BeTrue();
        conteudoDoMunicipio.Descartado.Should().BeTrue();
    }

    private static ApiResponse<T> RespostaCom<T>(HttpContent conteudo, T corpo)
    {
        HttpResponseMessage resposta = new(HttpStatusCode.OK) { RequestMessage = new HttpRequestMessage(), Content = conteudo };
        return new(resposta, corpo, new RefitSettings());
    }

    private sealed class ConteudoRastreado : HttpContent
    {
        public bool Descartado { get; private set; }

        protected override Task SerializeToStreamAsync(Stream stream, System.Net.TransportContext? context) => Task.CompletedTask;

        protected override bool TryComputeLength(out long length)
        {
            length = 0;
            return true;
        }

        protected override void Dispose(bool disposing)
        {
            Descartado = true;
            base.Dispose(disposing);
        }
    }

    [Fact(DisplayName = "CEP que resolve para a cidade e UF declaradas confere")]
    public async Task ConferirCep_ResolveParaDeclarado_Confere()
    {
        IGeoApi geo = Substitute.For<IGeoApi>();
        geo.ResolverCepAsync("68500000", Arg.Any<CancellationToken>())
            .Returns(Resposta(HttpStatusCode.OK, new CepResolvidoResponse("Marabá", "1504208", "PA")));

        ConferenciaEnderecoGeo conferencia = new(geo, NullLogger<ConferenciaEnderecoGeo>.Instance);
        Result resultado = await conferencia.ConferirCepAsync("68500000", "1504208", "PA");

        resultado.IsSuccess.Should().BeTrue();
    }

    [Fact(DisplayName = "CEP que resolve para OUTRA cidade é divergência, não indisponibilidade")]
    public async Task ConferirCep_ResolveParaOutraCidade_Diverge()
    {
        IGeoApi geo = Substitute.For<IGeoApi>();
        geo.ResolverCepAsync("68500000", Arg.Any<CancellationToken>())
            .Returns(Resposta(HttpStatusCode.OK, new CepResolvidoResponse("Belém", "1501402", "PA")));

        ConferenciaEnderecoGeo conferencia = new(geo, NullLogger<ConferenciaEnderecoGeo>.Instance);
        Result resultado = await conferencia.ConferirCepAsync("68500000", "1504208", "PA");

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(ConferenciaEnderecoGeoErrorCodes.CidadeDivergente);
    }

    [Fact(DisplayName = "CEP sem cobertura (404) é recusa nomeada, não indisponibilidade")]
    public async Task ConferirCep_NaoEncontrado_RecusaNomeada()
    {
        IGeoApi geo = Substitute.For<IGeoApi>();
        geo.ResolverCepAsync("00000000", Arg.Any<CancellationToken>())
            .Returns(Resposta<CepResolvidoResponse>(HttpStatusCode.NotFound, null));

        ConferenciaEnderecoGeo conferencia = new(geo, NullLogger<ConferenciaEnderecoGeo>.Instance);
        Result resultado = await conferencia.ConferirCepAsync("00000000", "1504208", "PA");

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(ConferenciaEnderecoGeoErrorCodes.CepNaoResolve);
    }

    [Fact(DisplayName = "Falha de rede na chamada ao Geo é falha fechada — erro temporário, nunca aceito")]
    public async Task ConferirCep_FalhaDeRede_FalhaFechada()
    {
        IGeoApi geo = Substitute.For<IGeoApi>();
        geo.ResolverCepAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException<ApiResponse<CepResolvidoResponse>>(new HttpRequestException("timeout")));

        ConferenciaEnderecoGeo conferencia = new(geo, NullLogger<ConferenciaEnderecoGeo>.Instance);
        Result resultado = await conferencia.ConferirCepAsync("68500000", "1504208", "PA");

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(ConferenciaEnderecoGeoErrorCodes.GeoIndisponivel);
    }

    [Fact(DisplayName = "Timeout da resiliência (Polly) na chamada ao Geo é falha fechada, não exceção não tratada")]
    public async Task ConferirCep_TimeoutDaResiliencia_FalhaFechada()
    {
        IGeoApi geo = Substitute.For<IGeoApi>();
        geo.ResolverCepAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException<ApiResponse<CepResolvidoResponse>>(new TimeoutRejectedException()));

        ConferenciaEnderecoGeo conferencia = new(geo, NullLogger<ConferenciaEnderecoGeo>.Instance);
        Result resultado = await conferencia.ConferirCepAsync("68500000", "1504208", "PA");

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(ConferenciaEnderecoGeoErrorCodes.GeoIndisponivel);
    }

    [Fact(DisplayName = "Circuito aberto (Polly) na chamada ao Geo é falha fechada, não exceção não tratada")]
    public async Task ConferirMunicipio_CircuitoAberto_FalhaFechada()
    {
        IGeoApi geo = Substitute.For<IGeoApi>();
        geo.ObterCidadeAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException<ApiResponse<CidadeDetalheResponse>>(new BrokenCircuitException()));

        ConferenciaEnderecoGeo conferencia = new(geo, NullLogger<ConferenciaEnderecoGeo>.Instance);
        Result resultado = await conferencia.ConferirMunicipioAsync("1504208", "PA");

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(ConferenciaEnderecoGeoErrorCodes.GeoIndisponivel);
    }

    [Fact(DisplayName = "CEP com sucesso (2xx) mas sem corpo utilizável é falha fechada, não exceção")]
    public async Task ConferirCep_SucessoSemCorpo_FalhaFechada()
    {
        IGeoApi geo = Substitute.For<IGeoApi>();
        geo.ResolverCepAsync("68500000", Arg.Any<CancellationToken>())
            .Returns(Resposta<CepResolvidoResponse>(HttpStatusCode.OK, null));

        ConferenciaEnderecoGeo conferencia = new(geo, NullLogger<ConferenciaEnderecoGeo>.Instance);
        Result resultado = await conferencia.ConferirCepAsync("68500000", "1504208", "PA");

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(ConferenciaEnderecoGeoErrorCodes.GeoIndisponivel);
    }

    [Fact(DisplayName = "Município com sucesso (2xx) mas sem corpo utilizável é falha fechada, não exceção")]
    public async Task ConferirMunicipio_SucessoSemCorpo_FalhaFechada()
    {
        IGeoApi geo = Substitute.For<IGeoApi>();
        geo.ObterCidadeAsync("1504208", Arg.Any<CancellationToken>())
            .Returns(Resposta<CidadeDetalheResponse>(HttpStatusCode.OK, null));

        ConferenciaEnderecoGeo conferencia = new(geo, NullLogger<ConferenciaEnderecoGeo>.Instance);
        Result resultado = await conferencia.ConferirMunicipioAsync("1504208", "PA");

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(ConferenciaEnderecoGeoErrorCodes.GeoIndisponivel);
    }

    [Fact(DisplayName = "Município cuja UF bate a declarada confere")]
    public async Task ConferirMunicipio_UfBate_Confere()
    {
        IGeoApi geo = Substitute.For<IGeoApi>();
        geo.ObterCidadeAsync("1504208", Arg.Any<CancellationToken>())
            .Returns(Resposta(HttpStatusCode.OK, new CidadeDetalheResponse("1504208", "Marabá", "PA")));

        ConferenciaEnderecoGeo conferencia = new(geo, NullLogger<ConferenciaEnderecoGeo>.Instance);
        Result resultado = await conferencia.ConferirMunicipioAsync("1504208", "PA");

        resultado.IsSuccess.Should().BeTrue();
    }

    [Fact(DisplayName = "Município cuja UF não bate a declarada é divergência")]
    public async Task ConferirMunicipio_UfDiverge_Recusa()
    {
        IGeoApi geo = Substitute.For<IGeoApi>();
        geo.ObterCidadeAsync("1504208", Arg.Any<CancellationToken>())
            .Returns(Resposta(HttpStatusCode.OK, new CidadeDetalheResponse("1504208", "Marabá", "PA")));

        ConferenciaEnderecoGeo conferencia = new(geo, NullLogger<ConferenciaEnderecoGeo>.Instance);
        Result resultado = await conferencia.ConferirMunicipioAsync("1504208", "SP");

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(ConferenciaEnderecoGeoErrorCodes.MunicipioDivergente);
    }

    [Fact(DisplayName = "Município inexistente (404) é recusa nomeada")]
    public async Task ConferirMunicipio_Inexistente_RecusaNomeada()
    {
        IGeoApi geo = Substitute.For<IGeoApi>();
        geo.ObterCidadeAsync("9999999", Arg.Any<CancellationToken>())
            .Returns(Resposta<CidadeDetalheResponse>(HttpStatusCode.NotFound, null));

        ConferenciaEnderecoGeo conferencia = new(geo, NullLogger<ConferenciaEnderecoGeo>.Instance);
        Result resultado = await conferencia.ConferirMunicipioAsync("9999999", "PA");

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(ConferenciaEnderecoGeoErrorCodes.MunicipioNaoEncontrado);
    }
}
