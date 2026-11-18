namespace Unifesspa.UniPlus.Selecao.IntegrationTests.Rascunhos;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.API.Http;
using Unifesspa.UniPlus.Selecao.Domain.Errors;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

public sealed class PrecondicaoDoRascunhoTests
{
    [Fact(DisplayName = "Sem o header a precondição é requerida")]
    public void SemHeader_Requerida()
    {
        Result<PrecondicaoIfMatch> resultado = PrecondicaoDoRascunho.Exigir(null);

        resultado.Error!.Code.Should().Be(PrecondicaoErrorCodes.Requerida);
    }

    [Fact(DisplayName = "O header malformado é recusado como malformado, e não como ausente")]
    public void HeaderMalformado_Malformada()
    {
        Result<PrecondicaoIfMatch> resultado = PrecondicaoDoRascunho.Exigir("sem-aspas");

        resultado.Error!.Code.Should().Be("Precondicao.Malformada");
    }

    [Fact(DisplayName = "O header com a tag forte é aceito sem conferir a revisão, que é do handler")]
    public void HeaderComTag_Aceito()
    {
        Result<PrecondicaoIfMatch> resultado = PrecondicaoDoRascunho.Exigir("\"7\"");

        resultado.IsSuccess.Should().BeTrue();
        resultado.Value!.Casa("\"7\"").Should().BeTrue();
    }
}
