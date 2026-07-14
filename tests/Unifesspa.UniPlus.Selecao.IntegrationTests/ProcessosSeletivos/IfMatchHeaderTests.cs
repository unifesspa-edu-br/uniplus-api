namespace Unifesspa.UniPlus.Selecao.IntegrationTests.ProcessosSeletivos;

using AwesomeAssertions;

using Microsoft.Extensions.Primitives;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.API.Http;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// A decodificação do <c>If-Match</c> no boundary (ADR-0110 D5, RFC 9110 §13.1.1).
/// </summary>
/// <remarks>
/// O que estes testes fixam é a <b>fronteira entre 400 e 412</b>, que é sutil e fácil de
/// errar: o transporte recusa o que viola a <b>gramática</b>; o que é sintaticamente válido
/// mas <b>não casa</b> — inclusive uma weak tag, que a gramática aceita e a comparação forte
/// nunca aprova — atravessa e vira 412 no handler. Trocar um pelo outro diria ao cliente
/// que ele mandou lixo quando ele apenas mandou um tag velho.
/// </remarks>
public sealed class IfMatchHeaderTests
{
    [Fact(DisplayName = "Header ausente é estado VÁLIDO — os Definir* servem também um processo em rascunho, que não tem sessão")]
    public void Ausente_NaoEErro()
    {
        Result<PrecondicaoIfMatch> resultado = IfMatchHeader.Analisar(StringValues.Empty);

        resultado.IsSuccess.Should().BeTrue();
        resultado.Value!.Presente.Should().BeFalse();
    }

    [Fact(DisplayName = "Uma entity-tag forte casa exatamente com o ETag da sessão")]
    public void TagForte_Casa()
    {
        Guid id = Guid.CreateVersion7();
        string etag = $"\"{id}:3\"";

        Result<PrecondicaoIfMatch> resultado = IfMatchHeader.Analisar(new StringValues(etag));

        resultado.IsSuccess.Should().BeTrue();
        resultado.Value!.Casa(etag).Should().BeTrue();
        resultado.Value!.Casa($"\"{id}:4\"").Should().BeFalse("a revisão avançou — o tag do cliente está defasado");
    }

    [Fact(DisplayName = "Lista de tags casa se ALGUMA delas casar")]
    public void ListaDeTags_CasaSeAlgumaCasar()
    {
        Guid id = Guid.CreateVersion7();
        string atual = $"\"{id}:2\"";

        Result<PrecondicaoIfMatch> resultado = IfMatchHeader.Analisar(
            new StringValues($"\"{id}:1\", {atual}, \"{Guid.CreateVersion7()}:9\""));

        resultado.IsSuccess.Should().BeTrue();
        resultado.Value!.Casa(atual).Should().BeTrue();
    }

    [Fact(DisplayName = "'*' casa com qualquer sessão existente")]
    public void Curinga_Casa()
    {
        Result<PrecondicaoIfMatch> resultado = IfMatchHeader.Analisar(new StringValues("*"));

        resultado.IsSuccess.Should().BeTrue();
        resultado.Value!.Presente.Should().BeTrue();
        resultado.Value!.Casa($"\"{Guid.CreateVersion7()}:1\"").Should().BeTrue();
    }

    [Fact(DisplayName = "Uma weak tag NÃO é erro de sintaxe — ela simplesmente não casa, e o cliente recebe 412, nunca 400")]
    public void WeakTag_NaoEErroDeSintaxe_MasNaoCasa()
    {
        Guid id = Guid.CreateVersion7();
        string etag = $"\"{id}:1\"";

        Result<PrecondicaoIfMatch> resultado = IfMatchHeader.Analisar(new StringValues($"W/{etag}"));

        resultado.IsSuccess.Should().BeTrue(
            "a gramática do If-Match aceita weak tags — recusá-las com 400 diria ao cliente que o header está malformado quando ele está apenas condenado a não casar");
        resultado.Value!.Presente.Should().BeTrue();
        resultado.Value!.Casa(etag).Should().BeFalse(
            "o If-Match exige comparação FORTE (RFC 9110 §13.1.1) — uma weak tag nunca casa nela");
    }

    [Fact(DisplayName = "'*' misturado com tags é sintaxe inválida — 400, não 412")]
    public void CuringaMisturado_Malformado()
    {
        Result<PrecondicaoIfMatch> resultado = IfMatchHeader.Analisar(
            new StringValues($"*, \"{Guid.CreateVersion7()}:1\""));

        resultado.IsFailure.Should().BeTrue(
            "'*' é 'qualquer representação existente' — misturá-lo com tags específicas é contradição, e a RFC não define qual venceria");
        resultado.Error!.Code.Should().Be("Precondicao.Malformada");
    }

    [Theory(DisplayName = "Tag sem aspas é sintaxe inválida — 400")]
    [InlineData("abc123")]
    [InlineData("1:2")]
    public void TagSemAspas_Malformado(string bruto)
    {
        Result<PrecondicaoIfMatch> resultado = IfMatchHeader.Analisar(new StringValues(bruto));

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("Precondicao.Malformada");
    }

    [Theory(DisplayName = "Header PRESENTE mas sem entity-tag alguma é malformado — não é o mesmo que ausente")]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(",")]
    [InlineData(" , , ")]
    public void PresenteSemTag_Malformado(string bruto)
    {
        Result<PrecondicaoIfMatch> resultado = IfMatchHeader.Analisar(new StringValues(bruto));

        resultado.IsFailure.Should().BeTrue(
            "a gramática exige 1#entity-tag, ao menos uma. Tratá-lo como ausente faria o cliente receber 428 ('informe o If-Match') tendo informado — ele o releria, remandaria igual, e giraria em falso");
        resultado.Error!.Code.Should().Be("Precondicao.Malformada");
    }
}
