namespace Unifesspa.UniPlus.Selecao.Domain.Errors;

/// <summary>Códigos de erro do documento que o processo envia para publicar.</summary>
public static class DocumentoAPublicarErrorCodes
{
    public const string NaoEncontrado = "DocumentoAPublicar.NaoEncontrado";
    public const string NaoConfirmado = "DocumentoAPublicar.NaoConfirmado";
    public const string StatusInvalidoParaConfirmacao = "DocumentoAPublicar.StatusInvalidoParaConfirmacao";
    public const string ObjetoNaoEncontrado = "DocumentoAPublicar.ObjetoNaoEncontrado";
    public const string TamanhoExcedido = "DocumentoAPublicar.TamanhoExcedido";
    public const string ArquivoVazio = "DocumentoAPublicar.ArquivoVazio";
    public const string TipoDeMidiaNaoAceito = "DocumentoAPublicar.TipoDeMidiaNaoAceito";
    public const string ConteudoDivergeDoFormato = "DocumentoAPublicar.ConteudoDivergeDoFormato";
    public const string ContemMacro = "DocumentoAPublicar.ContemMacro";
    public const string NomeArquivoInvalido = "DocumentoAPublicar.NomeArquivoInvalido";
}
