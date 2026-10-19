namespace Unifesspa.UniPlus.Selecao.Application.Commands.AtosDoProcesso;

/// <summary>
/// O que todo comando que publica um ato do processo declara, qualquer que seja o tipo: o documento, quem
/// assina, a data e, quando o ato corrige outro, a indicação dele e o motivo.
/// </summary>
public interface IDadosDoAtoPublicado
{
    /// <summary>O processo que publica o ato.</summary>
    Guid ProcessoSeletivoId { get; }

    /// <summary>Número que o documento declara, quando o tipo o tem.</summary>
    string? Numero { get; }

    /// <summary>Data que o documento declara.</summary>
    DateOnly DataPublicacao { get; }

    /// <summary>Quem assina o ato.</summary>
    string Assinante { get; }

    /// <summary>O documento a publicar confirmado que é o principal, sempre PDF.</summary>
    Guid DocumentoPrincipalId { get; }

    /// <summary>Os anexos, na ordem em que o ato os apresenta; nulo ou vazio quando não há.</summary>
    IReadOnlyList<AnexoSolicitadoDoAto>? Anexos { get; }

    /// <summary>O ato publicado que este corrige; nulo quando o ato não corrige nenhum.</summary>
    Guid? AtoCorrigidoId { get; }

    /// <summary>Motivo da correção; vem junto com <see cref="AtoCorrigidoId"/>.</summary>
    string? MotivoDaCorrecao { get; }
}
