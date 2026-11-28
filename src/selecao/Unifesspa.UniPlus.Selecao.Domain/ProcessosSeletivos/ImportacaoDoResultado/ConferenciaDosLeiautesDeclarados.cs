namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.ImportacaoDoResultado;

using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// O leiaute que cada relatório declarado cita existe no catálogo de leiautes, com a versão declarada. A conferência
/// só vale quando há catálogo; sem ele, a declaração é conferida pela forma.
/// </summary>
public static class ConferenciaDosLeiautesDeclarados
{
    public static IReadOnlyList<FieldError> RecusasDosLeiautes(
        DeclaracaoDaImportacaoDoResultado declaracao, IReadOnlyCollection<LeiauteDoRelatorio> catalogo)
    {
        ArgumentNullException.ThrowIfNull(declaracao);
        ArgumentNullException.ThrowIfNull(catalogo);

        return
        [
            .. declaracao.Relatorios
                .Where(relatorio => !catalogo.Any(leiaute => leiaute.Codigo == relatorio.LeiauteCodigo && leiaute.Versao == relatorio.LeiauteVersao))
                .Select(relatorio => new FieldError(
                    $"{ConferenciaDaImportacaoDoResultado.Campo}.relatorios[{relatorio.Posicao - 1}]",
                    new DomainError(ImportacaoDoResultadoErrorCodes.LeiauteDesconhecido, "O catálogo de leiautes não tem o leiaute declarado, com essa versão."))),
        ];
    }
}
