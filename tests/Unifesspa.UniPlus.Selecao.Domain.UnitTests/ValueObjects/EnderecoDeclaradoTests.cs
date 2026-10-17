namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.ValueObjects;

using System.Text.Json;

using AwesomeAssertions;

using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

public sealed class EnderecoDeclaradoTests
{
    [Fact(DisplayName = "A confirmação do CEP (cepConfirmado) é lida como booleano e volta no JSON do endereço")]
    public void CepConfirmado_LidoEDevolvido()
    {
        JsonElement json = JsonSerializer.SerializeToElement(new { cep = "68627476", cepConfirmado = true });

        EnderecoDeclarado.TentarLer(json, out EnderecoDeclarado endereco).Should().BeTrue();

        endereco.CepConfirmado.Should().BeTrue();
        endereco.ParaJson().GetProperty("cepConfirmado").GetBoolean().Should().BeTrue();
    }

    [Fact(DisplayName = "A confirmação do CEP que não é booleano nem nulo recusa o endereço")]
    public void CepConfirmado_Texto_Recusa()
    {
        JsonElement json = JsonSerializer.SerializeToElement(new { cep = "68627476", cepConfirmado = "sim" });

        EnderecoDeclarado.TentarLer(json, out _).Should().BeFalse();
    }
}
