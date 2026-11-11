namespace Unifesspa.UniPlus.Selecao.Domain.MotivosDeDecisao;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Errors;

/// <summary>
/// Base das políticas dos tipos que não definem subclassificação: a decisão é avaliada na fase
/// canônica de mesmo assunto e admite motivo favorável e desfavorável.
/// </summary>
public abstract class PoliticaSemSubclassificacao : IPoliticaDoTipoDeMotivo
{
    private readonly string _faseDaAvaliacao;

    protected PoliticaSemSubclassificacao(TipoMotivoDeDecisao tipo, string faseDaAvaliacao)
    {
        Tipo = tipo;
        _faseDaAvaliacao = faseDaAvaliacao;
    }

    public TipoMotivoDeDecisao Tipo { get; }

    public Result<string?> ConferirSubclassificacao(string? subclassificacao) =>
        string.IsNullOrWhiteSpace(subclassificacao)
            ? Result<string?>.Success(null)
            : Result<string?>.Failure(new DomainError(
                MotivoDeDecisaoErrorCodes.SubclassificacaoNaoAdmitida,
                $"O tipo {Tipo.ToCodigo()} não tem subclassificação; só o motivo da isenção da taxa a declara."));

    public bool AdmiteSentido(SentidoDoMotivo sentido) => sentido is SentidoDoMotivo.Favoravel or SentidoDoMotivo.Desfavoravel;

    public bool AvaliaNaFase(FaseCronograma fase)
    {
        ArgumentNullException.ThrowIfNull(fase);
        return string.Equals(fase.Codigo, _faseDaAvaliacao, StringComparison.Ordinal);
    }
}

/// <summary>Motivos da homologação das inscrições, avaliada na fase de homologação.</summary>
public sealed class PoliticaDaHomologacaoDasInscricoes() : PoliticaSemSubclassificacao(TipoMotivoDeDecisao.HomologacaoDasInscricoes, "HOMOLOGACAO");

/// <summary>Motivos da análise documental, avaliada na fase de habilitação.</summary>
public sealed class PoliticaDaAnaliseDocumental() : PoliticaSemSubclassificacao(TipoMotivoDeDecisao.AnaliseDocumental, "HABILITACAO");

/// <summary>Motivos da heteroidentificação, avaliada na fase de mesmo nome.</summary>
public sealed class PoliticaDaHeteroidentificacao() : PoliticaSemSubclassificacao(TipoMotivoDeDecisao.Heteroidentificacao, "HETEROIDENTIFICACAO");

/// <summary>Motivos da avaliação biopsicossocial, avaliada na fase de mesmo nome.</summary>
public sealed class PoliticaDaAvaliacaoBiopsicossocial() : PoliticaSemSubclassificacao(TipoMotivoDeDecisao.AvaliacaoBiopsicossocial, "AVALIACAO_BIOPSICOSSOCIAL");

/// <summary>Motivos da classificação, avaliada na fase de classificação.</summary>
public sealed class PoliticaDaClassificacao() : PoliticaSemSubclassificacao(TipoMotivoDeDecisao.Classificacao, "CLASSIFICACAO");
