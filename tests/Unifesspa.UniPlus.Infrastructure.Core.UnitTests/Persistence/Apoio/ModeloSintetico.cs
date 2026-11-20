namespace Unifesspa.UniPlus.Infrastructure.Core.UnitTests.Persistence.Apoio;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;

using Unifesspa.UniPlus.Infrastructure.Core.Persistence;

/// <summary>
/// Monta, sem banco, o modelo que a configuração recebida descreve e que a convenção dos enums de contrato percorre
/// em seguida, como a composição do modelo faz. Quem precisa das convenções do modelo (<c>ConfigureConventions</c>)
/// as entrega pelo segundo parâmetro.
/// </summary>
internal static class ModeloSintetico
{
    public static IModel Construir(Action<ModelBuilder> configurar, Action<ModelConfigurationBuilder>? convencoes = null)
    {
        using ContextoSintetico contexto = new(configurar, convencoes);

        return contexto.GetService<IDesignTimeModel>().Model;
    }

    private sealed class ContextoSintetico(Action<ModelBuilder> configurar, Action<ModelConfigurationBuilder>? convencoes) : DbContext
    {
        internal Guid ChaveDoModelo { get; } = Guid.NewGuid();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder) =>
            optionsBuilder
                .UseNpgsql("Host=modelo-sem-banco;Database=modelo;Username=x;Password=x")
                .ReplaceService<IModelCacheKeyFactory, UmModeloPorContexto>();

        protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder) =>
            convencoes?.Invoke(configurationBuilder);

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            configurar(modelBuilder);
            modelBuilder.ConfigurarConversaoDosEnumsDeContrato();
        }

        private sealed class UmModeloPorContexto : IModelCacheKeyFactory
        {
            public object Create(DbContext context, bool designTime) =>
                (((ContextoSintetico)context).ChaveDoModelo, designTime);
        }
    }
}
