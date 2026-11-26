namespace Unifesspa.UniPlus.Selecao.IntegrationTests.Rascunhos;

using System.Net;
using System.Text.Json;

using AwesomeAssertions;

using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

using NSubstitute;

using Unifesspa.UniPlus.Host;
using Unifesspa.UniPlus.Infrastructure.Core.Persistence;
using Unifesspa.UniPlus.IntegrationTests.Fixtures.Hosting;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.IntegrationTests.Outbox.Cascading;
using Unifesspa.UniPlus.Testes.Compartilhado;

using static ClienteDoRascunhoDeTeste;

/// <summary>
/// Salvar e importar o rascunho confere a prova presencial: o rascunho é sempre salvo, cada problema volta como aviso no ponteiro do
/// item, o Geo fora do ar dá um só aviso, e a consulta ao Geo nunca acontece com a linha do rascunho travada.
/// </summary>
[Collection(CascadingCollection.Name)]
[Trait("Category", "OutboxCapability")]
public sealed class ProvaPresencialNoSalvamentoDoRascunhoTests(CascadingFixture fixture)
{
    private const string Betina = "betina.neves";
    private const string Maraba = "1504208";
    private const string Belem = "1501402";
    private const string DesconhecidoDoGeo = "1599999";
    private const string CodigoDoGeoIndisponivel = "uniplus.selecao.conferencia_endereco_geo.geo_indisponivel";

    private readonly ClienteDoRascunhoDeTeste _cliente = new(fixture);

    [Fact(DisplayName = "Importar o documento com Marabá declarada duas vezes cria o rascunho e aponta a segunda ocorrência")]
    public async Task Criar_MunicipioRepetido_ApontaASegundaOcorrencia()
    {
        await ConcederAsync();
        await using WebApplicationFactory<HostAssemblyMarker> host = HostComGeo(GeoDoPara());

        using HttpResponseMessage resposta = await CriarAsync(host, DocumentoDoPsr2028(Municipios(Maraba, Maraba)));

        resposta.StatusCode.Should().Be(HttpStatusCode.Created);
        Avisos(await Corpo(resposta)).Should().Equal(("/provaPresencial/municipios/1/codigoIbge", "uniplus.selecao.prova_presencial.municipio_repetido"));
    }

    [Fact(DisplayName = "Importar o documento com código que o Geo não conhece cria o rascunho e aponta o município")]
    public async Task Criar_MunicipioDesconhecidoDoGeo_ApontaOMunicipio()
    {
        await ConcederAsync();
        await using WebApplicationFactory<HostAssemblyMarker> host = HostComGeo(GeoDoPara());

        using HttpResponseMessage resposta = await CriarAsync(host, DocumentoDoPsr2028(Municipios(Maraba, DesconhecidoDoGeo)));

        resposta.StatusCode.Should().Be(HttpStatusCode.Created);
        Avisos(await Corpo(resposta)).Should().Equal(("/provaPresencial/municipios/1/codigoIbge", "uniplus.selecao.prova_presencial.municipio_nao_conferido_no_geo"));
    }

    [Fact(DisplayName = "Substituir o rascunho confere a prova presencial: 200, nova revisão e um aviso por município desconhecido do Geo")]
    public async Task Substituir_MunicipioDesconhecidoDoGeo_ApontaCadaMunicipio()
    {
        await ConcederAsync();
        Guid id = await _cliente.CriarAsync(Betina, "CEPS");
        await using WebApplicationFactory<HostAssemblyMarker> host = HostComGeo(GeoDoPara());

        using HttpResponseMessage resposta = await SubstituirAsync(host, id, DocumentoDoPsr2028(Municipios(DesconhecidoDoGeo, Maraba, "1599998")));

        resposta.StatusCode.Should().Be(HttpStatusCode.OK);
        (await Corpo(resposta)).GetProperty("revisao").GetInt32().Should().Be(2);
        Avisos(await Corpo(resposta)).Should().Equal(
            ("/provaPresencial/municipios/0/codigoIbge", "uniplus.selecao.prova_presencial.municipio_nao_conferido_no_geo"),
            ("/provaPresencial/municipios/2/codigoIbge", "uniplus.selecao.prova_presencial.municipio_nao_conferido_no_geo"));
    }

