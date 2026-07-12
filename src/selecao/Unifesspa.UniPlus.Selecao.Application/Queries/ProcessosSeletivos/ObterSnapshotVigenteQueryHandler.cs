namespace Unifesspa.UniPlus.Selecao.Application.Queries.ProcessosSeletivos;

using System.Text.Json.Nodes;

using Domain.Entities;
using Domain.Interfaces;
using Unifesspa.UniPlus.Kernel.Results;
using DTOs;

/// <summary>
/// Handler convention-based do <see cref="ObterSnapshotVigenteQuery"/> (RN08,
/// Story #759 T6 #787, ADR-0075/0076/0068): resolve o instante (explícito ou
/// default do <c>TimeProvider</c>), seleciona a publicação vigente e projeta
/// o snapshot congelado. Distingue 404 (processo inexistente) de 422
/// (<c>Snapshot.VigenteAusente</c>) — nunca retorno silencioso.
/// </summary>
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

        // ADR-0075/0068: o instante é sempre explícito no seletor; quando o
        // cliente o omite, o default vem do TimeProvider injetado — nunca de um
        // relógio lido dentro do repositório por trás do contrato.
        DateTimeOffset instante = query.Instante ?? timeProvider.GetUtcNow();

        (Edital Edital, VersaoConfiguracao Versao)? vigente = await processoSeletivoRepository
            .ObterSnapshotVigenteAsync(query.ProcessoSeletivoId, instante, cancellationToken)
            .ConfigureAwait(false);

        if (vigente is { } resolvido)
        {
            return Result<SnapshotVigenteDto>.Success(MapearDto(resolvido.Edital, resolvido.Versao));
        }

        // Só quando não há vigente distingue 404 de 422 — o caminho comum
        // (existe publicação vigente) resolve em uma única consulta.
        bool existe = await processoSeletivoRepository
            .ExisteAsync(query.ProcessoSeletivoId, cancellationToken)
            .ConfigureAwait(false);

        return existe
            ? Result<SnapshotVigenteDto>.Failure(new DomainError(
                "Snapshot.VigenteAusente",
                $"Nenhuma publicação vigente para o instante {instante:O}."))
            : Result<SnapshotVigenteDto>.Failure(new DomainError(
                "ProcessoSeletivo.NaoEncontrado",
                $"Processo Seletivo {query.ProcessoSeletivoId} não encontrado."));
    }

    // O contrato de leitura não muda com a ADR-0104 (o mecanismo, sim): o
    // identificador forense devolvido é o da VERSÃO congelada — a mesma
    // referência durável que o ProcessoPublicadoEvent carrega —, e o hash do ato
    // criador é o hash do documento do Edital que a criou. #803 revisita os
    // campos que ainda vêm do documento (data e natureza).
    private static SnapshotVigenteDto MapearDto(Edital edital, VersaoConfiguracao versao) => new(
        versao.Id,
        edital.DataPublicacao!.Value,
        edital.Natureza.ToString(),
        versao.SchemaVersion,
        versao.AlgoritmoHash,
        versao.HashConfiguracao,
        versao.AtoCriadorHash,
        JsonNode.Parse(versao.ConfiguracaoCongelada)!);
}
