namespace Unifesspa.UniPlus.Configuracao.Domain.ValueObjects;

/// <summary>
/// O que o modelo escolhe sobre a emissão e a validade do documento exigido. Cada escolha ausente é "ainda
/// não escolhida": o processo que receber a cópia fica pendente dela até a unidade decidir.
/// </summary>
/// <param name="PrazoDeEmissao">O prazo de emissão, ou nulo quando o modelo ainda não escolheu.</param>
/// <param name="Validade">A validade, ou nulo quando o modelo ainda não escolheu.</param>
public sealed record RequisitosDoDocumentoDoModelo(PrazoDeEmissaoDoModelo? PrazoDeEmissao, ValidadeDoDocumentoDoModelo? Validade)
{
    /// <summary>Nem o prazo de emissão nem a validade foram escolhidos.</summary>
    public static RequisitosDoDocumentoDoModelo AindaNaoEscolhidos { get; } = new(null, null);

    /// <summary>Os requisitos com o código da fase de cada referência na forma gravada.</summary>
    public RequisitosDoDocumentoDoModelo NaFormaGravada() => new(PrazoDeEmissao?.NaFormaGravada(), Validade?.NaFormaGravada());

    /// <summary>Os códigos de fase que as referências citam, com o caminho do campo de cada um na entrada.</summary>
    public IEnumerable<(string Campo, string? Texto)> TextosDasReferencias(string caminho) =>
        new (string, string?)[]
        {
            ($"{caminho}.requisitos.prazoDeEmissao.referencia.faseCodigo", PrazoDeEmissao?.Referencia?.FaseCodigo),
            ($"{caminho}.requisitos.validade.referencia.faseCodigo", Validade?.Referencia?.FaseCodigo),
        };
}
