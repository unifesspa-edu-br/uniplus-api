namespace Unifesspa.UniPlus.Selecao.Infrastructure.Persistence;

using Microsoft.EntityFrameworkCore.Design;

using Unifesspa.UniPlus.Infrastructure.Core.Persistence;

/// <summary>
/// Factory consumido apenas pelo <c>dotnet ef</c> CLI em design-time (geração
/// de migrations). NÃO é registrado no DI runtime — o Program.cs continua
/// usando <c>UseUniPlusNpgsqlConventions</c> com interceptors + connection
/// string lida de <see cref="Microsoft.Extensions.Configuration.IConfiguration"/>.
///
/// <para>Delega a construção das options para
/// <see cref="UniPlusDbContextOptionsExtensions.BuildDesignTimeOptions{TContext}"/>
/// para manter naming convention (snake_case) e o pin do
/// <c>__EFMigrationsHistory</c> simétricos entre design-time e runtime —
/// caso contrário, <c>dotnet ef migrations script</c> e o
/// <c>MigrationHostedService</c> poderiam consultar tabelas de histórico
/// distintas (ADR-0054 + #437).</para>
/// </summary>
public sealed class SelecaoDbContextDesignTimeFactory : IDesignTimeDbContextFactory<SelecaoDbContext>
{
    public SelecaoDbContext CreateDbContext(string[] args)
    {
        return new SelecaoDbContext(
            UniPlusDbContextOptionsExtensions.BuildDesignTimeOptions<SelecaoDbContext>());
    }
}
