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
        if (quantidadeMinima is not null)
        {
            return new("NoExigencia.QuantidadeMinimaProibidaEmGrupoE", "Não informe a quantidade mínima de documentos para um grupo E.");
        }
        if (!string.IsNullOrWhiteSpace(consequencia))
        {
            return new("NoExigencia.ConsequenciaProibidaEmGrupoE", "Um grupo E é transparente e não carrega consequência própria.");
        }
        return temBaseLegal ? new("NoExigencia.BaseLegalProibidaEmGrupoE", "Um grupo E é transparente e não carrega base legal própria.") : null;
    }

    private static DomainError? RecusaDoGrupoOu(int quantidadeMinima, string? consequencia, int quantidadeDeFilhos, bool temBaseLegal)
    {
        if (quantidadeMinima < 1 || quantidadeMinima > quantidadeDeFilhos)
        {
            return new("NoExigencia.QuantidadeMinimaForaDosLimites", "A quantidade mínima de um grupo OU deve estar entre um e o número de filhos.");
        }
        string? normalizada = NormalizarConsequencia(consequencia);
        if (normalizada is not null && !ConsequenciaIndeferimentoCodigo.Validas.Contains(normalizada, StringComparer.Ordinal))
        {
            return new("NoExigencia.ConsequenciaInvalida", "A consequência deve pertencer ao vocabulário de indeferimento.");
        }
        return temBaseLegal && normalizada is null
            ? new("NoExigencia.BaseLegalSemConsequencia", "Base legal de grupo só é permitida quando o grupo carrega consequência.") : null;
    }

    private static string? NormalizarConsequencia(string? consequencia) => string.IsNullOrWhiteSpace(consequencia) ? null : consequencia.Trim();
}
