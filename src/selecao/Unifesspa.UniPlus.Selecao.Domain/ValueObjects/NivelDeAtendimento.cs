namespace Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// Um nível da escala de atendimento da etapa: o rótulo que a banca escolhe e a fração da
/// pontuação do item que esse nível vale (por exemplo, "Atendeu parcialmente" vale 0,5).
/// </summary>
public sealed record NivelDeAtendimento(string Rotulo, decimal Fracao)
{
    /// <summary>Largura máxima do rótulo do nível.</summary>
    public const int RotuloMaxLength = 100;

    public string Rotulo { get; set; } = Rotulo;

    public decimal Fracao { get; set; } = Fracao;

    public ChaveLocal Chave { get; init; }

    public int Ordem { get; set; }

    public static NivelDeAtendimento Criar(ChaveLocal chave, string rotulo, decimal fracao) =>
        new(rotulo, fracao) { Chave = chave };

    public static NivelDeAtendimento Reidratar(ChaveLocal chave, string rotulo, decimal fracao) =>
        Criar(chave, rotulo, fracao);
}
