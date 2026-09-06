namespace Unifesspa.UniPlus.Configuracao.IntegrationTests.Cursos;

using System.Diagnostics.CodeAnalysis;
using System.Reflection;

using AwesomeAssertions;

using Unifesspa.UniPlus.Configuracao.Contracts;

/// <summary>
/// Os campos que a API declara aceitar e os campos que a listagem sabe ordenar
/// são declarados em camadas diferentes — contrato de um lado, coluna do outro.
/// Estes testes são o que impede os dois de divergirem.
/// </summary>
/// <remarks>
/// Sem eles, declarar um campo sem mapear a coluna produziria um 500 na primeira
/// vez que alguém o pedisse, e mapear sem declarar deixaria um campo funcional
/// porém invisível na documentação e recusado pela validação. Nenhum dos dois
/// apareceria em teste de caminho feliz.
/// </remarks>
[SuppressMessage(
    "Performance",
    "CA1515:Consider making public types internal",
    Justification = "xUnit exige tipo de teste público.")]
public sealed class CatalogoDeOrdenacaoTests
{
    [Fact(DisplayName = "Todo campo que a API de Cursos declara tem coluna mapeada, e vice-versa")]
    public void Cursos_ContratoEMapeamento_Coincidem()
    {
        IReadOnlyList<string> mapeados = CamposMapeados("CamposDeCursoMapeados");

        mapeados.Should().BeEquivalentTo(CamposOrdenacaoCurso.Todos);
    }

    [Fact(DisplayName = "Todo campo que a API de Ofertas declara tem coluna mapeada, e vice-versa")]
    public void Ofertas_ContratoEMapeamento_Coincidem()
    {
        IReadOnlyList<string> mapeados = CamposMapeados("CamposDeOfertaMapeados");

        mapeados.Should().BeEquivalentTo(CamposOrdenacaoOfertaCurso.Todos);
    }

    [Fact(DisplayName = "Os campos declarados não se repetem")]
    public void CamposDeclarados_NaoSeRepetem()
    {
        CamposOrdenacaoCurso.Todos.Should().OnlyHaveUniqueItems();
        CamposOrdenacaoOfertaCurso.Todos.Should().OnlyHaveUniqueItems();
    }

    /// <summary>
    /// Lê a lista de produção por reflexão: o mapeamento é interno à
    /// infraestrutura, e expô-lo só para o teste enfraqueceria o encapsulamento
    /// que o torna seguro.
    /// </summary>
    private static IReadOnlyList<string> CamposMapeados(string propriedade)
    {
        Assembly infraestrutura = typeof(Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence.ConfiguracaoDbContext).Assembly;
        Type ordenacao = infraestrutura
            .GetType("Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence.Repositories.Ordenacao.OrdenacaoDeCursos")
            .Should().NotBeNull().And.Subject.As<Type>();

        object? valor = ordenacao
            .GetProperty(propriedade, BindingFlags.Public | BindingFlags.Static)
            .Should().NotBeNull().And.Subject.As<PropertyInfo>()
            .GetValue(null);

        return valor.Should().BeAssignableTo<IReadOnlyList<string>>().And.Subject.As<IReadOnlyList<string>>();
    }
}
