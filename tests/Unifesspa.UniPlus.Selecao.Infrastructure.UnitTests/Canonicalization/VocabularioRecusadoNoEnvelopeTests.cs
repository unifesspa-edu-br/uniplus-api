namespace Unifesspa.UniPlus.Selecao.Infrastructure.UnitTests.Canonicalization;

using System.Text.Json.Nodes;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Infrastructure.Canonicalization;

public sealed class VocabularioRecusadoNoEnvelopeTests
{
    [Fact]
    public void ConferirDerivados_CodigoRepetido_NaoRepeteValor()
    {
        const string valor = "EXEMPLO_DERIVADO_REPETIDO";
        JsonObject item = JsonNode.Parse("""{"codigoDoDado":"EXEMPLO_DERIVADO_REPETIDO","regras":[{"ordem":0,"contribui":null,"quando":null}]}""")!.AsObject();
        JsonObject documento = new() { ["calculosDosDados"] = new JsonArray(item, item.DeepClone()) };
        LeitorEnvelope leitor = new();
        IReadOnlyList<ConfiguracaoDerivacaoFato> regras = LeituraDaDerivacao.LerRegrasDerivacao(leitor, documento);
        DomainError erro = ConferenciaDoVocabularioDeFatos.ConferirDerivados(regras, [], [], [])!;
        leitor.Propagar<object>(erro);
        FieldError causa = leitor.Falha<object>().Errors.Should().ContainSingle().Subject;
        causa.Error.Code.Should().Be("EnvelopeCodec.EnvelopeMalformado");
        causa.Error.Message.Should().Contain("mais de uma configuração").And.NotContainEquivalentOf(valor);
        causa.Field.Should().BeNull();
    }
}
