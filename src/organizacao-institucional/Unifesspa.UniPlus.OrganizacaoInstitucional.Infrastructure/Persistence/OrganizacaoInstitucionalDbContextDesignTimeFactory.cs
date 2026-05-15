namespace Unifesspa.UniPlus.OrganizacaoInstitucional.Infrastructure.Persistence;

using Microsoft.EntityFrameworkCore.Design;

using Unifesspa.UniPlus.Infrastructure.Core.Persistence;

/// <summary>
/// Factory consumido apenas pelo <c>dotnet ef</c> CLI em design-time (geração
/// de migrations) — análogo aos factories de Selecao/Ingresso/Portal. NÃO
/// é registrado no DI runtime — o <c>Program.cs</c> usa
/// <c>UseUniPlusNpgsqlConventions</c> com connection string lida de
/// <see cref="Microsoft.Extensions.Configuration.IConfiguration"/>.
/// </summary>
public sealed class OrganizacaoInstitucionalDbContextDesignTimeFactory
    : IDesignTimeDbContextFactory<OrganizacaoInstitucionalDbContext>
{
    public OrganizacaoInstitucionalDbContext CreateDbContext(string[] args)
    {
        return new OrganizacaoInstitucionalDbContext(
            UniPlusDbContextOptionsExtensions.BuildDesignTimeOptions<OrganizacaoInstitucionalDbContext>());
    }
}
