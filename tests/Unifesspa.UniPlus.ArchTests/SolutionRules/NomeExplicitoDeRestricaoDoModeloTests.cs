namespace Unifesspa.UniPlus.ArchTests.SolutionRules;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using Unifesspa.UniPlus.ArchTests.TestSupport;
using Unifesspa.UniPlus.Autorizacao.Domain.Entities;

/// <summary>
/// A chave estrangeira e o índice das bases da permissão na concessão e das liberações da reclassificação têm nome configurado
/// de forma explícita: o PostgreSQL corta o identificador acima de 63 bytes e o nome que o EF deriva dos nomes das tabelas e das
/// colunas passa disso. A leitura é do modelo, sem banco.
/// </summary>
public sealed class NomeExplicitoDeRestricaoDoModeloTests
{
    [Fact(DisplayName = "As três chaves estrangeiras e os índices das bases e das liberações têm o nome configurado explicitamente")]
    public void BasesELiberacoes_TemNomeConfiguradoExplicitamente()
    {
        IEntityType[] tipos = [TipoDeEntidade<BaseDaPermissaoNaConcessao>(), TipoDeEntidade<LiberacaoDaReclassificacao>()];

        tipos.SelectMany(t => t.GetForeignKeys()).Should().HaveCount(3, "uma chave da base e duas da liberação são o que a regra confere");
        tipos.SelectMany(t => t.GetIndexes()).Should().NotBeEmpty("sem índice descoberto a conferência passaria vazia");
        NomesExplicitosDoModelo.Violacoes(tipos).Should().BeEmpty();
    }

    [Fact(DisplayName = "Chave estrangeira e índice sem nome configurado são acusados, ainda que o nome derivado seja o da convenção")]
    public void NomesDerivadosPelaConvencao_SaoAcusados()
    {
        using ContextoCanario contexto = new(nomear: false);

        IReadOnlyList<string> violacoes = NomesExplicitosDoModelo.Violacoes([contexto.GetService<IDesignTimeModel>().Model.FindEntityType(typeof(Canario))!]);

        violacoes.Should().HaveCount(2);
        violacoes.Should().Contain(v => v.Contains("chave estrangeira", StringComparison.Ordinal))
            .And.Contain(v => v.Contains("índice", StringComparison.Ordinal));
    }

    [Fact(DisplayName = "Chave estrangeira e índice com nome configurado são aceitos")]
    public void NomesConfigurados_SaoAceitos()
    {
        using ContextoCanario contexto = new(nomear: true);

        NomesExplicitosDoModelo.Violacoes([contexto.GetService<IDesignTimeModel>().Model.FindEntityType(typeof(Canario))!]).Should().BeEmpty();
    }

    private static IEntityType TipoDeEntidade<T>() =>
        ModelosSemTruncamento.Modelos().Select(m => m.Modelo.FindEntityType(typeof(T))).First(t => t is not null)!;

    private sealed class Canario
    {
        public Guid Id { get; init; }

        public Guid? PaiDoRegistroId { get; init; }
    }

    // Sem cache do provedor de serviços, cada contexto constrói o próprio modelo.
    private sealed class ContextoCanario(bool nomear) : DbContext(
        new DbContextOptionsBuilder<ContextoCanario>().UseNpgsql("Host=canario").UseSnakeCaseNamingConvention()
            .EnableServiceProviderCaching(false).Options)
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            ArgumentNullException.ThrowIfNull(modelBuilder);

            modelBuilder.Entity<Canario>(canario =>
            {
                IndexBuilder<Canario> indice = canario.HasIndex(c => c.PaiDoRegistroId);
                ReferenceCollectionBuilder<Canario, Canario> chave =
                    canario.HasOne<Canario>().WithMany().HasForeignKey(c => c.PaiDoRegistroId);
                if (nomear)
                {
                    indice.HasDatabaseName("ix_canarios_pai");
                    chave.HasConstraintName("fk_canarios_pai");
                }
            });
        }
    }
}
