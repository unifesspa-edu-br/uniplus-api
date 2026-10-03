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
public sealed class GrupoColetado : EntityBase
{
    private readonly List<FatoColetado> _subitens = [];

    public Guid ProcessoSeletivoId { get; private set; }

    /// <summary>A finalidade do formulário do grupo; atribuída pelo processo ao definir os itens.</summary>
    public FinalidadeFormulario Finalidade { get; private set; }

    /// <summary>Código do grupo no formulário, fora do espaço de códigos dos fatos.</summary>
    public string Codigo { get; private set; } = string.Empty;

    /// <summary>A posição do grupo na ordem de coleta, no mesmo espaço da ordem dos itens.</summary>
    public int Ordem { get; private set; }

    /// <summary>A seção do formulário em que o grupo aparece.</summary>
    public string? EtapaCodigo { get; private set; }

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

    /// <summary>
    /// Acumula as violações da forma do grupo e, quando ele inclui o candidato, as da identificação
    /// da ocorrência dele (ADR-0125); a citação dos campos e a posição do grupo são do formulário
    /// inteiro, conferidas pelo processo.
    /// </summary>
    public static Result<GrupoColetado> Criar(
        string codigo,
        int ordem,
        string? etapaCodigo,
        string rotulo,
        int minimo,
        int? maximo,
        PredicadoDnf? exibicao,
        Obrigatoriedade obrigatoriedade,
        IReadOnlyList<FatoColetado> subitens,
        FinalidadeFormulario finalidade = FinalidadeFormulario.Nenhuma,
        bool incluiCandidato = false)
    {
        ArgumentNullException.ThrowIfNull(obrigatoriedade);
        ArgumentNullException.ThrowIfNull(subitens);

        List<FieldError> erros = FormaDoGrupo.Conferir(
            codigo, ordem, rotulo, minimo, maximo, [.. subitens.Select(static s => ((string?)s.FatoCodigo, s.EtapaCodigo))], exibicao?.FatosCitados ?? [], obrigatoriedade);
        if (incluiCandidato)
        {
            erros.AddRange(CandidatoComoMembro.Conferir(
                minimo, [.. subitens.Select(static s => (s.FatoCodigo, !s.SemPrecondicao, s.Obrigatoriedade, s.Restricoes))]));
        }

        if (erros.Count > 0)
        {
            return Result<GrupoColetado>.ValidationFailure(erros);
        }

        GrupoColetado grupo = new()
        {
            Codigo = (codigo ?? string.Empty).Trim().Normalize(System.Text.NormalizationForm.FormC),
            Ordem = ordem,
            EtapaCodigo = string.IsNullOrWhiteSpace(etapaCodigo) ? null : etapaCodigo.Trim().Normalize(System.Text.NormalizationForm.FormC),
            Rotulo = (rotulo ?? string.Empty).Trim(),
            Minimo = minimo,
            Maximo = maximo,
            Exibicao = exibicao,
            Obrigatoriedade = obrigatoriedade,
            IncluiCandidato = incluiCandidato,
        };
        foreach (FatoColetado subitem in subitens)
        {
            subitem.VincularGrupo(grupo.Id);
            grupo._subitens.Add(subitem);
        }

        // O processo atribui a finalidade ao definir os itens; quem remonta o envelope a informa.
        grupo.VincularFinalidade(finalidade);
        return Result<GrupoColetado>.Success(grupo);
    }

    /// <summary>Os fatos que a exibição e a obrigatoriedade do grupo citam, sem repetição.</summary>
    public IReadOnlyCollection<string> FatosCitados =>
        [.. (Exibicao?.FatosCitados ?? []).Concat(Obrigatoriedade.FatosCitados).Distinct(StringComparer.Ordinal)];

    /// <summary>O grupo no que o grafo do formulário confere.</summary>
    public GrupoDoGrafo ParaGrafo() =>
        new(Codigo, Ordem, EtapaCodigo, FatosCitados, [.. _subitens.OrderBy(static s => s.Ordem).Select(static s => s.ParaGrafo())]);

    /// <summary>O grupo na forma que a estrutura do formulário confere: uma posição na ordem dos itens, numa seção.</summary>
    public ItemEstrutura ParaEstrutura() => new(Codigo, Ordem, EtapaCodigo);

    /// <summary>
    /// As condições das regras do grupo e dos campos dele, para os vínculos e as referências a valor
    /// do processo.
    /// </summary>
    public IEnumerable<CondicaoDnf> Condicoes =>
        (Exibicao?.Clausulas ?? []).Concat(Obrigatoriedade.Predicado?.Clausulas ?? []).SelectMany(static c => c.Condicoes)
            .Concat(_subitens.SelectMany(static s => s.Condicoes));

    /// <summary>O grupo e os campos dele pertencem ao processo.</summary>
    internal void VincularProcessoSeletivo(Guid processoSeletivoId)
    {
        ProcessoSeletivoId = processoSeletivoId;
        foreach (FatoColetado subitem in _subitens)
        {
            subitem.VincularProcessoSeletivo(processoSeletivoId);
        }
    }

    internal void VincularFinalidade(FinalidadeFormulario finalidade)
    {
        Finalidade = finalidade;
        foreach (FatoColetado subitem in _subitens)
        {
            subitem.VincularFinalidade(finalidade);
        }
    }
}
