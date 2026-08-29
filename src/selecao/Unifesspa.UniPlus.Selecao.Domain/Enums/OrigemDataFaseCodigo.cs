namespace Unifesspa.UniPlus.Selecao.Domain.Enums;

/// <summary>
/// Mapeamento entre <see cref="OrigemDataFase"/> e o token textual (UPPER_SNAKE)
/// exposto por <c>FaseCanonicaView.OrigemData</c> (Configuracao.Contracts,
/// ADR-0056). Fonte única do parsing no snapshot-copy (ADR-0061) e da projeção
/// de leitura — nunca comparar a string crua no handler nem emitir
/// <c>enum.ToString()</c>, que produziria <c>Propria</c> onde a origem publica
/// <c>PROPRIA</c>.
/// </summary>
public static class OrigemDataFaseCodigo
{
    public const string Propria = "PROPRIA";
    public const string Delegada = "DELEGADA";

    /// <summary>
    /// Converte o token cross-módulo para o enum local. Um token não reconhecido
    /// mapeia para <see cref="OrigemDataFase.Nenhuma"/> — que
    /// <see cref="Entities.FaseCronograma"/> já recusa ao criar a fase, com erro
    /// nomeado, em vez de deixar entrar uma fase sem regime de data definido.
    /// </summary>
    public static OrigemDataFase FromCodigo(string? codigo) => codigo switch
    {
        Propria => OrigemDataFase.Propria,
        Delegada => OrigemDataFase.Delegada,
        _ => OrigemDataFase.Nenhuma,
    };

    /// <summary>
    /// Converte o enum local de volta ao token cross-módulo — o mesmo que o
    /// catálogo de fases canônicas publica.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Se <paramref name="origem"/> é <see cref="OrigemDataFase.Nenhuma"/>: a fase
    /// persistida sempre tem regime declarado, porque
    /// <see cref="Entities.FaseCronograma.Criar"/> recusa o sentinela — encontrá-lo
    /// numa projeção denuncia corrupção, não um caso a projetar.
    /// </exception>
    public static string ToCodigo(this OrigemDataFase origem) => origem switch
    {
        OrigemDataFase.Propria => Propria,
        OrigemDataFase.Delegada => Delegada,
        OrigemDataFase.Nenhuma => throw new ArgumentOutOfRangeException(
            nameof(origem), origem, "OrigemDataFase.Nenhuma é sentinela e não tem token canônico."),
        _ => throw new ArgumentOutOfRangeException(nameof(origem), origem, "OrigemDataFase desconhecida."),
    };
}
