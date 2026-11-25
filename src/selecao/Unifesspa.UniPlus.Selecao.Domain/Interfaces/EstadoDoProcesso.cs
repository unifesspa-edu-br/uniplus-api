namespace Unifesspa.UniPlus.Selecao.Domain.Interfaces;

using Unifesspa.UniPlus.Selecao.Domain.Enums;

/// <summary>O estado do processo de que a decisão de leitura depende.</summary>
/// <param name="UnidadeId">A unidade dona do processo.</param>
/// <param name="Situacao">A situação do processo.</param>
/// <param name="JaPublicado">Se o processo já teve uma versão publicada, ainda que hoje esteja cancelado.</param>
public sealed record EstadoDoProcesso(Guid UnidadeId, StatusProcesso Situacao, bool JaPublicado)
{
    /// <summary>
    /// As situações em que o processo é lido por qualquer pessoa, além do cancelado já publicado
    /// (<see cref="LeituraAberta"/>): o cancelamento de um rascunho nunca saiu da unidade dona.
    /// </summary>
    public static IReadOnlyList<StatusProcesso> SituacoesDeLeituraAberta { get; } = [StatusProcesso.Publicado, StatusProcesso.Encerrado];

    /// <summary>Se o processo é lido por qualquer pessoa: saiu do rascunho pela publicação.</summary>
    public bool LeituraAberta => SituacoesDeLeituraAberta.Contains(Situacao) || (Situacao == StatusProcesso.Cancelado && JaPublicado);
}
