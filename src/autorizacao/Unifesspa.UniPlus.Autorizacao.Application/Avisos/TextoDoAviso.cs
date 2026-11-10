namespace Unifesspa.UniPlus.Autorizacao.Application.Avisos;

/// <summary>O assunto e a frase de abertura fixos de um tipo de aviso.</summary>
internal sealed record TextoDoAviso(string Assunto, string Abertura)
{
    public static KeyValuePair<TipoDeAviso, TextoDoAviso> Para(TipoDeAviso tipo, string assunto, string abertura) =>
        new(tipo, new TextoDoAviso(assunto, abertura));
}
