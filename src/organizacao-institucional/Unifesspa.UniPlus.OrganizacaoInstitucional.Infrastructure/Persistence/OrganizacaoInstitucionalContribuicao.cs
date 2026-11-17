namespace Unifesspa.UniPlus.OrganizacaoInstitucional.Infrastructure.Persistence;

using Microsoft.EntityFrameworkCore;

using Unifesspa.UniPlus.Infrastructure.Core.Persistence;

/// <summary>O que a Organização Institucional acrescenta ao contexto único: as configurações e a extensão de busca por trigramas.</summary>
public sealed class OrganizacaoInstitucionalContribuicao : IContribuicaoDoModulo
{
    public const string NomeDoEsquema = "organizacao";

    public string Esquema => NomeDoEsquema;

    public void AplicarAoModelo(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        // Os índices trigrama das colunas de busca da Unidade dependem desta extensão.
        modelBuilder.HasPostgresExtension("pg_trgm");
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(OrganizacaoInstitucionalContribuicao).Assembly);
    }
}
