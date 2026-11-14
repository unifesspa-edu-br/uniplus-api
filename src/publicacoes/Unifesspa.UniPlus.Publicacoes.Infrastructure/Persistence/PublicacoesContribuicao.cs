namespace Unifesspa.UniPlus.Publicacoes.Infrastructure.Persistence;

using Microsoft.EntityFrameworkCore;

using Unifesspa.UniPlus.Infrastructure.Core.Persistence;

/// <summary>O que as Publicações acrescentam ao contexto único: as suas configurações.</summary>
public sealed class PublicacoesContribuicao : IContribuicaoDoModulo
{
    public const string NomeDoEsquema = "publicacoes";

    public string Esquema => NomeDoEsquema;

    public void AplicarAoModelo(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(PublicacoesContribuicao).Assembly);
    }
}
