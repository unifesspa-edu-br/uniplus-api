namespace Unifesspa.UniPlus.Selecao.Application.Queries.Inscricoes;

using DTOs;

using Kernel.Results;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;

/// <summary>
/// O comprovante número <paramref name="Numero"/> da inscrição do dono do token (issue #1860,
/// `GET .../inscricao/comprovantes/{n}`): a primeira submissão é o comprovante 1, cada correção
/// emite o próximo — append-only, o anterior nunca muda (CA-01).
/// </summary>
public sealed record ObterComprovanteQuery(Guid ProcessoSeletivoId, int Numero) : IQuery<Result<ComprovanteInscricaoDto>>;
