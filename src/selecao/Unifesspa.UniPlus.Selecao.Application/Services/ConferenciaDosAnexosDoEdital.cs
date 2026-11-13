namespace Unifesspa.UniPlus.Selecao.Application.Services;

using Commands.ProcessosSeletivos;

using Domain.Entities;
using Domain.Errors;
using Domain.Interfaces;
using Domain.ValueObjects;

using Kernel.Results;

/// <summary>
/// A conferência dos anexos do edital na publicação, na retificação e no fechamento da retificação:
/// cada documento existe, é do processo e está confirmado — antes de qualquer escrita, porque o ato
/// é registrado depois, por mensagem durável, e a recusa tardia viraria dead letter.
/// </summary>
public sealed class ConferenciaDosAnexosDoEdital(IDocumentoAPublicarRepository repositorio)
{
    /// <summary>
    /// Os anexos da versão que nasce.
    /// </summary>
    /// <param name="processoSeletivoId">O processo do edital.</param>
    /// <param name="solicitados">
    /// O que o administrador pediu. Nulo mantém <paramref name="vigentes"/>, que já foram conferidos
    /// quando congelaram; uma lista é o conjunto inteiro da versão nova.
    /// </param>
    /// <param name="vigentes">Os anexos da versão em vigor, vazios na primeira publicação.</param>
    /// <param name="cancellationToken">Token de cancelamento.</param>
    /// <remarks>
    /// O anexo que continua o mesmo — mesmo documento, mesmo nome — mantém a identidade. Documento diferente ou nome diferente é outro anexo, com
    /// identidade nova. Mesmo o anexo que continua tem endereço novo na versão nova, porque a chave
    /// inclui o ato; os endereços da versão anterior seguem servindo o que serviam.
    /// </remarks>
    public async Task<Result<IReadOnlyList<AnexoDoEdital>>> ConferirAsync(
        Guid processoSeletivoId,
        IReadOnlyList<AnexoSolicitadoDoEdital>? solicitados,
        IReadOnlyList<AnexoDoEdital> vigentes,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(vigentes);

        return solicitados is null
            ? Result<IReadOnlyList<AnexoDoEdital>>.Success(vigentes)
            : await ConferirOsSolicitadosAsync(processoSeletivoId, solicitados, vigentes, cancellationToken).ConfigureAwait(false);
    }

    private async Task<Result<IReadOnlyList<AnexoDoEdital>>> ConferirOsSolicitadosAsync(
        Guid processoSeletivoId,
        IReadOnlyList<AnexoSolicitadoDoEdital> solicitados,
        IReadOnlyList<AnexoDoEdital> vigentes,
        CancellationToken cancellationToken)
    {
        if (solicitados.Count > AnexoDoEdital.QuantidadeMaxima)
        {
            return Result<IReadOnlyList<AnexoDoEdital>>.Failure(new DomainError(
                AnexoDoEditalErrorCodes.QuantidadeExcedida,
                $"O edital aceita no máximo {AnexoDoEdital.QuantidadeMaxima} anexos."));
        }

        IReadOnlyList<DocumentoAPublicar> documentos = await repositorio
            .ListarDoProcessoAsync(processoSeletivoId, [.. solicitados.Select(static s => s.DocumentoId).Distinct()], cancellationToken)
            .ConfigureAwait(false);

        return AnexosDosPedidos(solicitados, documentos, vigentes);
    }

    private static Result<IReadOnlyList<AnexoDoEdital>> AnexosDosPedidos(
        IReadOnlyList<AnexoSolicitadoDoEdital> solicitados, IReadOnlyList<DocumentoAPublicar> documentos, IReadOnlyList<AnexoDoEdital> vigentes)
    {
        List<AnexoDoEdital> anexos = [];
        foreach (AnexoSolicitadoDoEdital pedido in solicitados)
        {
            Result<AnexoDoEdital> anexo = AnexoDoPedido(pedido, documentos);
            if (anexo.IsFailure)
            {
                return Result<IReadOnlyList<AnexoDoEdital>>.Failure(anexo.Error!);
            }

            anexos.Add(ComAIdentidadeDoQueContinua(anexo.Value!, vigentes));
        }

        return Result<IReadOnlyList<AnexoDoEdital>>.Success(anexos);
    }

    private static Result<AnexoDoEdital> AnexoDoPedido(AnexoSolicitadoDoEdital pedido, IReadOnlyList<DocumentoAPublicar> documentos)
    {
        DocumentoAPublicar? documento = documentos.FirstOrDefault(d => d.Id == pedido.DocumentoId);

        return documento is null
            ? Result<AnexoDoEdital>.Failure(new DomainError(
                AnexoDoEditalErrorCodes.NaoEncontrado,
                $"Documento {pedido.DocumentoId} não encontrado ou não pertence a este processo."))
            : AnexoDoEdital.Criar(Guid.CreateVersion7(), documento, pedido.Nome);
    }

    private static AnexoDoEdital ComAIdentidadeDoQueContinua(AnexoDoEdital anexo, IReadOnlyList<AnexoDoEdital> vigentes)
    {
        AnexoDoEdital? continuado = vigentes.FirstOrDefault(v =>
            v.DocumentoId == anexo.DocumentoId && string.Equals(v.NomeArquivo, anexo.NomeArquivo, StringComparison.Ordinal));

        return continuado is null ? anexo : anexo with { Id = continuado.Id };
    }
}
