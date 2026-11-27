namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.FasesEEtapas;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Bancas;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Cronograma;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Etapas;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// O recurso da fase. Quem o julga é a banca da fase dedicada à 1ª instância e, sem ela, as bancas que avaliaram o item,
/// procuradas na fase e nas etapas dela, ou só na etapa que contém o alvo do procedimento.
/// </summary>
internal sealed record RecursoNaFase(FaseDeclarada Fase, int Posicao, RecursoDeclarado Recurso, EtapasDoProcesso Etapas) : IRecursoNoLugar
{
    public string Onde => $"fase {Fase.Citacao}";

    public EtapaJulgada? Etapa => null;

    public IReadOnlyList<QuemJulgaORecurso.BancasDoNivel> DedicadasDaPrimeiraInstancia =>
        [new(QuemJulgaORecurso.Nivel.BancaDeRecursosDaFase, QuemJulgaORecurso.DedicadasDaPrimeiraInstancia(Fase.Bancas.Declaradas))];

    public IReadOnlyList<BancaDeclarada> Avaliadoras(ProcedimentoDeRecursoDeclarado? procedimento) =>
        procedimento is null ? [] : QuemJulgaORecurso.QueAvaliaram(OndeSeProcura(procedimento.Alvo), procedimento.TipoDeDecisao);

    public bool Indeterminado(ProcedimentoDeRecursoDeclarado? procedimento) =>
        JulgadorasIndeterminadas.DoRecursoDaFase(Fase, Etapas, procedimento);

    public FieldError NasBancas(DomainError erro) =>
        CronogramaDoProcesso.NaFase(Posicao, FaseDeclarada.ListaDasBancasRequeridas, erro);

    public FieldError NaInstanciaSuperior(DomainError erro) =>
        CronogramaDoProcesso.NaFase(Posicao, $"{FaseDeclarada.CampoDaRegraDeRecurso}.{QuemJulgaORecurso.CampoDaInstanciaSuperior}", erro);

    public DomainError SemBanca(TipoMotivoDeDecisao? tipoDeDecisao) =>
        RecusasDoRecurso.SemBancaNaFase(Fase.Citacao, tipoDeDecisao);

    private IEnumerable<BancaDeclarada> OndeSeProcura(AlvoDoProcedimentoDeRecurso? alvo) => alvo switch
    {
        null => [.. Fase.Bancas.Declaradas, .. Etapas.DaFase(Fase).SelectMany(static etapa => etapa.Bancas.Declaradas)],
        { Tipo: TipoDeAlvoDoProcedimentoDeRecurso.ItemDeAvaliacao } => Etapas.Etapas.Where(etapa => etapa.ContemOAlvo(alvo)).SelectMany(static etapa => etapa.Bancas.Declaradas),
        _ => [],
    };
}
