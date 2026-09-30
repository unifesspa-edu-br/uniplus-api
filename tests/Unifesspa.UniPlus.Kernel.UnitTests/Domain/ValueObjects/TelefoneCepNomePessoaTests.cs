namespace Unifesspa.UniPlus.Kernel.UnitTests.Domain.ValueObjects;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Domain.ValueObjects;

/// <summary>
/// Validação e máscara de log dos formatos de texto que o catálogo de fatos declara (ADR-0136):
/// telefone, CEP e nome de pessoa. O valor mascarado nunca expõe o dado inteiro.
/// </summary>
public sealed class TelefoneCepNomePessoaTests
{
    [Theory(DisplayName = "Telefone aceita fixo e celular com DDD e mascara expondo só DDD e final")]
    [InlineData("(94) 3322-1234", "(94) ****-1234")]
    [InlineData("94 99123-4567", "(94) *****-4567")]
    public void Telefone_Valido_Mascara(string entrada, string mascarado) =>
        Telefone.Criar(entrada).Value!.Mascarado.Should().Be(mascarado);

    [Theory(DisplayName = "Telefone sem DDD, com DDD iniciado em zero ou celular sem o 9 é recusado")]
    [InlineData("3322-1234")]
    [InlineData("(04) 3322-1234")]
    [InlineData("(94) 83322-1234")]
    [InlineData("(\u0669\u0664) \u0663\u0663\u0662\u0662-\u0661\u0662\u0663\u0664")]
    [InlineData("abc94991234567")]
    public void Telefone_Invalido_Recusa(string entrada) =>
        Telefone.Criar(entrada).Error!.Code.Should().Be("Telefone.Invalido");

    [Fact(DisplayName = "CEP com oito dígitos é aceito e mascarado pela região postal")]
    public void Cep_Valido_Mascara() => Cep.Criar("68.507-590").Value!.Mascarado.Should().Be("68***-***");

    [Theory(DisplayName = "CEP com dígitos a menos ou todos iguais é recusado")]
    [InlineData("6850759")]
    [InlineData("00000-000")]
    [InlineData("\u0666\u0668\u0665\u0660\u0667\u0665\u0669\u0660")]
    [InlineData("CEP:68507590xyz")]
    public void Cep_Invalido_Recusa(string entrada) => Cep.Criar(entrada).Error!.Code.Should().Be("Cep.Invalido");

    [Fact(DisplayName = "Nome de pessoa normaliza espaços e mascara expondo só as iniciais")]
    public void NomePessoa_Valido_Mascara()
    {
        NomePessoa nome = NomePessoa.Criar("  Maria   da  Conceição D'Ávila ").Value!;

        nome.Valor.Should().Be("Maria da Conceição D'Ávila");
        nome.Mascarado.Should().Be("M*** d* C*** D***");
    }

    [Theory(DisplayName = "Nome de pessoa em forma decomposta ou com apóstrofo tipográfico é aceito, normalizado")]
    [InlineData("Jose\u0301 Silva", "José Silva")]
    [InlineData("Joana D\u2019Arc", "Joana D'Arc")]
    public void NomePessoa_ComposicaoEApostrofo_Normaliza(string entrada, string esperado) =>
        NomePessoa.Criar(entrada).Value!.Valor.Should().Be(esperado);

    [Theory(DisplayName = "Nome de pessoa sem sobrenome ou com dígito é recusado")]
    [InlineData("Maria")]
    [InlineData("Maria 2 Silva")]
    public void NomePessoa_Invalido_Recusa(string entrada) =>
        NomePessoa.Criar(entrada).Error!.Code.Should().Be("NomePessoa.Invalido");

    [Fact(DisplayName = "E-mail mascarado expõe só a primeira letra do usuário e o domínio")]
    public void Email_Mascara() =>
        Email.Criar("joana.silva@unifesspa.edu.br").Value!.Mascarado.Should().Be("j***@unifesspa.edu.br");
}
