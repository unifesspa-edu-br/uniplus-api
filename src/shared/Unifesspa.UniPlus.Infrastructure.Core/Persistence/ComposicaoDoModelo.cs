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

        AplicarAPlataforma(modelBuilder);
        AplicarEsquemaAosTiposPertencentes(modelBuilder);
        AplicarConvencoesDoModeloCompleto(modelBuilder);
    }

    private static void AplicarAPlataforma(ModelBuilder modelBuilder)
    {
        Aplicar(
            modelBuilder,
            UniPlusDbContext.EsquemaDaPlataforma,
            () => modelBuilder.ApplyConfigurationsFromAssembly(typeof(IdempotencyEntry).Assembly));
        RegistrarFuncoesDaPlataforma(modelBuilder);
    }

    private static void AplicarEsquemaAosTiposPertencentes(ModelBuilder modelBuilder)
    {
        foreach (IMutableEntityType tipo in modelBuilder.Model.GetEntityTypes().Where(static tipo => tipo.IsOwned()))
        {
            IMutableForeignKey posse = tipo.FindOwnership()!;
            while (posse.PrincipalEntityType.IsOwned())
            {
                posse = posse.PrincipalEntityType.FindOwnership()!;
            }

            tipo.SetSchema(posse.PrincipalEntityType.GetSchema());
        }
    }

    // As convenções varrem todos os tipos do modelo, então rodam depois que os módulos e a plataforma registraram os seus.
    private static void AplicarConvencoesDoModeloCompleto(ModelBuilder modelBuilder)
    {
        modelBuilder.ConfigurarConversaoDosEnumsDeContrato();
        modelBuilder.AplicarFiltroGlobalSoftDelete();
        modelBuilder.AplicarComentariosDasColunasComuns();
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
            // O pertencente segue o esquema do dono; ao ganhar outro dono em módulo posterior, o EF renomeia o primeiro e seu novo nome receberia o esquema seguinte.
            if (!anteriores.Contains(tipo.Name) && tipo.BaseType is null && !tipo.IsOwned())
            {
                tipo.SetSchema(esquema);
            }
        }
    }
}
