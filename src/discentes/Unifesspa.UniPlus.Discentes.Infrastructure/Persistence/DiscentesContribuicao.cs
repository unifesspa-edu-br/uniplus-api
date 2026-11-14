namespace Unifesspa.UniPlus.Discentes.Infrastructure.Persistence;

using Microsoft.EntityFrameworkCore;

using Unifesspa.UniPlus.Infrastructure.Core.Persistence;

/// <summary>O que os Discentes acrescentam ao contexto único: as suas configurações.</summary>
public sealed class DiscentesContribuicao : IContribuicaoDoModulo
{
    public const string NomeDoEsquema = "discentes";

    public string Esquema => NomeDoEsquema;

    public void AplicarAoModelo(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(DiscentesContribuicao).Assembly);
    }
}
