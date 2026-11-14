namespace Unifesspa.UniPlus.Infrastructure.Core.DependencyInjection;

using System.Reflection;

using Microsoft.EntityFrameworkCore;

/// <summary>
/// Os assemblies de onde a subida do <typeparamref name="TContext"/> lê os scripts SQL, na ordem em que os scripts de
/// cada fase são aplicados.
/// </summary>
internal sealed record AssembliesDosScriptsDoContexto<TContext>(IReadOnlyList<Assembly> Assemblies)
    where TContext : DbContext;
