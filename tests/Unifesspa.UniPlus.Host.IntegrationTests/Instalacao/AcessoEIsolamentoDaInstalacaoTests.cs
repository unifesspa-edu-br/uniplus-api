namespace Unifesspa.UniPlus.Host.IntegrationTests.Instalacao;

using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

using Minio;

using Unifesspa.UniPlus.Autorizacao.Application.Commands;
using Unifesspa.UniPlus.Autorizacao.Domain.Entities;
using Unifesspa.UniPlus.Autorizacao.Domain.Interfaces;
using Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;
using Unifesspa.UniPlus.Autorizacao.Infrastructure.Persistence;
using Unifesspa.UniPlus.Infrastructure.Core.Persistence;
using Unifesspa.UniPlus.Infrastructure.Core.Storage;
using Unifesspa.UniPlus.IntegrationTests.Fixtures.Hosting;
using Unifesspa.UniPlus.Selecao.Application.Commands.DocumentosDaInscricao;
using Unifesspa.UniPlus.Verificador.Antivirus;
using Unifesspa.UniPlus.Verificador.Armazenamento;
using Unifesspa.UniPlus.Verificador.Configuracao;
using Unifesspa.UniPlus.Verificador.Contratos;
using Unifesspa.UniPlus.Verificador.Observabilidade;
using Unifesspa.UniPlus.Verificador.Verificacao;

using Wolverine;
using Wolverine.Runtime.Handlers;

/// <summary>O acesso às operações da instalação, o isolamento das filas de resultado e o verificador real.</summary>
[Collection(MonolitoDaInstalacaoCollection.Name)]
public sealed class AcessoEIsolamentoDaInstalacaoTests : InstalacaoTestBase
{
    public AcessoEIsolamentoDaInstalacaoTests(MonolitoDaInstalacaoFixture fixture)
        : base(fixture)
    {
    }

    [Theory(DisplayName = "Sem o papel de instalação as operações respondem 403")]
    [InlineData("POST", "")]
    [InlineData("POST", "/019a0000-0000-7000-8000-000000000099/documento/confirmacao")]
    [InlineData("POST", "/019a0000-0000-7000-8000-000000000099/codigo")]
    [InlineData("POST", "/confirmacao")]
    [InlineData("GET", "/019a0000-0000-7000-8000-000000000099")]
    public async Task SemOPapel_Devolve403(string metodo, string sufixo)
    {
        using HttpClient semPapel = Cliente(Diogo, papel: false);

        using HttpResponseMessage resposta = await EnviarAsync(
            semPapel, new HttpMethod(metodo), Rota + sufixo, metodo == "POST" ? new { codigo = "12345678", etag = "x" } : null);

        resposta.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact(DisplayName = "A instalação de outra pessoa responde 404, como a inexistente")]
    public async Task Obter_DeOutraPessoa_Devolve404()
    {
        await ResetarAsync();
        using HttpClient paulo = Cliente(Paulo);
        (Guid id, _) = await IniciarAsync(paulo, Diogo, Isabella);
        using HttpClient manoel = Cliente(Manoel);

        using HttpResponseMessage resposta = await manoel.GetAsync(new Uri($"{Rota}/{id}", UriKind.Relative));

        resposta.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact(DisplayName = "A trilha e as tentativas sem pendência exigem a permissão de administrar concessões")]
    public async Task Trilha_SemPermissao_Devolve403()
    {
        await ResetarAsync();
        using HttpClient paulo = Cliente(Paulo);
        (Guid id, _) = await IniciarAsync(paulo, Diogo, Isabella);

        using HttpResponseMessage trilha = await paulo.GetAsync(new Uri($"/api/autorizacao/instalacoes/{id}/trilha", UriKind.Relative));
        using HttpResponseMessage tentativas = await paulo.GetAsync(new Uri("/api/autorizacao/instalacoes/tentativas-sem-pendencia", UriKind.Relative));

        trilha.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        tentativas.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact(DisplayName = "O resultado entregue à fila da Seleção não executa o handler da instalação")]
    public async Task Isolamento_ResultadoDaFilaDaSelecao_NaoToucaAInstalacao()
    {
        await ResetarAsync();
        using HttpClient paulo = Cliente(Paulo);
        (Guid id, Uri url) = await IniciarAsync(paulo, Diogo, Isabella);
        string etag = await EnviarPdfAsync(url);
        (await EnviarAsync(paulo, HttpMethod.Post, $"{Rota}/{id}/documento/confirmacao", new { etag })).StatusCode.Should().Be(HttpStatusCode.OK);

        await InjetarResultadoAsync(id, VeredictoDaVerificacao.Verificado, fila: FilasDaVerificacao.Resultados);

        // O handler da Seleção processou o resultado (não achou o arquivo): é o sinal de que a mensagem foi consumida.
        await AguardarLogAsync($"Resultado da verificação do arquivo {id} descartado");
        LerLog().Should().NotContain($"documento {id} da Autorização");
        (await LerAsync(paulo, $"{Rota}/{id}")).GetProperty("situacao").GetString().Should().Be("documento_em_verificacao");
    }

    [Fact(DisplayName = "O resultado entregue à fila da Autorização não executa o handler da Seleção")]
    public async Task Isolamento_ResultadoDaFilaDaAutorizacao_NaoExecutaAAtualizacaoDaSelecao()
    {
        Guid desconhecido = Guid.CreateVersion7();

        await InjetarResultadoAsync(desconhecido, VeredictoDaVerificacao.Verificado, fila: FilasDaVerificacao.ResultadosAutorizacao);

        await AguardarLogAsync($"documento {desconhecido} da Autorização descartado");
        LerLog().Should().NotContain($"Resultado da verificação do arquivo {desconhecido}");
    }

