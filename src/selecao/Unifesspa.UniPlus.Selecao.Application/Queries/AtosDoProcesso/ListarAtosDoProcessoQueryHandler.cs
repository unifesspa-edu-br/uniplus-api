namespace Unifesspa.UniPlus.Selecao.Application.Queries.AtosDoProcesso;

using Microsoft.Extensions.Options;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Application.DTOs;
using Unifesspa.UniPlus.Selecao.Application.Mappings;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;

/// <summary>
/// Lista os atos do processo, do mais recente para o mais antigo, com a situação e os documentos.
/// </summary>
/// <remarks>
/// A fonte é só o registro do ato do processo, que o edital de abertura e o de retificação também
/// têm: tipo, número, data e assinante de todos vêm do mesmo lugar. A pendência acima do limiar é
/// destacada, e só isso: a recusa do registro não é respondida, então a consulta é a única forma de
/// a pendência chegar a quem possa resolvê-la.
/// </remarks>
public static class ListarAtosDoProcessoQueryHandler
{
    public static async Task<Result<ListarAtosDoProcessoResult>> Handle(
        ListarAtosDoProcessoQuery query,
        IProcessoSeletivoRepository processoSeletivoRepository,
        IAtoDoProcessoRepository atoDoProcessoRepository,
        IDocumentoAPublicarRepository documentoAPublicarRepository,
        IOptions<AtosDoProcessoOptions> opcoes,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(processoSeletivoRepository);
        ArgumentNullException.ThrowIfNull(atoDoProcessoRepository);
        ArgumentNullException.ThrowIfNull(documentoAPublicarRepository);
        ArgumentNullException.ThrowIfNull(opcoes);
        ArgumentNullException.ThrowIfNull(timeProvider);

        bool existe = await processoSeletivoRepository
            .ExisteAsync(query.ProcessoSeletivoId, cancellationToken)
            .ConfigureAwait(false);
        if (!existe)
        {
            return Result<ListarAtosDoProcessoResult>.Failure(new DomainError(
                "ProcessoSeletivo.NaoEncontrado",
                $"Processo Seletivo {query.ProcessoSeletivoId} não encontrado."));
        }

        IReadOnlyList<AtoDoProcesso> atos = await atoDoProcessoRepository
            .ListarDoProcessoAsync(query.ProcessoSeletivoId, cancellationToken)
            .ConfigureAwait(false);

        // Só os documentos que os atos listados referenciam: o histórico de envios do processo inclui
        // pendentes e documentos que nenhum ato usou.
        Guid[] idsDosDocumentos =
        [
            .. atos.SelectMany(static a => a.Anexos.Select(static x => x.DocumentoAPublicarId).Append(a.DocumentoPrincipalId)).Distinct(),
        ];
        IReadOnlyList<DocumentoAPublicar> documentos = await documentoAPublicarRepository
            .ListarDoProcessoAsync(query.ProcessoSeletivoId, idsDosDocumentos, cancellationToken)
            .ConfigureAwait(false);

        DateTimeOffset limite = timeProvider.GetUtcNow() - opcoes.Value.LimiarDePendencia;
        Dictionary<Guid, DocumentoAPublicar> documentosPorId = documentos.ToDictionary(static d => d.Id);

        // A ordem é a do repositório (instante de gravação decrescente, ato como desempate); reordenar
        // aqui desfaria o que o contrato promete.
        AtoDoProcessoDto[] itens =
        [
            .. atos.Select(a => AtoDoProcessoMapping.ToDto(
                a,
                documentosPorId,
                AtoDoProcessoMapping.PendenciaAcimaDoLimiar(a, limite))),
        ];

        return Result<ListarAtosDoProcessoResult>.Success(new ListarAtosDoProcessoResult(itens));
    }
}
