namespace Unifesspa.UniPlus.Publicacoes.Application.Commands.AtosNormativos;

using System.Globalization;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Publicacoes.Domain.Entities;
using Unifesspa.UniPlus.Publicacoes.Domain.Errors;
using Unifesspa.UniPlus.Publicacoes.Domain.Interfaces;

/// <summary>
/// O que o ato que emenda outro herda e deve respeitar: as invariantes da cadeia de retificação
/// (ADR-0103), a linhagem a que ele passa a pertencer e os vínculos que ele herda.
/// </summary>
public sealed class CadeiaDeRetificacaoDoAto(IAtoNormativoRepository atos)
{
    private const string ClasseDivergenteMensagem =
        "A classe de congelamento do ato que retifica deve coincidir com a do ato retificado: "
        + "um ato não congelante não emenda um congelante, nem o inverso.";

    /// <summary>
    /// Verifica as invariantes da cadeia que dependem do ato retificado. Devolve o
    /// <see cref="DomainError"/> da primeira violação, ou <see langword="null"/> quando o registro
    /// não é retificação ou passa em todas. A garantia dura da linearidade contra a corrida é do
    /// índice único parcial; esta consulta dá a mensagem legível no caso comum.
    /// </summary>
    public async Task<DomainError?> ConferirAsync(
        RegistrarAtoNormativoCommand command, bool congelaConfiguracao, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (command.AtoRetificadoId is not { } retificadoId)
        {
            return null;
        }

        AtoNormativo? retificado = await atos
            .ObterPorIdParaLeituraAsync(retificadoId, cancellationToken)
            .ConfigureAwait(false);

        return retificado is null
            ? new DomainError(AtoNormativoErrorCodes.AtoRetificadoNaoEncontrado, RetificadoInexistenteMensagem(retificadoId))
            : await ConferirRetificadoAsync(retificado, congelaConfiguracao, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// A linhagem a que o ato pertence (os ids da cadeia do ato retificado, vazia quando o ato não
    /// emenda ninguém) e os vínculos que ele passa a ter.
    /// </summary>
    public async Task<LinhagemDoAto> ResolverAsync(
        RegistrarAtoNormativoCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        IReadOnlyList<Guid> ids = command.AtoRetificadoId is { } retificadoId
            ? await atos.ListarIdsDaCadeiaAsync(retificadoId, cancellationToken).ConfigureAwait(false)
            : [];
        return new LinhagemDoAto(
            ids, await ResolverVinculosAsync(command, cancellationToken).ConfigureAwait(false));
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
    private async Task<IReadOnlyList<(string EntidadeTipo, Guid EntidadeId)>> ResolverVinculosAsync(
        RegistrarAtoNormativoCommand command, CancellationToken cancellationToken)
    {
        List<(string EntidadeTipo, Guid EntidadeId)> vinculos = [.. VinculosDeclarados(command)];
        if (command.AtoRetificadoId is { } retificadoId)
        {
            vinculos.AddRange(await HerdadosSemRepetirAsync(retificadoId, vinculos, cancellationToken).ConfigureAwait(false));
        }

        return [.. vinculos.OrderBy(v => v.EntidadeTipo, StringComparer.Ordinal).ThenBy(v => v.EntidadeId)];
    }

    private async Task<IEnumerable<(string EntidadeTipo, Guid EntidadeId)>> HerdadosSemRepetirAsync(
        Guid retificadoId,
        IEnumerable<(string EntidadeTipo, Guid EntidadeId)> declarados,
        CancellationToken cancellationToken)
    {
        HashSet<(string, Guid)> vistos = [.. declarados];
        IReadOnlyList<(string EntidadeTipo, Guid EntidadeId)> herdados = await atos
            .ListarVinculosDoAtoAsync(retificadoId, cancellationToken)
            .ConfigureAwait(false);
        return herdados.Where(vistos.Add);
    }

    /// <summary>
    /// O elemento nulo já foi recusado pelo validator (422); descartá-lo aqui evita que um chamador
    /// interno, sem passar pelo HTTP, derrube a montagem com uma referência nula.
    /// </summary>
    internal static IEnumerable<(string EntidadeTipo, Guid EntidadeId)> VinculosDeclarados(
        RegistrarAtoNormativoCommand command) =>
        (command.Vinculos ?? [])
            .Where(v => v is not null)
            .Select(v => (v.EntidadeTipo.Trim(), v.EntidadeId));

    /// <summary>
    /// A classe de congelamento do ato que retifica deve coincidir com a do retificado: é essa
    /// classe, não o rótulo do tipo, que protege a integridade da configuração publicada
    /// (UNI-REQ-0019). Depois, a linearidade: se a raiz já foi retificada, a nova retificação
    /// deveria emendar a cabeça da cadeia, e a recusa nomeia quem já a retificou.
    /// </summary>
    private async Task<DomainError?> ConferirRetificadoAsync(
        AtoNormativo retificado, bool congelaConfiguracao, CancellationToken cancellationToken)
    {
        if (retificado.CongelaConfiguracao != congelaConfiguracao)
        {
            return new DomainError(AtoNormativoErrorCodes.ClasseDeCongelamentoDivergente, ClasseDivergenteMensagem);
        }

        AtoNormativo? retificador = await atos
            .ObterRetificadorAsync(retificado.Id, cancellationToken)
            .ConfigureAwait(false);

        return retificador is null
            ? null
            : new DomainError(AtoNormativoErrorCodes.RaizJaRetificada, RaizJaRetificadaMensagem(retificado.Id, retificador));
    }

    private static string RetificadoInexistenteMensagem(Guid retificadoId) =>
        string.Format(
            CultureInfo.InvariantCulture,
            "O ato retificado {0} não corresponde a nenhum ato registrado.",
            retificadoId);

    private static string RaizJaRetificadaMensagem(Guid retificadoId, AtoNormativo retificador)
    {
        string numero = retificador.Numero is null ? "sem número" : "nº " + retificador.Numero;
        return string.Format(
            CultureInfo.InvariantCulture,
            "O ato {0} já foi retificado pelo ato {1} ({2} {3}/{4}). A cadeia é linear: "
            + "retifique a cabeça da cadeia, não uma raiz já retificada.",
            retificadoId,
            retificador.Id,
            retificador.Serie,
            numero,
            retificador.Ano);
    }
}

/// <summary>
/// A linhagem a que o ato pertence, pelos ids da cadeia do ato que ele retifica, e os vínculos do
/// ato, declarados e herdados, em ordem determinística.
/// </summary>
public sealed record LinhagemDoAto(
    IReadOnlyList<Guid> IdsDaPropriaCadeia,
    IReadOnlyList<(string EntidadeTipo, Guid EntidadeId)> Vinculos);
