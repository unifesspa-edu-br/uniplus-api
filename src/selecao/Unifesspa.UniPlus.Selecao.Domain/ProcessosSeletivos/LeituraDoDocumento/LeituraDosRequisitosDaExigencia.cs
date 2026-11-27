namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.LeituraDoDocumento;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// Os requisitos do arquivo que responde à exigência: os formatos aceitos, o tamanho máximo, o prazo de emissão e a validade. O modelo
/// de documento não é lido aqui: a exigência é convertida sem ele. As recusas dos três blocos se acumulam; o tamanho máximo é conferido
/// com a forma da exigência.
/// </summary>
internal static class LeituraDosRequisitosDaExigencia
{
    private const string CampoDosFormatos = "formatosPermitidos";

    internal static ParteLida<RequisitosDoDocumento> Ler(ExigenciaNoDocumento exigencia, FasesLidas fases)
    {
        ParteLida<FormatosPermitidos> formatos = Formatos(exigencia.FormatosPermitidos);
        ParteLida<PrazoDeEmissao> prazo = LeituraDoPrazoEDaValidadeDaExigencia.Prazo(exigencia.PrazoDeEmissao, fases);
        ParteLida<ValidadeDoDocumento> validade = LeituraDoPrazoEDaValidadeDaExigencia.Validade(exigencia.Validade, fases);
        FieldError[] recusas = [.. formatos.Recusas, .. prazo.Recusas, .. validade.Recusas];

        return recusas.Length > 0 || formatos.Valor is null || prazo.Cortada || validade.Cortada
            ? new(null, recusas, prazo.Cortada || validade.Cortada)
            : ParteLida<RequisitosDoDocumento>.Montada(new(formatos.Valor, exigencia.TamanhoMaximoBytes, null, prazo.Valor, validade.Valor));
    }

    private static ParteLida<FormatosPermitidos> Formatos(FormatosPermitidosNoDocumento? formatos) =>
        ParteLida<FormatosPermitidos>.Do(
            FormatosPermitidos.Criar(formatos?.Qualquer ?? false, formatos is null ? null : [.. formatos.Lista.Select(Entrada)]),
            CampoDosFormatos);

    // O formato ausente fica sem código, e a fábrica o recusa como formato não reconhecido.
    private static (string Formato, int? TamanhoMaximoBytesMax) Entrada(FormatoPermitidoNoDocumento formato) =>
        (CodigoDoValorLido.De(formato.Formato) ?? string.Empty, formato.TamanhoMaximoBytesMax);
}
