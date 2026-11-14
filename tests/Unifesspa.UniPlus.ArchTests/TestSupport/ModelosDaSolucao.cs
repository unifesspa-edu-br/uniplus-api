namespace Unifesspa.UniPlus.ArchTests.TestSupport;

using System.IO;
using System.Reflection;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;

/// <summary>
/// Os <see cref="DbContext"/> da solução e os modelos de design-time deles, descobertos pelas fábricas de design-time.
/// Cada modelo é construído uma vez por execução: cada <see cref="DbContext"/> custa um <c>OnModelCreating</c> completo.
/// </summary>
internal static class ModelosDaSolucao
{
    // Os DbContexts vivem na infraestrutura de cada módulo e no projeto de persistência que compõe o contexto único.
    private static readonly string[] PadroesDeProjetoComContexto = ["Unifesspa.UniPlus.*.Infrastructure", "Unifesspa.UniPlus.*.Persistencia"];

    private static readonly Lazy<IReadOnlyList<(string Contexto, IModel Modelo)>> Construidos = new(Construir);

    internal static IReadOnlyList<(string Contexto, IModel Modelo)> Modelos() => Construidos.Value;

    internal static string[] ProjetosDeInfraestrutura() =>
        [.. PadroesDeProjetoComContexto.SelectMany(padrao =>
            Directory.EnumerateFiles(Path.Join(SolutionRootLocator.Locate(), "src"), padrao + ".csproj", SearchOption.AllDirectories))];

    internal static IEnumerable<Assembly> AssembliesDeInfraestrutura() =>
        PadroesDeProjetoComContexto.SelectMany(padrao => Directory.GetFiles(AppContext.BaseDirectory, padrao + ".dll")).Select(Assembly.LoadFrom);

    internal static IEnumerable<(Type Fabrica, Type Contexto, MethodInfo Metodo)> FabricasDeContexto() =>
        AssembliesDeInfraestrutura()
            .SelectMany(a => a.GetTypes())
            .Where(t => t is { IsAbstract: false, IsInterface: false })
            .SelectMany(t => t.GetInterfaces()
                .Where(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IDesignTimeDbContextFactory<>))
                .Select(i => (Fabrica: t, Contexto: i.GetGenericArguments()[0], Metodo: i.GetMethod(nameof(IDesignTimeDbContextFactory<DbContext>.CreateDbContext))!)));

    private static List<(string Contexto, IModel Modelo)> Construir() =>
    [
        .. FabricasDeContexto()
            .OrderBy(f => f.Contexto.Name, StringComparer.Ordinal)
            .Select(f => Modelo(f.Fabrica, f.Contexto, f.Metodo)),
    ];

    // O modelo de design-time guarda todas as anotações do modelo; o de runtime é otimizado e pode descartá-las.
    private static (string Contexto, IModel Modelo) Modelo(Type fabrica, Type tipoDoContexto, MethodInfo criar)
    {
        object instancia = Activator.CreateInstance(fabrica)!;
        using DbContext contexto = (DbContext)criar.Invoke(instancia, [Array.Empty<string>()])!;
        return (tipoDoContexto.Name, contexto.GetService<IDesignTimeModel>().Model);
    }
}
