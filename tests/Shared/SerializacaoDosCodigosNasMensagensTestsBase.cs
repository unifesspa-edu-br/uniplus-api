namespace Unifesspa.UniPlus.Testes.Compartilhado;

using System.Text;
using System.Text.Json;

using AwesomeAssertions;

using Wolverine;
using Wolverine.Runtime.Serialization;

/// <summary>
/// O código de contrato dos enums no corpo das mensagens, provado pelo serializador que o Wolverine de cada executável
/// resolve de verdade, e não por opções JSON soltas: o texto escrito traz o código declarado, a leitura devolve o mesmo
/// membro e o código desconhecido é recusado. Cada executável herda estas provas e informa o serializador que o seu
/// Wolverine entrega.
/// </summary>
/// <remarks>
/// As asserções são sobre o texto produzido: uma ida e volta passaria mesmo se os dois lados usassem a mesma
/// representação errada. O enum de teste declara código diferente do nome e do número do membro, de modo que nenhuma
/// das outras representações coincide com o código.
/// </remarks>
public abstract class SerializacaoDosCodigosNasMensagensTestsBase
{
    /// <summary>O serializador padrão de mensagens do Wolverine do executável sob prova.</summary>
    protected abstract IMessageSerializer Serializador { get; }

    [Fact(DisplayName = "O enum com código sai na mensagem pelo código declarado, e não pelo nome nem pelo número")]
    public void Escrever_EnumComCodigo_EscreveOCodigoDeclarado() =>
        Escrever(new MensagemComModo(ModoDeEntregaDeTeste.Postal)).Should().Be("""{"modo":"PELO_CORREIO"}""");

    [Theory(DisplayName = "O código declarado volta como o membro que o declara")]
    [InlineData("EM_MAOS", ModoDeEntregaDeTeste.Presencial)]
    [InlineData("PELO_CORREIO", ModoDeEntregaDeTeste.Postal)]
    public void Ler_CodigoDeclarado_DevolveOMembro(string codigo, ModoDeEntregaDeTeste esperado) =>
        Ler<MensagemComModo>($$"""{"modo":"{{codigo}}"}""").Modo.Should().Be(esperado);

    [Fact(DisplayName = "A mensagem escrita com o enum volta igual na leitura")]
    public void IdaEVolta_EnumComCodigo_VoltaIgual()
    {
        MensagemComModo original = new(ModoDeEntregaDeTeste.Presencial);

        Ler<MensagemComModo>(Escrever(original)).Should().Be(original);
    }

    [Theory(DisplayName = "Nome do membro, variação de caixa e texto fora do catálogo são recusados na leitura sem repetir o valor")]
    [InlineData("roxo")]
    [InlineData("Postal")]
    [InlineData("postal")]
    public void Ler_TextoQueNaoECodigoDeclarado_LancaJsonExceptionSemRepetirOValor(string texto)
    {
        Action ler = () => Ler<MensagemComModo>($$"""{"modo":"{{texto}}"}""");

        ler.Should().Throw<JsonException>().Which.Message.Should().NotContain(texto);
    }

    [Theory(DisplayName = "O número do membro no lugar do código é recusado na leitura")]
    [InlineData(7)]
    [InlineData(9)]
    public void Ler_NumeroNoLugarDoCodigo_LancaJsonException(int numero)
    {
        Action ler = () => Ler<MensagemComModo>($$"""{"modo":{{numero}}}""");

        ler.Should().Throw<JsonException>();
    }

    [Fact(DisplayName = "O enum anulável preenchido sai pelo código, o nulo sai como nulo, e os dois voltam iguais")]
    public void IdaEVolta_EnumAnulavel_SaiPeloCodigoEVoltaIgual()
    {
        MensagemComModoOpcional preenchida = new(ModoDeEntregaDeTeste.Presencial);
        MensagemComModoOpcional ausente = new(null);

        Escrever(preenchida).Should().Be("""{"modo":"EM_MAOS"}""");
        Escrever(ausente).Should().Be("""{"modo":null}""");
        Ler<MensagemComModoOpcional>(Escrever(preenchida)).Should().Be(preenchida);
        Ler<MensagemComModoOpcional>(Escrever(ausente)).Should().Be(ausente);
    }

    [Fact(DisplayName = "O código desconhecido no enum anulável é recusado na leitura")]
    public void Ler_EnumAnulavelComCodigoDesconhecido_LancaJsonException()
    {
        Action ler = () => Ler<MensagemComModoOpcional>("""{"modo":"roxo"}""");

        ler.Should().Throw<JsonException>();
    }

    [Fact(DisplayName = "O enum como chave de dicionário sai pelo código e volta igual")]
    public void IdaEVolta_EnumComoChaveDeDicionario_SaiPeloCodigoEVoltaIgual()
    {
        MensagemComPrazoPorModo original = new(new Dictionary<ModoDeEntregaDeTeste, string> { [ModoDeEntregaDeTeste.Postal] = "cinco dias" });

        string texto = Escrever(original);

        texto.Should().Be("""{"prazos":{"PELO_CORREIO":"cinco dias"}}""");
        Ler<MensagemComPrazoPorModo>(texto).Prazos.Should().Equal(original.Prazos);
    }

    [Fact(DisplayName = "O código desconhecido na chave de dicionário é recusado na leitura")]
    public void Ler_ChaveDeDicionarioComCodigoDesconhecido_LancaJsonException()
    {
        Action ler = () => Ler<MensagemComPrazoPorModo>("""{"prazos":{"roxo":"cinco dias"}}""");

        ler.Should().Throw<JsonException>();
    }

    private string Escrever(object mensagem) => Encoding.UTF8.GetString(Serializador.WriteMessage(mensagem));

    private T Ler<T>(string corpo) =>
        (T)Serializador.ReadFromData(typeof(T), new Envelope { Data = Encoding.UTF8.GetBytes(corpo) });

    private sealed record MensagemComModo(ModoDeEntregaDeTeste Modo);

    private sealed record MensagemComModoOpcional(ModoDeEntregaDeTeste? Modo);

    private sealed record MensagemComPrazoPorModo(IReadOnlyDictionary<ModoDeEntregaDeTeste, string> Prazos);
}
