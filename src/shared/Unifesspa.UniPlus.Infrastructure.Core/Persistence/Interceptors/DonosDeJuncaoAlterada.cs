namespace Unifesspa.UniPlus.Infrastructure.Core.Persistence.Interceptors;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata;

using Unifesspa.UniPlus.Kernel.Domain.Entities;

/// <summary>
/// Editar só uma coleção muitos-para-muitos muda apenas as linhas da tabela de junção, e o dono da
/// navegação continuaria <see cref="EntityState.Unchanged"/>: o banco não conferiria a versão da linha dele
/// (<c>xmin</c>) e duas edições concorrentes do mesmo conjunto se combinariam em silêncio. O dono declarante
/// de cada navegação cuja junção mudou passa a <see cref="EntityState.Modified"/>.
/// </summary>
/// <remarks>
/// Marca-se só <see cref="EntityBase.UpdatedAt"/>, que o carimbo regrava em seguida, e só o lado que declara
/// a navegação: o outro lado da junção (por exemplo, o catálogo referenciado) não é editado por quem muda o vínculo.
/// As navegações e os donos são indexados uma vez por gravação, para que substituir um conjunto grande custe
/// uma passada, e não uma por vínculo.
/// </remarks>
internal static class DonosDeJuncaoAlterada
{
    public static void Marcar(DbContext context)
    {
        List<EntityEntry> alteradas = [.. context.ChangeTracker.Entries()
            .Where(static e => e.State is EntityState.Added or EntityState.Deleted && e.Metadata.HasSharedClrType)];
        if (alteradas.Count == 0)
        {
            return;
        }

        ILookup<IEntityType, IForeignKey> chavesPorJuncao = ChavesParaOsDonosPorJuncao(context.Model);
        Dictionary<(IEntityType Tipo, string Chave), EntityEntry> donos = DonosSemAlteracao(context);
        foreach (EntityEntry juncao in alteradas)
        {
            foreach (IForeignKey paraODono in chavesPorJuncao[juncao.Metadata])
            {
                string chave = Chave(paraODono.Properties.Select(p => juncao.Property(p.Name).CurrentValue));
                if (donos.TryGetValue((paraODono.PrincipalEntityType, chave), out EntityEntry? dono))
                {
                    dono.Property(nameof(EntityBase.UpdatedAt)).IsModified = true;
                }
            }
        }
    }

    private static ILookup<IEntityType, IForeignKey> ChavesParaOsDonosPorJuncao(IModel modelo) =>
        modelo.GetEntityTypes()
            .SelectMany(static tipo => tipo.GetDeclaredSkipNavigations())
            .Where(static navegacao => !navegacao.IsShadowProperty())
            .ToLookup(static navegacao => navegacao.JoinEntityType, static navegacao => navegacao.ForeignKey);

    private static Dictionary<(IEntityType Tipo, string Chave), EntityEntry> DonosSemAlteracao(DbContext context)
    {
        Dictionary<(IEntityType Tipo, string Chave), EntityEntry> donos = [];
        foreach (EntityEntry entrada in context.ChangeTracker.Entries()
            .Where(static e => e is { State: EntityState.Unchanged, Entity: EntityBase } && e.Metadata.FindPrimaryKey() is not null))
        {
            string chave = Chave(entrada.Metadata.FindPrimaryKey()!.Properties.Select(p => entrada.Property(p.Name).CurrentValue));
            donos[(entrada.Metadata, chave)] = entrada;
        }

        return donos;
    }

    private static string Chave(IEnumerable<object?> valores) =>
        string.Join('\u001f', valores.Select(static v => Convert.ToString(v, System.Globalization.CultureInfo.InvariantCulture)));
}
