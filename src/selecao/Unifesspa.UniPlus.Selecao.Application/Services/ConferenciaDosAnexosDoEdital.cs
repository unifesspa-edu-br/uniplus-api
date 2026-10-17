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
public static class ConferenciaDosAnexosDoEdital
{
    /// <summary>
    /// Os anexos da versão que nasce.
    /// </summary>
    /// <param name="repositorio">Leitura dos documentos a publicar.</param>
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
    public static async Task<Result<IReadOnlyList<AnexoDoEdital>>> ConferirAsync(
        IDocumentoAPublicarRepository repositorio,
        Guid processoSeletivoId,
        IReadOnlyList<AnexoSolicitadoDoEdital>? solicitados,
        IReadOnlyList<AnexoDoEdital> vigentes,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(repositorio);
        ArgumentNullException.ThrowIfNull(vigentes);

        if (solicitados is null)
        {
            return Result<IReadOnlyList<AnexoDoEdital>>.Success(vigentes);
        }

        if (solicitados.Count > AnexoDoEdital.QuantidadeMaxima)
        {
            return Result<IReadOnlyList<AnexoDoEdital>>.Failure(new DomainError(
                AnexoDoEditalErrorCodes.QuantidadeExcedida,
                $"O edital aceita no máximo {AnexoDoEdital.QuantidadeMaxima} anexos."));
        }

        IReadOnlyList<DocumentoAPublicar> documentos = await repositorio
            .ListarDoProcessoAsync(processoSeletivoId, [.. solicitados.Select(static s => s.DocumentoId).Distinct()], cancellationToken)
            .ConfigureAwait(false);

        List<AnexoDoEdital> anexos = [];
        foreach (AnexoSolicitadoDoEdital pedido in solicitados)
        {
            DocumentoAPublicar? documento = documentos.FirstOrDefault(d => d.Id == pedido.DocumentoId);
            if (documento is null)
            {
                return Result<IReadOnlyList<AnexoDoEdital>>.Failure(new DomainError(
                    AnexoDoEditalErrorCodes.NaoEncontrado,
                    $"Documento {pedido.DocumentoId} não encontrado ou não pertence a este processo."));
            }

            Result<AnexoDoEdital> anexo = AnexoDoEdital.Criar(Guid.CreateVersion7(), documento, pedido.Nome);
            if (anexo.IsFailure)
            {
                return Result<IReadOnlyList<AnexoDoEdital>>.Failure(anexo.Error!);
            }

            AnexoDoEdital? continuado = vigentes.FirstOrDefault(v =>
                v.DocumentoId == anexo.Value!.DocumentoId
                && string.Equals(v.NomeArquivo, anexo.Value.NomeArquivo, StringComparison.Ordinal));
            anexos.Add(continuado is null ? anexo.Value! : anexo.Value! with { Id = continuado.Id });
        }

        return Result<IReadOnlyList<AnexoDoEdital>>.Success(anexos);
    }
}
