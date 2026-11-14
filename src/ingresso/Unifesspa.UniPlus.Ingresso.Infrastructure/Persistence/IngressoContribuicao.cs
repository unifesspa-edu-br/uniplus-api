namespace Unifesspa.UniPlus.Ingresso.Infrastructure.Persistence;

using Microsoft.EntityFrameworkCore;

using Unifesspa.UniPlus.Infrastructure.Core.Persistence;

/// <summary>O que o Ingresso acrescenta ao contexto único: as suas configurações.</summary>
public sealed class IngressoContribuicao : IContribuicaoDoModulo
{
    public const string NomeDoEsquema = "ingresso";

    public string Esquema => NomeDoEsquema;

    public void AplicarAoModelo(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(IngressoContribuicao).Assembly);
    }
}
