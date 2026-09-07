namespace Unifesspa.UniPlus.Selecao.Domain.Enums;

/// <summary>
/// O papel de um produto publicado por uma <see cref="Entities.FaseCronograma"/> dentro do
/// ciclo recursal daquela fase.
/// </summary>
/// <remarks>
/// <para>
/// O papel é <b>relacional</b> e declarado pelo operador no edital: "preliminar" significa
/// "vem um definitivo depois", que é desenho de certame, não natureza do documento. O eixo
/// intrínseco — se o documento <i>é</i> resultado — vive no catálogo de tipos de ato
/// (<c>TipoAtoPublicado.EhResultado</c>) e não varia por edital.
/// </para>
/// <para>
/// A <b>ausência</b> de papel é estado legítimo, e por isso não há membro sentinela: é o
/// caso do ato que não é resultado — a fase publica um aviso sem que isso a torne
/// produtora de resultado. A propriedade que carrega este enum é nulável.
/// </para>
/// </remarks>
public enum PapelProdutoFase
{
    /// <summary>Abre o ciclo recursal da matéria: há um definitivo depois dele.</summary>
    Preliminar = 1,

    /// <summary>Encerra o ciclo recursal da matéria — não há outro resultado sobre ela.</summary>
    Definitivo = 2,
}
