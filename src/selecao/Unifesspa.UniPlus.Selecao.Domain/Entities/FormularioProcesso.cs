namespace Unifesspa.UniPlus.Selecao.Domain.Entities;

using Unifesspa.UniPlus.Kernel.Domain.Entities;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Regras.ValueObjects;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Cronograma;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Formularios;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// O formulário do processo, identificado pelo código dentro dele (UNI-REQ-0144): a fase do cronograma
/// em que é respondido, se é obrigatório, o título, as páginas e o modelo de que partiu. Os itens e os
/// termos ficam no processo, ligados ao formulário a que pertencem, porque o produtor único de cada fato
/// e as condições vivas são regras do processo inteiro.
/// </summary>
public sealed class FormularioProcesso : EntityBase, IParteDoProcesso
{
    private readonly List<PaginaDoFormulario> _paginas = [];

    public Guid ProcessoSeletivoId { get; private set; }

    /// <summary>O código que identifica o formulário dentro do processo; não muda depois de criado.</summary>
    public CodigoDoFormulario Codigo { get; private set; } = null!;

    /// <summary>Se o formulário é obrigatório para o candidato.</summary>
    public bool Obrigatorio { get; private set; }

    /// <summary>
    /// A fase do cronograma do processo em que o formulário é respondido. O rascunho pode ainda não
    /// tê-la; a publicação exige a fase que o formulário responde.
    /// </summary>
    public Guid? FaseId { get; private set; }

    public string? Titulo { get; private set; }

    /// <summary>O modelo de que o formulário partiu, quando partiu de um; a cópia é a que vale.</summary>
    public ModeloDeOrigem? ModeloDeOrigem { get; private set; }

    /// <summary>
    /// O que acontece com quem não envia o formulário. Ausente, a falta de envio não tem consequência própria e
    /// nenhuma é presumida.
    /// </summary>
    public ConsequenciaDoDescumprimento? ConsequenciaDaFaltaDeEnvio { get; private set; }

    public IReadOnlyCollection<PaginaDoFormulario> Paginas => _paginas.AsReadOnly();

    private FormularioProcesso() { }

    /// <summary>
    /// Acumula as recusas de forma (ADR-0125): o título e a estrutura das páginas, conferida pelo que o formulário
    /// declara e pelo cronograma que diz se a fase dele coleta inscrição.
    /// </summary>
    /// <param name="declarado">O código, a fase e a obrigatoriedade do formulário.</param>
    /// <param name="cronograma">As fases do processo, para conferir se a fase do formulário coleta inscrição.</param>
    /// <param name="modeloDeOrigem">O modelo de que o formulário partiu, quando partiu de um.</param>
    public static Result<FormularioProcesso> Criar(
        FormularioDeclarado declarado,
        CronogramaDoProcesso cronograma,
        string? titulo,
        IReadOnlyList<PaginaDoFormulario> paginas,
        ModeloDeOrigem? modeloDeOrigem = null)
    {
        ArgumentNullException.ThrowIfNull(declarado);
        ArgumentNullException.ThrowIfNull(paginas);
        List<FieldError> erros = ValidarForma(declarado, cronograma, titulo, paginas);
        if (erros.Count > 0)
        {
            return Result<FormularioProcesso>.ValidationFailure(erros);
        }

        FormularioProcesso formulario = Novo(declarado, titulo, modeloDeOrigem);
        formulario.AdicionarPaginas(paginas);
        return Result<FormularioProcesso>.Success(formulario);
    }

    private static FormularioProcesso Novo(FormularioDeclarado declarado, string? titulo, ModeloDeOrigem? modeloDeOrigem) => new()
    {
        Codigo = declarado.Codigo,
        Obrigatorio = declarado.Obrigatorio,
        FaseId = declarado.FaseId == Guid.Empty ? null : declarado.FaseId,
        Titulo = string.IsNullOrWhiteSpace(titulo) ? null : titulo.Trim(),
        ModeloDeOrigem = modeloDeOrigem,
    };

