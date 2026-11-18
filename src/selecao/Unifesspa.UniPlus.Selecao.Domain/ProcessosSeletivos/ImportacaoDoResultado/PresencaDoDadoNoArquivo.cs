namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.ImportacaoDoResultado;

/// <summary>Com que garantia o leiaute traz um dado em cada linha do arquivo (UNI-REQ-0151).</summary>
public enum PresencaDoDadoNoArquivo
{
    /// <summary>A coluna existe e vem preenchida em toda linha: o dado conta como trazido pelo arquivo.</summary>
    EmTodaLinha = 1,

    /// <summary>A coluna pode faltar ou vir vazia: o dado não conta como trazido, e o formulário o complementa.</summary>
    Opcional,
}
