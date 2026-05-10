namespace Unifesspa.UniPlus.Kernel.UnitTests.Results;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;

public sealed class ResultTests
{
    [Fact(DisplayName = "Result.Success — IsSuccess true, IsFailure false, Error null")]
    public void Success_Estado_Coerente()
    {
        Result resultado = Result.Success();

        resultado.IsSuccess.Should().BeTrue();
        resultado.IsFailure.Should().BeFalse();
        resultado.Error.Should().BeNull();
    }

    [Fact(DisplayName = "Result.Failure — IsSuccess false, IsFailure true, Error preservado")]
    public void Failure_Estado_Coerente()
    {
        DomainError erro = new("Teste.Falha", "Mensagem de falha.");

        Result resultado = Result.Failure(erro);

        resultado.IsSuccess.Should().BeFalse();
        resultado.IsFailure.Should().BeTrue();
        resultado.Error.Should().Be(erro);
    }

    [Fact(DisplayName = "Result.Failure(null!) — pin do comportamento atual: aceita sem lançar, Error fica null")]
    public void Failure_ComErroNulo_NaoLancaEDeixaErrorNulo()
    {
        // Test pin: a assinatura é não-anulável (Failure(DomainError error)) mas
        // o construtor privado não valida. Hoje, passar null!? produz um Result
        // com IsFailure=true e Error=null — comportamento inconsistente.
        // Se o design adicionar ArgumentNullException ou similar, este teste
        // quebra e o time revisa intencionalmente.

        Result resultado = Result.Failure(null!);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error.Should().BeNull();
    }
}

public sealed class ResultGenericTests
{
    [Fact(DisplayName = "Result<T>.Success(value) — Value preservado, Error null")]
    public void SuccessGenerico_PreservaValor()
    {
        Result<int> resultado = Result<int>.Success(42);

        resultado.IsSuccess.Should().BeTrue();
        resultado.IsFailure.Should().BeFalse();
        resultado.Value.Should().Be(42);
        resultado.Error.Should().BeNull();
    }

    [Fact(DisplayName = "Result<T>.Failure(erro) — Error preservado, Value default")]
    public void FailureGenerico_PreservaErroEMantemValueDefault()
    {
        DomainError erro = new("Teste.Erro", "Falhou.");

        Result<int> resultado = Result<int>.Failure(erro);

        resultado.IsSuccess.Should().BeFalse();
        resultado.IsFailure.Should().BeTrue();
        resultado.Error.Should().Be(erro);
        resultado.Value.Should().Be(default);
    }

    [Fact(DisplayName = "Result<T>.Success aceita valor de tipo referência sem normalizar para null")]
    public void SuccessGenerico_TipoReferencia_PreservaInstancia()
    {
        const string valor = "preservado";

        Result<string> resultado = Result<string>.Success(valor);

        resultado.Value.Should().BeSameAs(valor);
    }

    [Fact(DisplayName = "Match em sucesso — invoca onSuccess com o valor e retorna o resultado")]
    public void Match_Sucesso_InvocaOnSuccess()
    {
        Result<int> resultado = Result<int>.Success(7);

        string saida = resultado.Match(
            onSuccess: v => $"ok:{v}",
            onFailure: e => $"err:{e.Code}");

        saida.Should().Be("ok:7");
    }

    [Fact(DisplayName = "Match em falha — invoca onFailure com o erro e retorna o resultado")]
    public void Match_Falha_InvocaOnFailure()
    {
        DomainError erro = new("X.Y", "boom");
        Result<int> resultado = Result<int>.Failure(erro);

        string saida = resultado.Match(
            onSuccess: v => $"ok:{v}",
            onFailure: e => $"err:{e.Code}");

        saida.Should().Be("err:X.Y");
    }

    [Fact(DisplayName = "Match com onSuccess nulo lança ArgumentNullException antes de avaliar o resultado")]
    public void Match_OnSuccessNulo_Lanca()
    {
        Result<int> resultado = Result<int>.Success(1);

        Action acao = () => resultado.Match<string>(onSuccess: null!, onFailure: _ => "x");

        acao.Should().Throw<ArgumentNullException>()
            .WithParameterName("onSuccess");
    }

    [Fact(DisplayName = "Match com onFailure nulo lança ArgumentNullException antes de avaliar o resultado")]
    public void Match_OnFailureNulo_Lanca()
    {
        Result<int> resultado = Result<int>.Failure(new DomainError("c", "m"));

        Action acao = () => resultado.Match<string>(onSuccess: _ => "x", onFailure: null!);

        acao.Should().Throw<ArgumentNullException>()
            .WithParameterName("onFailure");
    }
}
