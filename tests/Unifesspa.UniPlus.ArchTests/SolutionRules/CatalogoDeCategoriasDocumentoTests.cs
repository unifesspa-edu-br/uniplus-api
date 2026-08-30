namespace Unifesspa.UniPlus.ArchTests.SolutionRules;

using AwesomeAssertions;

using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence.Seed;

/// <summary>
/// Guarda o seed das categorias de documento (<see cref="CategoriaDocumentoSeed"/>)
/// contra edições que passariam despercebidas: uma categoria acrescentada ou
/// renomeada sem decisão, um código que o cadastro recusaria, ou duas categorias
/// disputando a mesma posição de exibição.
/// </summary>
/// <remarks>
/// O seed materializa linhas por <c>HasData</c>, sem passar pela factory do
/// agregado — então nada no caminho de escrita impediria o banco de nascer com um
/// dado que o CRUD rejeitaria. Estes testes fecham essa folga sem precisar de
/// banco: rodam no mesmo job de testes unitários.
/// </remarks>
public sealed class CatalogoDeCategoriasDocumentoTests
{
    [Fact(DisplayName = "O seed é exatamente o catálogo aprovado — código, nome e ordem")]
    public void Seed_BateComOCatalogoAprovado()
    {
        // Oráculo independente da fonte única: comparar o banco contra
        // CategoriaDocumentoSeed pega migration ausente, mas não pega uma edição
        // feita na lista e no HasData no mesmo commit. Esta tabela é o que a
        // decisão de negócio aprovou; alterá-la é ato deliberado.
        (string Codigo, string Nome, int Ordem)[] aprovadas =
        [
            ("IDENTIFICACAO", "Identificação", 1),
            ("ESCOLARIDADE", "Escolaridade", 2),
            ("TITULACAO_EXPERIENCIA", "Titulação e experiência", 3),
            ("RENDA", "Renda", 4),
            ("RESIDENCIA", "Residência", 5),
            ("RACA_ETNIA", "Raça/etnia", 6),
            ("SAUDE", "Saúde", 7),
            ("DOCUMENTO_PROCESSUAL", "Documento processual", 8),
            ("PRODUCAO_AVALIATIVA", "Produção avaliativa", 9),
            ("OUTROS", "Outros", 10),
        ];

        CategoriaDocumentoSeed.Itens
            .Select(item => (item.Codigo, item.Nome, item.Ordem))
            .Should().Equal(aprovadas);
    }

    [Fact(DisplayName = "Cada item do seed passa nas invariantes do agregado")]
    public void Seed_ItensRespeitamOAgregado()
    {
        // Coleta os recusados em vez de asserir item a item: a falha nomeia quais
        // códigos a factory rejeitou, em vez de dizer só que "algum" item falhou.
        string[] recusados = [.. CategoriaDocumentoSeed.Itens
            .Where(item => CategoriaDocumento.Criar(item.Codigo, item.Nome, null, item.Ordem).IsFailure)
            .Select(item => item.Codigo)];

        recusados.Should().BeEmpty(
            "o seed materializa linhas sem passar pela factory — um item que ela recusasse "
            + "nasceria no banco como dado que o cadastro rejeita");
    }

    [Fact(DisplayName = "O seed não repete identificador, código nem ordem")]
    public void Seed_NaoRepeteIdentidadeNemOrdem()
    {
        CategoriaDocumentoSeed.Itens.Select(i => i.Id).Should().OnlyHaveUniqueItems();
        CategoriaDocumentoSeed.Itens.Select(i => i.Codigo).Should().OnlyHaveUniqueItems(
            "o código é chave natural e o índice único parcial rejeitaria a duplicata na migração");
        CategoriaDocumentoSeed.Itens.Select(i => i.Ordem).Should().OnlyHaveUniqueItems(
            "ordem repetida deixaria a exibição dependente do desempate por código, "
            + "não da decisão do operador");
    }
}
