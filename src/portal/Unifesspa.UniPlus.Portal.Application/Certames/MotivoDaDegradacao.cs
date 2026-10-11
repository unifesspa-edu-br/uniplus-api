namespace Unifesspa.UniPlus.Portal.Application.Certames;

/// <summary>
/// Por que a linha do tempo de um certame foi servida vazia ou incompleta (ADR-0131, Emenda 1).
/// </summary>
/// <remarks>
/// Os dois últimos motivos são conferências de consistência entre as duas origens, não um
/// segundo critério de visibilidade: o certame continua sendo servido; só a linha do tempo leva
/// aviso.
/// </remarks>
public enum MotivoDaDegradacao
{
    /// <summary>Publicações não respondeu.</summary>
    OrigemIndisponivel = 1,

    /// <summary>Publicações deixou de responder, ou recusou o cursor, no meio do percurso de páginas.</summary>
    FalhaNaPaginacao = 2,

    /// <summary>
    /// A linha do tempo tem mais páginas do que o teto permite percorrer. A origem ordena de
    /// forma ascendente: o que ficou de fora são justamente os atos mais recentes.
    /// </summary>
    TetoDePaginas = 3,

    /// <summary>O ato que criou a versão servida não aparece na linha do tempo.</summary>
    CriadorAusente = 4,

    /// <summary>
    /// Há na linha do tempo um ato que cria versão, mais recente que o criador da versão servida:
    /// o detalhe ainda não alcançou o que Publicações já registrou.
    /// </summary>
    DetalheAtrasado = 5,
}
