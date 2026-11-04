namespace Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;

/// <summary>
/// O que a aplicação do modelo fez além de copiar: os fatos trazidos para a inscrição ou nela mantidos, as
/// partes que o catálogo mudou depois da composição do modelo e saíram da cópia, e as derivações copiadas
/// do catálogo ou mantidas como o processo já as tinha.
/// </summary>
internal sealed class RelatorioDaAplicacaoDoModelo
{
    public List<string> Trazidos { get; } = [];

    public List<string> Mantidos { get; } = [];

    public List<ParteDescartadaDto> Descartados { get; } = [];

    public List<string> Copiadas { get; } = [];

    public List<string> Mantidas { get; } = [];

    public AplicacaoDeModeloDto ParaDto(string finalidade) =>
        new(finalidade, Trazidos, Mantidos, Descartados, Copiadas, Mantidas);
}
