namespace Unifesspa.UniPlus.Selecao.Application.Queries.Inscricoes;

using DTOs;

using Kernel.Results;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;

/// <summary>
/// A avaliação das respostas do dono do token como estão na tela (ADR-0143,
/// `POST .../inscricao/avaliacoes`): as modalidades em que ele concorre e o que falta responder, na
/// versão do formulário fixada ao iniciar. Não grava nada.
/// </summary>
/// <remarks>As respostas carregam dado pessoal e sensível: não são gravadas nem registradas em log.</remarks>
public sealed record AvaliarInscricaoQuery(Guid ProcessoSeletivoId, AvaliacaoDaInscricaoRequest Request)
    : IQuery<Result<AvaliacaoDaInscricaoDto>>;
