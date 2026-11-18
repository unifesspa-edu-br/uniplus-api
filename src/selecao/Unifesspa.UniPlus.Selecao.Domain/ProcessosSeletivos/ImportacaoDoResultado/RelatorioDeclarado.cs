namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.ImportacaoDoResultado;

/// <summary>
/// Um relatório que o processo de resultado importado declara importar: o leiaute que ele segue, pelo código e
/// pela versão. O código é a chave local do relatório no documento, única e estável (ADR-0129).
/// </summary>
public sealed record RelatorioDeclarado(string LeiauteCodigo, string LeiauteVersao);

/// <summary>O relatório como a configuração o informa, ainda por conferir.</summary>
public sealed record RelatorioInformado(string? LeiauteCodigo, string? LeiauteVersao);
