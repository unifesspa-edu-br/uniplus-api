namespace Unifesspa.UniPlus.ArchTests.SolutionRules;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

using Unifesspa.UniPlus.ArchTests.TestSupport;
using Unifesspa.UniPlus.Testes.Compartilhado;

/// <summary>
/// Fitness function do nome de tabela (<c>docs/guia-banco-de-dados.md</c>, ADR-0054): toda tabela mapeada de todo
/// <see cref="DbContext"/> está em <c>snake_case</c>, tem o núcleo no plural e até 30 caracteres. A mesma regra confere o
/// catálogo do banco real em <c>NomesDeTabelaDoCatalogoIntegrationTests</c>; aqui ela corre no CI, sem banco.
/// </summary>
public sealed class NomesDeTabelaDoModeloTests
{
    [Fact(DisplayName = "A descoberta encontra tabelas mapeadas")]
    public void DescobertaEncontraTabelas() =>
        TabelasMapeadas(ModelosDaSolucao.Modelos().SelectMany(m => m.Modelo.GetEntityTypes())).Should().NotBeEmpty(
            "sem tabela descoberta, a conferência dos nomes passaria vazia");

    [Fact(DisplayName = "Toda tabela mapeada tem o núcleo no plural, snake_case e até 30 caracteres")]
    public void TodaTabelaMapeadaSegueARegraDeNome()
    {
        IReadOnlyList<string> violacoes = RegraDeNomeDeTabela.Violacoes(
            TabelasMapeadas(ModelosDaSolucao.Modelos().SelectMany(m => m.Modelo.GetEntityTypes())));

        violacoes.Should().BeEmpty(
            "o nome de tabela segue o docs/guia-banco-de-dados.md: núcleo no plural, snake_case, até 30 caracteres. Violações:\n"
            + string.Join("\n", violacoes));
    }

    [Fact(DisplayName = "Tabela mapeada com núcleo no singular é recusada pela mesma conferência")]
    public void TabelaMapeadaNoSingular_EhRecusada()
    {
        ModelBuilder construtor = new();
        construtor.Entity<Canario>().ToTable("ato_normativo", "selecao");

        IReadOnlyList<string> violacoes = RegraDeNomeDeTabela.Violacoes(TabelasMapeadas(construtor.Model.GetEntityTypes().Cast<IEntityType>()));

        violacoes.Should().Equal("selecao.ato_normativo: o núcleo 'ato' não está no plural");
    }

    private static IEnumerable<(string Esquema, string Tabela)> TabelasMapeadas(IEnumerable<IEntityType> entidades) =>
        entidades
            .Where(e => e.GetTableName() is not null)
            .Select(e => (e.GetSchema() ?? string.Empty, e.GetTableName()!))
            .Distinct();

    private sealed class Canario
    {
        public Guid Id { get; init; }
    }
}