    private static List<FieldError> ValidarForma(
        FormularioDeclarado declarado, CronogramaDoProcesso cronograma, string? titulo, IReadOnlyList<PaginaDoFormulario> paginas) =>
    [
        .. FormaDoCabecalho.ValidarTitulo(titulo),
        .. EstruturaFormulario.ValidarPaginas(declarado.Estrutura(cronograma), [.. paginas.Select(static e => e.Estrutura)]),
    ];

    private void AdicionarPaginas(IReadOnlyList<PaginaDoFormulario> paginas)
    {
        foreach (PaginaDoFormulario pagina in paginas)
        {
            pagina.VincularFormulario(Id);
            _paginas.Add(pagina);
        }
    }

    /// <summary>Indica se o item é solicitado a todo candidato que responde o formulário.</summary>
    internal bool PedeATodoCandidato(FatoColetado item) =>
        item.FormularioId == Id && item.GrupoColetadoId is null && item.SemPrecondicao && item.Obrigatoriedade.Tipo == TipoObrigatoriedade.Sempre
        && !_paginas.Any(e => string.Equals(e.Codigo, item.PaginaCodigo, StringComparison.Ordinal) && e.Exibicao is not null);

    /// <summary>O formulário no que as regras de alcance e de código conferem.</summary>
    public FormularioDeclarado Declarado => new(Codigo, FaseId, Obrigatorio);

    /// <summary>As páginas na forma que a estrutura do formulário confere.</summary>
    public IReadOnlyList<PaginaEstrutura> Estrutura => [.. _paginas.Select(static e => e.Estrutura)];

    /// <summary>
    /// Declara, ou retira com <see langword="null"/>, a consequência da falta de envio. A conferência de que a
    /// declaração cabe a este formulário é da configuração do processo, que conhece a fase e o cronograma.
    /// </summary>
    public void DeclararConsequenciaDaFaltaDeEnvio(ConsequenciaDoDescumprimento? consequencia) =>
        ConsequenciaDaFaltaDeEnvio = consequencia;

    /// <summary>Substitui o conteúdo por outro formulário já validado do mesmo código.</summary>
    internal void Substituir(FormularioProcesso outro)
    {
        Obrigatorio = outro.Obrigatorio;
        FaseId = outro.FaseId;
        Titulo = outro.Titulo;
        ConsequenciaDaFaltaDeEnvio = outro.ConsequenciaDaFaltaDeEnvio;
        _paginas.Clear();
        AdicionarPaginas(outro._paginas);
    }

    /// <summary>Registra o modelo de que o conteúdo atual partiu; a cópia continua sendo a que vale.</summary>
    internal void RegistrarOrigem(ModeloDeOrigem modeloDeOrigem) => ModeloDeOrigem = modeloDeOrigem;

    void IParteDoProcesso.VincularProcesso(Guid processoSeletivoId) => ProcessoSeletivoId = processoSeletivoId;
}

/// <summary>
/// Uma página do formulário: uma seção com os itens do administrador, ou um bloco que o sistema
/// monta (comprovação documental, modalidades calculadas, revisão e aceite).
/// </summary>
public sealed class PaginaDoFormulario : EntityBase
{
    public Guid FormularioProcessoId { get; private set; }

    public string Codigo { get; private set; } = string.Empty;

    public int Ordem { get; private set; }

    public TipoPaginaFormulario Tipo { get; private set; }

    /// <summary>O bloco que o sistema monta; nulo na seção.</summary>
    public BlocoSistema? Bloco { get; private set; }

    public string Titulo { get; private set; } = string.Empty;

    public string? Descricao { get; private set; }

    public string? Aviso { get; private set; }

    /// <summary>
    /// A condição em que a seção aparece, sobre fatos conhecidos antes dela (UNI-REQ-0145); nula
    /// quando a seção sempre aparece. Seção oculta leva os seus itens a não aplicável. O bloco de
    /// sistema não tem exibição condicional.
    /// </summary>
    public PredicadoDnf? Exibicao { get; private set; }

