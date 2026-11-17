namespace Unifesspa.UniPlus.Selecao.Domain.MotivosDeDecisao;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Errors;

/// <summary>
/// Base das políticas dos tipos que não definem subclassificação: admitem motivo favorável e desfavorável.
/// </summary>
public abstract class PoliticaSemSubclassificacao : IPoliticaDoTipoDeMotivo
{
    protected PoliticaSemSubclassificacao(TipoMotivoDeDecisao tipo) => Tipo = tipo;

    public TipoMotivoDeDecisao Tipo { get; }

    public Result<string?> ConferirSubclassificacao(string? subclassificacao) =>
        string.IsNullOrWhiteSpace(subclassificacao)
            ? Result<string?>.Success(null)
            : Result<string?>.Failure(new DomainError(
                MotivoDeDecisaoErrorCodes.SubclassificacaoNaoAdmitida,
                $"O tipo {Tipo.ToCodigo()} não tem subclassificação; só o motivo da isenção da taxa a declara."));

    public bool AdmiteSentido(SentidoDoMotivo sentido) => sentido is SentidoDoMotivo.Favoravel or SentidoDoMotivo.Desfavoravel;
}

/// <summary>Motivos da homologação das inscrições.</summary>
public sealed class PoliticaDaHomologacaoDasInscricoes() : PoliticaSemSubclassificacao(TipoMotivoDeDecisao.HomologacaoDasInscricoes);

/// <summary>Motivos da análise documental.</summary>
public sealed class PoliticaDaAnaliseDocumental() : PoliticaSemSubclassificacao(TipoMotivoDeDecisao.AnaliseDocumental);

/// <summary>Motivos da heteroidentificação.</summary>
public sealed class PoliticaDaHeteroidentificacao() : PoliticaSemSubclassificacao(TipoMotivoDeDecisao.Heteroidentificacao);

/// <summary>Motivos da avaliação biopsicossocial.</summary>
public sealed class PoliticaDaAvaliacaoBiopsicossocial() : PoliticaSemSubclassificacao(TipoMotivoDeDecisao.AvaliacaoBiopsicossocial);

/// <summary>Motivos da classificação.</summary>
public sealed class PoliticaDaClassificacao() : PoliticaSemSubclassificacao(TipoMotivoDeDecisao.Classificacao);
