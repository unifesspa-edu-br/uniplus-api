namespace Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;

using Unifesspa.UniPlus.Kernel.Domain.Enums;

/// <summary>O que o catálogo da instância registra de uma permissão, para o domínio gravar e comparar.</summary>
public sealed record LinhaDoRetratoDoCatalogo(string Codigo, string Modulo, ClassificacaoDeProtecao Classificacao, bool DeUnidade, bool Reservada);
