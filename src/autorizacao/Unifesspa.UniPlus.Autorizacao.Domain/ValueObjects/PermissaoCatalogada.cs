namespace Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;

using Unifesspa.UniPlus.Kernel.Domain.Enums;

/// <summary>Uma permissão como o catálogo a descrevia ao criar a versão: o retrato que a versão guarda.</summary>
public sealed record PermissaoCatalogada(string Codigo, ClassificacaoDeProtecao Classificacao, bool DeUnidade);
