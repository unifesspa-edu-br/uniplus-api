namespace Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;

using Domain.Entities;

using Kernel.Results;

using Unifesspa.UniPlus.Configuracao.Contracts;

/// <summary>O prazo de emissão e a validade do documento do modelo, na forma de entrada das regras do processo.</summary>
internal sealed record PrazoEValidadeCopiados(PrazoDeEmissaoInput? Prazo, ValidadeDoDocumentoInput? Validade);

/// <summary>
/// Copia o prazo de emissão e a validade escolhidos pelo modelo para a entrada da exigência do processo,
/// trocando o código da fase de cada referência pela fase do cronograma do processo. O que o modelo não
/// escolheu continua sem escolha. A referência que cita fase que o cronograma não tem é recusada; que os
/// campos combinem com a escolha quem confere é o processo, ao receber a exigência.
/// </summary>
internal static class CopiaDoPrazoEValidadeDoModelo
{
    public static Result<PrazoEValidadeCopiados> Copiar(
        RequisitosDoDocumentoDoModeloInput? requisitos, IReadOnlyCollection<FaseCronograma> fases, string campoDoDocumento)
    {
        List<FieldError> recusas = [];
        PrazoDeEmissaoInput? prazo = CopiarPrazo(requisitos?.PrazoDeEmissao, fases, $"{campoDoDocumento}.{EscritaDoPrazoEDaValidadeDoDocumento.CaminhoDoPrazo}", recusas);
        ValidadeDoDocumentoInput? validade = CopiarValidade(requisitos?.Validade, fases, $"{campoDoDocumento}.{EscritaDoPrazoEDaValidadeDoDocumento.CaminhoDaValidade}", recusas);
        return recusas.Count > 0
            ? Result<PrazoEValidadeCopiados>.ValidationFailure(recusas)
            : Result<PrazoEValidadeCopiados>.Success(new PrazoEValidadeCopiados(prazo, validade));
    }

    private static PrazoDeEmissaoInput? CopiarPrazo(
        PrazoDeEmissaoDoModeloInput? prazo, IReadOnlyCollection<FaseCronograma> fases, string campo, List<FieldError> recusas) =>
        prazo is null
            ? null
            : new PrazoDeEmissaoInput(
                prazo.Escolha, prazo.Valor, prazo.Unidade, CopiarReferencia(prazo.Referencia, fases, $"{campo}.referencia", recusas), prazo.EfeitoForaDoPrazo);

    private static ValidadeDoDocumentoInput? CopiarValidade(
        ValidadeDoDocumentoDoModeloInput? validade, IReadOnlyCollection<FaseCronograma> fases, string campo, List<FieldError> recusas) =>
        validade is null
            ? null
            : new ValidadeDoDocumentoInput(
                validade.Escolha, CopiarReferencia(validade.Referencia, fases, $"{campo}.referencia", recusas), validade.EfeitoDoVencimento);

    private static DataDeReferenciaDoDocumentoInput? CopiarReferencia(
        DataDeReferenciaDoDocumentoDoModeloInput? referencia, IReadOnlyCollection<FaseCronograma> fases, string campo, List<FieldError> recusas)
    {
        if (referencia is null)
        {
            return null;
        }

        Guid? faseId = referencia.FaseCodigo is null ? null : FaseDoCronograma(referencia.FaseCodigo, fases, $"{campo}.faseCodigo", recusas);
        return new DataDeReferenciaDoDocumentoInput(referencia.Tipo, referencia.Data, faseId);
    }

    private static Guid? FaseDoCronograma(string faseCodigo, IReadOnlyCollection<FaseCronograma> fases, string campo, List<FieldError> recusas)
    {
        FaseCronograma? fase = fases.FirstOrDefault(f => string.Equals(f.Codigo, faseCodigo, StringComparison.Ordinal));
        if (fase is null)
        {
            recusas.Add(RecusaDaFase(faseCodigo, campo));
        }

        return fase?.Id;
    }

    private static FieldError RecusaDaFase(string faseCodigo, string campo) =>
        new(campo, new DomainError(
            AplicacaoDeModeloErrorCodes.FaseDoDocumentoInexistente,
            $"O modelo conta o prazo ou a validade do documento a partir da fase '{faseCodigo}', que o cronograma do processo não tem; defina o cronograma antes."));
}
