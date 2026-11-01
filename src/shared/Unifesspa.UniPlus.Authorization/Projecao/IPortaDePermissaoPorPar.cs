namespace Unifesspa.UniPlus.Authorization.Projecao;

using Unifesspa.UniPlus.Authorization.Catalogo;
using Unifesspa.UniPlus.Authorization.Contracts;
using Unifesspa.UniPlus.Kernel.Domain.Enums;

/// <summary>
/// A porta pela qual um módulo responde qual é a sua permissão de consulta para um par (natureza, classificação)
/// (ADR-0153). Par sem permissão devolve <see langword="null"/>: ninguém vê o item.
/// </summary>
public interface IPortaDePermissaoPorPar
{
    /// <summary>O requisito da permissão do par, ou <see langword="null"/> quando o módulo não tem permissão para ele.</summary>
    PermissionRequirement? Requisito(NaturezaDoItem natureza, ClassificacaoDeProtecao classificacao);
}

/// <summary>
/// A porta lida do catálogo do módulo: a permissão do par é a única do enum que declara a natureza e tem a
/// classificação como sensibilidade. A unicidade do par é conferida na subida.
/// </summary>
/// <typeparam name="TPermissao">Enum de permissões do módulo.</typeparam>
public sealed class PortaDePermissaoPorPar<TPermissao> : IPortaDePermissaoPorPar
    where TPermissao : struct, Enum
{
    private readonly Dictionary<(NaturezaDoItem, ClassificacaoDeProtecao), PermissionRequirement> _porPar =
        CatalogoDePermissoes<TPermissao>.Todas()
            .Where(static d => d.Natureza != NaturezaDoItem.Nenhuma)
            .ToDictionary(static d => (d.Natureza, d.Requisito.Sensibilidade), static d => d.Requisito);

    /// <inheritdoc />
    public PermissionRequirement? Requisito(NaturezaDoItem natureza, ClassificacaoDeProtecao classificacao) =>
        _porPar.GetValueOrDefault((natureza, classificacao));
}
