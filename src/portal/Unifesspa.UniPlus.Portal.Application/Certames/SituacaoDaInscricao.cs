namespace Unifesspa.UniPlus.Portal.Application.Certames;

/// <summary>
/// Situação da janela de inscrição do certame, como Seleção a resolve no instante da leitura.
/// </summary>
/// <remarks>
/// A Portal não calcula a situação: repassa a que Seleção devolve, com o mesmo vocabulário da
/// vitrine, para a página e a vitrine nunca mostrarem situações diferentes para o mesmo certame.
/// </remarks>
public enum SituacaoDaInscricao
{
    /// <summary>A janela de inscrição ainda vai abrir.</summary>
    EmBreve = 1,

    /// <summary>Recebe inscrição, e o encerramento ainda não está próximo.</summary>
    InscricoesAbertas = 2,

    /// <summary>Recebe inscrição, e o encerramento está próximo.</summary>
    UltimosDias = 3,

    /// <summary>A janela de inscrição já encerrou.</summary>
    Encerradas = 4,
}
