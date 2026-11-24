namespace Unifesspa.UniPlus.Selecao.Domain.Enums;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;

/// <summary>
/// O papel de uma publicação prevista para uma <see cref="Entities.FaseCronograma"/> ou para uma etapa
/// dentro do ciclo recursal daquela fase.
/// </summary>
/// <remarks>
/// <para>
/// O papel é <b>relacional</b> e declarado pelo operador no edital: "preliminar" significa
/// "vem um definitivo depois", que é desenho de certame, não natureza do documento. O eixo
/// intrínseco — se o documento <i>é</i> resultado — vive no catálogo de tipos de ato
/// (<c>TipoAtoPublicado.EhResultado</c>) e não varia por edital.
/// </para>
/// <para>
/// A <b>ausência</b> de papel é estado legítimo: é o caso do ato que não é resultado, como um aviso
/// que a fase publica sem se tornar produtora de resultado. A propriedade que carrega este enum é
/// anulável.
/// </para>
/// </remarks>
public enum PapelDaPublicacaoPrevista
{
    [CodigoDeContrato(
        "PRELIMINAR",
        "Preliminar",
        Descricao = "Abre o ciclo recursal da matéria: há um resultado definitivo depois dele.")]
    Preliminar = 1,

    [CodigoDeContrato(
        "DEFINITIVO",
        "Definitivo",
        Descricao = "Encerra o ciclo recursal da matéria: não há outro resultado sobre ela.")]
    Definitivo = 2,
}
