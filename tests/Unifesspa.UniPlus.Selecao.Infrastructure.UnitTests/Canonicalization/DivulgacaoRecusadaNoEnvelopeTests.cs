namespace Unifesspa.UniPlus.Selecao.Infrastructure.UnitTests.Canonicalization;

using System.Text.Json.Nodes;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Infrastructure.Canonicalization;

public sealed class DivulgacaoRecusadaNoEnvelopeTests
{
    [Fact]
    public void LerDivulgacao_RegraDesconhecida_NaoRepeteValor()
    {
        const string valor = "ExemploRegraDesconhecida";
        JsonObject documento = JsonNode.Parse("""{"divulgacao":{"camposPublicos":["NOME_ABREVIADO"],"regraNomeAbreviado":"ExemploRegraDesconhecida","justificativa":null}}""")!.AsObject();
        LeitorEnvelope leitor = new();
        LeituraDaDivulgacaoETaxa.LerDivulgacao(leitor, documento);
        FieldError causa = leitor.Falha<object>().Errors.Should().ContainSingle().Subject;
        causa.Error.Code.Should().Be("EnvelopeCodec.EnvelopeMalformado");
        causa.Error.Message.Should().Be("'divulgacao.regraNomeAbreviado' deve ser uma regra conhecida.");
        causa.Error.Message.Should().NotContainEquivalentOf(valor);
        causa.Field.Should().BeNull();
    }
}
