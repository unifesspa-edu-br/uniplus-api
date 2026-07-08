namespace Unifesspa.UniPlus.Selecao.Application.Commands.DocumentosEdital;

using Abstractions;
using DTOs;
using Domain.Entities;
using Domain.Interfaces;
using Kernel.Results;

/// <summary>
/// Handler convention-based de <see cref="IniciarUploadDocumentoEditalCommand"/>:
/// valida que o processo existe, cria o registro pendente e devolve a URL
/// pre-assinada de PUT (TTL curto — <see cref="TtlUpload"/>).
/// </summary>
public static class IniciarUploadDocumentoEditalCommandHandler
{
    /// <summary>TTL da URL pre-assinada de upload — curto por design (ADR-0027-adjacent: janela mínima de exposição).</summary>
    public static readonly TimeSpan TtlUpload = TimeSpan.FromMinutes(15);

    public static async Task<Result<IniciarUploadDocumentoEditalDto>> Handle(
        IniciarUploadDocumentoEditalCommand command,
        IProcessoSeletivoRepository processoSeletivoRepository,
        IDocumentoEditalRepository documentoEditalRepository,
        IDocumentoEditalStorage storage,
        ISelecaoUnitOfWork unitOfWork,
        TimeProvider clock,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(processoSeletivoRepository);
        ArgumentNullException.ThrowIfNull(documentoEditalRepository);
        ArgumentNullException.ThrowIfNull(storage);
        ArgumentNullException.ThrowIfNull(unitOfWork);
        ArgumentNullException.ThrowIfNull(clock);

        ProcessoSeletivo? processo = await processoSeletivoRepository
            .ObterPorIdAsync(command.ProcessoSeletivoId, cancellationToken)
            .ConfigureAwait(false);
        if (processo is null)
        {
            return Result<IniciarUploadDocumentoEditalDto>.Failure(new DomainError(
                "ProcessoSeletivo.NaoEncontrado", "Processo Seletivo não encontrado."));
        }

        DocumentoEdital documento = DocumentoEdital.IniciarPendente(processo.Id, clock, TtlUpload);

        // ObjectKey já está determinada (deriva do Id gerado em memória) — dá
        // para gerar a URL antes de persistir. Se o MinIO falhar aqui, nada é
        // commitado: sem isso, uma falha depois do SaveChanges deixaria uma
        // linha pendente órfã e, sob retry com a mesma Idempotency-Key, o
        // filtro trataria a exceção como pré-conclusão e criaria outra.
        string urlUpload = await storage
            .GerarUrlUploadAsync(documento.ObjectKey, TtlUpload, cancellationToken)
            .ConfigureAwait(false);

        await documentoEditalRepository.AdicionarAsync(documento, cancellationToken).ConfigureAwait(false);
        await unitOfWork.SalvarAlteracoesAsync(cancellationToken).ConfigureAwait(false);

        return Result<IniciarUploadDocumentoEditalDto>.Success(
            new IniciarUploadDocumentoEditalDto(
                documento.Id,
                new Uri(urlUpload, UriKind.Absolute),
                DocumentoEdital.ContentTypeEsperado,
                documento.ExpiraEm));
    }
}
