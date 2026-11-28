namespace Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;

/// <summary>
/// Os dados do ato que o edital declarou, depois de a conferência confirmar que o ano e a data vieram: o que o
/// registro do ato e o ato em Publicações recebem, sem valor ausente.
/// </summary>
internal sealed record DadosDoAtoCompletos(
    string Orgao,
    string Serie,
    int Ano,
    DateOnly DataDoAto,
    string Assinante,
    string TipoAtoCodigo);
