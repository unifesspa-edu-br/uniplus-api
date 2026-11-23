namespace Unifesspa.UniPlus.Selecao.Domain.Entities;

using Unifesspa.UniPlus.Kernel.Domain.Entities;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Regras.ValueObjects;

/// <summary>
/// O formulário do processo para uma finalidade (UNI-REQ-0144): a fase do cronograma em que é
/// respondido, o título, as etapas e o modelo de que partiu. Os itens e os termos ficam no processo,
/// marcados com a finalidade do formulário a que pertencem, porque o produtor único de cada fato e as
/// condições vivas são regras do processo inteiro.
/// </summary>
public sealed class FormularioProcesso : EntityBase, IParteDoProcesso
{
    public const int ModeloOrigemCodigoMaxLength = 60;
    public const int ModeloOrigemNomeMaxLength = 200;

    /// <summary>O código canônico da fase de habilitação no catálogo de fases.</summary>
    public const string CodigoFaseHabilitacao = "HABILITACAO";

    private readonly List<EtapaFormulario> _etapas = [];

    public Guid ProcessoSeletivoId { get; private set; }

    public FinalidadeFormulario Finalidade { get; private set; }

    /// <summary>
    /// A fase do cronograma do processo em que o formulário é respondido. O rascunho pode ainda não
    /// tê-la; a publicação exige a fase que a finalidade pede.
    /// </summary>
    public Guid? FaseId { get; private set; }

    public string? Titulo { get; private set; }

    /// <summary>O modelo de que o formulário partiu, quando partiu de um; a cópia é a que vale.</summary>
    public Guid? ModeloOrigemId { get; private set; }

    public string? ModeloOrigemCodigo { get; private set; }

    public string? ModeloOrigemNome { get; private set; }

    public IReadOnlyCollection<EtapaFormulario> Etapas => _etapas.AsReadOnly();

    private FormularioProcesso() { }

    /// <summary>
    /// Acumula as recusas de forma (ADR-0125): finalidade, título e a estrutura das etapas pelas
    /// regras do formulário compartilhadas com o modelo. A fase é conferida pelo processo, que tem o
    /// cronograma.
    /// </summary>
    /// <param name="modeloOrigemId">O modelo de que o formulário partiu, quando partiu de um.</param>
    /// <param name="modeloOrigemCodigo">O código desse modelo.</param>
    /// <param name="modeloOrigemNome">O nome desse modelo.</param>
    public static Result<FormularioProcesso> Criar(
        FinalidadeFormulario finalidade,
        Guid? faseId,
        string? titulo,
        IReadOnlyList<EtapaFormulario> etapas,
        Guid? modeloOrigemId = null,
        string? modeloOrigemCodigo = null,
        string? modeloOrigemNome = null)
    {
        ArgumentNullException.ThrowIfNull(etapas);
        List<FieldError> erros = ValidarForma(finalidade, titulo, etapas);
        if (erros.Count > 0)
        {
            return Result<FormularioProcesso>.ValidationFailure(erros);
        }

        FormularioProcesso formulario = Novo(finalidade, faseId, titulo, modeloOrigemId, modeloOrigemCodigo, modeloOrigemNome);
        formulario.AdicionarEtapas(etapas);
        return Result<FormularioProcesso>.Success(formulario);
    }

    private static FormularioProcesso Novo(
        FinalidadeFormulario finalidade, Guid? faseId, string? titulo, Guid? modeloOrigemId, string? modeloOrigemCodigo, string? modeloOrigemNome) => new()
        {
            Finalidade = finalidade,
            FaseId = faseId == Guid.Empty ? null : faseId,
            Titulo = string.IsNullOrWhiteSpace(titulo) ? null : titulo.Trim(),
            ModeloOrigemId = modeloOrigemId,
            ModeloOrigemCodigo = modeloOrigemId is null ? null : modeloOrigemCodigo?.Trim(),
            ModeloOrigemNome = modeloOrigemId is null ? null : modeloOrigemNome?.Trim(),
        };

