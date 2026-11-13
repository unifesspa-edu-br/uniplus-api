namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands.Bancas;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;

public sealed class RecusasAcumuladasTests
{
    [Fact(DisplayName = "Sem recusa, o resultado entrega o que foi montado, na ordem em que chegou")]
    public void Resultado_SemRecusa_EntregaOsMontadosNaOrdem()
    {
        RecusasAcumuladas<string> acumuladas = new();

        acumuladas.Juntar(Result<string>.Success("primeiro"));
        acumuladas.Juntar(Result<string>.Success("segundo"));

        Result<List<string>> resultado = acumuladas.Resultado();
        resultado.IsSuccess.Should().BeTrue();
        resultado.Value.Should().Equal("primeiro", "segundo");
    }

    [Fact(DisplayName = "A recusa de um item não esconde a dos outros: o resultado as traz todas")]
    public void Resultado_ComRecusas_TrazTodasEDescartaOsMontados()
    {
        RecusasAcumuladas<string> acumuladas = new();

        acumuladas.Juntar(Result<string>.Success("montado"));
        acumuladas.Juntar(Recusa("itens[1]"));
        acumuladas.Juntar(Recusa("itens[2]"));

        Result<List<string>> resultado = acumuladas.Resultado();
        resultado.IsFailure.Should().BeTrue();
        resultado.Errors.Select(static erro => erro.Field).Should().Equal("itens[1]", "itens[2]");
    }

    [Fact(DisplayName = "Sem item algum, o resultado é uma lista vazia")]
    public void Resultado_SemItens_EntregaListaVazia()
    {
        Result<List<string>> resultado = new RecusasAcumuladas<string>().Resultado();

        resultado.IsSuccess.Should().BeTrue();
        resultado.Value.Should().BeEmpty();
    }

    private static Result<string> Recusa(string campo) =>
        Result<string>.ValidationFailure([new FieldError(campo, new DomainError("Teste.Recusa", "Recusa de teste."))]);
}
