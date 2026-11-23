namespace Unifesspa.UniPlus.Selecao.Domain.Entities;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

public sealed class RequisitosDoDocumento
{
    private RequisitosDoDocumento() { }

    public RequisitosDoDocumento(FormatosPermitidos formatosPermitidos, int? tamanhoMaximoBytes,
        ModeloDaExigencia? modelo, PrazoDeEmissao? prazoDeEmissao, ValidadeDoDocumento? validade)
    {
        ArgumentNullException.ThrowIfNull(formatosPermitidos);
        FormatosPermitidos = formatosPermitidos;
        TamanhoMaximoBytes = tamanhoMaximoBytes;
        Modelo = modelo;
        PrazoDeEmissao = prazoDeEmissao;
        Validade = validade;
    }

    public FormatosPermitidos FormatosPermitidos { get; private set; } = null!;
    public int? TamanhoMaximoBytes { get; private set; }
    public ModeloDaExigencia? Modelo { get; private set; }
    public PrazoDeEmissao? PrazoDeEmissao { get; private set; }
    public ValidadeDoDocumento? Validade { get; private set; }

    public static IReadOnlyList<FieldError> ValidarTamanho(int? tamanhoMaximoBytes) =>
        tamanhoMaximoBytes is <= 0
            ? [new("tamanhoMaximoBytes", new DomainError(
                "DocumentoExigido.TamanhoMaximoBytesInvalido",
                "O tamanho máximo em bytes, quando presente, deve ser maior que zero."))]
            : [];
}
