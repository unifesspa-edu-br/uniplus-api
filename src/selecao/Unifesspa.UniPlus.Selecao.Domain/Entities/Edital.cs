namespace Unifesspa.UniPlus.Selecao.Domain.Entities;

using Enums;
using Events;
using ValueObjects;
using Unifesspa.UniPlus.Kernel.Domain.Entities;

public sealed class Edital : EntityBase
{
    public NumeroEdital NumeroEdital { get; private set; } = null!;
    public string Titulo { get; private set; } = string.Empty;

    /// <summary>
    /// FK preparatória para a futura entidade <c>TipoEdital</c> (Story #455).
    /// Permanece <c>null</c> nesta Story #454 — a entidade ainda não existe;
    /// será populada quando a promoção do enum <see cref="TipoProcesso"/>
    /// for concluída e o seed Newman (#463) tiver inserido as linhas-template.
    /// FK não-nula entra em migration futura quando dados existirem.
    /// </summary>
    public Guid? TipoEditalId { get; private set; }
    public StatusEdital Status { get; private set; }
    public PeriodoInscricao? PeriodoInscricao { get; private set; }
    public FormulaCalculo? FormulaCalculo { get; private set; }
    public int MaximoOpcoesCurso { get; private set; } = 1;
    public bool BonusRegionalHabilitado { get; private set; }

    private readonly List<Etapa> _etapas = [];
    public IReadOnlyCollection<Etapa> Etapas => _etapas.AsReadOnly();

    private readonly List<Cota> _cotas = [];
    public IReadOnlyCollection<Cota> Cotas => _cotas.AsReadOnly();

    private Edital() { }

    public static Edital Criar(NumeroEdital numeroEdital, string titulo, Guid? tipoEditalId = null)
    {
        // Invariante de factory: TipoEditalId é opcional (null), mas
        // Guid.Empty é estado inválido — significa "informado, mas vazio".
        // Validator espelha a regra na borda HTTP; este guard cobre callers
        // internos (handlers, seeds) que não passam pelo command bus.
        if (tipoEditalId.HasValue && tipoEditalId.Value == Guid.Empty)
        {
            throw new ArgumentException(
                "TipoEditalId não pode ser Guid vazio. Omita o argumento para nulo.",
                nameof(tipoEditalId));
        }

        return new Edital
        {
            NumeroEdital = numeroEdital,
            Titulo = titulo,
            TipoEditalId = tipoEditalId,
            Status = StatusEdital.Rascunho,
        };
    }

    public void DefinirPeriodoInscricao(PeriodoInscricao periodo) =>
        PeriodoInscricao = periodo;

    public void DefinirFormulaCalculo(FormulaCalculo formula) =>
        FormulaCalculo = formula;

    public void DefinirMaximoOpcoesCurso(int maximo) =>
        MaximoOpcoesCurso = maximo;

    public void AdicionarEtapa(Etapa etapa) => _etapas.Add(etapa);

    public void AdicionarCota(Cota cota) => _cotas.Add(cota);

    public void Publicar()
    {
        Status = StatusEdital.Publicado;
        AddDomainEvent(new EditalPublicadoEvent(Id, NumeroEdital.ToString()));
    }
}
