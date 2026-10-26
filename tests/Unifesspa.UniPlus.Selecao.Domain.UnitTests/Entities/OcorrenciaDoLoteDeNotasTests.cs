namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.Entities;

using AwesomeAssertions;

using Unifesspa.UniPlus.Selecao.Domain.Entities;

/// <summary>A linha sem inscrito só entra com o CPF já mascarado: o completo nunca chega à entidade.</summary>
public sealed class OcorrenciaDoLoteDeNotasTests
{
    [Theory(DisplayName = "linha sem inscrito recusa CPF ausente, completo ou fora da máscara")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("12345678909")]
    [InlineData("123.456.789-09")]
    public void LinhaSemInscrito_CpfNaoMascarado_Recusa(string? cpf)
    {
        Action criar = () => OcorrenciaDoLoteDeNotas.LinhaSemInscrito(Guid.CreateVersion7(), 3, cpf!, null, null);

        criar.Should().Throw<ArgumentException>();
    }

    [Fact(DisplayName = "linha sem inscrito guarda o CPF mascarado, o nome e o sexo do arquivo")]
    public void LinhaSemInscrito_CpfMascarado_Guarda()
    {
        OcorrenciaDoLoteDeNotas ocorrencia = OcorrenciaDoLoteDeNotas.LinhaSemInscrito(Guid.CreateVersion7(), 3, "***.456.789-**", "Nome do Arquivo", "F");

        ocorrencia.CpfMascarado.Should().Be("***.456.789-**");
        ocorrencia.Nome.Should().Be("Nome do Arquivo");
        ocorrencia.Sexo.Should().Be("F");
    }
}
