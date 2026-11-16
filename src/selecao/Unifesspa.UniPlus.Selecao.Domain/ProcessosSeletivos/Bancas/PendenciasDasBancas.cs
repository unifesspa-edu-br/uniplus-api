namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Bancas;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.ConceitoDaAvaliacao;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Cronograma;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Edicao;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Etapas;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.FasesEEtapas;

/// <summary>
/// O que falta nas bancas declaradas para o processo ser congelado: a banca de recursos que todo
/// recurso exige, a regra de deliberação de cada banca e o mínimo de integrantes. A restauração por
/// descarte não passa por aqui: ela devolve a banca que a versão divulgada já tinha aceitado.
/// </summary>
internal static class PendenciasDasBancas
{
    internal static DomainError? PendenciaDasBancas(EstadoDaConfiguracao estado, CorretoresDasEtapas corretores)
    {
        ArgumentNullException.ThrowIfNull(estado);
        ArgumentNullException.ThrowIfNull(corretores);

        CronogramaDoProcesso cronograma = CronogramaDoProcesso.Do(estado.CronogramaFases);
        EtapasDoProcesso etapas = EtapasDoProcesso.Das(estado.Etapas);

        return CausasDaRecusa.Primeira(ConferenciaDasBancas.ConferirRecursosSemBanca(cronograma, etapas))
            ?? CausasDaRecusa.Primeira(cronograma.RecusasDasDeclaracoes(ConferenciaDasBancas.CodigosDoRecorte))
            ?? CausasDaRecusa.Primeira(ConferenciaDasBancas.ConferirRegrasDeDeliberacao(cronograma, etapas))
            ?? CausasDaRecusa.Primeira(ConferenciaDasBancas.ConferirMinimoDeIntegrantes(cronograma, etapas, corretores));
    }
}
