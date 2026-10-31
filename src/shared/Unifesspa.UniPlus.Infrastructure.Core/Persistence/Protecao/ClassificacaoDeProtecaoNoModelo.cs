namespace Unifesspa.UniPlus.Infrastructure.Core.Persistence.Protecao;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using Unifesspa.UniPlus.Kernel.Domain.Enums;
using Unifesspa.UniPlus.Kernel.Domain.Interfaces;

/// <summary>
/// Declaração e resolução da <see cref="ClassificacaoDeProtecao"/> de cada coluna mapeada (ADR-0153).
/// Cada tipo mapeado declara uma classificação padrão e a propriedade só é anotada quando difere dela;
/// os membros de auditoria resolvem <see cref="ClassificacaoDeProtecao.Pessoal"/> pela interface ou pela
/// base. Um tipo owned declara a própria classificação, nunca herda a do dono.
/// </summary>
public static class ClassificacaoDeProtecaoNoModelo
{
    /// <summary>Prefixo comum das anotações que carregam a classificação no modelo.</summary>
    public const string PrefixoDasAnotacoes = "UniPlus:Classificacao";

    private const string AnotacaoDoTipo = PrefixoDasAnotacoes + "Padrao";
    private const string AnotacaoDaPropriedade = PrefixoDasAnotacoes;

    /// <summary>Declara a classificação das colunas do tipo que não têm anotação própria.</summary>
    public static EntityTypeBuilder<TEntity> ClassificaPorPadrao<TEntity>(
        this EntityTypeBuilder<TEntity> builder,
        ClassificacaoDeProtecao classificacao)
        where TEntity : class
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.HasAnnotation(AnotacaoDoTipo, Validar(classificacao));
        return builder;
    }

    /// <summary>Declara a classificação das colunas do tipo owned que não têm anotação própria.</summary>
    public static OwnedNavigationBuilder<TOwner, TDependent> ClassificaPorPadrao<TOwner, TDependent>(
        this OwnedNavigationBuilder<TOwner, TDependent> builder,
        ClassificacaoDeProtecao classificacao)
        where TOwner : class
        where TDependent : class
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.HasAnnotation(AnotacaoDoTipo, Validar(classificacao));
        return builder;
    }

    /// <summary>Declara a classificação de uma coluna que difere do padrão do tipo.</summary>
    public static PropertyBuilder<TProperty> ClassificaComo<TProperty>(
        this PropertyBuilder<TProperty> builder,
        ClassificacaoDeProtecao classificacao)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.HasAnnotation(AnotacaoDaPropriedade, Validar(classificacao));
        return builder;
    }

    /// <summary>
    /// A classificação da coluna: Pessoal, no mínimo, para os membros de auditoria pela interface ou pela base;
    /// senão, a anotação da propriedade; senão, o padrão do tipo. <see langword="null"/> quando nada foi declarado.
    /// </summary>
    public static ClassificacaoDeProtecao? Resolver(IReadOnlyProperty propriedade)
    {
        ArgumentNullException.ThrowIfNull(propriedade);

        ClassificacaoDeProtecao? anotada = propriedade.FindAnnotation(AnotacaoDaPropriedade)?.Value as ClassificacaoDeProtecao?;

        // A auditoria registra quem agiu: nenhuma anotação a rebaixa abaixo de Pessoal.
        if (EhMembroDeAuditoria(propriedade))
        {
            return anotada is { } maisAlta && maisAlta > ClassificacaoDeProtecao.Pessoal ? maisAlta : ClassificacaoDeProtecao.Pessoal;
        }

        if (anotada is not null)
        {
            return anotada;
        }

        return PadraoDoTipo(propriedade.DeclaringType as IReadOnlyEntityType);
    }

    private static ClassificacaoDeProtecao? PadraoDoTipo(IReadOnlyEntityType? tipo)
    {
        for (IReadOnlyEntityType? atual = tipo; atual is not null; atual = atual.BaseType)
        {
            if (atual.FindAnnotation(AnotacaoDoTipo)?.Value is ClassificacaoDeProtecao padrao)
            {
                return padrao;
            }
        }

        return null;
    }

    private static bool EhMembroDeAuditoria(IReadOnlyProperty propriedade)
    {
        Type? clr = propriedade.DeclaringType.ClrType;

        return propriedade.Name switch
        {
            nameof(IAuditableEntity.CreatedBy) or nameof(IAuditableEntity.UpdatedBy) =>
                typeof(IAuditableEntity).IsAssignableFrom(clr),
            nameof(ISoftDeletable.DeletedBy) => typeof(ISoftDeletable).IsAssignableFrom(clr),
            _ => false,
        };
    }

    // A escala é fechada: a checagem de domínio é a de ClassificacoesDeProtecao, que recusa o sentinela e o que
    // está fora do roster.
    private static ClassificacaoDeProtecao Validar(ClassificacaoDeProtecao classificacao)
    {
        _ = ClassificacoesDeProtecao.ParaTokenCanonico(classificacao);
        return classificacao;
    }
}
