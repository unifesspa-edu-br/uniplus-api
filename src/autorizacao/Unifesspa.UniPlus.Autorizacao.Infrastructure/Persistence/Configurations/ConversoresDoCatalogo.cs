namespace Unifesspa.UniPlus.Autorizacao.Infrastructure.Persistence.Configurations;

using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

using Unifesspa.UniPlus.Kernel.Domain.Enums;

/// <summary>Os conversores de texto compartilhados pelas tabelas da reclassificação do catálogo.</summary>
internal static class ConversoresDoCatalogo
{
    /// <summary>A classificação como texto em minúsculas, o mesmo formato gravado nas versões dos perfis.</summary>
    public static readonly ValueConverter<ClassificacaoDeProtecao, string> Classificacao = new(
        classificacao => classificacao.ToString().ToLowerInvariant(),
        codigo => Enum.Parse<ClassificacaoDeProtecao>(codigo, true));

    public static readonly ValueConverter<ClassificacaoDeProtecao?, string?> ClassificacaoOpcional = new(
        classificacao => classificacao == null ? null : classificacao.Value.ToString().ToLowerInvariant(),
        codigo => codigo == null ? null : Enum.Parse<ClassificacaoDeProtecao>(codigo, true));
}
