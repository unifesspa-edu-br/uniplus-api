namespace Unifesspa.UniPlus.Selecao.IntegrationTests.ProcessosSeletivos;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

using Unifesspa.UniPlus.Infrastructure.Core.Persistence;
using Unifesspa.UniPlus.Monolito.Persistencia;

/// <summary>
/// O modelo do EF construído em memória, para os testes que confrontam as colunas com o que o decodificador do envelope aceita.
/// </summary>
internal static class ModeloEmMemoria
{
    /// <summary>
    /// Nenhuma conexão é aberta: a connection string existe só porque o provider a exige para montar o modelo.
    /// </summary>
    internal static MonolitoDbContext Contexto() => new(
        new DbContextOptionsBuilder<MonolitoDbContext>()
            .UseNpgsql("Host=localhost;Database=nao-conecta;Username=x;Password=x")
            .UseSnakeCaseNamingConvention()
            .Options);

    /// <summary>O tipo embutido na navegação que o dono declara.</summary>
    internal static IEntityType EmbutidoDe<TDono>(UniPlusDbContext contexto, string navegacao) =>
        contexto.Model.FindEntityType(typeof(TDono))!.GetNavigations().Single(n => n.Name == navegacao).TargetEntityType;
}
