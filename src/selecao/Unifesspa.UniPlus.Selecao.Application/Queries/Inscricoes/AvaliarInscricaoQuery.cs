namespace Unifesspa.UniPlus.Selecao.Application.Queries.Inscricoes;

using DTOs;

using Kernel.Results;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;

/// <summary>
/// A avaliação das respostas do dono do token como estão na tela (ADR-0143,
/// `POST .../inscricao/avaliacoes`): as modalidades em que ele concorre, os documentos que o edital
/// exige dele e o que falta responder, na versão do formulário em que está a inscrição (a divulgada, no rascunho). Não grava nada.
/// <see cref="FaseId"/> restringe os documentos a uma fase do cronograma da versão; nulo, vêm todas.
/// </summary>
/// <remarks>As respostas carregam dado pessoal e sensível: não são gravadas nem registradas em log.</remarks>
public sealed record AvaliarInscricaoQuery(Guid ProcessoSeletivoId, AvaliacaoDaInscricaoRequest Request, Guid? FaseId = null)
    : IQuery<Result<AvaliacaoDaInscricaoDto>>;