    private static List<FieldError> ValidarForma(FinalidadeFormulario finalidade, string? titulo, IReadOnlyList<EtapaFormulario> etapas)
    {
        List<FieldError> erros = FormaDoCabecalho.Validar(finalidade, titulo);
        if (finalidade != FinalidadeFormulario.Nenhuma)
        {
            erros.AddRange(EstruturaFormulario.ValidarEtapas(finalidade, [.. etapas.Select(static e => e.Estrutura)]));
        }

        return erros;
    }

    private void AdicionarEtapas(IReadOnlyList<EtapaFormulario> etapas)
    {
        foreach (EtapaFormulario etapa in etapas)
        {
            etapa.VincularFormulario(Id);
            _etapas.Add(etapa);
        }
    }

    /// <summary>As etapas na forma que a estrutura do formulário confere.</summary>
    public IReadOnlyList<EtapaEstrutura> Estrutura => [.. _etapas.Select(static e => e.Estrutura)];

    /// <summary>Substitui o conteúdo por outro formulário já validado da mesma finalidade.</summary>
    internal void Substituir(FormularioProcesso outro)
    {
        FaseId = outro.FaseId;
        Titulo = outro.Titulo;
        _etapas.Clear();
        foreach (EtapaFormulario etapa in outro._etapas)
        {
            etapa.VincularFormulario(Id);
            _etapas.Add(etapa);
        }
    }

    /// <summary>Registra o modelo de que o conteúdo atual partiu; a cópia continua sendo a que vale.</summary>
    internal void RegistrarOrigem(Guid modeloId, string modeloCodigo, string modeloNome)
    {
        ModeloOrigemId = modeloId;
        ModeloOrigemCodigo = modeloCodigo.Trim();
        ModeloOrigemNome = modeloNome.Trim();
    }

    void IParteDoProcesso.VincularProcesso(Guid processoSeletivoId) => ProcessoSeletivoId = processoSeletivoId;
}

/// <summary>
/// Uma etapa do formulário: uma seção com os itens do administrador, ou um bloco que o sistema
/// monta (comprovação documental, modalidades calculadas, revisão e aceite).
/// </summary>
public sealed class EtapaFormulario : EntityBase
{
    public Guid FormularioProcessoId { get; private set; }

    public string Codigo { get; private set; } = string.Empty;

    public int Ordem { get; private set; }

    public TipoEtapaFormulario Tipo { get; private set; }

    public BlocoSistema Bloco { get; private set; }

    public string Titulo { get; private set; } = string.Empty;

    public string? Descricao { get; private set; }

    public string? Aviso { get; private set; }

    /// <summary>
    /// A condição em que a seção aparece, sobre fatos conhecidos antes dela (UNI-REQ-0145); nula
    /// quando a seção sempre aparece. Seção oculta leva os seus itens a não aplicável. O bloco de
    /// sistema não tem exibição condicional.
    /// </summary>
    public PredicadoDnf? Exibicao { get; private set; }

    private EtapaFormulario() { }

    public EtapaEstrutura Estrutura => new(Codigo, Ordem, Tipo, Bloco);

    /// <summary>Os fatos que a exibição cita.</summary>
    public IReadOnlyCollection<string> FatosCitados => Exibicao?.FatosCitados ?? [];

    /// <summary>A etapa no que o grafo do formulário confere.</summary>
    public EtapaDoGrafo ParaGrafo() => new(Codigo, Ordem, FatosCitados);

    /// <summary>As condições da exibição, para os vínculos e as referências a valor do processo.</summary>
    public IEnumerable<CondicaoDnf> Condicoes => (Exibicao?.Clausulas ?? []).SelectMany(static c => c.Condicoes);

    public static Result<EtapaFormulario> Criar(
        string codigo, int ordem, TipoEtapaFormulario tipo, BlocoSistema bloco, string titulo, string? descricao, string? aviso,
        PredicadoDnf? exibicao = null)
    {
        List<FieldError> erros = FormaDaEtapa.Conferir(codigo, ordem, tipo, titulo, descricao, aviso, exibicao is not null);
        if (erros.Count > 0)
        {
            return Result<EtapaFormulario>.ValidationFailure(erros);
        }

        return Result<EtapaFormulario>.Success(new EtapaFormulario
        {
            Codigo = (codigo ?? string.Empty).Trim().Normalize(System.Text.NormalizationForm.FormC),
            Ordem = ordem,
            Tipo = tipo,
            Bloco = bloco,
            Titulo = (titulo ?? string.Empty).Trim(),
            Descricao = FormaDoItem.TextoOpcional(descricao),
            Aviso = FormaDoItem.TextoOpcional(aviso),
            Exibicao = exibicao,
        });
    }

