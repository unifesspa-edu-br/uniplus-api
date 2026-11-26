namespace Unifesspa.UniPlus.Selecao.Infrastructure.UnitTests.Canonicalization;

using System.Text.Json.Nodes;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Infrastructure.Canonicalization;

public sealed class CondicaoDeAtendimentoRecusadaNoEnvelopeTests
{
    [Fact]
    public void LerAtendimento_CodigoDeCondicaoForaDoFormato_NaoRepeteValor()
    {
        const string valor = "exemplo-condicao-invalida";
        JsonObject documento = JsonNode.Parse("""
            {"atendimento":{"condicoes":[{"condicaoOrigemId":"11111111-1111-1111-1111-111111111111","condicaoCodigo":"exemplo-condicao-invalida","condicaoNome":"Condição"}],"recursos":[],"tiposDeficiencia":[]}}
            """)!.AsObject();
        LeitorEnvelope leitor = new();
        LeituraDoAtendimento.LerAtendimento(leitor, documento);
        FieldError causa = leitor.Falha<object>().Errors.Should().ContainSingle().Subject;
        causa.Error.Code.Should().Be("EnvelopeCodec.EnvelopeMalformado");
        causa.Error.Message.Should().Be("Envelope malformado em 'atendimento.condicoes[0]': o código de condição não tem o formato do cadastro.");
        causa.Error.Message.Should().NotContainEquivalentOf(valor);
        causa.Field.Should().BeNull();
    }
}
