namespace Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;

using Kernel.Results;

/// <summary>
/// O que a publicação e a retificação do edital recusam quando o pedido não traz um campo obrigatório: o documento do
/// edital, o bloco do ato e, dentro dele, o ano e a data do ato, e o documento de cada anexo. Ausente e nulo são a mesma coisa. As regras que
/// dependem de um campo ausente não são conferidas.
/// </summary>
internal static class AusenciasNaDeclaracaoDoEdital
{
    public static IReadOnlyList<FieldError> Listar(Guid? documentoEditalId, DadosDoAto? ato, IReadOnlyList<AnexoSolicitadoDoEdital>? anexos) =>
    [
        .. documentoEditalId is null ? [Ausencia("documentoEditalId", "Informe o documento do edital.")] : Array.Empty<FieldError>(),
        .. ato is null ? [Ausencia("ato", "Informe os dados do ato do edital.")] : AusenciasDoAto(ato),
        .. AusenciasDosAnexos(anexos ?? []),
    ];

    /// <summary>Os dados do ato sem lacuna, ou nulo quando falta o bloco, o ano ou a data.</summary>
    public static DadosDoAtoCompletos? Completar(DadosDoAto? ato) =>
        ato is { Ano: { } ano, DataDoAto: { } data }
            ? new DadosDoAtoCompletos(ato.Orgao, ato.Serie, ano, data, ato.Assinante, ato.TipoAtoCodigo)
            : null;

    private static IEnumerable<FieldError> AusenciasDoAto(DadosDoAto ato)
    {
        if (ato.Ano is null)
        {
            yield return Ausencia("ato.ano", "Informe o ano do ato.");
        }

        if (ato.DataDoAto is null)
        {
            yield return Ausencia("ato.dataDoAto", "Informe a data do ato declarada pelo documento.");
        }
    }

    // O item nulo da lista é corpo malformado e o validator o recusa antes de o comando chegar aqui.
    private static IEnumerable<FieldError> AusenciasDosAnexos(IReadOnlyList<AnexoSolicitadoDoEdital> anexos) =>
        anexos
            .Select(static (anexo, posicao) => (anexo, posicao))
            .Where(static par => par.anexo is { DocumentoId: null })
            .Select(static par => Ausencia($"anexos[{par.posicao}].documentoId", "Informe o documento do anexo."));

    private static FieldError Ausencia(string campo, string mensagem) =>
        new(campo, new DomainError(CodigosDeValidacao.Obrigatorio, mensagem));
}
