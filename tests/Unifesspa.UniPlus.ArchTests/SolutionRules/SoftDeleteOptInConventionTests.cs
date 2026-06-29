namespace Unifesspa.UniPlus.ArchTests.SolutionRules;

using System.Linq;
using System.Reflection;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

using Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence;
using Unifesspa.UniPlus.Infrastructure.Core.Persistence;
using Unifesspa.UniPlus.Ingresso.Infrastructure.Persistence;
using Unifesspa.UniPlus.Kernel.Domain.Entities;
using Unifesspa.UniPlus.Kernel.Domain.Interfaces;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Infrastructure.Persistence;
using Unifesspa.UniPlus.Portal.Infrastructure.Persistence;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence;

/// <summary>
/// Fitness function da issue #629: soft-delete é capability opt-in via
/// <see cref="ISoftDeletable"/>. Garante as invariantes:
/// <list type="bullet">
/// <item>CA-01: <see cref="EntityBase"/> não declara membros de soft-delete;</item>
/// <item>CA-07: a convenção <c>AplicarFiltroGlobalSoftDelete</c> aplica o filtro
/// <c>!IsDeleted</c> a toda entidade <see cref="ISoftDeletable"/> dos seis
/// DbContexts e a nenhuma outra; nenhuma entidade não-soft mapeia a coluna
/// <c>is_deleted</c>.</item>
/// </list>
/// </summary>
public sealed class SoftDeleteOptInConventionTests
{
    [Fact(DisplayName = "CA-01: EntityBase não declara soft-delete; SoftDeletableEntity carrega o contrato")]
    public void EntityBase_NaoDeclaraSoftDelete()
    {
        string[] membrosSoftDelete = ["IsDeleted", "DeletedAt", "DeletedBy", "MarkAsDeleted"];

        foreach (string membro in membrosSoftDelete)
        {
            typeof(EntityBase)
                .GetMember(membro, BindingFlags.Public | BindingFlags.Instance)
                .Should().BeEmpty($"EntityBase não deve declarar '{membro}' (issue #629: soft-delete é opt-in)");
        }

        typeof(ISoftDeletable).IsAssignableFrom(typeof(EntityBase)).Should().BeFalse(
            "EntityBase não implementa ISoftDeletable — só SoftDeletableEntity o faz");
        typeof(ISoftDeletable).IsAssignableFrom(typeof(SoftDeletableEntity)).Should().BeTrue(
            "SoftDeletableEntity é a base que carrega a implementação de ISoftDeletable");
        typeof(EntityBase).IsAssignableFrom(typeof(SoftDeletableEntity)).Should().BeTrue(
            "SoftDeletableEntity estende EntityBase (soft-delete é opt-in sobre a identidade)");
    }

    [Fact(DisplayName = "CA-07: convenção aplica soft-delete a toda ISoftDeletable e a nenhuma outra (6 DbContexts)")]
    public void Convencao_SoftDelete_OptIn_Invariante()
    {
        List<string> violacoes = [];

        foreach (DbContext contexto in CriarContextos())
        {
            using (contexto)
            {
                string ctxNome = contexto.GetType().Name;

                foreach (IEntityType entityType in contexto.Model.GetEntityTypes())
                {
                    if (entityType.IsOwned())
                    {
                        continue;
                    }

                    Type clr = entityType.ClrType;
                    bool isSoftDeletable = typeof(ISoftDeletable).IsAssignableFrom(clr);
                    bool temFiltroSoftDelete = entityType.GetDeclaredQueryFilters()
                        .Any(f => f.Key == ModelBuilderSoftDeleteExtensions.FiltroSoftDeleteKey);
                    bool mapeiaColunaIsDeleted =
                        entityType.FindProperty(nameof(ISoftDeletable.IsDeleted)) is not null;

                    if (isSoftDeletable)
                    {
                        if (!typeof(SoftDeletableEntity).IsAssignableFrom(clr))
                        {
                            violacoes.Add($"{clr.FullName}: implementa ISoftDeletable mas não deriva de SoftDeletableEntity.");
                        }

                        if (!temFiltroSoftDelete)
                        {
                            violacoes.Add($"{clr.FullName} ({ctxNome}): ISoftDeletable sem o filtro de soft-delete da convenção.");
                        }

                        if (!mapeiaColunaIsDeleted)
                        {
                            violacoes.Add($"{clr.FullName} ({ctxNome}): ISoftDeletable sem coluna is_deleted mapeada.");
                        }
                    }
                    else
                    {
                        if (mapeiaColunaIsDeleted)
                        {
                            violacoes.Add($"{clr.FullName} ({ctxNome}): NÃO implementa ISoftDeletable mas mapeia coluna is_deleted.");
                        }

                        if (temFiltroSoftDelete)
                        {
                            violacoes.Add($"{clr.FullName} ({ctxNome}): NÃO implementa ISoftDeletable mas tem filtro de soft-delete.");
                        }
                    }
                }
            }
        }

        violacoes.Should().BeEmpty(
            "issue #629: soft-delete é opt-in — toda ISoftDeletable filtra por convenção e carrega is_deleted; "
            + "quem não implementa não filtra nem mapeia a coluna. Violações:\n" + string.Join("\n", violacoes));
    }

    // Constrói o modelo de cada DbContext com o provider InMemory: a convenção
    // de query filter e o mapeamento de colunas são provider-agnostic, então o
    // modelo materializado reflete fielmente o que vai a produção (Npgsql).
    private static IEnumerable<DbContext> CriarContextos()
    {
        yield return Criar<SelecaoDbContext>();
        yield return Criar<IngressoDbContext>();
        yield return Criar<PortalDbContext>();
        yield return Criar<OrganizacaoInstitucionalDbContext>();
        yield return Criar<ConfiguracaoDbContext>();
    }

    private static TContext Criar<TContext>()
        where TContext : DbContext
    {
        DbContextOptions<TContext> options = new DbContextOptionsBuilder<TContext>()
            .UseInMemoryDatabase(typeof(TContext).Name)
            .Options;

        return (TContext)Activator.CreateInstance(typeof(TContext), options)!;
    }
}
