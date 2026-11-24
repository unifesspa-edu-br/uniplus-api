namespace Unifesspa.UniPlus.OrganizacaoInstitucional.IntegrationTests.Unidades;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;

using Unifesspa.UniPlus.Infrastructure.Core.Persistence;
using Unifesspa.UniPlus.Monolito.Persistencia;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Domain.Entities;

/// <summary>
/// O modelo efetivo do EF para a busca textual da Unidade: cinco colunas geradas pela função de normalização da
/// plataforma, cada uma com o seu índice GIN de trigramas, e a extensão que os índices pedem. Lido pelo modelo, sem
/// banco: é o que a migration reproduz e o que <c>has-pending-model-changes</c> confere.
/// </summary>
public sealed class ModeloDaBuscaDeUnidadesTests
{
    private static readonly IEntityType TipoDaUnidade = LerTipoDaUnidade();

    public static IEnumerable<object[]> Colunas =>
    [
        ["nome", nameof(Unidade.Nome), "NomeParaBusca", false],
        ["sigla", nameof(Unidade.Sigla), "SiglaParaBusca", false],
        ["codigo", nameof(Unidade.Codigo), "CodigoParaBusca", false],
        ["identificador_no_endereco_web", nameof(Unidade.Slug), "IdentificadorNoEnderecoWebParaBusca", false],
        ["apelido", nameof(Unidade.Alias), "ApelidoParaBusca", true],
    ];

    public static IEnumerable<object[]> Propriedades => Colunas.Select(linha => new[] { linha[0], linha[2] });

    [Theory(DisplayName = "A propriedade para busca é gerada pela função da plataforma, guardada e na collation C")]
    [MemberData(nameof(Colunas))]
    public void Propriedade_EhGeradaPelaFuncaoDaPlataforma(string coluna, string origem, string propriedade, bool anulavel)
    {
        IProperty gerada = TipoDaUnidade.FindProperty(propriedade)!;

        gerada.GetComputedColumnSql().Should().Be(NormalizacaoTextual.ExpressaoSql(coluna));
        gerada.GetIsStored().Should().BeTrue();
        gerada.GetCollation().Should().Be("C");
        gerada.GetMaxLength().Should().Be(TipoDaUnidade.FindProperty(origem)!.GetMaxLength());
        gerada.IsNullable.Should().Be(anulavel);
    }

    [Theory(DisplayName = "A propriedade para busca tem um índice GIN de trigramas com o nome do campo")]
    [MemberData(nameof(Propriedades))]
    public void Propriedade_TemIndiceGinDeTrigramas(string coluna, string propriedade)
    {
        IIndex indice = TipoDaUnidade.GetIndexes()
            .Single(i => i.Properties.Count == 1 && i.Properties[0].Name == propriedade);

        indice.GetDatabaseName().Should().Be($"ix_unidades_{coluna}_para_busca_trgm");
        indice.GetMethod().Should().Be("gin");
        indice.GetOperators().Should().Equal("gin_trgm_ops");
    }

    [Fact(DisplayName = "O modelo declara a extensão de trigramas que os índices pedem")]
    public void Modelo_DeclaraAExtensaoDeTrigramas()
    {
        TipoDaUnidade.Model.GetPostgresExtensions().Select(extensao => extensao.Name).Should().Contain("pg_trgm");
    }

    private static IEntityType LerTipoDaUnidade()
    {
        using MonolitoDbContext contexto = new(UniPlusDbContextOptionsExtensions.BuildDesignTimeOptions<MonolitoDbContext>());
        return contexto.GetService<IDesignTimeModel>().Model.FindEntityType(typeof(Unidade))!;
    }
}
