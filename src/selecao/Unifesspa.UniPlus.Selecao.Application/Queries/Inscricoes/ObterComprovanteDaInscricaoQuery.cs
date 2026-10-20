namespace Unifesspa.UniPlus.Selecao.Application.Queries.Inscricoes;

using DTOs;

using Kernel.Results;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;

/// <summary>
/// O comprovante número <paramref name="Numero"/> de uma inscrição do dono do token, pelo id dela
/// (`GET /api/selecao/inscricoes/{id}/comprovantes/{n}`). É a rota da inscrição cancelada, que a rota
/// por processo não alcança: ali só a ativa responde.
/// </summary>
public sealed record ObterComprovanteDaInscricaoQuery(Guid InscricaoId, int Numero) : IQuery<Result<ComprovanteInscricaoDto>>;
