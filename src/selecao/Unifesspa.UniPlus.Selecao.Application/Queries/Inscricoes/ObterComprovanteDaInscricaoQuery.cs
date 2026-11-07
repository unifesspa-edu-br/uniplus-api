namespace Unifesspa.UniPlus.Selecao.Application.Queries.Inscricoes;

using DTOs.Comprovantes;

using Kernel.Results;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;

/// <summary>
/// O comprovante número <paramref name="Numero"/> de uma inscrição do dono do token, pelo id dela
/// (<c>GET /api/selecao/inscricoes/{inscricaoId}/comprovantes/{numero}</c>): o comprovante 1 é o primeiro envio e cada correção
/// emite o próximo, append-only. A inscrição cancelada não tem comprovante para exibir.
/// </summary>
public sealed record ObterComprovanteDaInscricaoQuery(Guid InscricaoId, int Numero) : IQuery<Result<ComprovanteDaInscricaoDto>>;
