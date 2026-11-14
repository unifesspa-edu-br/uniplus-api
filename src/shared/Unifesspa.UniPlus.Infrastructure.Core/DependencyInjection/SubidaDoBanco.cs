namespace Unifesspa.UniPlus.Infrastructure.Core.DependencyInjection;

using System.Reflection;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;

/// <summary>
/// A subida do banco, sempre na mesma ordem e sob a trava do banco: scripts <c>Pre</c>, migrations do EF, scripts pós e,
/// por fim, a etapa posterior. Roda também sem migration pendente.
/// </summary>
internal static class SubidaDoBanco
{
    public static async Task ExecutarAsync(
        DbContext contexto,
        ILogger logger,
        IReadOnlyList<Assembly> assembliesDosScripts,
        Func<CancellationToken, Task>? etapaPosterior,
        CancellationToken cancellationToken)
    {
        await GarantirQueOBancoExisteAsync(contexto, cancellationToken).ConfigureAwait(false);
        string cadeia = CadeiaDeConexaoDe(contexto);
        IReadOnlyList<ScriptsSqlDoModulo> scripts = await LerScriptsAsync(assembliesDosScripts, cancellationToken)
            .ConfigureAwait(false);

        TravaDeMigrationDoBanco trava = await TravaDeMigrationDoBanco
            .AdquirirAsync(cadeia, logger, cancellationToken)
            .ConfigureAwait(false);
        await using (trava.ConfigureAwait(false))
        {
            await AplicarFasesAsync(contexto, cadeia, logger, scripts, etapaPosterior, cancellationToken).ConfigureAwait(false);
        }
    }

    private static async Task AplicarFasesAsync(
        DbContext contexto,
        string cadeia,
        ILogger logger,
        IReadOnlyList<ScriptsSqlDoModulo> scripts,
        Func<CancellationToken, Task>? etapaPosterior,
        CancellationToken cancellationToken)
    {
        await ExecutorDeScriptsSql.ExecutarAsync(cadeia, [.. scripts.SelectMany(s => s.Pre)], logger, cancellationToken)
            .ConfigureAwait(false);
        await MigrationServiceCollectionExtensions.AplicarMigrationsPendentesAsync(contexto, logger, cancellationToken)
            .ConfigureAwait(false);
        await ExecutorDeScriptsSql.ExecutarAsync(cadeia, [.. scripts.SelectMany(s => s.Pos)], logger, cancellationToken)
            .ConfigureAwait(false);
        if (etapaPosterior is not null)
        {
            await etapaPosterior(cancellationToken).ConfigureAwait(false);
        }
    }

    private static async Task<IReadOnlyList<ScriptsSqlDoModulo>> LerScriptsAsync(
        IReadOnlyList<Assembly> assemblies,
        CancellationToken cancellationToken)
    {
        List<ScriptsSqlDoModulo> scripts = [];
        foreach (Assembly assembly in assemblies)
        {
            scripts.Add(await ScriptsSqlDoModulo.DescobrirAsync(assembly, cancellationToken).ConfigureAwait(false));
        }

        return scripts;
    }

    // O MigrateAsync cria o banco ausente; a trava e os scripts Pre precisam dele antes disso.
    private static async Task GarantirQueOBancoExisteAsync(DbContext contexto, CancellationToken cancellationToken)
    {
        IRelationalDatabaseCreator criador = contexto.GetService<IRelationalDatabaseCreator>();
        if (!await criador.ExistsAsync(cancellationToken).ConfigureAwait(false))
        {
            await criador.CreateAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    // A cadeia configurada, e não a da conexão do EF: depois de aberta, a conexão do Npgsql omite a senha.
    private static string CadeiaDeConexaoDe(DbContext contexto) =>
        contexto.Database.GetConnectionString()
        ?? throw new InvalidOperationException(
            $"O contexto '{contexto.GetType().Name}' não tem cadeia de conexão; a subida dos scripts SQL exige PostgreSQL.");
}
