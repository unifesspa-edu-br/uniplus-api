namespace Unifesspa.UniPlus.Selecao.Domain.Entities;


using Unifesspa.UniPlus.Kernel.Domain.Entities;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Regras.ValueObjects;

/// <summary>
/// Um grupo repetível do formulário (UNI-REQ-0146), como a composição familiar: uma lista de
/// ocorrências, cada uma com os mesmos campos, com o mínimo e, quando declarado, o máximo de
/// ocorrências e a exibição e a obrigatoriedade do grupo inteiro.
/// </summary>
/// <remarks>
/// O grupo é dono dos seus campos: cada campo é um <see cref="FatoColetado"/> de fato de membro,
/// que existe uma vez por ocorrência e por isso nunca é item do formulário nem é citado fora do
/// grupo. Como os itens, o campo pertence também ao processo, que guarda um produtor só por fato.
/// O grupo ocupa uma posição na ordem dos itens; os campos têm ordem própria dentro dele.
/// </remarks>
public sealed class GrupoColetado : EntityBase, IParteDoProcesso
{
    private readonly List<FatoColetado> _subitens = [];

    public Guid ProcessoSeletivoId { get; private set; }

    /// <summary>O formulário do grupo; atribuído pelo processo ao definir os itens.</summary>
    public Guid? FormularioId { get; private set; }

    /// <summary>A finalidade do formulário do grupo; atribuída pelo processo ao definir os itens.</summary>
    public FinalidadeFormulario? Finalidade { get; private set; }

    /// <summary>Código do grupo no formulário, fora do espaço de códigos dos fatos.</summary>
    public string Codigo { get; private set; } = string.Empty;

    /// <summary>A posição do grupo na ordem de coleta, no mesmo espaço da ordem dos itens.</summary>
    public int Ordem { get; private set; }

    /// <summary>A seção do formulário em que o grupo aparece.</summary>
    public string? PaginaCodigo { get; private set; }

    public string Rotulo { get; private set; } = string.Empty;

    public int Minimo { get; private set; }

    /// <summary>O máximo de ocorrências; <see langword="null"/> aceita qualquer quantidade.</summary>
    public int? Maximo { get; private set; }

    /// <summary>Quando o grupo aparece; <see langword="null"/> é sempre.</summary>
    public PredicadoDnf? Exibicao { get; private set; }

    public Obrigatoriedade Obrigatoriedade { get; private set; } = Obrigatoriedade.Nunca;

    /// <summary>O próprio candidato é um dos membros, identificado pelo parentesco (UNI-REQ-0146).</summary>
    public bool IncluiCandidato { get; private set; }

    /// <summary>Os campos de cada ocorrência; a ordem dentro do grupo é a de cada um.</summary>
    public IReadOnlyCollection<FatoColetado> Subitens => _subitens.AsReadOnly();

    private GrupoColetado() { }

    private GrupoColetado(
        string? codigo, int ordem, string? paginaCodigo, string? rotulo, OcorrenciasDoGrupo ocorrencias, ExibicaoEObrigatoriedade regras, IReadOnlyList<FatoColetado> subitens)
    {
        Codigo = codigo ?? string.Empty;
        Ordem = ordem;
        PaginaCodigo = paginaCodigo;
        Rotulo = (rotulo ?? string.Empty).Trim();
        Minimo = ocorrencias.Minimo;
        Maximo = ocorrencias.Maximo;
        IncluiCandidato = ocorrencias.IncluiCandidato;
        Exibicao = regras.Exibicao;
        Obrigatoriedade = regras.Obrigatoriedade;
        AdotarCampos(subitens);
    }

    /// <summary>As ocorrências que o grupo admite, calculadas do estado gravado.</summary>
    public OcorrenciasDoGrupo Ocorrencias => new(Minimo, Maximo, IncluiCandidato);

    /// <summary>A exibição e a obrigatoriedade do grupo, calculadas do estado gravado.</summary>
    public ExibicaoEObrigatoriedade Regras => new(Exibicao, Obrigatoriedade);

    /// <summary>Os fatos que a exibição e a obrigatoriedade do grupo citam, sem repetição.</summary>
    public IReadOnlyCollection<string> FatosCitados => Regras.FatosCitados;

