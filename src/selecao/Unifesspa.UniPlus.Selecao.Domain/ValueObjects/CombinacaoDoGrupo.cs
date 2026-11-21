namespace Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;

/// <summary>A cardinalidade e a consequência próprias de um grupo de exigências.</summary>
public sealed record CombinacaoDoGrupo(TipoNo Tipo, int? QuantidadeMinima, string? Consequencia)
{
    public static Result<CombinacaoDoGrupo> Declarar(
        TipoNo tipo, int? quantidadeMinima, string? consequencia, int quantidadeDeFilhos, bool temBaseLegal)
    {
        DomainError? recusa = tipo == TipoNo.GrupoE
            ? RecusaDoGrupoE(quantidadeMinima, consequencia, temBaseLegal)
            : RecusaDoGrupoOu(quantidadeMinima ?? NoExigencia.QuantidadeMinimaPadrao, consequencia, quantidadeDeFilhos, temBaseLegal);
        return recusa is not null ? Result<CombinacaoDoGrupo>.Failure(recusa)
            : Result<CombinacaoDoGrupo>.Success(tipo == TipoNo.GrupoE
                ? new(tipo, null, null)
                : new(tipo, quantidadeMinima ?? NoExigencia.QuantidadeMinimaPadrao, NormalizarConsequencia(consequencia)));
    }

    private static DomainError? RecusaDoGrupoE(int? quantidadeMinima, string? consequencia, bool temBaseLegal)
    {
        if (RecusaDaQuantidade(TipoNo.GrupoE, quantidadeMinima, 0) is { } quantidade)
        {
            return quantidade;
        }
        return RecusaDaConsequencia(TipoNo.GrupoE, consequencia) ?? RecusaDaBaseLegal(TipoNo.GrupoE, consequencia, temBaseLegal);
    }

    private static DomainError? RecusaDoGrupoOu(int quantidadeMinima, string? consequencia, int quantidadeDeFilhos, bool temBaseLegal)
    {
        if (RecusaDaQuantidade(TipoNo.GrupoOu, quantidadeMinima, quantidadeDeFilhos) is { } quantidade)
        {
            return quantidade;
        }
        return RecusaDaConsequencia(TipoNo.GrupoOu, consequencia) ?? RecusaDaBaseLegal(TipoNo.GrupoOu, consequencia, temBaseLegal);
    }

    internal static DomainError? RecusaDaConsequencia(TipoNo tipo, string? consequencia)
    {
        string? normalizada = NormalizarConsequencia(consequencia);
        if (tipo == TipoNo.GrupoE && normalizada is not null)
        {
            return new("NoExigencia.ConsequenciaProibidaEmGrupoE", "Este grupo exige todos os itens (\"E\"), mas informa uma consequência própria. Retire a consequência do grupo.");
        }
        return tipo == TipoNo.GrupoOu && normalizada is not null && !ConsequenciaIndeferimentoCodigo.Validas.Contains(normalizada, StringComparer.Ordinal)
            ? new("NoExigencia.ConsequenciaInvalida", "A consequência deste grupo não é reconhecida. Escolha \"ELIMINA\" para retirar a pessoa do processo, \"RECLASSIFICA_AC\" para a ampla concorrência, \"REMOVE_VANTAGEM\" para retirar a vantagem ou \"PENDENCIA_REENVIO\" para permitir reenvio.") : null;
    }

    internal static DomainError? RecusaDaBaseLegal(TipoNo tipo, string? consequencia, bool temBaseLegal) => tipo switch
    {
        TipoNo.GrupoE when temBaseLegal => new("NoExigencia.BaseLegalProibidaEmGrupoE",
            "Este grupo exige todos os itens (\"E\"), mas informa base legal própria. Retire a base legal do grupo."),
        TipoNo.GrupoOu when temBaseLegal && NormalizarConsequencia(consequencia) is null => new("NoExigencia.BaseLegalSemConsequencia",
            "Este grupo tem base legal, mas não tem consequência. Informe a consequência ou retire a base legal."),
        _ => null,
    };

    internal static DomainError? RecusaDaQuantidade(TipoNo tipo, int? quantidade, int quantidadeDeFilhos) => tipo switch
    {
        TipoNo.GrupoE when quantidade is not null => new("NoExigencia.QuantidadeMinimaProibidaEmGrupoE",
            "Este grupo exige todos os itens (\"E\"), mas informa uma quantidade mínima. Retire a quantidade mínima."),
        TipoNo.GrupoOu when quantidade is { } valor && (valor < 1 || valor > quantidadeDeFilhos) => new(
            "NoExigencia.QuantidadeMinimaForaDosLimites", "A quantidade mínima deste grupo (\"OU\") está fora dos limites. Informe um valor entre 1 e o número de itens do grupo."),
        _ => null,
    };

    internal static string? NormalizarConsequencia(string? consequencia) => string.IsNullOrWhiteSpace(consequencia) ? null : consequencia.Trim();
}
