namespace Unifesspa.UniPlus.Configuracao.Application.Queries.FatosCandidato;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Configuracao.Application.DTOs;

/// <summary>O fato para manutenção, ativo ou desativado, com os valores de domínio.</summary>
public sealed record ObterFatoCandidatoParaManutencaoQuery(Guid Id) : IQuery<FatoCandidatoDto?>;
