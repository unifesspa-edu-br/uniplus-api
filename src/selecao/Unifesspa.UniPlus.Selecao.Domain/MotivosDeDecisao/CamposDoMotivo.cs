namespace Unifesspa.UniPlus.Selecao.Domain.MotivosDeDecisao;

using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>Os campos declarados no cadastro do motivo, como chegaram, antes da validação.</summary>
/// <param name="Tipo">Já convertido do código do wire; <see cref="TipoMotivoDeDecisao.Nenhum"/> quando ausente ou desconhecido.</param>
/// <param name="Sentido">Já convertido do código do wire; <see cref="SentidoDoMotivo.Nenhum"/> quando ausente ou desconhecido.</param>
public sealed record CamposDoMotivo(
    TipoMotivoDeDecisao Tipo,
    string? Codigo,
    string? Descricao,
    string? Subclassificacao,
    SentidoDoMotivo Sentido);

/// <summary>Os campos já validados e normalizados, prontos para aplicar ao motivo.</summary>
public sealed record CamposValidados(
    TipoMotivoDeDecisao Tipo,
    CodigoMotivoDecisao Codigo,
    string Descricao,
    string? Subclassificacao,
    SentidoDoMotivo Sentido);
