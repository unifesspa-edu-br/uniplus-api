namespace Unifesspa.UniPlus.Selecao.Domain.Entities;

using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Events;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.SharedKernel.Domain.Entities;

public sealed class Edital : EntityBase
{
    public NumeroEdital NumeroEdital { get; private set; } = null!;
    public string Titulo { get; private set; } = string.Empty;
    public TipoProcesso TipoProcesso { get; private set; }
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

    public static Edital Criar(NumeroEdital numeroEdital, string titulo, TipoProcesso tipoProcesso)
    {
        var edital = new Edital
        {
            NumeroEdital = numeroEdital,
            Titulo = titulo,
            TipoProcesso = tipoProcesso,
            Status = StatusEdital.Rascunho
        };

        return edital;
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
