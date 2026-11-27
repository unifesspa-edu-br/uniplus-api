namespace Unifesspa.UniPlus.Publicacoes.Application.Commands.AtosNormativos;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Publicacoes.Domain.Entities;
using Unifesspa.UniPlus.Publicacoes.Domain.Errors;
using Unifesspa.UniPlus.Publicacoes.Domain.Interfaces;
using Unifesspa.UniPlus.Publicacoes.Domain.ValueObjects;

/// <summary>
/// O que o ato que emenda outro herda e deve respeitar: as invariantes da cadeia de retificação
/// (ADR-0103), a linhagem a que ele passa a pertencer e os vínculos que ele herda.
/// </summary>
public sealed class CadeiaDeRetificacaoDoAto(IAtoNormativoRepository atos)
{
    private const string RetificadoInexistenteMensagem = "O ato retificado informado não corresponde a nenhum ato registrado.";

    private const string ClasseDivergenteMensagem =
        "A classe de congelamento do ato que retifica deve coincidir com a do ato retificado: "
        + "um ato não congelante não emenda um congelante, nem o inverso.";

    /// <summary>
    /// Verifica as invariantes da cadeia que dependem do ato retificado e devolve todas as violações:
    /// ato inexistente, classe de congelamento divergente e ato já retificado. A classe só é conferida
    /// quando o tipo do ato que retifica foi resolvido. A garantia dura da linearidade contra a corrida
    /// é do índice único parcial; esta consulta dá a mensagem legível no caso comum.
    /// </summary>
    public async Task<IReadOnlyList<FieldError>> ConferirAsync(
        Guid atoRetificadoId, TipoDoAtoNoRegistro? tipo, CancellationToken cancellationToken)
    {
        AtoNormativo? retificado = await atos
            .ObterPorIdParaLeituraAsync(atoRetificadoId, cancellationToken)
            .ConfigureAwait(false);

        return retificado is null
            ? [Recusa(AtoNormativoErrorCodes.AtoRetificadoNaoEncontrado, RetificadoInexistenteMensagem)]
            : await ConferirRetificadoAsync(retificado, tipo, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Indica se as recusas da cadeia não incluem a de ato retificado inexistente.</summary>
    public static bool RetificadoExiste(IReadOnlyList<FieldError> recusasDaCadeia)
    {
        ArgumentNullException.ThrowIfNull(recusasDaCadeia);

        return recusasDaCadeia.All(recusa => recusa.Error.Code != AtoNormativoErrorCodes.AtoRetificadoNaoEncontrado);
    }

    /// <summary>Os vínculos do ato retificado, que a retificação herda.</summary>
    public async Task<IReadOnlyList<EntidadeVinculada>> ListarVinculosHerdadosAsync(
        Guid atoRetificadoId, CancellationToken cancellationToken) =>
        await atos.ListarVinculosDoAtoAsync(atoRetificadoId, cancellationToken).ConfigureAwait(false);

    /// <summary>
    /// A linhagem a que o ato pertence (os ids da cadeia do ato retificado, vazia quando o ato não
    /// emenda ninguém) e os vínculos que ele passa a ter: os declarados e os herdados do retificado.
    /// </summary>
    public async Task<LinhagemDoAto> ResolverAsync(
        Guid? atoRetificadoId,
        IReadOnlyList<EntidadeVinculada> declarados,
        IReadOnlyList<EntidadeVinculada> herdados,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(declarados);
        ArgumentNullException.ThrowIfNull(herdados);

        IReadOnlyList<Guid> ids = atoRetificadoId is { } retificadoId
            ? await atos.ListarIdsDaCadeiaAsync(retificadoId, cancellationToken).ConfigureAwait(false)
            : [];
        return new LinhagemDoAto(ids, VinculosDoAto(declarados, herdados));
    }

    /// <summary>
    /// A raiz da linhagem é a chave da vaga do objeto: o próprio ato, quando não emenda ninguém;
    /// a raiz da cadeia do ato retificado, quando emenda. Uma retificação não disputa a vaga que a
    /// sua própria linhagem já ocupa.
    /// </summary>
    public async Task<Guid> ObterRaizAsync(
        Guid? atoRetificadoId, Guid atoId, CancellationToken cancellationToken) =>
        atoRetificadoId is { } retificadoId
            ? await atos.ObterRaizDaCadeiaAsync(retificadoId, cancellationToken).ConfigureAwait(false)
            : atoId;

    /// <summary>
    /// Os vínculos do ato: os declarados no comando, mais, quando o ato emenda outro, os do ato
    /// retificado, que a retificação <b>herda</b>.
    /// </summary>
    /// <remarks>
    /// A herança não é conveniência: sem ela, uma retificação registrada sem repetir os vínculos
    /// sumiria da consulta do certame, e a consulta passaria a exibir a versão superada escondendo
    /// a que a emenda. Acrescentar entidades novas continua permitido: o ato retificador pode
    /// passar a tratar de um objeto que o retificado não declarava. A ordem é determinística, e
    /// não estética: as vagas do objeto são reservadas nesta ordem, e duas transações que reservem
    /// o mesmo par de objetos em ordens opostas travariam uma na outra.
    /// </remarks>
    private static IReadOnlyList<EntidadeVinculada> VinculosDoAto(
        IReadOnlyList<EntidadeVinculada> declarados, IReadOnlyList<EntidadeVinculada> herdados)
    {
        HashSet<EntidadeVinculada> vistos = [.. declarados];
        IEnumerable<EntidadeVinculada> todos = declarados.Concat(herdados.Where(vistos.Add));

        return [.. todos.OrderBy(v => v.Tipo, StringComparer.Ordinal).ThenBy(v => v.Id)];
    }

    /// <summary>
    /// A classe de congelamento do ato que retifica deve coincidir com a do retificado: é essa
    /// classe, não o rótulo do tipo, que protege a integridade da configuração publicada
    /// (UNI-REQ-0019). Depois, a linearidade: se o ato já foi retificado, a nova retificação
    /// deveria emendar a cabeça da cadeia, e a recusa nomeia quem já a retificou.
    /// </summary>
    private async Task<IReadOnlyList<FieldError>> ConferirRetificadoAsync(
        AtoNormativo retificado, TipoDoAtoNoRegistro? tipo, CancellationToken cancellationToken)
    {
        AtoNormativo? retificador = await atos
            .ObterRetificadorAsync(retificado.Id, cancellationToken)
            .ConfigureAwait(false);

        return
        [
            .. tipo is { } resolvido && resolvido.CongelaConfiguracao != retificado.CongelaConfiguracao
                ? [Recusa(AtoNormativoErrorCodes.ClasseDeCongelamentoDivergente, ClasseDivergenteMensagem)]
                : Array.Empty<FieldError>(),
            .. retificador is null
                ? Array.Empty<FieldError>()
                : [Recusa(AtoNormativoErrorCodes.AtoJaRetificado, AtoJaRetificadoMensagem(retificador))],
        ];
    }

    private static FieldError Recusa(string codigo, string mensagem) =>
        new("atoRetificadoId", new DomainError(codigo, mensagem));

    private static string AtoJaRetificadoMensagem(AtoNormativo retificador) =>
        $"O ato que se tentou retificar já foi retificado pelo ato {retificador.Numeracao.Descrever()}. "
        + "A cadeia é linear: retifique a cabeça da cadeia, não um ato já retificado.";
}

/// <summary>
/// A linhagem a que o ato pertence, pelos ids da cadeia do ato que ele retifica, e os vínculos do
/// ato, declarados e herdados, em ordem determinística.
/// </summary>
public sealed record LinhagemDoAto(
    IReadOnlyList<Guid> IdsDaPropriaCadeia,
    IReadOnlyList<EntidadeVinculada> Vinculos);
