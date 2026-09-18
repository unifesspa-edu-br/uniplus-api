namespace Unifesspa.UniPlus.Selecao.ArchTests;

using AwesomeAssertions;

using Unifesspa.UniPlus.Selecao.API.Controllers;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;

/// <summary>
/// O catálogo de campos de ordenação da vitrine aparece em três lugares, e os três precisam
/// concordar: o contrato que o declara, o mapa que traduz cada nome numa coluna, e a descrição que
/// o cliente lê.
/// </summary>
/// <remarks>
/// Cada divergência tem um sintoma próprio e nenhum deles quebra a construção sozinho. Campo
/// declarado e não mapeado é recusa que o contrato prometeu aceitar — ou, pior, uma exceção de
/// chave ausente. Campo mapeado e não declarado nunca é alcançável. Campo fora da descrição existe
/// e ninguém descobre.
/// </remarks>
public sealed class CatalogoDeOrdenacaoDaVitrineTests
{
    [Fact(DisplayName = "Todo campo declarado no contrato aparece na descrição que o cliente lê")]
    public void Catalogo_QuandoCampoEDeclarado_DeveAparecerNaDescricao() =>
        CamposOrdenacaoDaVitrine.Todos.Should().AllSatisfy(campo =>
            CertamePublicadoController.DescricaoDoSort.Should().Contain(
                campo,
                "o contrato precisa anunciar exatamente os campos que a rota aceita"));

    [Fact(DisplayName = "A descrição não anuncia campo que o catálogo não tem")]
    public void Descricao_QuandoListaOsCampos_NaoDeveAnunciarCampoForaDoCatalogo()
    {
        // O outro sentido da conferência: um campo removido do catálogo que sobra no texto faz o
        // contrato oferecer uma ordenação que a rota recusa.
        string listados = CertamePublicadoController.DescricaoDoSort;
        int inicio = listados.IndexOf("Campos aceitos: ", StringComparison.Ordinal) + "Campos aceitos: ".Length;
        int fim = listados.IndexOf(". Sem o parâmetro", StringComparison.Ordinal);

        string[] naDescricao = [.. listados[inicio..fim].Split(',', StringSplitOptions.TrimEntries)];

        naDescricao.Should().BeEquivalentTo(CamposOrdenacaoDaVitrine.Todos);
    }
}
