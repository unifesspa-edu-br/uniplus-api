namespace Unifesspa.UniPlus.Kernel.UnitTests.Domain.ValueObjects;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Domain.ValueObjects;

public sealed class CodigoDePermissaoTests
{
    [Theory(DisplayName = "O código com três segmentos em kebab-case é aceito, e o segmento pode começar por dígito")]
    [InlineData("teste:itens:manter")]
    [InlineData("selecao:processos-seletivos:consultar")]
    [InlineData("selecao:2a-fase:analisar")]
    [InlineData("0:1:2")]
    public void TentarCriar_FormatoValido_Aceita(string texto)
    {
        bool criou = CodigoDePermissao.TentarCriar(texto, out CodigoDePermissao? codigo);

        criou.Should().BeTrue();
        codigo!.Valor.Should().Be(texto);
    }

    [Theory(DisplayName = "O texto fora do formato é recusado, sem nenhuma correção")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(" teste:itens:manter")]
    [InlineData("teste:itens:manter ")]
    [InlineData("Teste:itens:manter")]
    [InlineData("teste:itens:mantér")]
    [InlineData("teste:itens")]
    [InlineData("teste:itens:manter:extra")]
    [InlineData("teste::manter")]
    [InlineData("teste:itens:")]
    [InlineData(":itens:manter")]
    [InlineData("-teste:itens:manter")]
    [InlineData("teste:itens-:manter")]
    [InlineData("teste:it--ens:manter")]
    [InlineData("teste:itens:manter\n")]
    public void TentarCriar_FormatoInvalido_Recusa(string? texto)
    {
        bool criou = CodigoDePermissao.TentarCriar(texto, out CodigoDePermissao? codigo);

        criou.Should().BeFalse();
        codigo.Should().BeNull();
    }

    [Fact(DisplayName = "O módulo, o resource e a ação são os três segmentos, e ToString devolve o código inteiro")]
    public void Partes_SaoOsSegmentos()
    {
        CodigoDePermissao codigo = CodigoDePermissao.De("selecao:processos-seletivos:consultar");

        codigo.Modulo.Should().Be("selecao");
        codigo.Resource.Should().Be("processos-seletivos");
        codigo.Acao.Should().Be("consultar");
        codigo.ToString().Should().Be("selecao:processos-seletivos:consultar");
    }

    [Fact(DisplayName = "Dois códigos do mesmo texto são iguais e servem de chave de dicionário")]
    public void Igualdade_PorValor()
    {
        Dictionary<CodigoDePermissao, int> indice = new() { [CodigoDePermissao.De("teste:itens:manter")] = 1 };

        CodigoDePermissao.De("teste:itens:manter").Should().Be(CodigoDePermissao.De("teste:itens:manter"));
        indice[CodigoDePermissao.De("teste:itens:manter")].Should().Be(1);
        CodigoDePermissao.De("teste:itens:ler").Should().NotBe(CodigoDePermissao.De("teste:itens:manter"));
    }

    [Theory(DisplayName = "De falha alto com o texto fora do formato")]
    [InlineData("")]
    [InlineData("teste:itens")]
    [InlineData("Teste:itens:manter")]
    public void De_FormatoInvalido_Lanca(string texto)
    {
        Action criar = () => CodigoDePermissao.De(texto);

        criar.Should().Throw<ArgumentException>();
    }

    [Fact(DisplayName = "De falha alto com o texto nulo")]
    public void De_Nulo_Lanca()
    {
        Action criar = () => CodigoDePermissao.De(null!);

        criar.Should().Throw<ArgumentNullException>();
    }
}
