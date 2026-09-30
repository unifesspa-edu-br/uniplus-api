namespace Unifesspa.UniPlus.Selecao.Domain.Entities;

using Unifesspa.UniPlus.Kernel.Domain.Entities;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Formularios;

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
    public const int CodigoMaxLength = 60;
    public const int TituloMaxLength = 300;
    public const int TextoMaxLength = 2000;

    public Guid FormularioProcessoId { get; private set; }

    public string Codigo { get; private set; } = string.Empty;

    public int Ordem { get; private set; }

    public TipoEtapaFormulario Tipo { get; private set; }

    public BlocoSistema Bloco { get; private set; }

    public string Titulo { get; private set; } = string.Empty;

    public string? Descricao { get; private set; }

    public string? Aviso { get; private set; }

    private EtapaFormulario() { }

    public EtapaEstrutura Estrutura => new(Codigo, Ordem, Tipo, Bloco);

    public static Result<EtapaFormulario> Criar(
        string codigo, int ordem, TipoEtapaFormulario tipo, BlocoSistema bloco, string titulo, string? descricao, string? aviso)
    {
        List<FieldError> erros = [];
        void Recusar(string campo, string codigoErro, string mensagem) => erros.Add(new(campo, new DomainError(codigoErro, mensagem)));

        string codigoNormalizado = codigo?.Trim() ?? string.Empty;
        if (codigoNormalizado.Length is 0 or > CodigoMaxLength)
        {
            Recusar("codigo", FormularioProcessoErrorCodes.EtapaCodigoInvalido,
                $"O código da etapa é obrigatório e tem no máximo {CodigoMaxLength} caracteres.");
        }

        if (ordem < 0)
        {
            Recusar("ordem", FormularioProcessoErrorCodes.EtapaOrdemInvalida, "A ordem da etapa não pode ser negativa.");
        }

        string tituloNormalizado = titulo?.Trim() ?? string.Empty;
        if (tituloNormalizado.Length is 0 or > TituloMaxLength)
        {
            Recusar("titulo", FormularioProcessoErrorCodes.EtapaTituloInvalido,
                $"O título da etapa é obrigatório e tem no máximo {TituloMaxLength} caracteres.");
        }

        string? descricaoNormalizada = string.IsNullOrWhiteSpace(descricao) ? null : descricao.Trim();
        string? avisoNormalizado = string.IsNullOrWhiteSpace(aviso) ? null : aviso.Trim();
        if (descricaoNormalizada is { Length: > TextoMaxLength })
        {
            Recusar("descricao", FormularioProcessoErrorCodes.EtapaTextoTamanho,
                $"A descrição da etapa tem no máximo {TextoMaxLength} caracteres.");
        }

        if (avisoNormalizado is { Length: > TextoMaxLength })
        {
            Recusar("aviso", FormularioProcessoErrorCodes.EtapaTextoTamanho,
                $"O aviso da etapa tem no máximo {TextoMaxLength} caracteres.");
        }

        if (erros.Count > 0)
        {
            return Result<EtapaFormulario>.ValidationFailure(erros);
        }

        return Result<EtapaFormulario>.Success(new EtapaFormulario
        {
            Codigo = codigoNormalizado.Normalize(System.Text.NormalizationForm.FormC),
            Ordem = ordem,
            Tipo = tipo,
            Bloco = bloco,
            Titulo = tituloNormalizado,
            Descricao = descricaoNormalizada,
            Aviso = avisoNormalizado,
        });
    }

    internal void VincularFormulario(Guid formularioProcessoId) => FormularioProcessoId = formularioProcessoId;
}

public static class FormularioProcessoErrorCodes
{
    public const string FinalidadeInvalida = "FormularioProcesso.FinalidadeInvalida";
    public const string TituloTamanho = "FormularioProcesso.TituloTamanho";
    public const string EtapaCodigoInvalido = "FormularioProcesso.EtapaCodigoInvalido";
    public const string EtapaOrdemInvalida = "FormularioProcesso.EtapaOrdemInvalida";
    public const string EtapaTituloInvalido = "FormularioProcesso.EtapaTituloInvalido";
    public const string EtapaTextoTamanho = "FormularioProcesso.EtapaTextoTamanho";

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
