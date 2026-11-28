namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.ImportacaoDoResultado;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Coleta;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// A gravação da declaração da importação do resultado: a forma dos relatórios e as recusas que já valem sobre o
/// rascunho. Sem relatório e sem primeira fase, o processo volta a "por declarar", que a ausência representa.
/// </summary>
internal static class RegrasDaImportacaoDoResultado
{
    private static readonly FieldError RelatoriosAusentes = new(
        $"{ConferenciaDaImportacaoDoResultado.Campo}.relatorios",
        new DomainError(CodigosDeValidacao.Obrigatorio, "Informe os relatórios da importação; envie [] quando não houver nenhum."));

    internal static Result<DeclaracaoDaImportacaoDoResultado?> Planejar(
        ConfiguracaoDeclarada configuracao, IReadOnlyList<RelatorioInformado>? relatorios, Guid? primeiraFaseDepoisDaImportacaoId)
    {
        ArgumentNullException.ThrowIfNull(configuracao);

        if (relatorios is { Count: 0 } && primeiraFaseDepoisDaImportacaoId is null)
        {
            return Result<DeclaracaoDaImportacaoDoResultado?>.Success(null);
        }

        ProcessoDosFormularios processo = ProcessoDosFormularios.De(configuracao);
        IReadOnlyList<FieldError> daOrigem = ConferenciaDaImportacaoDoResultado.RecusaDaOrigem(processo.InscricaoPeloSistema);
        return daOrigem.Count > 0
            ? Result<DeclaracaoDaImportacaoDoResultado?>.ValidationFailure([.. daOrigem, .. relatorios is null ? [RelatoriosAusentes] : Array.Empty<FieldError>()])
            : DeclararNoProcesso(processo, configuracao, relatorios, primeiraFaseDepoisDaImportacaoId);
    }

    // Os relatórios (ausentes ou informados) e a primeira fase no cronograma são conferências independentes: as duas rodam e as recusas se somam.
    private static Result<DeclaracaoDaImportacaoDoResultado?> DeclararNoProcesso(
        ProcessoDosFormularios processo, ConfiguracaoDeclarada configuracao, IReadOnlyList<RelatorioInformado>? relatorios, Guid? primeiraFaseDepoisDaImportacaoId)
    {
        FieldError[] recusas =
        [
            .. relatorios is null ? [RelatoriosAusentes] : DeclaracaoDaImportacaoDoResultado.RecusasDaForma(relatorios),
            .. ConferenciaDaImportacaoDoResultado.RecusasDaGravacao(processo, primeiraFaseDepoisDaImportacaoId, configuracao.Cronograma.CronogramaFases),
        ];

        return recusas.Length > 0
            ? Result<DeclaracaoDaImportacaoDoResultado?>.ValidationFailure(recusas)
            : Result<DeclaracaoDaImportacaoDoResultado?>.Success(DeclaracaoDaImportacaoDoResultado.Declarar(relatorios!, primeiraFaseDepoisDaImportacaoId).Value);
    }
}
