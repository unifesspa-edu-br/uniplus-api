namespace Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;

/// <summary>A cardinalidade e a consequência próprias de um grupo de exigências.</summary>
public sealed record CombinacaoDoGrupo(FormaDeComprovacao Forma, int? QuantidadeMinima, string? ConsequenciaDoDescumprimento, bool AbrePrazoParaReenvio = false)
{
    public static Result<CombinacaoDoGrupo> Declarar(
        FormaDeComprovacao forma, int? quantidadeMinima, string? consequenciaDoDescumprimento, int quantidadeDeItens, bool temBaseLegal, bool abrePrazoParaReenvio = false)
    {
        DomainError? recusa = forma == FormaDeComprovacao.TodosOsItens
            ? RecusaQuandoExigeTodosOsItens(quantidadeMinima, consequenciaDoDescumprimento, temBaseLegal)
            : RecusaQuandoExigeQuantidadeMinima(quantidadeMinima ?? RegraDeComprovacao.QuantidadeMinimaPadrao, consequenciaDoDescumprimento, quantidadeDeItens, temBaseLegal);
        return recusa is not null ? Result<CombinacaoDoGrupo>.Failure(recusa)
            : Result<CombinacaoDoGrupo>.Success(forma == FormaDeComprovacao.TodosOsItens
                ? new(forma, null, null, abrePrazoParaReenvio)
                : new(forma, quantidadeMinima ?? RegraDeComprovacao.QuantidadeMinimaPadrao, NormalizarConsequencia(consequenciaDoDescumprimento), abrePrazoParaReenvio));
    }

    private static DomainError? RecusaQuandoExigeTodosOsItens(int? quantidadeMinima, string? consequenciaDoDescumprimento, bool temBaseLegal)
    {
        if (RecusaDaQuantidade(FormaDeComprovacao.TodosOsItens, quantidadeMinima, 0) is { } quantidade)
        {
            return quantidade;
        }
        return RecusaDaConsequencia(FormaDeComprovacao.TodosOsItens, consequenciaDoDescumprimento) ?? RecusaDaBaseLegal(FormaDeComprovacao.TodosOsItens, consequenciaDoDescumprimento, temBaseLegal);
    }

    private static DomainError? RecusaQuandoExigeQuantidadeMinima(int quantidadeMinima, string? consequenciaDoDescumprimento, int quantidadeDeItens, bool temBaseLegal)
    {
        if (RecusaDaQuantidade(FormaDeComprovacao.QuantidadeMinimaDeItens, quantidadeMinima, quantidadeDeItens) is { } quantidade)
        {
            return quantidade;
        }
        return RecusaDaConsequencia(FormaDeComprovacao.QuantidadeMinimaDeItens, consequenciaDoDescumprimento) ?? RecusaDaBaseLegal(FormaDeComprovacao.QuantidadeMinimaDeItens, consequenciaDoDescumprimento, temBaseLegal);
    }

    internal static DomainError? RecusaDaConsequencia(FormaDeComprovacao forma, string? consequenciaDoDescumprimento)
    {
        string? normalizada = NormalizarConsequencia(consequenciaDoDescumprimento);
        if (forma == FormaDeComprovacao.TodosOsItens && normalizada is not null)
        {
            return new("RegraDeComprovacao.ConsequenciaProibidaQuandoExigeTodos", "Este grupo exige todos os itens, mas informa uma consequência própria. Retire a consequência do grupo.");
        }
        return forma == FormaDeComprovacao.QuantidadeMinimaDeItens && normalizada is not null && !CatalogoDe<ConsequenciaDoDescumprimento>.TentarObter(normalizada, out _)
            ? new("RegraDeComprovacao.ConsequenciaInvalida", "A consequência deste grupo não é reconhecida.") : null;
    }

    internal static DomainError? RecusaDaBaseLegal(FormaDeComprovacao forma, string? consequenciaDoDescumprimento, bool temBaseLegal) => forma switch
    {
        FormaDeComprovacao.TodosOsItens when temBaseLegal => new("RegraDeComprovacao.BaseLegalProibidaQuandoExigeTodos",
            "Este grupo exige todos os itens, mas informa base legal própria. Retire a base legal do grupo."),
        FormaDeComprovacao.QuantidadeMinimaDeItens when temBaseLegal && NormalizarConsequencia(consequenciaDoDescumprimento) is null => new("RegraDeComprovacao.BaseLegalSemConsequencia",
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

    /// <summary>A regra de documento não tem indeferimento próprio: o reenvio vive na exigência do documento.</summary>
    public static DomainError? RecusaDoReenvioEmRegraDeDocumento(bool abrePrazoParaReenvio) => abrePrazoParaReenvio
        ? new("RegraDeComprovacao.ReenvioProibidoEmRegraDeDocumento", "A regra de documento não declara reenvio próprio. Declare o reenvio na exigência do documento.") : null;

    internal static string? NormalizarConsequencia(string? consequenciaDoDescumprimento) => string.IsNullOrWhiteSpace(consequenciaDoDescumprimento) ? null : consequenciaDoDescumprimento;
}
