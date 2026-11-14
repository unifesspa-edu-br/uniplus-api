namespace Unifesspa.UniPlus.OrganizacaoInstitucional.Infrastructure.Persistence;

using Microsoft.EntityFrameworkCore;

using Unifesspa.UniPlus.Infrastructure.Core.Persistence;

/// <summary>O que a Organização Institucional acrescenta ao contexto único: as configurações e a função que remove diacríticos.</summary>
public sealed class OrganizacaoInstitucionalContribuicao : IContribuicaoDoModulo
{
    public const string NomeDoEsquema = "organizacao";

    private const string EsquemaDasFuncoesUtilitarias = "public";

    public string Esquema => NomeDoEsquema;

    public void AplicarAoModelo(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(OrganizacaoInstitucionalContribuicao).Assembly);

        // A função é um utilitário global, e não do esquema do módulo: sem o esquema explícito o EF chamaria
        // organizacao.immutable_unaccent, que não existe.
        modelBuilder.HasDbFunction(typeof(FuncoesDeTextoDaOrganizacao).GetMethod(nameof(FuncoesDeTextoDaOrganizacao.ImmutableUnaccent))!)
            .HasName("immutable_unaccent")
            .HasSchema(EsquemaDasFuncoesUtilitarias);
    }
}
