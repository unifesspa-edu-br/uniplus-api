namespace Unifesspa.UniPlus.Selecao.Domain.Enums;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;

/// <summary>
/// Para que o administrador envia o documento ao processo, declarado no início do envio e
/// imutável. Catálogo fechado: é o que impede o anexo de um edital de ser oferecido, ou aceito,
/// como o edital. Os valores são gravados; não se reordenam.
/// </summary>
public enum FinalidadeDoDocumento
{
    /// <summary>O documento principal do edital de abertura ou de retificação, sempre PDF.</summary>
    [CodigoDeContrato("DOCUMENTO_DO_EDITAL", "Documento do edital", Descricao = "Documento principal do edital.")]
    DocumentoDoEdital = 1,

    /// <summary>Arquivo que acompanha o edital (quadro de vagas, cronograma, formulário), em PDF, DOCX ou ODT.</summary>
    [CodigoDeContrato("ANEXO_DO_EDITAL", "Anexo do edital", Descricao = "Documento que acompanha o edital.")]
    AnexoDoEdital = 2,

    /// <summary>Documento de um ato que não cria versão do edital, em PDF, DOCX ou ODT.</summary>
    [CodigoDeContrato("DOCUMENTO_DE_ATO", "Documento de ato", Descricao = "Documento de um ato do processo.")]
    DocumentoDeAto = 3,
}
