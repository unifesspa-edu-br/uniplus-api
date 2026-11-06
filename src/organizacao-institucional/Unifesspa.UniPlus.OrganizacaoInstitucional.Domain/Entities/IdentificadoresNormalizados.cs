namespace Unifesspa.UniPlus.OrganizacaoInstitucional.Domain.Entities;

using Unifesspa.UniPlus.OrganizacaoInstitucional.Domain.ValueObjects;

/// <summary>Slug, Sigla, Codigo e Alias já normalizados, os quatro que têm histórico de vigência.</summary>
internal readonly record struct IdentificadoresNormalizados(Slug Slug, string Sigla, string Codigo, string? Alias);
