namespace Unifesspa.UniPlus.Infrastructure.Core.Persistence;

using System.Linq.Expressions;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

using Unifesspa.UniPlus.Kernel.Domain.Interfaces;

/// <summary>
/// Convenção global de soft-delete (issue #629): aplica o filtro
/// <c>e =&gt; !e.IsDeleted</c> a todo tipo de entidade não-owned que implemente
/// <see cref="ISoftDeletable"/>. Substitui as chamadas <c>HasQueryFilter</c>
/// manuais espalhadas pelas configurações — o opt-in via interface é o único
/// critério: quem não implementa não filtra nem carrega colunas de soft-delete.
/// </summary>
public static class ModelBuilderSoftDeleteExtensions
{
    /// <summary>
    /// Chave do query filter nomeado de soft-delete. Usa filtro <b>nomeado</b>
    /// (EF Core 10) em vez de anônimo para coexistir com futuros filtros por
    /// entidade (ex.: visibilidade área-scoped, ADR-0060): o EF Core proíbe
    /// combinar um filtro anônimo e um nomeado no mesmo tipo
    /// (<c>AnonymousAndNamedFiltersCombined</c>). <c>IgnoreQueryFilters()</c>
    /// sem argumentos continua removendo todos os filtros, inclusive este.
    /// </summary>
    public const string FiltroSoftDeleteKey = "SoftDelete";

    /// <summary>
    /// Invocada no fim de cada <c>OnModelCreating</c>, após as configurações
    /// terem registrado os tipos no modelo. Owned types são ignorados (não
    /// suportam query filter); entidades sem <see cref="ISoftDeletable"/> não
    /// são tocadas.
    /// </summary>
    /// <param name="modelBuilder">O <see cref="ModelBuilder"/> em construção.</param>
    public static void AplicarFiltroGlobalSoftDelete(this ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        foreach (IMutableEntityType entityType in modelBuilder.Model.GetEntityTypes())
        {
            // Owned types compartilham a tabela do dono e não admitem query
            // filter próprio — a issue #629 exige pulá-los explicitamente.
            if (entityType.IsOwned())
            {
                continue;
            }

            if (!typeof(ISoftDeletable).IsAssignableFrom(entityType.ClrType))
            {
                continue;
            }

            LambdaExpression filtro = ConstruirFiltroNaoExcluido(entityType.ClrType);
            modelBuilder.Entity(entityType.ClrType).HasQueryFilter(FiltroSoftDeleteKey, filtro);
        }
    }

    // e => !e.IsDeleted — construído dinamicamente porque o tipo concreto só é
    // conhecido em runtime ao varrer o modelo. IsDeleted é membro de
    // ISoftDeletable, sempre presente nos tipos selecionados pelo filtro acima.
    private static LambdaExpression ConstruirFiltroNaoExcluido(Type clrType)
    {
        ParameterExpression parametro = Expression.Parameter(clrType, "e");
        MemberExpression isDeleted = Expression.Property(parametro, nameof(ISoftDeletable.IsDeleted));
        UnaryExpression naoExcluido = Expression.Not(isDeleted);
        return Expression.Lambda(naoExcluido, parametro);
    }
}
