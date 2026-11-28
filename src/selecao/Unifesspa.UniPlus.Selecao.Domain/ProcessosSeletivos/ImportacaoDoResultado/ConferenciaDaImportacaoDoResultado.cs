namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.ImportacaoDoResultado;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Coleta;

/// <summary>
/// O que a declaração da importação do resultado exige (UNI-REQ-0144): o processo com inscrição pelo sistema não a
/// declara; o de resultado importado a declara, com ao menos um relatório de leiaute bem formado e com a primeira
/// fase depois da importação, que é fase do cronograma. A recusa de quem declara em inscrição pelo sistema e a de
/// quem não declara interrompem as demais, que só valem sobre uma declaração. As demais recusas se acumulam
/// (ADR-0125): a forma dos relatórios e a primeira fase no cronograma não dependem uma da outra, e a gravação confere as duas juntas, depois da origem; a
/// publicação confere a completude e a primeira fase no cronograma de então, que pode ter mudado desde a gravação. A origem
/// dos candidatos é fixada na criação do processo e a forma dos relatórios, na declaração.
/// </summary>
public static class ConferenciaDaImportacaoDoResultado
{
    internal const string Campo = "importacao";
    internal const string CampoDaPrimeiraFase = Campo + ".primeiraFaseDepoisDaImportacao";

    public static IReadOnlyList<FieldError> RecusasDaGravacao(
        ProcessoDosFormularios processo,
        Guid? primeiraFaseDepoisDaImportacaoId,
        IReadOnlyCollection<FaseCronograma> cronograma)
    {
        ArgumentNullException.ThrowIfNull(processo);
        ArgumentNullException.ThrowIfNull(cronograma);

        return processo.InscricaoPeloSistema
            ? [EmInscricaoPeloSistema()]
            : [.. RecusaDaPrimeiraFaseForaDoCronograma(primeiraFaseDepoisDaImportacaoId, cronograma)];
    }

    public static IReadOnlyList<FieldError> Conferir(
        ProcessoDosFormularios processo,
        DeclaracaoDaImportacaoDoResultado? declaracao,
        IReadOnlyCollection<FaseCronograma> cronograma)
    {
        ArgumentNullException.ThrowIfNull(processo);
        ArgumentNullException.ThrowIfNull(cronograma);

        if (declaracao is null)
        {
            return processo.InscricaoPeloSistema
                ? []
                : [Recusa(Campo, ImportacaoDoResultadoErrorCodes.PorDeclarar, "O processo de resultado importado ainda não declara os relatórios que importa.")];
        }

        return [.. RecusaDaFaltaDeRelatorio(declaracao), .. RecusasDaPrimeiraFase(declaracao, cronograma)];
    }

    private static IEnumerable<FieldError> RecusaDaFaltaDeRelatorio(DeclaracaoDaImportacaoDoResultado declaracao) =>
        declaracao.Relatorios.Count == 0
            ? [Recusa($"{Campo}.relatorios", ImportacaoDoResultadoErrorCodes.SemRelatorio, "O processo declara a importação e nenhum relatório.")]
            : [];

    private static IEnumerable<FieldError> RecusasDaPrimeiraFase(DeclaracaoDaImportacaoDoResultado declaracao, IReadOnlyCollection<FaseCronograma> cronograma) =>
        declaracao.PrimeiraFaseDepoisDaImportacaoId is null
            ? [Recusa(CampoDaPrimeiraFase, ImportacaoDoResultadoErrorCodes.PrimeiraFasePorDeclarar, "O processo ainda não declara a primeira fase depois da importação.")]
            : RecusaDaPrimeiraFaseForaDoCronograma(declaracao.PrimeiraFaseDepoisDaImportacaoId, cronograma);

    internal static FieldError PrimeiraFaseForaDoCronograma() =>
        Recusa(CampoDaPrimeiraFase, ImportacaoDoResultadoErrorCodes.PrimeiraFaseForaDoCronograma, "A primeira fase declarada depois da importação não é fase do cronograma do processo.");

    private static IEnumerable<FieldError> RecusaDaPrimeiraFaseForaDoCronograma(Guid? primeiraFaseId, IReadOnlyCollection<FaseCronograma> cronograma) =>
        primeiraFaseId is { } faseId && cronograma.All(fase => fase.Id != faseId)
            ? [PrimeiraFaseForaDoCronograma()]
            : [];

    /// <summary>A recusa de origem: o processo com inscrição pelo sistema não declara importação, e ela vem antes de qualquer outra.</summary>
    internal static IReadOnlyList<FieldError> RecusaDaOrigem(bool inscricaoPeloSistema) =>
        inscricaoPeloSistema ? [EmInscricaoPeloSistema()] : [];

    private static FieldError EmInscricaoPeloSistema() =>
        Recusa(Campo, ImportacaoDoResultadoErrorCodes.EmInscricaoPeloSistema, "O processo com inscrição pelo sistema não declara importação de resultado.");

    private static FieldError Recusa(string campo, string codigo, string mensagem) => new(campo, new DomainError(codigo, mensagem));
}
