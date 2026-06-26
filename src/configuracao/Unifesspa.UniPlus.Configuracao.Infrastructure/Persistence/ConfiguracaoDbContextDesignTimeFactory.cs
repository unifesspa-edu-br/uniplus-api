namespace Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence;

using Microsoft.EntityFrameworkCore.Design;

using Unifesspa.UniPlus.Infrastructure.Core.Persistence;

/// <summary>
/// Factory consumido apenas pelo <c>dotnet ef</c> CLI em design-time —
/// análogo a Selecao/Ingresso/Portal/OrganizacaoInstitucional. Não é
/// registrado no DI runtime; runtime usa <c>UseUniPlusNpgsqlConventions</c>
/// com connection string lida de <see cref="Microsoft.Extensions.Configuration.IConfiguration"/>.
/// </summary>
public sealed class ConfiguracaoDbContextDesignTimeFactory
    : IDesignTimeDbContextFactory<ConfiguracaoDbContext>
{
    public ConfiguracaoDbContext CreateDbContext(string[] args)
    {
        return new ConfiguracaoDbContext(
            UniPlusDbContextOptionsExtensions.BuildDesignTimeOptions<ConfiguracaoDbContext>(
                schema: ConfiguracaoDbContext.Schema));
    }
}
