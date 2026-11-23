namespace Unifesspa.UniPlus.Selecao.Application.Queries.ProcessosSeletivos;

using System.Text.Json.Nodes;

using Domain.Entities;
using Domain.Interfaces;

using DTOs;

using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Handler convention-based do <see cref="ObterSnapshotVigenteQuery"/> (congelamento dos parâmetros na publicação, UNI-REQ-0019,
/// ADR-0075/0076/0068/0104): resolve o instante (explícito ou default do
/// <c>TimeProvider</c>), seleciona a VERSÃO vigente da configuração e a projeta.
/// Distingue 404 (processo inexistente) de 422 (<c>Snapshot.VigenteAusente</c>) —
/// nunca retorno silencioso.
/// </summary>
/// <remarks>
/// Uma leitura, e uma só: a versão. Não há mais uma segunda consulta para hidratar
/// atributos do documento, porque o documento não é de Seleção (ADR-0103/0105) — o
/// contrato publica dele apenas o par <c>{id, hash}</c>, que a própria versão já
/// carrega por valor (ADR-0061). Não existe, nem existia, caminho em que um atributo
/// do ato decida qual configuração vale.
/// </remarks>
public static class ObterSnapshotVigenteQueryHandler
{
    public static async Task<Result<SnapshotVigenteDto>> Handle(
        ObterSnapshotVigenteQuery query,
        IProcessoSeletivoRepository processoSeletivoRepository,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(processoSeletivoRepository);
        ArgumentNullException.ThrowIfNull(timeProvider);

        DateTimeOffset instante = query.Instante ?? timeProvider.GetUtcNow();
        Result<VersaoConfiguracao> resultado = await SelecaoDaVersaoVigente.ObterAsync(
            query.ProcessoSeletivoId, instante, processoSeletivoRepository, cancellationToken).ConfigureAwait(false);
        return resultado.IsFailure
            ? Result<SnapshotVigenteDto>.Failure(resultado.Error!)
            : Result<SnapshotVigenteDto>.Success(Projetar(resultado.Value!));
    }

    /// <summary>
    /// Projeta a versão congelada no DTO. O identificador forense devolvido é o da VERSÃO — a mesma
    /// referência durável que o <c>ProcessoPublicadoEvent</c> carrega. O ato entra por VALOR, com o par
    /// <c>{id, hash}</c> que a versão já guarda: nenhuma consulta a Publicações no caminho da
    /// requisição.
    /// </summary>
    internal static SnapshotVigenteDto Projetar(VersaoConfiguracao versao) =>
        new(
            versao.Id,
            versao.AtoCriadorId,
            versao.SchemaVersion,
            versao.AlgoritmoHash,
            versao.HashConfiguracao,
            versao.AtoCriadorHash,
            JsonNode.Parse(versao.ConfiguracaoCongelada)!);
}
