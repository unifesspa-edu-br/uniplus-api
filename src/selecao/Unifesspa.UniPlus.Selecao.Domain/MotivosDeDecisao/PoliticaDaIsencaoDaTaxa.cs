namespace Unifesspa.UniPlus.Selecao.Domain.MotivosDeDecisao;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Errors;

/// <summary>
/// Motivos da decisão sobre o pedido de isenção da taxa (UNI-REQ-0120): a subclassificação é o
/// fundamento de isenção, obrigatório, porque a lista de motivos de cada fundamento é própria e a
/// cobertura da publicação é conferida por fundamento (UNI-REQ-0123).
/// </summary>
public sealed class PoliticaDaIsencaoDaTaxa : IPoliticaDoTipoDeMotivo
{
    public TipoMotivoDeDecisao Tipo => TipoMotivoDeDecisao.IsencaoDaTaxa;

    public Result<string?> ConferirSubclassificacao(string? subclassificacao)
    {
        if (string.IsNullOrWhiteSpace(subclassificacao))
        {
            return Result<string?>.Failure(new DomainError(
                MotivoDeDecisaoErrorCodes.SubclassificacaoObrigatoria,
                "O motivo da isenção da taxa exige o fundamento de isenção na subclassificação: "
                + $"{FundamentoIsencaoCodigo.CodigosEmTexto}."));
        }

        string normalizada = subclassificacao.Trim();
        return FundamentoIsencaoCodigo.FromCodigo(normalizada) == FundamentoIsencao.Nenhum
            ? Result<string?>.Failure(new DomainError(
                MotivoDeDecisaoErrorCodes.SubclassificacaoInvalida,
                $"Fundamento de isenção desconhecido. Use um dos códigos: {FundamentoIsencaoCodigo.CodigosEmTexto}."))
            : Result<string?>.Success(normalizada);
    }

    public bool AdmiteSentido(SentidoDoMotivo sentido) => sentido is SentidoDoMotivo.Favoravel or SentidoDoMotivo.Desfavoravel;

    /// <summary>A isenção é avaliada na fase que abre o pedido, qualquer que seja o código dela.</summary>
    public bool AvaliaNaFase(FaseCronograma fase)
    {
        ArgumentNullException.ThrowIfNull(fase);
        return fase.ColetaSolicitacaoIsencao;
    }
}
