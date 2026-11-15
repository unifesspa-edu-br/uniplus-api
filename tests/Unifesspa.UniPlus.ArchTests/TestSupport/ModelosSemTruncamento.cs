namespace Unifesspa.UniPlus.ArchTests.TestSupport;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

/// <summary>
/// Os modelos de design-time dos <see cref="DbContext"/> da solução construídos sem o limite de 63 bytes do PostgreSQL. O EF e o
/// plugin de <c>snake_case</c> cortam em silêncio o nome de FK, índice e restrição que eles mesmos derivam, e o modelo comum
/// só mostra o nome já cortado; sem o corte, o nome derivado aparece inteiro e a regra de tamanho o enxerga.
/// </summary>
internal static class ModelosSemTruncamento
{
    private const int LimiteDoModelo = 1024;

    private static readonly Lazy<IReadOnlyList<(string Contexto, IModel Modelo)>> Construidos = new(Construir);

    internal static IReadOnlyList<(string Contexto, IModel Modelo)> Modelos() => Construidos.Value;

    /// <summary>As mesmas opções, mais a extensão que tira o limite de tamanho de identificador do modelo.</summary>
    internal static DbContextOptions SemTruncamento(DbContextOptions opcoes)
    {
        ArgumentNullException.ThrowIfNull(opcoes);

        return opcoes.WithExtension(new ExtensaoSemTruncamento());
    }

    private static List<(string Contexto, IModel Modelo)> Construir() =>
    [
        .. ModelosDaSolucao.FabricasDeContexto()
            .OrderBy(f => f.Contexto.Name, StringComparer.Ordinal)
            .Select(f => (f.Contexto.Name, Modelo(f.Fabrica, f.Contexto, f.Metodo))),
    ];

    private static IModel Modelo(Type fabrica, Type tipoDoContexto, System.Reflection.MethodInfo criar)
    {
        using DbContext original = (DbContext)criar.Invoke(Activator.CreateInstance(fabrica), [Array.Empty<string>()])!;
        DbContextOptions opcoes = SemTruncamento((DbContextOptions)original.GetService<IDbContextOptions>());
        using DbContext contexto = (DbContext)Activator.CreateInstance(tipoDoContexto, opcoes)!;
        return contexto.GetService<IDesignTimeModel>().Model;
    }

    private sealed class ExtensaoSemTruncamento : IDbContextOptionsExtension
    {
        public DbContextOptionsExtensionInfo Info => new InformacaoDaExtensao(this);

        public void ApplyServices(IServiceCollection services) =>
            services.Replace(ServiceDescriptor.Scoped<IModelCustomizer, CustomizadorSemTruncamento>());

        public void Validate(IDbContextOptions options)
        {
        }
    }

    private sealed class InformacaoDaExtensao(IDbContextOptionsExtension extensao) : DbContextOptionsExtensionInfo(extensao)
    {
        public override bool IsDatabaseProvider => false;

        public override string LogFragment => "SemTruncamentoDeIdentificadores ";

        public override int GetServiceProviderHashCode() => LimiteDoModelo;

        public override bool ShouldUseSameServiceProvider(DbContextOptionsExtensionInfo other) => other is InformacaoDaExtensao;

        public override void PopulateDebugInfo(IDictionary<string, string> debugInfo) =>
            debugInfo["SemTruncamentoDeIdentificadores"] = LimiteDoModelo.ToString(System.Globalization.CultureInfo.InvariantCulture);
    }

    private sealed class CustomizadorSemTruncamento(ModelCustomizerDependencies dependencias) : RelationalModelCustomizer(dependencias)
    {
        public override void Customize(ModelBuilder modelBuilder, DbContext context)
        {
            ArgumentNullException.ThrowIfNull(modelBuilder);

            modelBuilder.Model.SetMaxIdentifierLength(LimiteDoModelo);
            base.Customize(modelBuilder, context);
        }
    }
}
