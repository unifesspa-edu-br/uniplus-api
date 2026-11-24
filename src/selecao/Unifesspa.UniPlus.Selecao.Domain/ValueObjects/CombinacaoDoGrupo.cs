namespace Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;

/// <summary>A cardinalidade e a consequência próprias de um grupo de exigências.</summary>
public sealed record CombinacaoDoGrupo(FormaDeComprovacao Forma, int? QuantidadeMinima, string? Consequencia)
{
    public static Result<CombinacaoDoGrupo> Declarar(
        FormaDeComprovacao forma, int? quantidadeMinima, string? consequencia, int quantidadeDeItens, bool temBaseLegal)
    {
        DomainError? recusa = forma == FormaDeComprovacao.TodosOsItens
            ? RecusaQuandoExigeTodosOsItens(quantidadeMinima, consequencia, temBaseLegal)
            : RecusaQuandoExigeQuantidadeMinima(quantidadeMinima ?? RegraDeComprovacao.QuantidadeMinimaPadrao, consequencia, quantidadeDeItens, temBaseLegal);
        return recusa is not null ? Result<CombinacaoDoGrupo>.Failure(recusa)
            : Result<CombinacaoDoGrupo>.Success(forma == FormaDeComprovacao.TodosOsItens
                ? new(forma, null, null)
                : new(forma, quantidadeMinima ?? RegraDeComprovacao.QuantidadeMinimaPadrao, NormalizarConsequencia(consequencia)));
    }

    private static DomainError? RecusaQuandoExigeTodosOsItens(int? quantidadeMinima, string? consequencia, bool temBaseLegal)
    {
        if (RecusaDaQuantidade(FormaDeComprovacao.TodosOsItens, quantidadeMinima, 0) is { } quantidade)
        {
            return quantidade;
        }
        return RecusaDaConsequencia(FormaDeComprovacao.TodosOsItens, consequencia) ?? RecusaDaBaseLegal(FormaDeComprovacao.TodosOsItens, consequencia, temBaseLegal);
    }

    private static DomainError? RecusaQuandoExigeQuantidadeMinima(int quantidadeMinima, string? consequencia, int quantidadeDeItens, bool temBaseLegal)
    {
        if (RecusaDaQuantidade(FormaDeComprovacao.QuantidadeMinimaDeItens, quantidadeMinima, quantidadeDeItens) is { } quantidade)
        {
            return quantidade;
        }
        return RecusaDaConsequencia(FormaDeComprovacao.QuantidadeMinimaDeItens, consequencia) ?? RecusaDaBaseLegal(FormaDeComprovacao.QuantidadeMinimaDeItens, consequencia, temBaseLegal);
    }

    internal static DomainError? RecusaDaConsequencia(FormaDeComprovacao forma, string? consequencia)
    {
        string? normalizada = NormalizarConsequencia(consequencia);
        if (forma == FormaDeComprovacao.TodosOsItens && normalizada is not null)
        {
            return new("RegraDeComprovacao.ConsequenciaProibidaQuandoExigeTodos", "Este grupo exige todos os itens, mas informa uma consequência própria. Retire a consequência do grupo.");
        }
        return forma == FormaDeComprovacao.QuantidadeMinimaDeItens && normalizada is not null && !CatalogoDe<ConsequenciaDoIndeferimento>.TentarObter(normalizada, out _)
            ? new("RegraDeComprovacao.ConsequenciaInvalida", "A consequência deste grupo não é reconhecida. Escolha \"ELIMINA\" para retirar a pessoa do processo, \"RECLASSIFICA_AC\" para a ampla concorrência, \"REMOVE_VANTAGEM\" para retirar a vantagem ou \"ABRE_PRAZO_PARA_REENVIO\" para permitir reenvio.") : null;
    }

    internal static DomainError? RecusaDaBaseLegal(FormaDeComprovacao forma, string? consequencia, bool temBaseLegal) => forma switch
    {
        FormaDeComprovacao.TodosOsItens when temBaseLegal => new("RegraDeComprovacao.BaseLegalProibidaQuandoExigeTodos",
            "Este grupo exige todos os itens, mas informa base legal própria. Retire a base legal do grupo."),
        FormaDeComprovacao.QuantidadeMinimaDeItens when temBaseLegal && NormalizarConsequencia(consequencia) is null => new("RegraDeComprovacao.BaseLegalSemConsequencia",
            "Este grupo tem base legal, mas não tem consequência. Informe a consequência ou retire a base legal."),
        _ => null,
    };

    internal static DomainError? RecusaDaQuantidade(FormaDeComprovacao forma, int? quantidade, int quantidadeDeItens) => forma switch
    {
        FormaDeComprovacao.TodosOsItens when quantidade is not null => new("RegraDeComprovacao.QuantidadeMinimaProibidaQuandoExigeTodos",
            "Este grupo exige todos os itens, mas informa uma quantidade mínima. Retire a quantidade mínima."),
        FormaDeComprovacao.QuantidadeMinimaDeItens when quantidade is { } valor && (valor < 1 || quantidadeDeItens > 0 && valor > quantidadeDeItens) => new(
            "RegraDeComprovacao.QuantidadeMinimaDeItensForaDosLimites", "A quantidade mínima deste grupo está fora dos limites. Informe um valor entre 1 e o número de itens do grupo."),
        _ => null,
    };

    internal static string? NormalizarConsequencia(string? consequencia) => string.IsNullOrWhiteSpace(consequencia) ? null : consequencia.Trim();
}
