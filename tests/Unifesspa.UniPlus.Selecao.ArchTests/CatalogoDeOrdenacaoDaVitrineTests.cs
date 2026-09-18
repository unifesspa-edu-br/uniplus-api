namespace Unifesspa.UniPlus.Selecao.ArchTests;

using System.Reflection;

using AwesomeAssertions;

using Unifesspa.UniPlus.Selecao.API.Controllers;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence;

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

    [Fact(DisplayName = "Todo campo declarado no contrato tem coluna mapeada, e vice-versa")]
    public void Catalogo_ContratoEMapeamento_Coincidem() =>
        // A terceira ponta da conferência, e a única cuja divergência não é cosmética: campo
        // declarado sem coluna mapeada faz `Campos[campo.Campo]` estourar KeyNotFoundException —
        // 500 numa rota anônima, na primeira vez que alguém pedir a ordenação que o contrato
        // prometeu. Campo mapeado e não declarado é recusado pela validação e nunca alcançável.
        CamposMapeados().Should().BeEquivalentTo(CamposOrdenacaoDaVitrine.Todos);

    /// <summary>
    /// Lê o mapeamento de produção por reflexão: ele é interno à infraestrutura, e expô-lo só para
    /// o teste enfraqueceria o encapsulamento que o torna seguro.
    /// </summary>
    private static IReadOnlyList<string> CamposMapeados()
    {
        Assembly infraestrutura = typeof(SelecaoDbContext).Assembly;
        Type ordenacao = infraestrutura
            .GetType("Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Repositories.Ordenacao.OrdenacaoDaVitrine")
            .Should().NotBeNull().And.Subject.As<Type>();

        object? valor = ordenacao
            .GetProperty("CamposMapeados", BindingFlags.Public | BindingFlags.Static)
            .Should().NotBeNull().And.Subject.As<PropertyInfo>()
            .GetValue(null);

        return valor.Should().BeAssignableTo<IReadOnlyList<string>>().And.Subject.As<IReadOnlyList<string>>();
    }
}
