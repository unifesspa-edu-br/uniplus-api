namespace Unifesspa.UniPlus.Infrastructure.Core.Persistence;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

using Unifesspa.UniPlus.Infrastructure.Core.Idempotency;

internal static class ComposicaoDoModelo
{
    public static void Compor(ModelBuilder modelBuilder, IReadOnlyList<IContribuicaoDoModulo> modulos)
    {
        ValidarEsquemas(modulos);

        foreach (IContribuicaoDoModulo modulo in modulos)
        {
            Aplicar(modelBuilder, modulo.Esquema, () => modulo.AplicarAoModelo(modelBuilder));
        }

        Aplicar(
            modelBuilder,
            UniPlusDbContext.EsquemaDaPlataforma,
            () => modelBuilder.ApplyConfigurationsFromAssembly(typeof(IdempotencyEntry).Assembly));
        RegistrarFuncoesDaPlataforma(modelBuilder);

        modelBuilder.AplicarFiltroGlobalSoftDelete();
    }

    // Sem este registro a consulta que chama FuncoesDaPlataforma.NormalizarParaComparacao não traduz, e o EF tentaria avaliá-la
    // no cliente, onde o stub lança.
    private static void RegistrarFuncoesDaPlataforma(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDbFunction(typeof(FuncoesDaPlataforma).GetMethod(nameof(FuncoesDaPlataforma.NormalizarParaComparacao))!)
            .HasName("normalizar_para_comparacao")
            .HasSchema(UniPlusDbContext.EsquemaDaPlataforma);
    }

    private static void ValidarEsquemas(IReadOnlyList<IContribuicaoDoModulo> modulos)
    {
        HashSet<string> vistos = [UniPlusDbContext.EsquemaDaPlataforma];

        foreach (IContribuicaoDoModulo modulo in modulos)
        {
            if (!vistos.Add(modulo.Esquema))
            {
                throw new InvalidOperationException(
                    $"O esquema '{modulo.Esquema}' já pertence a outro módulo ou à plataforma; cada módulo tem o seu.");
            }
        }
    }

    private static void Aplicar(ModelBuilder modelBuilder, string esquema, Action registrar)
    {
        HashSet<string> anteriores = [.. modelBuilder.Model.GetEntityTypes().Select(static t => t.Name)];

        registrar();

        foreach (IMutableEntityType tipo in modelBuilder.Model.GetEntityTypes())
        {
            if (!anteriores.Contains(tipo.Name) && tipo.BaseType is null)
            {
                tipo.SetSchema(esquema);
            }
        }
    }
}
