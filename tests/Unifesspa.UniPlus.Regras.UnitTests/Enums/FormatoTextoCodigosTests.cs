namespace Unifesspa.UniPlus.Regras.UnitTests.Enums;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Regras.Enums;

public sealed class FormatoTextoCodigosTests
{
    private static readonly (FormatoTexto Valor, string Codigo, string Rotulo)[] Formatos =
    [
        (FormatoTexto.Livre, "LIVRE", "Texto livre"),
        (FormatoTexto.Cpf, "CPF", "CPF"),
        (FormatoTexto.Email, "EMAIL", "E-mail"),
        (FormatoTexto.Telefone, "TELEFONE", "Telefone"),
        (FormatoTexto.Cep, "CEP", "CEP"),
        (FormatoTexto.NomePessoa, "NOME_PESSOA", "Nome de pessoa"),
    ];

    [Fact(DisplayName = "Cada formato de texto tem o código e o rótulo que o contrato publica")]
    public void FormatoTexto_CodigoERotuloDeCadaValor() => ConferirPares(Formatos);

    [Theory(DisplayName = "O código de formato fora do contrato não é reconhecido")]
    [InlineData("livre")]
    [InlineData("NENHUM")]
    public void FormatoTexto_CodigoForaDoContrato_NaoEhReconhecido(string codigo) =>
        CatalogoDe<FormatoTexto>.TentarObter(codigo, out _).Should().BeFalse();

    private static void ConferirPares<TEnum>((TEnum Valor, string Codigo, string Rotulo)[] esperados)
        where TEnum : struct, Enum
    {
        CatalogoDe<TEnum>.Valores().Should().Equal(esperados.Select(static item => item.Valor));
        foreach ((TEnum valor, string codigo, string rotulo) in esperados)
        {
            valor.Codigo().Should().Be(codigo);
            valor.Rotulo().Should().Be(rotulo);
            CatalogoDe<TEnum>.TentarObter(codigo, out TEnum obtido).Should().BeTrue();
            obtido.Should().Be(valor);
        }
    }
}
