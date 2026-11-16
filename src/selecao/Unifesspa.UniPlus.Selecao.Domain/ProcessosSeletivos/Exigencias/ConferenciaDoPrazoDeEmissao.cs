namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Exigencias;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// O prazo máximo de emissão do documento é opcional e congela com as demais propriedades da exigência
/// (UNI-REQ-0016, UNI-REQ-0078); a regra do processo pode obrigá-lo (UNI-REQ-0151). O valor é sempre o
/// declarado na exigência. A âncora do prazo, uma fase do cronograma ou o fim da inscrição, existe no
/// processo e tem o extremo definido, sem fallback silencioso. A conferência da data de emissão do
/// arquivo enviado é da execução da habilitação.
/// </summary>
public static class ConferenciaDoPrazoDeEmissao
{
    private const string CampoDoPrazo = "idadeMaximaEmissao";

    public static IReadOnlyList<FieldError> Conferir(
        IReadOnlyCollection<FaseCronograma> cronograma, IReadOnlyList<PrazoDeEmissaoDaExigencia> exigencias)
    {
        ArgumentNullException.ThrowIfNull(cronograma);
        ArgumentNullException.ThrowIfNull(exigencias);

        return [.. exigencias.Select((exigencia, indice) => (recusa: Recusa(cronograma, exigencia), indice))
            .Where(static conferida => conferida.recusa is not null)
            .Select(static conferida => new FieldError($"exigencias[{conferida.indice}].{CampoDoPrazo}", conferida.recusa!))];
    }

    private static DomainError? Recusa(IReadOnlyCollection<FaseCronograma> cronograma, PrazoDeEmissaoDaExigencia exigencia) => exigencia.Idade switch
    {
        null when exigencia.PrazoObrigatorio => new DomainError(
            PrazoDeEmissaoErrorCodes.PrazoObrigatorio, "A exigência obriga o prazo máximo de emissão do documento e ele não foi declarado."),
        null => null,
        { ReferenciaFaseId: { } faseId } idade => RecusaDaFaseAncora(cronograma, faseId, idade),
        { ReferenciaTipo: ReferenciaTipoIdadeEmissao.FimInscricao } => RecusaDoFimDaInscricao(cronograma),
        _ => null,
    };

    private static DomainError? RecusaDaFaseAncora(IReadOnlyCollection<FaseCronograma> cronograma, Guid faseId, IdadeMaximaEmissao idade)
    {
        if (cronograma.FirstOrDefault(fase => fase.Id == faseId) is not { } faseAncora)
        {
            return new DomainError(
                PrazoDeEmissaoErrorCodes.FaseNaoPertenceAoProcesso, "A fase âncora do prazo de emissão não pertence ao cronograma deste processo.");
        }

        DateTimeOffset? extremo = idade.ReferenciaTipo == ReferenciaTipoIdadeEmissao.InicioFase ? faseAncora.Inicio : faseAncora.Fim;
        return extremo is null
            ? new DomainError(PrazoDeEmissaoErrorCodes.FaseExtremoAusente, "A fase âncora do prazo de emissão não tem o extremo definido.")
            : null;
    }

    private static DomainError? RecusaDoFimDaInscricao(IReadOnlyCollection<FaseCronograma> cronograma)
    {
        if (!cronograma.Any(static fase => fase.ColetaInscricao))
        {
            return new DomainError(
                PrazoDeEmissaoErrorCodes.FaseNaoPertenceAoProcesso, "O prazo de emissão ancorado no fim da inscrição exige uma fase que coleta inscrição, e o processo não tem nenhuma.");
        }

        return cronograma.Any(static fase => fase.ColetaInscricao && fase.Fim is not null)
            ? null
            : new DomainError(PrazoDeEmissaoErrorCodes.FaseExtremoAusente, "Nenhuma fase que coleta inscrição tem o fim definido.");
    }
}
