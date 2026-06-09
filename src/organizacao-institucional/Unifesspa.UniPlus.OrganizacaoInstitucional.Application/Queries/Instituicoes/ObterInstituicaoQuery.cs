namespace Unifesspa.UniPlus.OrganizacaoInstitucional.Application.Queries.Instituicoes;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Application.DTOs;

/// <summary>
/// Obtém a Instituição singleton viva, ou <see langword="null"/> se nenhuma foi
/// cadastrada. Não há parâmetro de seleção — há no máximo uma (ADR-0055).
/// </summary>
public sealed record ObterInstituicaoQuery : IQuery<InstituicaoDto?>;
