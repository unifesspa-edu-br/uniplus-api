namespace Unifesspa.UniPlus.Selecao.Infrastructure.UnitTests.Canonicalization;

using System.Text.Json.Nodes;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Selecao.Infrastructure.Canonicalization;

public sealed class ValoresDeDominioRecusadosNoEnvelopeTests
{
    [Theory]
    [InlineData(TipoDeCampo.Numero)]
    [InlineData(TipoDeCampo.SimOuNao)]
    public void LerValoresSelecionaveis_CampoSemSelecao_NaoRepeteValor(TipoDeCampo tipo)
    {
        const string valor = "ExemploOpcaoIncompativel";
        JsonObject item = new() { ["valoresSelecionaveis"] = new JsonArray(valor) };
        LeitorEnvelope leitor = new();
        LeituraDosValoresDeDominio.LerValoresSelecionaveis(leitor, item, "item", tipo);
        FieldError causa = leitor.Falha<object>().Errors.Should().ContainSingle().Subject;
        causa.Error.Code.Should().Be("EnvelopeCodec.EnvelopeMalformado");
        causa.Error.Message.Should().Be("'item.valoresSelecionaveis' é um array, mas o tipo de campo não é de seleção — campos que não são de seleção não podem ter opções.").And.NotContain(valor).And.NotContain(tipo.ToString());
        causa.Field.Should().BeNull();
    }
}
