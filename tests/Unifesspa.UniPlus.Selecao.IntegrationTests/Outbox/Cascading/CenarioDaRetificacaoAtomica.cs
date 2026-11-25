namespace Unifesspa.UniPlus.Selecao.IntegrationTests.Outbox.Cascading;

using System.Globalization;

using AwesomeAssertions;

using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

using Unifesspa.UniPlus.Host;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Monolito.Persistencia;
using Unifesspa.UniPlus.Publicacoes.Contracts;
using Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;

using Wolverine;

using static Unifesspa.UniPlus.Selecao.IntegrationTests.Outbox.Cascading.PublicacaoPeloEndpointDeTeste;
using static Unifesspa.UniPlus.Selecao.IntegrationTests.Outbox.Cascading.RegistroPelaPortaDeTeste;

/// <summary>
/// Um processo publicado, no host real, com o controle que age na confirmação da transação da retificação, e a
/// retificação pelo comando real, pelo mesmo caminho do endpoint. O host é derivado do host da coleção: aponta para o
/// mesmo banco, e o que o teste conferir depois lê por outro escopo. A espera pela trava do processo é longa, para a
/// retificação que chega durante a retenção esperar a liberação em vez de ser recusada.
/// </summary>
internal sealed class CenarioDaRetificacaoAtomica : IAsyncDisposable
{
    private static readonly TimeSpan EsperaPelaTrava = TimeSpan.FromSeconds(30);

    private readonly CascadingApiFactory _preparo;
    private readonly WebApplicationFactory<HostAssemblyMarker> _host;

    private CenarioDaRetificacaoAtomica(CascadingApiFactory preparo, Guid processoId, Guid documentoDaAbertura)
    {
        _preparo = preparo;
        ProcessoId = processoId;
        DocumentoDaAbertura = documentoDaAbertura;
        _host = preparo.WithWebHostBuilder(ConfigurarOHost);
        Cliente = _host.CreateClient();
    }

    public Guid ProcessoId { get; }

    public Guid DocumentoDaAbertura { get; }

    /// <summary>O ato que criou a versão 1.</summary>
    public Guid AtoDaAbertura { get; private set; }

    public ControleDaConfirmacaoDaRetificacao Controle { get; } = new();

    public ConexoesDasRetificacoes Conexoes { get; } = new();

    public HttpClient Cliente { get; }

    public IServiceProvider Servicos => _host.Services;

    /// <summary>Semeia o processo, publica a versão 1 pelo comando real e devolve o cenário pronto para retificar.</summary>
    public static async Task<CenarioDaRetificacaoAtomica> PublicadoAsync(CascadingFixture fixture, string nome)
    {
        ArgumentNullException.ThrowIfNull(fixture);
        await TiposDeAtoSeeder.SemearAsync(fixture.Factory.Services);
        (Guid processoId, Guid documentoId) = await SemearProcessoAsync(fixture.Factory, nome);
        CenarioDaRetificacaoAtomica cenario = new(fixture.Factory, processoId, documentoId);
        await cenario.PublicarAAberturaAsync();

        return cenario;
    }

    public Task<Guid> SemearDocumentoAsync() => SemearDocumentoConfirmadoAsync(_preparo, ProcessoId);

    /// <summary>O sufixo troca cada dígito hexadecimal por uma letra, preservando a unicidade e o formato válido.</summary>
    private static string SufixoSoComLetras() =>
        string.Concat(Guid.CreateVersion7().ToString("N", System.Globalization.CultureInfo.InvariantCulture)
            .Select(c => (char)('A' + Convert.ToInt32(c.ToString(), 16))));

    /// <summary>Semeia um tipo de ato que admite um único ato vivo por objeto e devolve o código dele.</summary>
    public async Task<string> SemearTipoDeAtoUnicoPorObjetoAsync()
    {
        string codigo = $"RETIFICACAO_UNICA_{SufixoSoComLetras()}";
        await TiposDeAtoSeeder.SemearTipoAsync(_preparo.Services, codigo, unicoPorObjeto: true);

        return codigo;
    }

    /// <summary>Registra, pela porta de Publicações e como outra linhagem, um ato do tipo no mesmo objeto do processo.</summary>
    public Task OcuparAVagaDoObjetoAsync(string tipoDoAto) =>
        RegistrarEGravarAsync(
            _preparo,
            AtoDoProcesso(ProcessoId, tipoDoAto, "888/2026", new AtributosDoTipoAto(CongelaConfiguracao: true, UnicoPorObjeto: true, EfeitoIrreversivel: false, Nome: null)));

    /// <summary>Retifica pelo comando real, num escopo próprio; sem tipo de ato, usa o tipo de retificação do edital.</summary>
    public async Task<Result> RetificarAsync(Guid documentoId, string? tipoDoAto = null)
    {
        Conexoes.Marcar(documentoId);
        DadosDoAto ato = tipoDoAto is null ? DadosDoAtoDeTeste.Retificacao : DadosDoAtoDeTeste.Retificacao with { TipoAtoCodigo = tipoDoAto };
        RetificarProcessoSeletivoCommand comando = new(
            ProcessoId, "Correção do prazo de inscrição", Numero: "001/2026-R1", PeriodoInscricaoInicio: null,
            PeriodoInscricaoFim: null, DocumentoEditalId: documentoId, Ato: ato);

        return await InvocarAsync(comando);
    }

    public async ValueTask DisposeAsync()
    {
        Controle.Liberar();
        Cliente.Dispose();
        await _host.DisposeAsync();
    }

    private void ConfigurarOHost(IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration((_, configuracao) => configuracao.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["TravaDoProcesso:EsperaMaxima"] = EsperaPelaTrava.ToString("c", CultureInfo.InvariantCulture),
        }));
        builder.ConfigureTestServices(servicos =>
            servicos.ConfigureDbContext<MonolitoDbContext>(opcoes => opcoes.AddInterceptors(Controle, Conexoes)));
    }

    private async Task PublicarAAberturaAsync()
    {
        Result publicada = await InvocarAsync(new PublicarProcessoSeletivoCommand(
            ProcessoId, Numero: null, PeriodoInscricaoInicio: null, PeriodoInscricaoFim: null,
            DocumentoEditalId: DocumentoDaAbertura, Ato: DadosDoAtoDeTeste.Padrao));
        publicada.IsSuccess.Should().BeTrue(publicada.Error?.Message);
        AtoDaAbertura = (await EstadoDaRetificacaoNoBanco.VersoesAsync(Servicos, ProcessoId)).Single().AtoCriadorId;
    }

    private async Task<Result> InvocarAsync(object comando)
    {
        await using AsyncServiceScope escopo = Servicos.CreateAsyncScope();

        return await escopo.ServiceProvider.GetRequiredService<IMessageBus>().InvokeAsync<Result>(comando);
    }
}
