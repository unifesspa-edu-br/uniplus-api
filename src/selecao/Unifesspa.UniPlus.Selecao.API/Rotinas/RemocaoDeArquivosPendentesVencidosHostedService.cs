namespace Unifesspa.UniPlus.Selecao.API.Rotinas;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using Unifesspa.UniPlus.Infrastructure.Core.Hosting;
using Unifesspa.UniPlus.Selecao.Application.Services;

/// <summary>
/// Agenda <see cref="RemocaoDeArquivosPendentesVencidos"/> no intervalo de
/// <see cref="RemocaoDeArquivosPendentesOptions"/>. Com mais de uma réplica, cada uma executa a
/// sua: a remoção do registro é condicionada no banco, e a réplica que chega depois a um mesmo
/// pendente não remove nada.
/// </summary>
internal sealed class RemocaoDeArquivosPendentesVencidosHostedService : RotinaPeriodicaHostedService
{
    private readonly TimeSpan _intervalo;

    public RemocaoDeArquivosPendentesVencidosHostedService(
        IServiceScopeFactory scopeFactory,
        TimeProvider timeProvider,
        IOptions<RemocaoDeArquivosPendentesOptions> options,
        ILogger<RemocaoDeArquivosPendentesVencidosHostedService> logger)
        : base(scopeFactory, timeProvider, logger)
    {
        ArgumentNullException.ThrowIfNull(options);
        _intervalo = options.Value.Intervalo;
    }

    protected override string Nome => "remoção dos envios de arquivo pendentes vencidos";

    protected override TimeSpan Intervalo => _intervalo;

    protected override Task ExecutarAsync(IServiceProvider servicos, CancellationToken cancellationToken) =>
        servicos.GetRequiredService<RemocaoDeArquivosPendentesVencidos>().ExecutarAsync(cancellationToken);
}