    private PaginaDoFormulario() { }

    public PaginaEstrutura Estrutura => new(Codigo, Ordem, Tipo, Bloco);

    /// <summary>Os fatos que a exibição cita.</summary>
    public IReadOnlyCollection<string> FatosCitados => Exibicao?.FatosCitados ?? [];

    /// <summary>A página no que o grafo do formulário confere.</summary>
    public PaginaDoGrafo ParaGrafo() => new(Codigo, Ordem, FatosCitados);

    /// <summary>As condições da exibição, para os vínculos e as referências a valor do processo.</summary>
    public IEnumerable<CondicaoDnf> Condicoes => (Exibicao?.Clausulas ?? []).SelectMany(static c => c.Condicoes);

    public static Result<PaginaDoFormulario> Criar(
        PaginaEstrutura estrutura, string titulo, string? descricao, string? aviso, PredicadoDnf? exibicao = null)
    {
        ArgumentNullException.ThrowIfNull(estrutura);

        List<FieldError> erros = FormaDaPagina.Conferir(
            estrutura.Codigo, estrutura.Ordem, estrutura.Tipo, titulo, descricao, aviso, exibicao is not null);
        return erros.Count > 0
            ? Result<PaginaDoFormulario>.ValidationFailure(erros)
            : Result<PaginaDoFormulario>.Success(Montar(estrutura, titulo, descricao, aviso, exibicao));
    }

    private static PaginaDoFormulario Montar(
        PaginaEstrutura estrutura, string titulo, string? descricao, string? aviso, PredicadoDnf? exibicao) => new()
        {
            Codigo = estrutura.Codigo ?? string.Empty,
            Ordem = estrutura.Ordem,
            Tipo = estrutura.Tipo,
            Bloco = estrutura.Bloco,
            Titulo = (titulo ?? string.Empty).Trim(),
            Descricao = FormaDoItem.TextoOpcional(descricao),
            Aviso = FormaDoItem.TextoOpcional(aviso),
            Exibicao = exibicao,
        };

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

    /// <summary>Formulário de isenção num processo que não cobra taxa.</summary>
    public const string IsencaoSemTaxa = "FormularioProcesso.IsencaoSemTaxa";

    /// <summary>A aplicação de modelo cita um código de formulário que o processo não tem.</summary>
    public const string FormularioInexistente = "FormularioProcesso.FormularioInexistente";

    /// <summary>A remoção, os itens ou os termos citam, no caminho, um código de formulário que o processo não tem.</summary>
    public const string NaoEncontrado = "FormularioProcesso.NaoEncontrado";

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

    /// <summary>A fase organizada em chamadas não tem formulário obrigatório.</summary>
    public const string ChamadasSemFormularioObrigatorio = "FormularioProcesso.ChamadasSemFormularioObrigatorio";

    /// <summary>A primeira fase depois da importação não tem o formulário obrigatório dos dados básicos que faltam.</summary>
    public const string PosImportacaoSemFormularioObrigatorio = "FormularioProcesso.PosImportacaoSemFormularioObrigatorio";

    /// <summary>A fase do cronograma é usada por um formulário e não pode sair.</summary>
    public const string FaseReferenciadaPorFormulario = "ProcessoSeletivo.FaseReferenciadaPorFormulario";

    /// <summary>A consequência da falta de envio informada não é uma opção do vocabulário.</summary>
    public const string ConsequenciaDaFaltaDeEnvioInvalida = "FormularioProcesso.ConsequenciaDaFaltaDeEnvioInvalida";

    /// <summary>O formulário facultativo declara consequência para a falta de envio.</summary>
    public const string ConsequenciaEmFormularioFacultativo = "FormularioProcesso.ConsequenciaEmFormularioFacultativo";

    /// <summary>O formulário que coleta a inscrição declara consequência para a falta de envio.</summary>
    public const string ConsequenciaEmFormularioDaInscricao = "FormularioProcesso.ConsequenciaEmFormularioDaInscricao";
}
