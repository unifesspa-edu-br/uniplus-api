namespace Unifesspa.UniPlus.Selecao.Infrastructure.UnitTests.Canonicalization;

using System.Text.Json.Nodes;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Infrastructure.Canonicalization;

public sealed class PropagacaoDasCausasNoLeitorTests
{
    [Theory]
    [InlineData(null, "itens[0]")]
    [InlineData("", "itens[0]")]
    [InlineData("valor", "itens[0].valor")]
    public void Propagar_CampoRelativo_SituaCausaNoItem(string? campo, string caminhoEsperado)
    {
        LeitorEnvelope leitor = new();
        FieldError causa = new(campo, new DomainError("codigo", "mensagem"));

        leitor.Propagar<object>([causa], "itens[0]");

        leitor.Falha<object>().Errors.Should().ContainSingle().Which.Field.Should().Be(caminhoEsperado);
    }

    [Fact]
    public void Propagar_MultiplasCausas_PreservaOrdemEDetalhes()
    {
        LeitorEnvelope leitor = new();
        DomainError primeiroErro = new("primeiro", "primeira mensagem");
        FieldError[] causas = [new(null, primeiroErro, "origem"), new("valor", new DomainError("segundo", "segunda mensagem"))];

        leitor.Propagar<object>(causas, "item");
        Result<object> resultado = leitor.Falha<object>();

        leitor.Erro.Should().Be(primeiroErro);
        resultado.Error.Should().Be(primeiroErro);
        resultado.Errors.Should().Equal(causas[0] with { Field = "item" }, causas[1] with { Field = "item.valor" });
    }

    [Fact]
    public void Propagar_AposRecusa_PreservaErroAnterior()
    {
        LeitorEnvelope leitor = new();
        leitor.Recusar<object>("recusa anterior");
        DomainError erroAnterior = leitor.Erro;

        leitor.Propagar<object>([new FieldError("valor", new DomainError("codigo", "mensagem"))], "item");

        leitor.Erro.Should().Be(erroAnterior);
        leitor.Falha<object>().Errors.Should().ContainSingle().Which.Should().Be(new FieldError(null, erroAnterior));
    }

    [Fact]
    public void LerPrazo_ValorInvalido_PropagaCaminhoCompleto()
    {
        LeitorEnvelope leitor = new();
        JsonObject prazo = new()
        {
            ["escolha"] = "COM_PRAZO",
            ["valor"] = 0,
            ["unidade"] = null,
            ["referencia"] = null,
            ["efeitoForaDoPrazo"] = null
        };
        JsonObject item = new() { ["prazoDeEmissao"] = prazo };

        LeituraDoPrazoEDaValidade.LerPrazo(leitor, item, "exigencias[0]");

        leitor.Falha<object>().Errors.Should().ContainSingle().Which.Field.Should().Be("exigencias[0].prazoDeEmissao.valor");
    }
}
