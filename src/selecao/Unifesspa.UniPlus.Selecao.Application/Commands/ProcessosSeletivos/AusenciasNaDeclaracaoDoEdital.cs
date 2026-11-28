namespace Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;

using Kernel.Results;

using Services;

/// <summary>
/// O que a publicação e a retificação do edital recusam quando o pedido não traz um campo obrigatório: o documento do
/// edital, o bloco do ato e, dentro dele, o órgão, a série, o ano, a data, o assinante e o tipo do ato, e o documento de cada anexo. Ausente e nulo são a mesma coisa. As regras que
/// dependem de um campo ausente não são conferidas.
/// </summary>
internal static class AusenciasNaDeclaracaoDoEdital
{
    /// <summary>O campo do pedido que traz o tipo do ato, onde a ausência e a recusa do tipo se corrigem.</summary>
    public const string CampoDoTipoDoAto = "ato.tipoAtoCodigo";

    public static IReadOnlyList<FieldError> Listar(Guid? documentoEditalId, DadosDoAto? ato, IReadOnlyList<AnexoSolicitadoDoEdital>? anexos) =>
    [
        .. documentoEditalId is null ? [Ausencia(ConferenciaDoDocumentoDoEdital.CampoDoDocumento, "Informe o documento do edital.")] : Array.Empty<FieldError>(),
        .. ato is null ? [Ausencia("ato", "Informe os dados do ato do edital.")] : AusenciasDoAto(ato),
        .. AusenciasDosAnexos(anexos ?? []),
    ];

    /// <summary>Os dados do ato sem lacuna, ou nulo quando falta o bloco ou algum dos seis campos.</summary>
    public static DadosDoAtoCompletos? Completar(DadosDoAto? ato) =>
        ato is { Ano: { } ano, DataDoAto: { } data, Orgao: { } orgao, Serie: { } serie, Assinante: { } assinante, TipoAtoCodigo: { } tipo }
        && !TextoEmBranco(orgao, serie, assinante, tipo)
            ? new DadosDoAtoCompletos(orgao, serie, ano, data, assinante, tipo)
            : null;

    private static IEnumerable<FieldError> AusenciasDoAto(DadosDoAto ato) =>
    [
        .. AusenciaDoTexto(ato.Orgao, "ato.orgao", "Informe o órgão publicador do ato."),
        .. AusenciaDoTexto(ato.Serie, "ato.serie", "Informe a série do ato."),
        .. ato.Ano is null ? [Ausencia("ato.ano", "Informe o ano do ato.")] : Array.Empty<FieldError>(),
        .. ato.DataDoAto is null ? [Ausencia("ato.dataDoAto", "Informe a data do ato declarada pelo documento.")] : Array.Empty<FieldError>(),
        .. AusenciaDoTexto(ato.Assinante, "ato.assinante", "Informe o assinante do ato."),
        .. AusenciaDoTexto(ato.TipoAtoCodigo, CampoDoTipoDoAto, "Informe o tipo do ato."),
    ];

    private static FieldError[] AusenciaDoTexto(string? valor, string campo, string mensagem) =>
        string.IsNullOrWhiteSpace(valor) ? [Ausencia(campo, mensagem)] : [];

    private static bool TextoEmBranco(params string[] valores) =>
        valores.Any(string.IsNullOrWhiteSpace);

    // O item nulo da lista é corpo malformado e o validator o recusa antes de o comando chegar aqui.
    private static IEnumerable<FieldError> AusenciasDosAnexos(IReadOnlyList<AnexoSolicitadoDoEdital> anexos) =>
        anexos
            .Select(static (anexo, posicao) => (anexo, posicao))
            .Where(static par => par.anexo is { DocumentoId: null })
            .Select(static par => Ausencia($"anexos[{par.posicao}].documentoId", "Informe o documento do anexo."));

    private static FieldError Ausencia(string campo, string mensagem) =>
        new(campo, new DomainError(CodigosDeValidacao.Obrigatorio, mensagem));
}
