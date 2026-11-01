namespace Unifesspa.UniPlus.Selecao.Application.Queries.InscricoesDoProcesso;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Application.DTOs;
using Unifesspa.UniPlus.Selecao.Application.DTOs.Administrativos;

/// <summary>Pede a inscrição enviada do processo na visão de quem o opera, projetada pela permissão da classe de cada item.</summary>
public sealed record ObterInscricaoAdministrativaQuery(Guid ProcessoSeletivoId, Guid InscricaoId) : IQuery<Result<InscricaoAdministrativaDto>>;
