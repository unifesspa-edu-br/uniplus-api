namespace Unifesspa.UniPlus.Selecao.Domain.Interfaces;

using Unifesspa.UniPlus.Selecao.Domain.Enums;

/// <summary>O estado do processo de que a decisão de leitura depende.</summary>
/// <param name="UnidadeId">A unidade dona do processo.</param>
/// <param name="Status">O status do processo.</param>
/// <param name="JaPublicado">Se o processo já teve uma versão publicada, ainda que hoje esteja cancelado.</param>
public sealed record EstadoDoProcesso(Guid UnidadeId, StatusProcesso Status, bool JaPublicado)
{
    /// <summary>
    /// Os status em que o processo é lido por qualquer pessoa. O cancelado só entra se já foi publicado
    /// (<see cref="LeituraAberta"/>): o cancelamento de um rascunho nunca saiu da unidade dona.
    /// </summary>
    public static IReadOnlyList<StatusProcesso> StatusDeLeituraAberta { get; } = [StatusProcesso.Publicado, StatusProcesso.Encerrado];

    /// <summary>Se o processo é lido por qualquer pessoa: saiu do rascunho pela publicação.</summary>
    public bool LeituraAberta => StatusDeLeituraAberta.Contains(Status) || (Status == StatusProcesso.Cancelado && JaPublicado);
}
