namespace Unifesspa.UniPlus.Infrastructure.Core.UnitTests.Persistence.Apoio;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

using Unifesspa.UniPlus.Infrastructure.Core.Persistence;

internal sealed class ContextoDeTeste : UniPlusDbContext
{
    private readonly IReadOnlyList<IContribuicaoDoModulo> _modulos;

    private ContextoDeTeste(DbContextOptions<ContextoDeTeste> opcoes, IReadOnlyList<IContribuicaoDoModulo> modulos)
        : base(opcoes)
    {
        _modulos = modulos;
    }

    internal Guid ChaveDoModelo { get; } = Guid.NewGuid();

    protected override IReadOnlyList<IContribuicaoDoModulo> Modulos => _modulos;

    public static ContextoDeTeste Criar(params IContribuicaoDoModulo[] modulos)
    {
        DbContextOptions<ContextoDeTeste> opcoes = new DbContextOptionsBuilder<ContextoDeTeste>()
            .UseNpgsql("Host=modelo-sem-banco;Database=modelo;Username=x;Password=x")
            .ReplaceService<IModelCacheKeyFactory, UmModeloPorContexto>()
            .Options;

        return new ContextoDeTeste(opcoes, modulos);
    }

    private sealed class UmModeloPorContexto : IModelCacheKeyFactory
    {
        public object Create(DbContext context, bool designTime) =>
            (((ContextoDeTeste)context).ChaveDoModelo, designTime);
    }
}
