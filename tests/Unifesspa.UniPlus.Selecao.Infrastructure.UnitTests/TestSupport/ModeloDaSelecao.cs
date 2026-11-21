namespace Unifesspa.UniPlus.Selecao.Infrastructure.UnitTests.TestSupport;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;

using Unifesspa.UniPlus.Infrastructure.Core.Persistence;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence;

/// <summary>O modelo do EF que a Seleção contribui, montado sem abrir conexão com o banco.</summary>
internal static class ModeloDaSelecao
{
    private static readonly Lazy<IModel> Montado = new(Montar);

    public static IProperty Propriedade(Type entidade, string nome)
    {
        ArgumentNullException.ThrowIfNull(entidade);

        return Montado.Value.FindEntityType(entidade)?.FindProperty(nome)
            ?? throw new InvalidOperationException($"A propriedade {entidade.Name}.{nome} não está no modelo.");
    }

    private static IModel Montar()
    {
        DbContextOptions<ContextoDaSelecao> opcoes = new DbContextOptionsBuilder<ContextoDaSelecao>()
            .UseNpgsql("Host=modelo-sem-banco;Database=modelo;Username=x;Password=x")
            .Options;
        using ContextoDaSelecao contexto = new(opcoes);

        return contexto.GetService<IDesignTimeModel>().Model;
    }

    private sealed class ContextoDaSelecao(DbContextOptions<ContextoDaSelecao> opcoes) : UniPlusDbContext(opcoes)
    {
        protected override IReadOnlyList<IContribuicaoDoModulo> Contribuicoes { get; } = [new SelecaoContribuicao()];
    }
}
