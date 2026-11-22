namespace Unifesspa.UniPlus.Contratos.UnitTests.SolutionRules;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

using Unifesspa.UniPlus.Infrastructure.Core.Persistence;
using Unifesspa.UniPlus.Kernel.Domain.Entities;
using Unifesspa.UniPlus.Kernel.Domain.Interfaces;
using Unifesspa.UniPlus.Monolito.Persistencia;
using Unifesspa.UniPlus.Portal.Infrastructure.Persistence;

/// <summary>
/// Fitness function da issue #629: soft-delete é capability opt-in via
/// <see cref="ISoftDeletable"/>. Garante as invariantes:
/// <list type="bullet">
/// <item>CA-07: a convenção <c>AplicarFiltroGlobalSoftDelete</c> aplica o filtro
/// <c>!IsDeleted</c> a toda entidade <see cref="ISoftDeletable"/> do contexto único dos
/// módulos e do Portal, e a nenhuma outra; nenhuma entidade não-soft mapeia a coluna
/// <c>is_deleted</c>.</item>
/// </list>
/// </summary>
public sealed class SoftDeleteOptInConventionTests
{
    [Fact(DisplayName = "CA-07: convenção aplica soft-delete a toda ISoftDeletable e a nenhuma outra (contexto único e Portal)")]
    public void Convencao_SoftDelete_OptIn_Invariante()
    {
        List<string> violacoes = [];

        foreach (DbContext contexto in CriarContextos())
        {
            using (contexto)
            {
                violacoes.AddRange(ConferirModelo(contexto));
            }
        }

        violacoes.Should().BeEmpty(
            "issue #629: soft-delete é opt-in — toda ISoftDeletable filtra por convenção e carrega is_deleted; "
            + "quem não implementa não filtra nem mapeia a coluna. Violações:\n" + string.Join("\n", violacoes));
    }

    private static IEnumerable<string> ConferirModelo(DbContext contexto) =>
        contexto.Model.GetEntityTypes().Where(static entidade => !entidade.IsOwned())
            .SelectMany(entidade => ConferirEntidade(entidade, contexto.GetType().Name));

    private static IEnumerable<string> ConferirEntidade(IEntityType entidade, string contexto)
    {
        Type tipo = entidade.ClrType;
        bool implementa = typeof(ISoftDeletable).IsAssignableFrom(tipo);
        bool filtro = entidade.GetDeclaredQueryFilters().Any(f => f.Key == ModelBuilderSoftDeleteExtensions.FiltroSoftDeleteKey);
        bool coluna = entidade.FindProperty(nameof(ISoftDeletable.IsDeleted)) is not null;
        return ConferirCompatibilidade(tipo, contexto, implementa, filtro, coluna);
    }

    private static IEnumerable<string> ConferirCompatibilidade(Type tipo, string contexto, bool implementa, bool filtro, bool coluna)
    {
        if (implementa && !typeof(SoftDeletableEntity).IsAssignableFrom(tipo))
            yield return $"{tipo.FullName}: implementa ISoftDeletable mas não deriva de SoftDeletableEntity.";
        if (implementa != filtro)
            yield return $"{tipo.FullName} ({contexto}): filtro de soft-delete incompatível com ISoftDeletable.";
        if (implementa != coluna)
            yield return $"{tipo.FullName} ({contexto}): coluna is_deleted incompatível com ISoftDeletable.";
    }

    // Constrói o modelo de cada DbContext com o provider InMemory: a convenção
    // de query filter e o mapeamento de colunas são provider-agnostic, então o
    // modelo materializado reflete fielmente o que vai a produção (Npgsql).
    private static IEnumerable<DbContext> CriarContextos()
    {
        yield return new MonolitoDbContext(new DbContextOptionsBuilder<MonolitoDbContext>().UseInMemoryDatabase(nameof(MonolitoDbContext)).Options);
        yield return new PortalDbContext(new DbContextOptionsBuilder<PortalDbContext>().UseInMemoryDatabase(nameof(PortalDbContext)).Options);
    }
}
