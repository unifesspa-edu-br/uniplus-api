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

    /// <summary>As colunas da tabela do esquema da Seleção, inclusive as dos tipos próprios gravados na mesma tabela.</summary>
    public static IReadOnlyList<ColunaDoModelo> Colunas(string tabela)
    {
        StoreObjectIdentifier objeto = StoreObjectIdentifier.Table(tabela, "selecao");
        return [.. Montado.Value.GetEntityTypes().SelectMany(static e => e.GetProperties())
            .Select(p => (Propriedade: p, Nome: p.GetColumnName(objeto)))
            .Where(static c => c.Nome is not null)
            .Select(c => new ColunaDoModelo(c.Nome!, c.Propriedade.IsColumnNullable(objeto), c.Propriedade.GetComment(objeto)))
            .DistinctBy(static c => c.Nome)];
    }

    public static string SqlDaRestricao(Type entidade, string nome) =>
        Montado.Value.FindEntityType(entidade)!.GetCheckConstraints().Single(c => c.Name == nome).Sql;

    public static string? ComentarioDaTabela(Type entidade) => Montado.Value.FindEntityType(entidade)?.GetComment();

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

/// <summary>O que o modelo diz de uma coluna da tabela: o nome, se aceita nulo e o comentário.</summary>
internal sealed record ColunaDoModelo(string Nome, bool Anulavel, string? Comentario);
