namespace Unifesspa.UniPlus.Selecao.Application.Services;

using Commands.ProcessosSeletivos;

using Domain.Entities;
using Domain.Errors;
using Domain.Interfaces;
using Domain.ValueObjects;

using Kernel.Results;

/// <summary>
/// A conferência dos anexos do edital na publicação e na retificação:
/// cada documento existe, é do processo e está confirmado — antes de qualquer escrita, porque a
/// recusa tardia desfaria o que a transação já rastreou.
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

        // O documento ausente já foi recusado na conferência do ato: aqui todo anexo tem documento, e o padrão só satisfaz o tipo.
        IReadOnlyList<DocumentoAPublicar> documentos = await repositorio
            .ListarDoProcessoAsync(processoSeletivoId, [.. solicitados.Select(static s => s.DocumentoId.GetValueOrDefault()).Distinct()], cancellationToken)
            .ConfigureAwait(false);

        return AnexosDosPedidos(solicitados, documentos, vigentes);
    }

    private static Result<IReadOnlyList<AnexoDoEdital>> AnexosDosPedidos(
        IReadOnlyList<AnexoSolicitadoDoEdital> solicitados, IReadOnlyList<DocumentoAPublicar> documentos, IReadOnlyList<AnexoDoEdital> vigentes)
    {
        Result<AnexoDoEdital>[] conferidos = [.. solicitados.Select((pedido, posicao) => AnexoDoPedido(pedido, posicao, documentos))];
        FieldError[] recusas = [.. conferidos.SelectMany(static c => c.Errors)];

        return recusas.Length > 0
            ? Result<IReadOnlyList<AnexoDoEdital>>.ValidationFailure(recusas)
            : Result<IReadOnlyList<AnexoDoEdital>>.Success([.. conferidos.Select(c => ComAIdentidadeDoQueContinua(c.Value!, vigentes))]);
    }

    private static Result<AnexoDoEdital> AnexoDoPedido(
        AnexoSolicitadoDoEdital pedido, int posicao, IReadOnlyList<DocumentoAPublicar> documentos)
    {
        DocumentoAPublicar? documento = documentos.FirstOrDefault(d => d.Id == pedido.DocumentoId);

        return documento is null
            ? Result<AnexoDoEdital>.ValidationFailure([new FieldError(
                $"anexos[{posicao}].documentoId",
                new DomainError(AnexoDoEditalErrorCodes.NaoEncontrado, "O documento do anexo deve ser um dos documentos deste processo."))])
            : ComOCampoDoAnexo(AnexoDoEdital.Criar(Guid.CreateVersion7(), documento, pedido.Nome), posicao, AnexoDoEdital.RecusaDoNome(documento, pedido.Nome));
    }

    // As recusas do anexo apontam o item do pedido: o nome inválido no nome, o documento sem confirmação ou de outra finalidade no documento.
    // O documento recusado (outra finalidade ou sem confirmação) vem antes do nome na criação, mas o nome inválido é defeito independente e sai junto.
    private static Result<AnexoDoEdital> ComOCampoDoAnexo(Result<AnexoDoEdital> criado, int posicao, DomainError? recusaDoNome)
    {
        if (criado.IsSuccess)
        {
            return criado;
        }

        IEnumerable<FieldError> doDocumento = criado.Errors
            .Where(static recusa => !string.Equals(recusa.Error.Code, AnexoDoEditalErrorCodes.NomeInvalido, StringComparison.Ordinal))
            .Select(recusa => new FieldError($"anexos[{posicao}].documentoId", recusa.Error));
        IEnumerable<FieldError> doNome = recusaDoNome is null ? [] : [new FieldError($"anexos[{posicao}].nome", recusaDoNome)];

        return Result<AnexoDoEdital>.ValidationFailure([.. doDocumento, .. doNome]);
    }

    private static AnexoDoEdital ComAIdentidadeDoQueContinua(AnexoDoEdital anexo, IReadOnlyList<AnexoDoEdital> vigentes)
    {
        AnexoDoEdital? continuado = vigentes.FirstOrDefault(v =>
            v.DocumentoId == anexo.DocumentoId && string.Equals(v.NomeArquivo, anexo.NomeArquivo, StringComparison.Ordinal));

        return continuado is null ? anexo : anexo with { Id = continuado.Id };
    }
}