    [Fact(DisplayName = "O documento sem a prova presencial é salvo com o aviso de por declarar")]
    public async Task Criar_SemProvaPresencial_ApontaPorDeclarar()
    {
        await ConcederAsync();
        await using WebApplicationFactory<HostAssemblyMarker> host = HostComGeo(GeoDoPara());

        using HttpResponseMessage resposta = await CriarAsync(host, Documento("CEPS", "PSR 2028"));

        resposta.StatusCode.Should().Be(HttpStatusCode.Created);
        Avisos(await Corpo(resposta)).Should().Equal(("/provaPresencial", "uniplus.selecao.prova_presencial.por_declarar"));
    }

    [Fact(DisplayName = "Com o Geo fora do ar o rascunho é salvo e a resposta traz um só aviso, no campo dos municípios")]
    public async Task Criar_GeoForaDoAr_UmSoAviso()
    {
        await ConcederAsync();
        await using WebApplicationFactory<HostAssemblyMarker> host = HostComGeo(GeoDoPara().Indisponivel());

        using HttpResponseMessage resposta = await CriarAsync(host, DocumentoDoPsr2028(Municipios(Maraba, Belem)));

        resposta.StatusCode.Should().Be(HttpStatusCode.Created);
        Avisos(await Corpo(resposta)).Should().Equal(("/provaPresencial/municipios", CodigoDoGeoIndisponivel));
    }

