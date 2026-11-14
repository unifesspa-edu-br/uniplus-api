namespace Unifesspa.UniPlus.Autorizacao.Infrastructure.Persistence;

using System.Reflection;

using Microsoft.EntityFrameworkCore;

using Unifesspa.UniPlus.Kernel.Domain.Enums;

/// <summary>
/// As funções do banco que as consultas de acesso usam. A ordem da classificação, criada pelo script 001_ordem_da_classificacao.sql,
/// segue a do enum <see cref="ClassificacaoDeProtecao"/>, e um teste confere que as duas batem.
/// </summary>
internal static class FuncoesDoBanco
{
    /// <summary>O nome da função SQL, criada pelo script 001_ordem_da_classificacao.sql.</summary>
    public const string OrdemDaClassificacaoSql = "ordem_da_classificacao";

    /// <summary>A posição da classificação na escala de proteção; só existe traduzida para SQL.</summary>
    [DbFunction(OrdemDaClassificacaoSql, AutorizacaoContribuicao.NomeDoEsquema)]
    public static int OrdemDaClassificacao(ClassificacaoDeProtecao classificacao) =>
        throw new NotSupportedException("A função só é traduzida para SQL.");

    internal static MethodInfo MetodoDaOrdemDaClassificacao { get; } =
        typeof(FuncoesDoBanco).GetMethod(nameof(OrdemDaClassificacao))!;
}
