namespace Unifesspa.UniPlus.Selecao.Infrastructure.UnitTests.Persistence;

using System.Text.Json;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Infrastructure.UnitTests.TestSupport;

public sealed class CamposPublicosGravadosTests
{
    private static ValueConverter Conversor => ModeloDaSelecao.Propriedade(typeof(ConfiguracaoDivulgacao), nameof(ConfiguracaoDivulgacao.CamposPublicos)).GetValueConverter()!;
    private static ValueComparer Comparador => ModeloDaSelecao.Propriedade(typeof(ConfiguracaoDivulgacao), nameof(ConfiguracaoDivulgacao.CamposPublicos)).GetValueComparer()!;

    [Theory(DisplayName = "Campos públicos nulos ou vazios são recusados na leitura")]
    [InlineData("null")]
    [InlineData("[]")]
    public void Conversor_CamposNulosOuVazios_RecusaDadoGravado(string json)
    {
        Action leitura = () => Conversor.ConvertFromProvider(json);
        leitura.Should().Throw<InvalidOperationException>().WithMessage("*campos_publicos*");
    }

    [Theory(DisplayName = "Texto vazio ou JSON malformado de campos públicos é recusado")]
    [InlineData("")]
    [InlineData("[")]
    public void Conversor_JsonInvalido_RecusaJson(string json)
    {
        Action leitura = () => Conversor.ConvertFromProvider(json);
        leitura.DeveRecusarComoDocumentoInvalido(nameof(ConfiguracaoDivulgacao.CamposPublicos));
    }

    [Fact(DisplayName = "Campos públicos válidos são lidos")]
    public void Conversor_CamposValidos_RetornaCampos()
    {
        var campos = (IReadOnlyList<string>)Conversor.ConvertFromProvider("[\"NUMERO_INSCRICAO\",\"NOME\"]")!;
        campos.Should().Equal("NUMERO_INSCRICAO", "NOME");
    }

    [Fact(DisplayName = "Comparador copia lista vazia sem validar leitura do banco")]
    public void Comparador_ListaVazia_CopiaSemRecusar()
    {
        IReadOnlyList<string> original = new List<string>();
        var copia = (IReadOnlyList<string>)Comparador.Snapshot(original)!;
        copia.Should().BeEmpty().And.NotBeSameAs(original);
    }
}
