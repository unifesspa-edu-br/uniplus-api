namespace Unifesspa.UniPlus.Selecao.Domain.Enums;

/// <summary>
/// Para que o administrador envia o documento ao processo, declarado no início do envio e
/// imutável. Catálogo fechado: é o que impede o anexo de um edital de ser oferecido, ou aceito,
/// como o edital. Os valores são gravados; não se reordenam.
/// </summary>
public enum FinalidadeDoDocumento
{
    /// <summary>O documento principal do edital de abertura ou de retificação, sempre PDF.</summary>
    DocumentoDoEdital = 1,

    /// <summary>Arquivo que acompanha o edital (quadro de vagas, cronograma, formulário), em PDF, DOCX ou ODT.</summary>
    AnexoDoEdital = 2,

    /// <summary>Documento de um ato que não cria versão do edital, em PDF, DOCX ou ODT.</summary>
    DocumentoDeAto = 3,
}