    /// <summary>
    /// Acumula as violações da forma do grupo e, quando ele inclui o candidato, as da identificação
    /// da ocorrência dele (ADR-0125); a citação dos campos e a posição do grupo são do formulário
    /// inteiro, conferidas pelo processo. O grupo adota a finalidade e o formulário dos campos, que têm de ser os
    /// mesmos: o processo vincula o do formulário depois, e o envelope remonta os campos com o lido.
    /// </summary>
    /// <exception cref="ArgumentException">Os campos são de finalidades ou de formulários diferentes.</exception>
    public static Result<GrupoColetado> Criar(
        string codigo,
        int ordem,
        string? paginaCodigo,
        string rotulo,
        OcorrenciasDoGrupo ocorrencias,
        ExibicaoEObrigatoriedade regras,
        IReadOnlyList<FatoColetado> subitens)
    {
        ArgumentNullException.ThrowIfNull(ocorrencias);
        ArgumentNullException.ThrowIfNull(regras);
        ArgumentNullException.ThrowIfNull(subitens);

        List<FieldError> erros = ConferirForma(codigo, ordem, rotulo, ocorrencias, regras, subitens);
        return erros.Count > 0
            ? Result<GrupoColetado>.ValidationFailure(erros)
            : Result<GrupoColetado>.Success(new GrupoColetado(codigo, ordem, paginaCodigo, rotulo, ocorrencias, regras, subitens));
    }

    private static List<FieldError> ConferirForma(
        string codigo, int ordem, string rotulo, OcorrenciasDoGrupo ocorrencias, ExibicaoEObrigatoriedade regras, IReadOnlyList<FatoColetado> subitens) =>
    [
        .. FormaDoGrupo.Conferir(
            codigo, ordem, rotulo, ocorrencias, [.. subitens.Select(static s => ((string?)s.FatoCodigo, s.PaginaCodigo, s.Impedimento is not null))], regras),
        .. ocorrencias.ConferirCandidatoComoMembro([.. subitens.Select(static s => (s.FatoCodigo, !s.SemPrecondicao, s.Obrigatoriedade, s.Restricoes))]),
    ];

    private void AdotarCampos(IReadOnlyList<FatoColetado> subitens)
    {
        Finalidade = FinalidadeComumDos(subitens);
        FormularioId = FormularioComumDos(subitens);
        foreach (FatoColetado subitem in subitens)
        {
            subitem.VincularGrupo(Id);
            _subitens.Add(subitem);
        }
    }

    private static FinalidadeFormulario? FinalidadeComumDos(IReadOnlyList<FatoColetado> campos) =>
        campos.Select(static c => c.Finalidade).Distinct().ToList() is [var comum]
            ? comum
            : throw new ArgumentException("Os campos do grupo são de finalidades diferentes: o grupo tem a finalidade dos seus campos.", nameof(campos));

    private static Guid? FormularioComumDos(IReadOnlyList<FatoColetado> campos) =>
        campos.Select(static c => c.FormularioId).Distinct().ToList() is [var comum]
            ? comum
            : throw new ArgumentException("Os campos do grupo são de formulários diferentes: o grupo tem o formulário dos seus campos.", nameof(campos));

    /// <summary>O grupo no que o grafo do formulário confere.</summary>
    public GrupoDoGrafo ParaGrafo() =>
        new(Codigo, Ordem, PaginaCodigo, FatosCitados, [.. _subitens.OrderBy(static s => s.Ordem).Select(static s => s.ParaGrafo())]);

    /// <summary>O grupo na forma que a estrutura do formulário confere: uma posição na ordem dos itens, numa seção.</summary>
    public ItemEstrutura ParaEstrutura() => new(Codigo, Ordem, PaginaCodigo);

    /// <summary>
    /// As condições das regras do grupo e dos campos dele, para os vínculos e as referências a valor
    /// do processo.
    /// </summary>
    public IEnumerable<CondicaoDnf> Condicoes => Regras.Condicoes.Concat(_subitens.SelectMany(static s => s.Condicoes));

    /// <summary>O grupo e os campos dele pertencem ao processo.</summary>
    void IParteDoProcesso.VincularProcesso(Guid processoSeletivoId)
    {
        ProcessoSeletivoId = processoSeletivoId;
        foreach (FatoColetado subitem in _subitens)
        {
            ((IParteDoProcesso)subitem).VincularProcesso(processoSeletivoId);
        }
    }

    internal void VincularFormulario(FormularioProcesso formulario)
    {
        FormularioId = formulario.Id;
        Finalidade = formulario.Finalidade;
        foreach (FatoColetado subitem in _subitens)
        {
            subitem.VincularFormulario(formulario);
        }
    }
}
