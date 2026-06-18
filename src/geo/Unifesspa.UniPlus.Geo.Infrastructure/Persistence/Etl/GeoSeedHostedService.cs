namespace Unifesspa.UniPlus.Geo.Infrastructure.Persistence.Etl;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using Unifesspa.UniPlus.Geo.Application.Abstractions;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Seed automático do Geo em desenvolvimento (Story #674, CA-01): no boot, se a base
/// estiver vazia, dispara a carga da versão configurada pelo mesmo caminho do endpoint
/// admin (não bloqueia o boot — a carga roda no worker). Gate duplo: ambiente
/// <see cref="IHostEnvironment.IsDevelopment"/> <strong>e</strong>
/// <see cref="EtlOpcoes.SeedHabilitado"/> (default <see langword="false"/>) — produção
/// nunca semeia, e os testes (que rodam como <c>Development</c>) também não, por não
/// ligarem a flag.
/// </summary>
internal sealed partial class GeoSeedHostedService : IHostedService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IHostEnvironment _ambiente;
    private readonly EtlOpcoes _opcoes;
    private readonly ILogger<GeoSeedHostedService> _logger;

    public GeoSeedHostedService(
        IServiceScopeFactory scopeFactory,
        IHostEnvironment ambiente,
        IOptions<EtlOpcoes> opcoes,
        ILogger<GeoSeedHostedService> logger)
    {
        ArgumentNullException.ThrowIfNull(scopeFactory);
        ArgumentNullException.ThrowIfNull(ambiente);
        ArgumentNullException.ThrowIfNull(opcoes);
        ArgumentNullException.ThrowIfNull(logger);

        _scopeFactory = scopeFactory;
        _ambiente = ambiente;
        _opcoes = opcoes.Value;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (!_ambiente.IsDevelopment() || !_opcoes.SeedHabilitado)
        {
            LogSeedDesabilitado(_logger);
            return;
        }

        using IServiceScope escopo = _scopeFactory.CreateScope();
        GeoDbContext contexto = escopo.ServiceProvider.GetRequiredService<GeoDbContext>();

        bool baseVazia = !await contexto.Cidades.AnyAsync(cancellationToken).ConfigureAwait(false);
        if (!baseVazia)
        {
            LogBasePopulada(_logger);
            return;
        }

        IGeoImportacaoService servico = escopo.ServiceProvider.GetRequiredService<IGeoImportacaoService>();
        Result<Guid> resultado = await servico.IniciarAsync(_opcoes.VersaoSeed, "seed", cancellationToken).ConfigureAwait(false);

        if (resultado.IsSuccess)
        {
            LogSeedDisparado(_logger, _opcoes.VersaoSeed, resultado.Value);
        }
        else
        {
            LogSeedNaoDisparado(_logger, _opcoes.VersaoSeed, resultado.Error!.Code);
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    [LoggerMessage(Level = LogLevel.Debug, Message = "Seed do Geo desabilitado (ambiente não-Development ou flag desligada).")]
    private static partial void LogSeedDesabilitado(ILogger logger);

    [LoggerMessage(Level = LogLevel.Information, Message = "Seed do Geo: base já populada — nada a semear.")]
    private static partial void LogBasePopulada(ILogger logger);

    [LoggerMessage(Level = LogLevel.Information, Message = "Seed do Geo: carga {ExecucaoId} disparada para a versão {Versao}.")]
    private static partial void LogSeedDisparado(ILogger logger, string versao, Guid execucaoId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Seed do Geo: carga da versão {Versao} não disparada ({Codigo}).")]
    private static partial void LogSeedNaoDisparado(ILogger logger, string versao, string codigo);
}
