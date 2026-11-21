namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.FasesEEtapas;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Bancas;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Cronograma;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Etapas;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// O recurso que a etapa abre. Quem o julga é a banca da etapa dedicada à 1ª instância, depois a da fase em que a etapa
/// acontece e, sem nenhuma delas, as bancas da própria etapa que avaliaram o item.
/// </summary>
internal sealed record RecursoNaEtapa(CronogramaDoProcesso Cronograma, EtapaJulgada Julgada, int PosicaoDoRecurso, RecursoDeclarado Recurso) : IRecursoNoLugar
{
    public string Onde => $"etapa na posição {Julgada.Posicao + 1}";

    public EtapaJulgada? Etapa => Julgada;

    public IReadOnlyList<QuemJulgaORecurso.BancasDoNivel> DedicadasDaPrimeiraInstancia =>
    [
        new(QuemJulgaORecurso.Nivel.BancaDeRecursosDaEtapa, QuemJulgaORecurso.DedicadasDaPrimeiraInstancia(Julgada.Etapa.Bancas.Declaradas)),
        new(QuemJulgaORecurso.Nivel.BancaDeRecursosDaFase, QuemJulgaORecurso.DedicadasDaPrimeiraInstancia(BancasDasFasesDaEtapa())),
    ];

    public IReadOnlyList<BancaDeclarada> Avaliadoras(ProcedimentoDeRecursoDeclarado? procedimento) =>
        procedimento is null ? [] : QuemJulgaORecurso.QueAvaliaram(OndeSeProcura(procedimento.Alvo), procedimento.TipoDeDecisao);

    public bool Indeterminado(ProcedimentoDeRecursoDeclarado? procedimento) =>
        JulgadorasIndeterminadas.DoRecursoDaEtapa(Cronograma, Julgada.Etapa);

    public FieldError NasBancas(DomainError erro) =>
        EtapasDoProcesso.NaEtapa(Julgada.Posicao, EtapaDeclarada.ListaDasBancas, erro);

    public FieldError NaInstanciaSuperior(DomainError erro) =>
        EtapasDoProcesso.NaEtapa(Julgada.Posicao, $"{EtapaDeclarada.ListaDosRecursos}[{PosicaoDoRecurso}].{QuemJulgaORecurso.CampoDaInstanciaSuperior}", erro);

    public DomainError SemBanca(TipoMotivoDeDecisao? tipoDeDecisao) =>
        RecusasDoRecurso.SemBancaNaEtapa(Julgada.Posicao, Julgada.Etapa.FaseCodigo, tipoDeDecisao);

    private IEnumerable<BancaDeclarada> BancasDasFasesDaEtapa() => Cronograma.Fases
        .Where(fase => string.Equals(fase.Codigo, Julgada.Etapa.FaseCodigo, StringComparison.Ordinal))
        .SelectMany(static fase => fase.Bancas.Declaradas);

    private IEnumerable<BancaDeclarada> OndeSeProcura(AlvoDoProcedimentoDeRecurso? alvo) => alvo switch
    {
        null => Julgada.Etapa.Bancas.Declaradas,
        { Tipo: TipoDeAlvoDoProcedimentoDeRecurso.ItemDeAvaliacao } when Julgada.Etapa.ContemOAlvo(alvo) => Julgada.Etapa.Bancas.Declaradas,
        _ => [],
    };
}
