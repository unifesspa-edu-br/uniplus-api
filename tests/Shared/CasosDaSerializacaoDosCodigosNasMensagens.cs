namespace Unifesspa.UniPlus.Testes.Compartilhado;

using System.Text;

using Wolverine;
using Wolverine.Runtime.Serialization;

public sealed class CasosDaSerializacaoDosCodigosNasMensagens(IMessageSerializer serializador)
{
    public static TheoryData<object, string> Escritas => new()
    {
        { new MensagemComModo(ModoDeEntregaDeTeste.Postal), """{"modo":"PELO_CORREIO"}""" },
        { new MensagemComModo(ModoDeEntregaDeTeste.Presencial), """{"modo":"EM_MAOS"}""" },
        { new MensagemComModoOpcional(ModoDeEntregaDeTeste.Presencial), """{"modo":"EM_MAOS"}""" },
        { new MensagemComModoOpcional(null), """{"modo":null}""" },
        { new MensagemComPrazoPorModo(new Dictionary<ModoDeEntregaDeTeste, string> { [ModoDeEntregaDeTeste.Postal] = "cinco dias" }), """{"prazos":{"PELO_CORREIO":"cinco dias"}}""" },
    };

    public static TheoryData<object, string> IdasEVoltas => new()
    {
        { new MensagemComModo(ModoDeEntregaDeTeste.Presencial), """{"modo":"EM_MAOS"}""" },
        { new MensagemComModoOpcional(ModoDeEntregaDeTeste.Presencial), """{"modo":"EM_MAOS"}""" },
        { new MensagemComModoOpcional(null), """{"modo":null}""" },
        { new MensagemComPrazoPorModo(new Dictionary<ModoDeEntregaDeTeste, string> { [ModoDeEntregaDeTeste.Postal] = "cinco dias" }), """{"prazos":{"PELO_CORREIO":"cinco dias"}}""" },
    };

    public static TheoryData<string, ModoDeEntregaDeTeste> CodigosDeclarados => new()
    {
        { "EM_MAOS", ModoDeEntregaDeTeste.Presencial },
        { "PELO_CORREIO", ModoDeEntregaDeTeste.Postal },
    };

    public static TheoryData<string> TextosQueNaoSaoCodigo => new() { "roxo", "Postal", "postal" };

    public static TheoryData<Type, string> CorposRecusados => new()
    {
        { typeof(MensagemComModo), """{"modo":7}""" },
        { typeof(MensagemComModo), """{"modo":9}""" },
        { typeof(MensagemComModoOpcional), """{"modo":"roxo"}""" },
        { typeof(MensagemComPrazoPorModo), """{"prazos":{"roxo":"cinco dias"}}""" },
    };

    public string Escrever(object mensagem) => Encoding.UTF8.GetString(serializador.WriteMessage(mensagem));

    public object Ler(Type tipo, string corpo) =>
        serializador.ReadFromData(tipo, new Envelope { Data = Encoding.UTF8.GetBytes(corpo) });

    public T Ler<T>(string corpo) => (T)Ler(typeof(T), corpo);
}

public sealed record MensagemComModo(ModoDeEntregaDeTeste Modo);

public sealed record MensagemComModoOpcional(ModoDeEntregaDeTeste? Modo);

public sealed record MensagemComPrazoPorModo(IReadOnlyDictionary<ModoDeEntregaDeTeste, string> Prazos);
