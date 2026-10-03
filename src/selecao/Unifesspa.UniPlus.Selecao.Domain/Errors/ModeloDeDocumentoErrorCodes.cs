namespace Unifesspa.UniPlus.Selecao.Domain.Errors;

/// <summary>Códigos de erro do modelo de documento que a exigência oferece ao candidato.</summary>
public static class ModeloDeDocumentoErrorCodes
{
    public const string NaoEncontrado = "ModeloDeDocumento.NaoEncontrado";
    public const string NaoConfirmado = "ModeloDeDocumento.NaoConfirmado";
    public const string StatusInvalidoParaConfirmacao = "ModeloDeDocumento.StatusInvalidoParaConfirmacao";
    public const string ObjetoNaoEncontrado = "ModeloDeDocumento.ObjetoNaoEncontrado";
    public const string TamanhoExcedido = "ModeloDeDocumento.TamanhoExcedido";
    public const string FormatoNaoEditavel = "ModeloDeDocumento.FormatoNaoEditavel";
    public const string ConteudoDivergeDoFormato = "ModeloDeDocumento.ConteudoDivergeDoFormato";
    public const string ContemMacro = "ModeloDeDocumento.ContemMacro";
    public const string NomeArquivoInvalido = "ModeloDeDocumento.NomeArquivoInvalido";
}
