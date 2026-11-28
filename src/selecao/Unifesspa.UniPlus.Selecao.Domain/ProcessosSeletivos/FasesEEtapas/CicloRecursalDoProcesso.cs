namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.FasesEEtapas;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Cronograma;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Etapas;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>O fechamento do ciclo recursal do processo: cada fase e cada etapa que publica resultado preliminar publica, nela mesma, o definitivo do mesmo tipo de ato.</summary>
public static class CicloRecursalDoProcesso
{
    public static IReadOnlyList<FieldError> Conferir(CronogramaDoProcesso cronograma, EtapasDoProcesso etapas, NomesDosTiposDeAto nomes)
    {
        ArgumentNullException.ThrowIfNull(cronograma);
        ArgumentNullException.ThrowIfNull(etapas);
        ArgumentNullException.ThrowIfNull(nomes);

        return [.. cronograma.ConferirFechamentoDoCiclo(nomes), .. etapas.ConferirFechamentoDoCiclo(nomes)];
    }

    public static ItemConformidade Item(CronogramaDoProcesso cronograma, EtapasDoProcesso etapas, NomesDosTiposDeAto nomes)
    {
        ArgumentNullException.ThrowIfNull(cronograma);
        ArgumentNullException.ThrowIfNull(etapas);
        ArgumentNullException.ThrowIfNull(nomes);

        return new ItemConformidade(
            "cronograma_conclusao_do_ciclo_recursal",
            DimensaoConformidade.Cronograma,
            "Cronograma: a fase ou a etapa que publica resultado preliminar publica, nela mesma, o definitivo do mesmo tipo de ato",
            !ProdutosComParteRecusada(cronograma, etapas) && Conferir(cronograma, etapas, nomes).Count == 0);
    }

    // Com parte recusada em alguma lista de produtos não se sabe o que se publica, e o item fica pendente em vez de aprovado por omissão.
    private static bool ProdutosComParteRecusada(CronogramaDoProcesso cronograma, EtapasDoProcesso etapas) =>
        cronograma.ProdutosComParteRecusadaEmAlgumaFase || etapas.Etapas.Any(static etapa => etapa.ParteRecusadaEm(EtapaDeclarada.ListaDosProdutos));
}