    internal void VincularFormulario(Guid formularioProcessoId) => FormularioProcessoId = formularioProcessoId;
}

public static class FormularioProcessoErrorCodes
{
    /// <summary>Formulário publicado sem a fase em que é respondido.</summary>
    public const string SemFase = "ProcessoSeletivo.FormularioSemFase";

    /// <summary>Item de formulário publicado fora de uma seção.</summary>
    public const string ItemForaDeSecao = "ProcessoSeletivo.ItemDoFormularioForaDeSecao";

    /// <summary>A fase declarada não está no cronograma do processo.</summary>
    public const string FaseForaDoCronograma = "FormularioProcesso.FaseForaDoCronograma";

    /// <summary>A fase declarada não é a que a finalidade exige.</summary>
    public const string FaseIncoerenteComFinalidade = "FormularioProcesso.FaseIncoerenteComFinalidade";

    /// <summary>Formulário de isenção num processo que não cobra taxa.</summary>
    public const string IsencaoSemTaxa = "FormularioProcesso.IsencaoSemTaxa";

    /// <summary>Item ou termo para finalidade sem formulário.</summary>
    public const string FormularioInexistente = "FormularioProcesso.FormularioInexistente";

    /// <summary>Processo com inscrição própria publicado sem formulário de inscrição.</summary>
    public const string InscricaoSemFormulario = "ProcessoSeletivo.InscricaoSemFormulario";

    /// <summary>Formulário de inscrição publicado sem algum dado do conjunto básico do candidato.</summary>
    public const string InscricaoSemConjuntoBasico = "ProcessoSeletivo.InscricaoSemConjuntoBasico";

    /// <summary>Classificação baseada em ENEM com inscrição própria, e o formulário de inscrição não pede a edição do ENEM a todo candidato.</summary>
    public const string InscricaoSemEdicaoDoEnem = "ProcessoSeletivo.InscricaoSemEdicaoDoEnem";

    /// <summary>Formulário que pede a edição do ENEM num processo cuja classificação não é baseada em ENEM.</summary>
    public const string EdicaoDoEnemSemClassificacaoEnem = "ProcessoSeletivo.EdicaoDoEnemSemClassificacaoEnem";

    /// <summary>O processo com inscrição pelo sistema não tem formulário obrigatório na fase que coleta inscrição.</summary>
    public const string InscricaoSemFormularioObrigatorio = "FormularioProcesso.InscricaoSemFormularioObrigatorio";

    /// <summary>O processo de resultado importado tem formulário na fase que coleta inscrição.</summary>
    public const string InscricaoEmResultadoImportado = "FormularioProcesso.InscricaoEmResultadoImportado";

    /// <summary>Formulário obrigatório numa fase da janela de isenção que não coleta inscrição.</summary>
    public const string FormularioDaIsencaoObrigatorio = "FormularioProcesso.FormularioDaIsencaoObrigatorio";

    /// <summary>O processo que cobra taxa não tem formulário facultativo na fase da janela de isenção.</summary>
    public const string TaxaSemFormularioDeIsencao = "FormularioProcesso.TaxaSemFormularioDeIsencao";

    /// <summary>A primeira fase depois da importação não tem o formulário obrigatório dos dados básicos que faltam.</summary>
    public const string PosImportacaoSemFormularioObrigatorio = "FormularioProcesso.PosImportacaoSemFormularioObrigatorio";

    /// <summary>A fase do cronograma é usada por um formulário e não pode sair.</summary>
    public const string FaseReferenciadaPorFormulario = "ProcessoSeletivo.FaseReferenciadaPorFormulario";
}
