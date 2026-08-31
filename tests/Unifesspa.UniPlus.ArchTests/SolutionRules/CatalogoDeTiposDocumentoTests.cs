namespace Unifesspa.UniPlus.ArchTests.SolutionRules;

using System.Text.RegularExpressions;

using AwesomeAssertions;

using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence.Seed;

/// <summary>
/// Guarda o seed do catálogo de tipos de documento
/// (<see cref="TipoDocumentoSeed"/>) contra edições que passariam despercebidas:
/// um tipo acrescentado sem decisão, um código que o cadastro recusaria, uma
/// categoria que não existe, ou — o mais fácil de deixar passar — um nome que
/// volte a carregar a finalidade do documento em vez de nomeá-lo.
/// </summary>
/// <remarks>
/// O seed materializa linhas por <c>HasData</c>, sem passar pela factory do
/// agregado, então nada no caminho de escrita impediria o banco de nascer com um
/// dado que o CRUD rejeitaria. Estes testes fecham essa folga sem precisar de
/// banco: rodam no mesmo job de testes unitários.
/// </remarks>
public sealed partial class CatalogoDeTiposDocumentoTests
{
    [Fact(DisplayName = "O catálogo tem os setenta tipos consolidados")]
    public void Seed_TemSetentaTipos()
    {
        TipoDocumentoSeed.Itens.Should().HaveCount(70,
            "o catálogo consolidado dos dois sistemas legados foi aprovado com esse tamanho — "
            + "acrescentar ou remover tipo é decisão, não ajuste");
    }

    [Fact(DisplayName = "Cada item do seed passa nas invariantes do agregado")]
    public void Seed_ItensRespeitamOAgregado()
    {
        // Coleta os recusados em vez de asserir item a item: a falha nomeia quais
        // códigos a factory rejeitou, em vez de dizer só que "algum" item falhou.
        string[] recusados = [.. TipoDocumentoSeed.Itens
            .Where(item => TipoDocumento.Criar(item.Codigo, item.Nome, null, item.Categoria, null, null, null).IsFailure)
            .Select(item => item.Codigo)];

        recusados.Should().BeEmpty(
            "o seed materializa linhas sem passar pela factory — um item que ela recusasse "
            + "nasceria no banco como dado que o cadastro rejeita");
    }

    [Fact(DisplayName = "O seed não repete identificador nem código")]
    public void Seed_NaoRepeteIdentidade()
    {
        TipoDocumentoSeed.Itens.Select(i => i.Id).Should().OnlyHaveUniqueItems();
        TipoDocumentoSeed.Itens.Select(i => i.Codigo).Should().OnlyHaveUniqueItems(
            "o código é chave natural e o índice único parcial rejeitaria a duplicata na migração");
    }

    [Fact(DisplayName = "Toda categoria referenciada existe no cadastro semeado")]
    public void Seed_CategoriasExistemNoCadastro()
    {
        // A categoria é código de outro cadastro, sem chave estrangeira: nada no banco
        // impediria um tipo de nascer apontando para categoria que não existe, e o
        // handler só confere na escrita pelo CRUD — não no seed.
        HashSet<string> categoriasSemeadas = [.. CategoriaDocumentoSeed.Itens.Select(c => c.Codigo)];

        string[] orfaos = [.. TipoDocumentoSeed.Itens
            .Where(item => !categoriasSemeadas.Contains(item.Categoria))
            .Select(item => $"{item.Codigo} → {item.Categoria}")];

        orfaos.Should().BeEmpty("um tipo semeado em categoria inexistente sumiria de todo filtro por categoria");
    }

    [Fact(DisplayName = "Nenhum nome carrega finalidade, competência temporal ou aplicabilidade")]
    public void Seed_NomesNaoCarregamRegra()
    {
        // O nome nomeia o documento; quando, para quem e para quê são da exigência do
        // edital. O legado embutia tudo isso no rótulo — inclusive um gatilho DNF
        // escrito em português ("candidato do sexo masculino maiores de 18 anos") —, e
        // é esse retrocesso que este teste barra.
        string[] comRegraNoNome = [.. TipoDocumentoSeed.Itens
            .Where(item => MarcaDeRegraNoNome().IsMatch(item.Nome))
            .Select(item => $"{item.Codigo}: {item.Nome}")];

        comRegraNoNome.Should().BeEmpty(
            "finalidade, recorte de público e competência temporal pertencem à exigência "
            + "documental do edital, não ao cadastro classificatório");
    }

    [Fact(DisplayName = "Os nomes seguem a uniformização do catálogo")]
    public void Seed_NomesUniformizados()
    {
        string[] foraDoPadrao = [.. TipoDocumentoSeed.Itens
            .Where(item => item.Nome.EndsWith('.')
                || item.Nome.Trim() != item.Nome
                || item.Nome != NormalizarEspacos(item.Nome))
            .Select(item => $"{item.Codigo}: '{item.Nome}'")];

        foraDoPadrao.Should().BeEmpty("sem ponto final, sem espaço supérfluo — o nome é rótulo, não frase");
    }

    [Theory(DisplayName = "O detector reconhece os rótulos do legado que a curadoria desfez")]
    [InlineData("Certidão de quitação com o Serviço Militar (candidato do sexo masculino maiores de 18 anos)")]
    [InlineData("DIPLOMA DE GRADUAÇÃO (SOMENTE PARA TRANSFERÊNCIA EXTERNA)")]
    [InlineData("DECLARAÇÃO DE RESIDÊNCIA (PARA FINS DE BÔNUS)")]
    [InlineData("DECLARAÇÃO DE IRPF FÍSICA OU JURÍDICA (2019/2020)")]
    public void Detector_ReconheceRotuloDoLegado(string nomeLegado)
    {
        // Sem isto, o teste acima passaria de vazio: uma regex que não casa com nada
        // aprova qualquer catálogo. Estes são os quatro rótulos que a curadoria citou
        // ao justificar a separação entre o que o documento é e quando ele é exigido.
        MarcaDeRegraNoNome().IsMatch(nomeLegado).Should().BeTrue();
    }

    [Theory(DisplayName = "O detector não acusa nome legítimo do catálogo")]
    [InlineData("Quitação com o serviço militar")]
    [InlineData("Declaração de imposto de renda")]
    [InlineData("Diploma de graduação")]
    [InlineData("Comprovante de residência")]
    public void Detector_NaoAcusaNomeLegitimo(string nome)
    {
        MarcaDeRegraNoNome().IsMatch(nome).Should().BeFalse();
    }

    private static string NormalizarEspacos(string valor) => EspacosRepetidos().Replace(valor, " ");

    // Parênteses e o vocabulário de recorte que o legado usava para embutir regra no
    // rótulo: público-alvo, exercício fiscal, finalidade e exclusividade.
    [GeneratedRegex(@"[()]|\b(somente|apenas|para fins de|caso|quando|maiores|menores|sexo masculino|sexo feminino)\b|\b(19|20)\d{2}\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex MarcaDeRegraNoNome();

    [GeneratedRegex(@"\s{2,}", RegexOptions.CultureInvariant)]
    private static partial Regex EspacosRepetidos();
}
