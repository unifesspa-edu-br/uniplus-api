namespace Unifesspa.UniPlus.Ingresso.Infrastructure.Persistence;

using Microsoft.EntityFrameworkCore.Design;

using Unifesspa.UniPlus.Infrastructure.Core.Persistence;

/// <summary>
/// Factory consumido apenas pelo <c>dotnet ef</c> CLI em design-time. Ver
/// <c>SelecaoDbContextDesignTimeFactory</c> para a justificativa.
/// </summary>
public sealed class IngressoDbContextDesignTimeFactory : IDesignTimeDbContextFactory<IngressoDbContext>
{
    public IngressoDbContext CreateDbContext(string[] args)
    {
        return new IngressoDbContext(
            UniPlusDbContextOptionsExtensions.BuildDesignTimeOptions<IngressoDbContext>());
    }
}