    [Theory(DisplayName = "Importar o documento com a prova presencial fora da forma recusa 422, não grava rascunho e não consulta o Geo")]
    [InlineData("""{ "temProvaPresencial": true, "municipios": [ { "codigoIbge": "15", "uf": "PA" } ] }""")]
    [InlineData("""{ "temProvaPresencial": false, "municipios": [ { "codigoIbge": "1504208", "uf": "PA" } ] }""")]
    public async Task Criar_ProvaPresencialForaDaForma_RecusaSemGravarENemConsultarOGeo(string provaPresencial)
    {
        await ConcederAsync();
        IConferenciaEnderecoGeo geo = Substitute.For<IConferenciaEnderecoGeo>();
        await using WebApplicationFactory<HostAssemblyMarker> host = HostComGeo(geo);
        int antes = await ContarRascunhosAsync();

        using HttpResponseMessage resposta = await CriarAsync(host, DocumentoComProvaPresencial(provaPresencial));

        resposta.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await ContarRascunhosAsync()).Should().Be(antes);
        geo.ReceivedCalls().Should().BeEmpty();
    }

    private static string DocumentoComProvaPresencial(string provaPresencial) => $$"""
        { "formato": "uniplus.selecao.configuracao", "versaoFormato": 1,
          "identificacao": { "nome": "PSR 2028", "unidadeAdministradoraCodigo": "CEPS" },
          "provaPresencial": {{provaPresencial}} }
        """;

    private async Task<int> ContarRascunhosAsync()
    {
        await using AsyncServiceScope escopo = fixture.Factory.Services.CreateAsyncScope();
        return await escopo.ServiceProvider.GetRequiredService<UniPlusDbContext>().Set<RascunhoDeProcessoSeletivo>().IgnoreQueryFilters().CountAsync();
    }

    [Fact(DisplayName = "A consulta ao Geo no PUT acontece com a linha do rascunho livre: outra transação a trava sem esperar")]
    public async Task Substituir_ConsultaAoGeo_NaoSegueATravaDaLinha()
    {
        await ConcederAsync();
        Guid id = await _cliente.CriarAsync(Betina, "CEPS");
        GeoQueObservaATravaDoRascunho geo = new(fixture.Factory.Services, id);
        await using WebApplicationFactory<HostAssemblyMarker> host = HostComGeo(geo);

        using HttpResponseMessage resposta = await SubstituirAsync(host, id, DocumentoDoPsr2028(Municipios(Maraba)));

        resposta.StatusCode.Should().Be(HttpStatusCode.OK);
        geo.TravaFicouLivreNaConsulta.Should().BeTrue();
    }

    private Task ConcederAsync() => _cliente.ConcederAsync(Betina, UnidadesDeTeste.Ceps.OrigemId);

    private static FakeConferenciaEnderecoGeo GeoDoPara() =>
        new FakeConferenciaEnderecoGeo().ComMunicipio(Maraba, "PA", "Marabá").ComMunicipio(Belem, "PA", "Belém");

    private WebApplicationFactory<HostAssemblyMarker> HostComGeo(IConferenciaEnderecoGeo geo) =>
        fixture.Factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IConferenciaEnderecoGeo>();
            services.AddSingleton(geo);
        }));

    private static string Municipios(params string[] codigos) => string.Join(", ", codigos.Select(static codigo => $$"""{ "codigoIbge": "{{codigo}}", "uf": "PA" }"""));

    private static string DocumentoDoPsr2028(string municipios) => $$"""
        { "formato": "uniplus.selecao.configuracao", "versaoFormato": 1,
          "identificacao": { "nome": "PSR 2028", "unidadeAdministradoraCodigo": "CEPS" },
          "provaPresencial": { "temProvaPresencial": true, "municipios": [ {{municipios}} ] } }
        """;

    private static async Task<HttpResponseMessage> CriarAsync(WebApplicationFactory<HostAssemblyMarker> host, string documento) =>
        await EnviarAsync(host, Requisicao(Betina, HttpMethod.Post, Rota, documento, chave: Guid.CreateVersion7().ToString()));

    private static async Task<HttpResponseMessage> SubstituirAsync(WebApplicationFactory<HostAssemblyMarker> host, Guid id, string documento) =>
        await EnviarAsync(host, Requisicao(Betina, HttpMethod.Put, $"{Rota}/{id}", documento, ifMatch: "\"1\""));

    private static async Task<HttpResponseMessage> EnviarAsync(WebApplicationFactory<HostAssemblyMarker> host, HttpRequestMessage requisicao)
    {
        using HttpClient client = host.CreateClient();
        using HttpRequestMessage enviada = requisicao;
        return await client.SendAsync(enviada);
    }

    private static (string? Campo, string Codigo)[] Avisos(JsonElement corpo) =>
        [.. corpo.GetProperty("diagnosticos").EnumerateArray().Select(static aviso => (aviso.GetProperty("field").GetString(), aviso.GetProperty("code").GetString()!))];

    /// <summary>
    /// O Geo que, ao ser consultado, tenta travar a linha do rascunho por outra transação: se a requisição a segurasse, a tentativa
    /// esperaria o limite e falharia.
    /// </summary>
    private sealed class GeoQueObservaATravaDoRascunho(IServiceProvider servicos, Guid rascunhoId) : IConferenciaEnderecoGeo
    {
        private readonly FakeConferenciaEnderecoGeo _geo = GeoDoPara();

        public bool TravaFicouLivreNaConsulta { get; private set; }

        public Task<Result<string>> ConferirCepAsync(string cep, string cidadeCodigoIbgeDeclarada, string ufDeclarada, CancellationToken cancellationToken = default) =>
            _geo.ConferirCepAsync(cep, cidadeCodigoIbgeDeclarada, ufDeclarada, cancellationToken);

        public Task<Result<CepResolvido>> ResolverCepAsync(string cep, CancellationToken cancellationToken = default) =>
            _geo.ResolverCepAsync(cep, cancellationToken);

        public async Task<Result<string>> ConferirMunicipioAsync(string cidadeCodigoIbgeDeclarado, string ufDeclarada, CancellationToken cancellationToken = default)
        {
            TravaFicouLivreNaConsulta = await TravarEmOutraTransacaoAsync(cancellationToken).ConfigureAwait(false);
            return await _geo.ConferirMunicipioAsync(cidadeCodigoIbgeDeclarado, ufDeclarada, cancellationToken).ConfigureAwait(false);
        }

        private async Task<bool> TravarEmOutraTransacaoAsync(CancellationToken cancellationToken)
        {
            await using AsyncServiceScope escopo = servicos.CreateAsyncScope();
            UniPlusDbContext contexto = escopo.ServiceProvider.GetRequiredService<UniPlusDbContext>();
            await using IDbContextTransaction transacao = await contexto.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                await RascunhosSemeadosParaTeste.Repositorio(contexto).ObterParaMutacaoAsync(rascunhoId, cancellationToken).ConfigureAwait(false);
                return true;
            }
            catch (EsperaPelaTravaEsgotadaException)
            {
                return false;
            }
        }
    }
}
