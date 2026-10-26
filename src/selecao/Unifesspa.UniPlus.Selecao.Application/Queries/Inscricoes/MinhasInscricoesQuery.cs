namespace Unifesspa.UniPlus.Selecao.Application.Queries.Inscricoes;

using DTOs;

using Kernel.Results;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;

/// <summary>
/// As inscrições do dono do token em qualquer processo seletivo (issue #1860,
/// `GET /api/selecao/minhas-inscricoes`), com os cursos que escolheram e sem número de documento.
/// </summary>
public sealed record MinhasInscricoesQuery : IQuery<Result<IReadOnlyList<MinhaInscricaoDto>>>;
