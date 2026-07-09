namespace Unifesspa.UniPlus.Publicacoes.Infrastructure.Persistence;

using Microsoft.EntityFrameworkCore.Design;

using Unifesspa.UniPlus.Infrastructure.Core.Persistence;

/// <summary>
/// Factory consumido apenas pelo <c>dotnet ef</c> CLI em design-time. Ver
/// <c>SelecaoDbContextDesignTimeFactory</c> para a justificativa.
/// </summary>
public sealed class PublicacoesDbContextDesignTimeFactory : IDesignTimeDbContextFactory<PublicacoesDbContext>
{
    public PublicacoesDbContext CreateDbContext(string[] args)
    {
        return new PublicacoesDbContext(
            UniPlusDbContextOptionsExtensions.BuildDesignTimeOptions<PublicacoesDbContext>(schema: PublicacoesDbContext.Schema));
    }
}
