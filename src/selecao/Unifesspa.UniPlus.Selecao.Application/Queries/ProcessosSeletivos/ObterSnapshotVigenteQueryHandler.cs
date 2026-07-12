namespace Unifesspa.UniPlus.Selecao.Application.Queries.ProcessosSeletivos;

using System.Text.Json.Nodes;

using Domain.Entities;
using Domain.Interfaces;
using Unifesspa.UniPlus.Kernel.Results;
using DTOs;

/// <summary>
/// Handler convention-based do <see cref="ObterSnapshotVigenteQuery"/> (RN08,
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

        // ADR-0075/0068: o instante é sempre explícito no seletor; quando o
        // cliente o omite, o default vem do TimeProvider injetado — nunca de um
        // relógio lido dentro do repositório por trás do contrato.
        //
        // O "agora" é o relógio como ele está, sem correção. Se o host regredir
        // para antes da vigência da versão corrente, a consulta corrente aflora
        // Snapshot.VigenteAusente até o relógio se recuperar — e não uma versão
        // que ainda não vigora. Um "agora" artificialmente monotônico daria uma
        // configuração como vigente contra um presente que o próprio sistema não
        // reconhece, mascarando o relógio errado em vez de expô-lo (ADR-0076).
        DateTimeOffset instante = query.Instante ?? timeProvider.GetUtcNow();

        VersaoConfiguracao? versao = await processoSeletivoRepository
            .ObterVersaoVigenteAsync(query.ProcessoSeletivoId, instante, cancellationToken)
            .ConfigureAwait(false);

        if (versao is null)
        {
            // Só quando não há vigente distingue 404 de 422 — o caminho comum
            // (existe configuração vigente) não paga essa consulta.
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

        // O identificador forense devolvido é o da VERSÃO congelada — a mesma
        // referência durável que o ProcessoPublicadoEvent carrega. O ato entra por
        // VALOR, com o par {id, hash} que a versão já guarda: nenhuma consulta a
        // Publicações, e portanto nenhuma espera pela drenagem do outbox (ADR-0108).
        return Result<SnapshotVigenteDto>.Success(new SnapshotVigenteDto(
            versao.Id,
            versao.AtoCriadorId,
            versao.SchemaVersion,
            versao.AlgoritmoHash,
            versao.HashConfiguracao,
            versao.AtoCriadorHash,
            JsonNode.Parse(versao.ConfiguracaoCongelada)!));
    }
}
