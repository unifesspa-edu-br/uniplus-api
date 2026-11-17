namespace Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;

using Unifesspa.UniPlus.Kernel.Domain.Enums;
using Unifesspa.UniPlus.Kernel.Domain.ValueObjects;

/// <summary>O que o catálogo da instância registra de uma permissão, para o domínio gravar e comparar.</summary>
public sealed record PermissaoDoCatalogo(CodigoDePermissao Codigo, ClassificacaoDeProtecao Classificacao, bool DeUnidade, bool Reservada);
