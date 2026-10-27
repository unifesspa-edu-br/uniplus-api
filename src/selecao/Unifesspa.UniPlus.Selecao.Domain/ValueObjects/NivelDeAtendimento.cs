namespace Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// Um nível da escala de atendimento da etapa: o rótulo que a banca escolhe e a fração da
/// pontuação do item que esse nível vale (por exemplo, "Atendeu parcialmente" vale 0,5).
/// </summary>
public sealed record NivelDeAtendimento(string Rotulo, decimal Fracao)
{
    /// <summary>Largura máxima do rótulo; o nível vive em coluna jsonb, então o teto é do domínio.</summary>
    public const int RotuloMaxLength = 100;
}
