namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Bancas;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Edicao;

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

        return ExigenciaDeBancaDeRecurso.RecursosSemBanca(estado).FirstOrDefault()
            ?? ExigenciaDeBancaDeRecurso.RecortesEmBancaDeRecursos(estado).FirstOrDefault()
            ?? BancasSemRegra(estado).FirstOrDefault()
            ?? MinimoDeIntegrantes.Violacoes(estado, corretores).FirstOrDefault();
    }

    internal static DomainError? PendenciaDoRecursoSemBanca(EstadoDaConfiguracao estado) =>
        ExigenciaDeBancaDeRecurso.RecursosSemBanca(estado).FirstOrDefault();

    internal static DomainError? PendenciaDoRecorteEmBancaDeRecursos(EstadoDaConfiguracao estado) =>
        ExigenciaDeBancaDeRecurso.RecortesEmBancaDeRecursos(estado).FirstOrDefault();

    internal static DomainError? PendenciaDaRegraDeDeliberacao(EstadoDaConfiguracao estado) =>
        BancasSemRegra(estado).FirstOrDefault();

    internal static DomainError? PendenciaDoMinimoDeIntegrantes(EstadoDaConfiguracao estado, CorretoresDasEtapas corretores) =>
        MinimoDeIntegrantes.Violacoes(estado, corretores).FirstOrDefault();

    private static IEnumerable<DomainError> BancasSemRegra(EstadoDaConfiguracao estado) =>
    [
        .. estado.CronogramaFases.SelectMany(static fase => fase.BancasRequeridas
            .Where(static banca => banca.RegraDeDeliberacao is null)
            .Select(banca => SemRegra(banca.Nome, $"da fase '{fase.Codigo}'"))),
        .. estado.Etapas.SelectMany(static etapa => etapa.Bancas
            .Where(static banca => banca.RegraDeDeliberacao is null)
            .Select(banca => SemRegra(banca.Nome, $"da etapa '{etapa.Nome}'"))),
    ];

    private static DomainError SemRegra(string nome, string onde) =>
        new(BancaErrorCodes.RegraDeDeliberacaoObrigatoria, $"A banca '{nome}' {onde} não declara a regra de deliberação.");
}
