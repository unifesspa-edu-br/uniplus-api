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
public sealed class FormularioProcesso : EntityBase
{
    public const int TituloMaxLength = 300;
    public const int ModeloOrigemCodigoMaxLength = 60;

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

    public IReadOnlyCollection<EtapaFormulario> Etapas => _etapas.AsReadOnly();

    private FormularioProcesso() { }

    /// <summary>A finalidade e o título do formulário, conferidos sem as etapas.</summary>
    public static List<FieldError> ValidarCabecalho(FinalidadeFormulario finalidade, string? titulo)
    {
        List<FieldError> erros = [];
        if (finalidade == FinalidadeFormulario.Nenhuma || !Enum.IsDefined(finalidade))
        {
            erros.Add(new("finalidade", new DomainError(FormularioProcessoErrorCodes.FinalidadeInvalida,
                "A finalidade do formulário é inscrição, isenção de taxa ou habilitação.")));
        }

        if (!string.IsNullOrWhiteSpace(titulo) && titulo.Trim().Length > TituloMaxLength)
        {
            erros.Add(new("titulo", new DomainError(FormularioProcessoErrorCodes.TituloTamanho,
                $"Título do formulário deve ter no máximo {TituloMaxLength} caracteres.")));
        }

        return erros;
    }

    /// <summary>
    /// Acumula as recusas de forma (ADR-0125): finalidade, título e a estrutura das etapas pelas
    /// regras do formulário compartilhadas com o modelo. A fase é conferida pelo processo, que tem o
    /// cronograma.
    /// </summary>
    /// <param name="modeloOrigemId">O modelo de que o formulário partiu, quando partiu de um.</param>
    /// <param name="modeloOrigemCodigo">O código desse modelo.</param>
    public static Result<FormularioProcesso> Criar(
        FinalidadeFormulario finalidade,
        Guid? faseId,
        string? titulo,
        IReadOnlyList<EtapaFormulario> etapas,
        Guid? modeloOrigemId = null,
        string? modeloOrigemCodigo = null)
    {
        ArgumentNullException.ThrowIfNull(etapas);

        List<FieldError> erros = ValidarCabecalho(finalidade, titulo);
        if (finalidade != FinalidadeFormulario.Nenhuma)
        {
            erros.AddRange(EstruturaFormulario.ValidarEtapas(finalidade, [.. etapas.Select(static e => e.Estrutura)]));
        }

        if (erros.Count > 0)
        {
            return Result<FormularioProcesso>.ValidationFailure(erros);
        }

        string? tituloNormalizado = string.IsNullOrWhiteSpace(titulo) ? null : titulo.Trim();
        FormularioProcesso formulario = new()
        {
            Finalidade = finalidade,
            FaseId = faseId == Guid.Empty ? null : faseId,
            Titulo = tituloNormalizado,
            ModeloOrigemId = modeloOrigemId,
            ModeloOrigemCodigo = modeloOrigemId is null ? null : modeloOrigemCodigo?.Trim(),
        };
        foreach (EtapaFormulario etapa in etapas)
        {
            etapa.VincularFormulario(formulario.Id);
            formulario._etapas.Add(etapa);
        }

        return Result<FormularioProcesso>.Success(formulario);
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

    /// <summary>Repõe o conteúdo congelado, inclusive o modelo de que o formulário partiu.</summary>
    internal void Repor(FormularioProcesso congelado)
    {
        Substituir(congelado);
        ModeloOrigemId = congelado.ModeloOrigemId;
        ModeloOrigemCodigo = congelado.ModeloOrigemCodigo;
    }

    internal void RemapearFase(Guid faseId) => FaseId = faseId;

    internal void VincularProcessoSeletivo(Guid processoSeletivoId) => ProcessoSeletivoId = processoSeletivoId;
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
    public const string FinalidadeInvalida = "FormularioProcesso.FinalidadeInvalida";
    public const string TituloTamanho = "FormularioProcesso.TituloTamanho";

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

    /// <summary>Remover formulário fora do rascunho: a retificação só acrescenta.</summary>
    public const string RemocaoSoEmRascunho = "FormularioProcesso.RemocaoSoEmRascunho";

    /// <summary>Processo com inscrição própria publicado sem formulário de inscrição.</summary>
    public const string InscricaoSemFormulario = "ProcessoSeletivo.InscricaoSemFormulario";

    /// <summary>A fase do cronograma é usada por um formulário e não pode sair.</summary>
    public const string FaseReferenciadaPorFormulario = "ProcessoSeletivo.FaseReferenciadaPorFormulario";
}
