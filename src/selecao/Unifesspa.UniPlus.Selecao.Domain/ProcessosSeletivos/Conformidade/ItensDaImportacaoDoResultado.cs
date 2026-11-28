namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Conformidade;

using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Coleta;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.ImportacaoDoResultado;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

internal static class ItensDaImportacaoDoResultado
{
    internal static ItemConformidade Avaliar(ConfiguracaoDeclarada configuracao) =>
        new("importacao_do_resultado", DimensaoConformidade.ImportacaoDoResultado,
            "Importação do resultado declarada: relatórios e primeira fase depois da importação no cronograma",
            ConferenciaDaImportacaoDoResultado.Conferir(
                ProcessoDosFormularios.De(configuracao), configuracao.ImportacaoDoResultado, configuracao.Cronograma.CronogramaFases).Count == 0);
}
