namespace Unifesspa.UniPlus.Infrastructure.Core.DependencyInjection;

using System.Reflection;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Logging;

/// <summary>
/// A preparaÃ§Ã£o do banco para quem tem só o contexto, como as fixtures de teste. A subida padrão e o Job
/// <c>ApplyAndExit</c> passam pelo mesmo caminho, registrado em <c>AddDbContextMigrationsOnStartup</c>.
/// </summary>
public static class PreparacaoDoBancoExtensions
{
    /// <summary>
    /// Aplica, sob a trava do banco, os scripts <c>Pre</c> de <paramref name="assembliesDosScripts"/>, as migrations do
    /// <paramref name="contexto"/> e os scripts pós, cada fase na ordem dos assemblies. Não roda a etapa posterior, que
    /// precisa do provider da aplicação.
    /// </summary>
    /// <param name="contexto">O contexto, apontado para o banco a subir.</param>
    /// <param name="assembliesDosScripts">Os assemblies que embutem os scripts, na ordem de aplicação.</param>
    /// <param name="cancellationToken">Token de cancelamento — opcional.</param>
    /// <exception cref="InvalidOperationException">Se um script falha.</exception>
    public static async Task PrepararOBancoAsync(
        this DbContext contexto,
        IReadOnlyList<Assembly> assembliesDosScripts,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(contexto);
        ArgumentNullException.ThrowIfNull(assembliesDosScripts);

        ILogger logger = contexto.GetService<ILoggerFactory>().CreateLogger(contexto.GetType());
        await PreparacaoDoBanco
            .ExecutarAsync(contexto, logger, assembliesDosScripts, etapaPosterior: null, cancellationToken)
            .ConfigureAwait(false);
    }
}
