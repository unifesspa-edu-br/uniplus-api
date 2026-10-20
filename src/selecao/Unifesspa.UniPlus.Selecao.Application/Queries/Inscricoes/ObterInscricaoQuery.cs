namespace Unifesspa.UniPlus.Selecao.Application.Queries.Inscricoes;

using DTOs;

using Kernel.Results;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;

/// <summary>
/// A inscrição do dono do token no processo (issue #1859, `GET .../inscricao`): respostas,
/// aceites, oferta e situação, na versão do formulário em que está a inscrição (a divulgada, no rascunho) — com o <c>ETag</c> para
/// o <c>If-Match</c> do rascunho e da submissão.
/// </summary>
public sealed record ObterInscricaoQuery(Guid ProcessoSeletivoId) : IQuery<Result<InscricaoDto>>;
