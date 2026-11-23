namespace Unifesspa.UniPlus.Selecao.Infrastructure.UnitTests.Canonicalization;

using System.Text.Json.Nodes;

using AwesomeAssertions;

using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Selecao.Domain.UnitTests.Cenarios;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Selecao.Infrastructure.Canonicalization;

/// <summary>
/// O prazo de emissão e a validade no envelope da exigência: escritos pelo código declarado de cada escolha, com os
/// detalhes nulos quando a escolha não os usa, e lidos de volta no mesmo valor.
/// </summary>
public sealed class PrazoEValidadeNoEnvelopeTests
{
    private static readonly Guid Fase = Guid.Parse("0197a3c4-0000-7000-8000-000000000001");

    // O envelope é lido dos bytes que foram gravados, não do objeto que a escrita montou.
    private static JsonObject Item(string chave, JsonObject? bloco) =>
        (JsonObject)JsonNode.Parse(new JsonObject { [chave] = bloco }.ToJsonString())!;

    [Fact(DisplayName = "Sem a escolha do prazo, o envelope escreve o prazo nulo")]
    public void Prazo_NaoEscolhido_EscreveNulo() =>
        SerializacaoDoPrazoEDaValidade.Prazo(null).Should().BeNull();

    [Fact(DisplayName = "Prazo sem prazo escreve só a escolha, com os detalhes nulos")]
    public void Prazo_SemPrazo_EscreveSoAEscolha() =>
        SerializacaoDoPrazoEDaValidade.Prazo(new PrazoDeEmissaoBuilder().SemPrazo().Construir())!.ToJsonString()
            .Should().Be("""{"escolha":"SEM_PRAZO","valor":null,"unidade":null,"referencia":null,"efeitoForaDoPrazo":null}""");

    [Fact(DisplayName = "Prazo com prazo escreve valor, unidade, referência e efeito pelo código declarado")]
    public void Prazo_ComPrazo_EscreveOsDetalhes() =>
        SerializacaoDoPrazoEDaValidade.Prazo(new PrazoDeEmissaoBuilder().De(3, UnidadeDoPrazoDeEmissao.Meses)
                .Referenciado(new DataDeReferenciaBuilder().NoFimDaFase(Fase).Construir()).Construir())!.ToJsonString()
            .Should().Be($$"""{"escolha":"COM_PRAZO","valor":3,"unidade":"MESES","referencia":{"tipo":"FIM_DA_FASE","data":null,"faseId":"{{Fase}}"},"efeitoForaDoPrazo":"RECUSAR_DOCUMENTO"}""");

    [Fact(DisplayName = "Sem a escolha da validade, o envelope escreve a validade nula")]
    public void Validade_NaoEscolhida_EscreveNulo() =>
        SerializacaoDoPrazoEDaValidade.Validade(null).Should().BeNull();

    [Fact(DisplayName = "Validade que não exige documento válido escreve só a escolha")]
    public void Validade_NaoExige_EscreveSoAEscolha() =>
        SerializacaoDoPrazoEDaValidade.Validade(new ValidadeDoDocumentoBuilder().NaoExige().Construir())!.ToJsonString()
            .Should().Be("""{"escolha":"NAO_EXIGE_DOCUMENTO_VALIDO","referencia":null,"efeitoDoVencimento":null}""");

    [Fact(DisplayName = "Validade que exige documento válido escreve a referência e o efeito")]
    public void Validade_Exige_EscreveOsDetalhes() =>
        SerializacaoDoPrazoEDaValidade.Validade(new ValidadeDoDocumentoBuilder().Referenciada(new DataDeReferenciaBuilder().NaData(new DateOnly(2026, 3, 1)).Construir()).Construir())!
            .ToJsonString().Should().Be("""{"escolha":"EXIGE_DOCUMENTO_VALIDO","referencia":{"tipo":"DATA_ESPECIFICA","data":"2026-03-01","faseId":null},"efeitoDoVencimento":"RECUSAR_DOCUMENTO"}""");

    [Fact(DisplayName = "O prazo escrito é lido de volta no mesmo valor")]
    public void Prazo_EscritoELido_VoltaIgual()
    {
        PrazoDeEmissao prazo = new PrazoDeEmissaoBuilder().Referenciado(new DataDeReferenciaBuilder().NoInicioDaFase(Fase).Construir()).Construir();
        LeitorEnvelope leitor = new();

        PrazoDeEmissao? lido = LeituraDoPrazoEDaValidade.LerPrazo(leitor, Item("prazoDeEmissao", SerializacaoDoPrazoEDaValidade.Prazo(prazo)), "item");

        leitor.Falhou.Should().BeFalse();
        lido.Should().BeEquivalentTo(prazo);
    }

    [Fact(DisplayName = "A validade escrita é lida de volta no mesmo valor")]
    public void Validade_EscritaELida_VoltaIgual()
    {
        ValidadeDoDocumento validade = new ValidadeDoDocumentoBuilder().Construir();
        LeitorEnvelope leitor = new();

        ValidadeDoDocumento? lida = LeituraDoPrazoEDaValidade.LerValidade(leitor, Item("validade", SerializacaoDoPrazoEDaValidade.Validade(validade)), "item");

        leitor.Falhou.Should().BeFalse();
        lida.Should().BeEquivalentTo(validade);
    }

    [Fact(DisplayName = "Prazo e validade nulos no envelope são lidos como escolha por fazer")]
    public void PrazoEValidade_Nulos_LidosComoNaoEscolhidos()
    {
        LeitorEnvelope leitor = new();
        JsonObject item = new() { ["prazoDeEmissao"] = null, ["validade"] = null };

        LeituraDoPrazoEDaValidade.LerPrazo(leitor, item, "item").Should().BeNull();
        LeituraDoPrazoEDaValidade.LerValidade(leitor, item, "item").Should().BeNull();

        leitor.Falhou.Should().BeFalse();
    }

    [Fact(DisplayName = "Escolha que não é código declarado recusa o envelope")]
    public void Prazo_EscolhaForaDoCatalogo_RecusaOEnvelope()
    {
        LeitorEnvelope leitor = new();
        JsonObject bloco = SerializacaoDoPrazoEDaValidade.Prazo(new PrazoDeEmissaoBuilder().Construir())!;
        bloco["escolha"] = "PRAZO_INEXISTENTE";

        LeituraDoPrazoEDaValidade.LerPrazo(leitor, Item("prazoDeEmissao", bloco), "item");

        leitor.Falhou.Should().BeTrue();
    }

    [Fact(DisplayName = "Detalhe que a escolha não usa recusa o envelope pelo próprio domínio")]
    public void Validade_DetalheSemEscolhaQueOUse_RecusaOEnvelope()
    {
        LeitorEnvelope leitor = new();
        JsonObject bloco = SerializacaoDoPrazoEDaValidade.Validade(new ValidadeDoDocumentoBuilder().Construir())!;
        bloco["escolha"] = "NAO_EXIGE_DOCUMENTO_VALIDO";

        LeituraDoPrazoEDaValidade.LerValidade(leitor, Item("validade", bloco), "item");

        leitor.Falhou.Should().BeTrue();
    }
}
