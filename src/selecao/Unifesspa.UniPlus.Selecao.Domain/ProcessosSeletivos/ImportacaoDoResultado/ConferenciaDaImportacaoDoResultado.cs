namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.ImportacaoDoResultado;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Coleta;

/// <summary>
/// O que a publicação exige da declaração da importação do resultado (UNI-REQ-0144): o processo com inscrição
/// pelo sistema não a declara; o de resultado importado a declara, com ao menos um relatório de leiaute que o
/// catálogo tem e com a primeira fase depois da importação, que é fase do cronograma. A recusa de quem declara
/// em inscrição pelo sistema e a de quem não declara interrompem as demais, que só valem sobre uma declaração.
/// As demais recusas se acumulam (ADR-0125).
/// </summary>
public static class ConferenciaDaImportacaoDoResultado
{
    private const string Campo = "importacao";

    public static IReadOnlyList<FieldError> Conferir(
        ProcessoDosFormularios processo,
        DeclaracaoDaImportacaoDoResultado? declaracao,
        IReadOnlyCollection<LeiauteDoRelatorio> catalogo,
        IReadOnlyCollection<FaseCronograma> cronograma)
    {
        ArgumentNullException.ThrowIfNull(processo);
        ArgumentNullException.ThrowIfNull(catalogo);
        ArgumentNullException.ThrowIfNull(cronograma);

        if (declaracao is null)
        {
            return processo.InscricaoPeloSistema
                ? []
                : [Recusa(Campo, ImportacaoDoResultadoErrorCodes.PorDeclarar, "O processo de resultado importado ainda não declara os relatórios que importa.")];
        }

        return processo.InscricaoPeloSistema
            ? [Recusa(Campo, ImportacaoDoResultadoErrorCodes.EmInscricaoPeloSistema, "O processo com inscrição pelo sistema não declara importação de resultado.")]
            : [.. RecusasDosRelatorios(declaracao, catalogo), .. RecusaDaPrimeiraFase(declaracao, cronograma)];
    }

    private static IEnumerable<FieldError> RecusasDosRelatorios(DeclaracaoDaImportacaoDoResultado declaracao, IReadOnlyCollection<LeiauteDoRelatorio> catalogo)
    {
        if (declaracao.Relatorios.Count == 0)
        {
            yield return Recusa($"{Campo}.relatorios", ImportacaoDoResultadoErrorCodes.SemRelatorio, "O processo declara a importação e nenhum relatório.");
        }

        for (int indice = 0; indice < declaracao.Relatorios.Count; indice++)
        {
            RelatorioDeclarado relatorio = declaracao.Relatorios[indice];
            if (!catalogo.Any(leiaute => leiaute.Codigo == relatorio.LeiauteCodigo && leiaute.Versao == relatorio.LeiauteVersao))
            {
                yield return Recusa($"{Campo}.relatorios[{indice}]", ImportacaoDoResultadoErrorCodes.LeiauteDesconhecido, "O catálogo de leiautes não tem o leiaute declarado, com essa versão.");
            }
        }
    }

    private static IEnumerable<FieldError> RecusaDaPrimeiraFase(DeclaracaoDaImportacaoDoResultado declaracao, IReadOnlyCollection<FaseCronograma> cronograma)
    {
        if (declaracao.PrimeiraFaseDepoisDaImportacaoId is not { } faseId)
        {
            yield return Recusa($"{Campo}.primeiraFaseDepoisDaImportacao", ImportacaoDoResultadoErrorCodes.PrimeiraFasePorDeclarar, "O processo ainda não declara a primeira fase depois da importação.");
        }
        else if (cronograma.All(fase => fase.Id != faseId))
        {
            yield return Recusa($"{Campo}.primeiraFaseDepoisDaImportacao", ImportacaoDoResultadoErrorCodes.PrimeiraFaseForaDoCronograma, "A primeira fase declarada depois da importação não é fase do cronograma do processo.");
        }
    }

    private static FieldError Recusa(string campo, string codigo, string mensagem) => new(campo, new DomainError(codigo, mensagem));
}
