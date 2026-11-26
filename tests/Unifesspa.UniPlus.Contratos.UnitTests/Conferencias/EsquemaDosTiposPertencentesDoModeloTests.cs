namespace Unifesspa.UniPlus.Contratos.UnitTests.SolutionRules;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;

using Unifesspa.UniPlus.Monolito.Persistencia;

public sealed class EsquemaDosTiposPertencentesDoModeloTests
{
    [Fact]
    public void Descoberta_ModeloDoMonolito_EncontraTiposPertencentes()
    {
        using MonolitoDbContext contexto = new MonolitoDbContextDesignTimeFactory().CreateDbContext([]);
        contexto.GetService<IDesignTimeModel>().Model.GetEntityTypes().Where(tipo => tipo.IsOwned())
            .Should().NotBeEmpty("a conferência não deve passar sem encontrar tipos pertencentes");
    }

    [Fact]
    public void Conferencia_ModeloDoMonolito_TiposPertencentesSeguemEsquemaDoDono()
    {
        using MonolitoDbContext contexto = new MonolitoDbContextDesignTimeFactory().CreateDbContext([]);
        IReadOnlyList<string> violacoes = ViolacoesDeEsquema(contexto.GetService<IDesignTimeModel>().Model.GetEntityTypes());

        violacoes.Should().BeEmpty("cada tipo pertencente deve seguir o esquema do seu dono: {0}", string.Join("; ", violacoes));
    }

    [Fact]
    public void Conferencia_PertencenteEmOutroEsquema_AcusaViolacao()
    {
        ModelBuilder construtor = new();
        construtor.Entity<Dono>().ToTable("donos", "primeiro")
            .OwnsOne(dono => dono.Detalhe, detalhe => detalhe.ToTable("detalhes", "segundo"));

        IReadOnlyList<string> violacoes = ViolacoesDeEsquema(construtor.Model.GetEntityTypes().Cast<IEntityType>());

        violacoes.Should().ContainSingle().Which.Should().Contain("segundo").And.Contain("primeiro");
    }

    private static List<string> ViolacoesDeEsquema(IEnumerable<IEntityType> tipos) =>
        tipos.Where(tipo => tipo.IsOwned() && tipo.GetSchema() != tipo.FindOwnership()!.PrincipalEntityType.GetSchema())
            .Select(tipo => $"{tipo.Name}: esquema '{tipo.GetSchema()}', dono '{tipo.FindOwnership()!.PrincipalEntityType.GetSchema()}'")
            .ToList();

    private sealed class Dono
    {
        public Guid Id { get; init; }
        public Detalhe Detalhe { get; init; } = null!;
    }

    private sealed class Detalhe
    {
        public string Valor { get; init; } = string.Empty;
    }
}