    [Fact(DisplayName = "A política de falha própria alcança a cadeia do handler da instalação, e cada cadeia tem só a sua")]
    public void PoliticaDeFalha_AlcancaACadeiaDoHandlerDaInstalacao()
    {
        HandlerGraph grafo = Fixture.Factory.Services.GetRequiredService<HandlerGraph>();
        HandlerChain[] cadeias = [.. grafo.AllChains().Where(c => c.MessageType == typeof(ResultadoDaVerificacaoDeArquivo))];

        HandlerChain daInstalacao = cadeias.Single(c => c.Handlers.Any(h => h.HandlerType == typeof(RegistrarResultadoDaVerificacaoDoDocumentoDaAutorizacaoHandler)));
        HandlerChain daSelecao = cadeias.Single(c => c.Handlers.Any(h => h.HandlerType == typeof(RegistrarResultadoDaVerificacaoDoArquivoHandler)));

        // Duas regras por cadeia: o resultado inválido direto para a fila de erro, e a reentrega com cooldown.
        daInstalacao.Failures.Count().Should().Be(2);
        daSelecao.Failures.Count().Should().Be(2);
    }

    [Fact(DisplayName = "O verificador real promove o PDF e devolve o veredicto pela fila da Autorização")]
    public async Task Verificador_EmProcesso_PromoveOPdfEPublicaNaFilaDaAutorizacao()
    {
        await ResetarAsync();
        byte[] pdf = await File.ReadAllBytesAsync(Path.Join(AppContext.BaseDirectory, "Instalacao", "sem-senha.pdf"));
        using HttpClient paulo = Cliente(Paulo);
        (Guid id, Uri url) = await IniciarAsync(paulo, Diogo, Isabella, pdf.Length);
        string etag = await EnviarPdfAsync(url, pdf);

        using IHost verificador = await IniciarVerificadorAsync();
        (await EnviarAsync(paulo, HttpMethod.Post, $"{Rota}/{id}/documento/confirmacao", new { etag })).StatusCode.Should().Be(HttpStatusCode.OK);

        await AguardarSituacaoAsync(paulo, id, "documento_verificado");
        await ConferirPdfPromovidoAsync(id, pdf);
    }

    private async Task ConferirPdfPromovidoAsync(Guid id, byte[] pdf)
    {
        await using AsyncServiceScope scope = Fixture.Factory.Services.CreateAsyncScope();
        Instalacao gravada = await scope.ServiceProvider.GetRequiredService<UniPlusDbContext>().Instalacoes
            .AsNoTracking().SingleAsync(i => i.Id == id);
        gravada.DocumentoSha256.Should().Be(Convert.ToHexStringLower(SHA256.HashData(pdf)));
        IStorageService storage = scope.ServiceProvider.GetRequiredService<IStorageService>();
        (await storage.ObterMetadadosAsync(MonolitoDaInstalacaoFixture.BucketDefinitivo, $"autorizacao/instalacao/{id}/definitivo"))
            .Should().NotBeNull("o verificador promove o PDF limpo ao bucket definitivo");
    }

    // O verificador que a produção roda como serviço à parte, com o armazenamento e as filas reais do teste e um
    // antivírus que nada acusa.
    private async Task<IHost> IniciarVerificadorAsync()
    {
        VerificadorOptions opcoes = OpcoesDoVerificador();
        IHost host = Host.CreateDefaultBuilder()
            .ConfigureServices(services => RegistrarServicosDoVerificador(services, opcoes))
            .UseWolverine(wolverine =>
            {
                // O serviço real descobre o handler pelo assembly do próprio executável; aqui o executável é o do teste.
                wolverine.Discovery.IncludeAssembly(typeof(VerificarArquivoHandler).Assembly);
                ConfiguracaoDoWolverine.Configurar(wolverine, opcoes);
            })
            .Build();
        await host.StartAsync();
        return host;
    }

    private VerificadorOptions OpcoesDoVerificador() =>
        new()
        {
            Paralelismo = 1,
            RabbitMq = { Uri = Environment.GetEnvironmentVariable("RabbitMq__Uri")! },
            Armazenamento =
            {
                Endpoint = Fixture.EnderecoDoMinio,
                AccessKey = MinioContainerFixture.AccessKey,
                SecretKey = MinioContainerFixture.SecretKey,
                BucketEnvios = MonolitoDaInstalacaoFixture.BucketDeEnvio,
                BucketArquivos = MonolitoDaInstalacaoFixture.BucketDefinitivo,
            },
        };

    private static void RegistrarServicosDoVerificador(IServiceCollection services, VerificadorOptions opcoes)
    {
        services.AddSingleton(Options.Create(opcoes));
        services.AddSingleton<IMinioClient>(_ => new MinioClient()
            .WithEndpoint(opcoes.Armazenamento.Endpoint)
            .WithCredentials(opcoes.Armazenamento.AccessKey, opcoes.Armazenamento.SecretKey)
            .Build());
        services.AddSingleton<IArmazenamentoDeArquivos, ArmazenamentoMinio>();
        services.AddSingleton<IClamd, ClamdQueNadaAcusa>();
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<MetricasDoVerificador>();
    }

    private sealed class ClamdQueNadaAcusa : IClamd
    {
        public Task<ResultadoDoClamd> VerificarAsync(Stream conteudo, CancellationToken cancellationToken) =>
            Task.FromResult(new ResultadoDoClamd(false, null));

        public Task<DateTimeOffset> LerDataDaBaseAsync(CancellationToken cancellationToken) =>
            Task.FromResult(DateTimeOffset.UtcNow);
    }
}
