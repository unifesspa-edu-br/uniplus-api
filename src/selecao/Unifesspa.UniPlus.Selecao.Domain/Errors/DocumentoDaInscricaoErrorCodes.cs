namespace Unifesspa.UniPlus.Selecao.Domain.Errors;

/// <summary>Códigos de erro do documento que o candidato envia para atender uma exigência da inscrição.</summary>
public static class DocumentoDaInscricaoErrorCodes
{
    public const string ExigenciaNaoEncontrada = "DocumentoDaInscricao.ExigenciaNaoEncontrada";
    public const string OcorrenciaInvalida = "DocumentoDaInscricao.OcorrenciaInvalida";
    public const string ApresentacaoNaoEncontrada = "DocumentoDaInscricao.ApresentacaoNaoEncontrada";
    public const string ArquivoNaoEncontrado = "DocumentoDaInscricao.ArquivoNaoEncontrado";
    public const string FormatoNaoAceitoPelaExigencia = "DocumentoDaInscricao.FormatoNaoAceitoPelaExigencia";
    public const string TamanhoExcedido = "DocumentoDaInscricao.TamanhoExcedido";
    public const string ArquivoVazio = "DocumentoDaInscricao.ArquivoVazio";
    public const string ObjetoNaoEncontrado = "DocumentoDaInscricao.ObjetoNaoEncontrado";
    public const string StatusInvalidoParaConfirmacao = "DocumentoDaInscricao.StatusInvalidoParaConfirmacao";
    public const string ETagObrigatorio = "DocumentoDaInscricao.ETagObrigatorio";
    public const string ArquivoNaoVerificado = "DocumentoDaInscricao.ArquivoNaoVerificado";
    public const string RemocaoSoNoRascunho = "DocumentoDaInscricao.RemocaoSoNoRascunho";
    public const string InscricaoAlterada = "DocumentoDaInscricao.InscricaoAlterada";
}
