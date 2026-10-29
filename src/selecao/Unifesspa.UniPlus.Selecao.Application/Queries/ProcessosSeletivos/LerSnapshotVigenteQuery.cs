namespace Unifesspa.UniPlus.Selecao.Application.Queries.ProcessosSeletivos;

using System.Text.Json.Nodes;

using Domain.Interfaces;

using DTOs;

using Services;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Authorization.Abstractions;
using Unifesspa.UniPlus.Kernel.Results;

using Wolverine.Attributes;

/// <summary>
/// A leitura do snapshot vigente por quem o consulta: para quem configura o processo, o que
/// <see cref="ObterSnapshotVigenteQuery"/> devolve (a versão vigente no <paramref name="Instante"/>); para quem não
/// configura, a versão DIVULGADA, só com os blocos que o contrato público do certame classifica como públicos — e o
/// <paramref name="Instante"/> não escolhe versão.
/// </summary>
public sealed record LerSnapshotVigenteQuery(Guid ProcessoSeletivoId, DateTimeOffset? Instante) : IQuery<Result<SnapshotVigenteDto>>;

/// <summary>
/// Quem configura lê a versão vigente por tempo, inclusive a que uma retificação já gravou; os demais leem a versão que o
/// certame divulgou, filtrada pela classificação de <see cref="ClassificacaoDosBlocosDoCertame"/>.
/// </summary>
public static class LerSnapshotVigenteQueryHandler
{
    /// <summary>
    /// <c>[NonTransactional]</c>: a decisão lê o <c>AutorizacaoDbContext</c> e o repositório, o <c>SelecaoDbContext</c>; o
    /// detector de transação do Wolverine recusa dois contextos. A consulta só lê.
    /// </summary>
    [NonTransactional]
    public static async Task<Result<SnapshotVigenteDto>> Handle(
        LerSnapshotVigenteQuery query,
        IProcessoSeletivoRepository processos,
        ICertameDivulgadoRepository certameDivulgado,
        IVerificadorDeAcesso verificador,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(processos);
        ArgumentNullException.ThrowIfNull(certameDivulgado);

        EstadoDoProcesso? estado = await processos
            .ObterEstadoAsync(query.ProcessoSeletivoId, cancellationToken).ConfigureAwait(false);
        bool configura = estado is not null
            && await AcessoAoProcesso
                .ConfiguraAsync(verificador, estado.UnidadeId, query.ProcessoSeletivoId, cancellationToken)
                .ConfigureAwait(false);

        if (configura)
        {
            return await ObterSnapshotVigenteQueryHandler
                .Handle(new ObterSnapshotVigenteQuery(query.ProcessoSeletivoId, query.Instante), processos, timeProvider, cancellationToken)
                .ConfigureAwait(false);
        }

        // Sob retificação cuja configuração já foi gravada e cujo ato ainda não foi divulgado, a versão
        // mais nova por tempo não tem publicidade: quem não configura lê a que a divulgação aponta.
        Result<VersaoDivulgada> divulgada = await VersaoDivulgadaResolver
            .ResolverAsync(query.ProcessoSeletivoId, certameDivulgado, processos, cancellationToken)
            .ConfigureAwait(false);
        if (divulgada.IsFailure)
        {
            return Result<SnapshotVigenteDto>.Failure(divulgada.Error!);
        }

        SnapshotVigenteDto snapshot = ObterSnapshotVigenteQueryHandler.Projetar(divulgada.Value!.Versao);
        return Result<SnapshotVigenteDto>.Success(snapshot with { Configuracao = SoOsPublicos(snapshot.Configuracao) });
    }

    private static JsonObject SoOsPublicos(JsonNode configuracao)
    {
        JsonObject publicos = [];
        if (configuracao is JsonObject envelope)
        {
            foreach ((string bloco, JsonNode? valor) in envelope.Where(static b => ClassificacaoDosBlocosDoCertame.Publicados.Contains(b.Key)))
            {
                publicos[bloco] = valor?.DeepClone();
            }
        }

        return publicos;
    }
}
