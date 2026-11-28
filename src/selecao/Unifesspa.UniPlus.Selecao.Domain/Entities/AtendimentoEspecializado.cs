namespace Unifesspa.UniPlus.Selecao.Domain.Entities;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Kernel.Domain.Entities;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// Contêiner (um por processo) do atendimento especializado do
/// <see cref="ProcessoSeletivo"/>: as condições, recursos de acessibilidade e
/// tipos de deficiência que o certame disponibiliza aos candidatos, todos
/// congelados por snapshot-copy (ADR-0061) dos cadastros do módulo
/// Configuração.
/// </summary>
/// <remarks>
/// <see cref="EntityBase"/> puro (sem soft-delete): ver justificativa em
/// <see cref="EtapaProcesso"/>.
/// </remarks>
public sealed class AtendimentoEspecializado : EntityBase, IParteDoProcesso
{
    /// <summary>
    /// Código canônico da condição PcD no cadastro de condições de atendimento
    /// (linha protegida do catálogo). Âncora da invariante ADR-0067.
    /// </summary>
    public const string CodigoCondicaoPcd = "PCD";

    /// <summary>Dado do candidato cujas opções são as condições de atendimento ofertadas.</summary>
    public const string FatoCondicaoAtendimento = "CONDICAO_ATENDIMENTO";

    /// <summary>Dado do candidato cujas opções são os recursos de acessibilidade ofertados.</summary>
    public const string FatoRecursoAcessibilidade = "RECURSO_ACESSIBILIDADE";

    /// <summary>Dado do candidato cujas opções são os tipos de deficiência ofertados.</summary>
    public const string FatoTipoDeficiencia = "TIPO_DEFICIENCIA";

    /// <summary>
    /// Indica que as opções do dado vêm sempre do atendimento especializado, qualquer que seja a origem
    /// copiada para o campo: o atendimento é, por natureza, o dono da condição e do recurso.
    /// </summary>
    public static bool ImpoeOrigemDoFato(string codigoDoDado) =>
        codigoDoDado is FatoCondicaoAtendimento or FatoRecursoAcessibilidade;

    /// <summary>
    /// Indica que, quando o campo tem as opções do processo, quem responde por elas é o atendimento
    /// especializado, e não as opções declaradas. Inclui o tipo de deficiência: o campo novo dele tem os
    /// tipos do cadastro (UNI-REQ-0012), mas o processo publicado antes disso congelou a origem do
    /// processo, e as opções dele continuam sendo as do atendimento.
    /// </summary>
    public static bool GereOpcoesDoFato(string codigoDoDado) =>
        ImpoeOrigemDoFato(codigoDoDado) || codigoDoDado is FatoTipoDeficiencia;

    private static readonly DomainError CondicaoDuplicada = new(
        "AtendimentoEspecializado.CondicaoDuplicada", "Cada condição de atendimento só pode ser ofertada uma vez.");
    private static readonly DomainError RecursoDuplicado = new(
        "AtendimentoEspecializado.RecursoDuplicado", "Cada recurso de acessibilidade só pode ser ofertado uma vez.");
    private static readonly DomainError TipoDeficienciaDuplicado = new(
        "AtendimentoEspecializado.TipoDeficienciaDuplicado", "Cada tipo de deficiência só pode ser ofertado uma vez.");
    private static readonly DomainError TipoDeficienciaSemCondicaoPcd = new(
        "AtendimentoEspecializado.TipoDeficienciaSemCondicaoPcd",
        "Tipos de deficiência só podem ser ofertados quando a condição PcD está ofertada.");

    /// <summary>Recusa de uma condição de atendimento que o cadastro não tem ou que foi removida.</summary>
    public static DomainError CondicaoNaoEncontrada() => new(
        "AtendimentoEspecializado.CondicaoNaoEncontrada", "A condição de atendimento deve ser uma do cadastro e não pode ter sido removida.");

    /// <summary>Recusa de um recurso de acessibilidade que o cadastro não tem ou que foi removido.</summary>
    public static DomainError RecursoNaoEncontrado() => new(
        "AtendimentoEspecializado.RecursoNaoEncontrado", "O recurso de acessibilidade deve ser um do cadastro e não pode ter sido removido.");

    /// <summary>Recusa de um tipo de deficiência que o cadastro não tem ou que foi removido.</summary>
    public static DomainError TipoDeficienciaNaoEncontrado() => new(
        "AtendimentoEspecializado.TipoDeficienciaNaoEncontrado", "O tipo de deficiência deve ser um do cadastro e não pode ter sido removido.");

    public Guid ProcessoSeletivoId { get; private set; }

    private readonly List<CondicaoOfertada> _condicoes = [];
    public IReadOnlyCollection<CondicaoOfertada> Condicoes => _condicoes.AsReadOnly();

    private readonly List<RecursoOfertado> _recursos = [];
    public IReadOnlyCollection<RecursoOfertado> Recursos => _recursos.AsReadOnly();

    private readonly List<DeficienciaAtendida> _tiposDeficiencia = [];
    public IReadOnlyCollection<DeficienciaAtendida> TiposDeficiencia => _tiposDeficiencia.AsReadOnly();

    private AtendimentoEspecializado() { }

    private AtendimentoEspecializado(
        IReadOnlyList<CondicaoOfertada> condicoes,
        IReadOnlyList<RecursoOfertado> recursos,
        IReadOnlyList<DeficienciaAtendida> tiposDeficiencia)
    {
        OfertarCondicoes(condicoes);
        OfertarRecursos(recursos);
        AtenderDeficiencias(tiposDeficiencia);
    }

    /// <summary>
    /// As opções que o atendimento dá ao dado, ordenadas pelo código e numeradas a partir de zero: o
    /// atendimento não tem ordem de negócio própria, e remover um item renumera os demais preservando
    /// a ordem relativa. Vazio para dado que o atendimento não gere.
    /// </summary>
    public IReadOnlyList<OpcaoDoProcesso> OpcoesDoFato(string codigoDoDado) =>
        codigoDoDado switch
        {
            FatoCondicaoAtendimento => [.. _condicoes
                .OrderBy(static c => c.CondicaoCodigo, StringComparer.Ordinal)
                .Select(static (c, indice) => new OpcaoDoProcesso(c.CondicaoCodigo, c.CondicaoNome, indice))],
            FatoRecursoAcessibilidade => [.. _recursos
                .OrderBy(static r => r.RecursoCodigo, StringComparer.Ordinal)
                .Select(static (r, indice) => new OpcaoDoProcesso(r.RecursoCodigo, r.RecursoNome, indice))],
            FatoTipoDeficiencia => [.. _tiposDeficiencia
                .OrderBy(static t => t.TipoDeficienciaCodigo, StringComparer.Ordinal)
                .Select(static (t, indice) => new OpcaoDoProcesso(t.TipoDeficienciaCodigo, t.TipoDeficienciaNome, indice))],
            _ => [],
        };

    /// <summary>
    /// Valida as ofertas e acumula as recusas de duplicidade e de tipos de deficiência sem condição PcD.
    /// </summary>
    public static Result<AtendimentoEspecializado> Criar(
        IReadOnlyList<CondicaoOfertada> condicoes,
        IReadOnlyList<RecursoOfertado> recursos,
        IReadOnlyList<DeficienciaAtendida> tiposDeficiencia)
    {
        ArgumentNullException.ThrowIfNull(condicoes);
        ArgumentNullException.ThrowIfNull(recursos);
        ArgumentNullException.ThrowIfNull(tiposDeficiencia);
        List<FieldError> recusas = ValidarIdsUnicos(
            [.. condicoes.Select(static c => c.CondicaoOrigemId)],
            [.. recursos.Select(static r => r.RecursoOrigemId)],
            [.. tiposDeficiencia.Select(static t => t.TipoDeficienciaOrigemId)]);
        recusas.AddRange(ValidarDeficienciaComCondicaoPcd(condicoes, tiposDeficiencia));
        return recusas.Count > 0
            ? Result<AtendimentoEspecializado>.ValidationFailure(recusas)
            : Result<AtendimentoEspecializado>.Success(new(condicoes, recursos, tiposDeficiencia));
    }

    /// <summary>
    /// Recusa a lista ausente ou nula: a lista vazia declara que nada é ofertado naquela dimensão, e ausente é
    /// pedido incompleto.
    /// </summary>
    public static List<FieldError> ValidarListasInformadas(
        IReadOnlyList<Guid>? condicaoIds, IReadOnlyList<Guid>? recursoIds, IReadOnlyList<Guid>? tipoDeficienciaIds)
    {
        List<FieldError> recusas = [];
        RecusarListaAusente(recusas, condicaoIds, "condicaoIds", "Informe as condições de atendimento; envie [] quando não houver nenhuma.");
        RecusarListaAusente(recusas, recursoIds, "recursoIds", "Informe os recursos de acessibilidade; envie [] quando não houver nenhum.");
        RecusarListaAusente(recusas, tipoDeficienciaIds, "tipoDeficienciaIds", "Informe os tipos de deficiência; envie [] quando não houver nenhum.");
        return recusas;
    }

    private static void RecusarListaAusente(List<FieldError> recusas, IReadOnlyList<Guid>? lista, string campo, string mensagem)
    {
        if (lista is null)
        {
            recusas.Add(new(campo, new DomainError(CodigosDeValidacao.Obrigatorio, mensagem)));
        }
    }

    /// <summary>
    /// Confere a unicidade dos IDs de origem antes da consulta aos cadastros.
    /// </summary>
    public static List<FieldError> ValidarIdsUnicos(
        IReadOnlyList<Guid> condicaoIds, IReadOnlyList<Guid> recursoIds, IReadOnlyList<Guid> tipoDeficienciaIds)
    {
        ArgumentNullException.ThrowIfNull(condicaoIds);
        ArgumentNullException.ThrowIfNull(recursoIds);
        ArgumentNullException.ThrowIfNull(tipoDeficienciaIds);
        List<FieldError> recusas = [];
        RecusarOfertaRepetida(recusas, condicaoIds, "condicaoIds", CondicaoDuplicada);
        RecusarOfertaRepetida(recusas, recursoIds, "recursoIds", RecursoDuplicado);
        RecusarOfertaRepetida(recusas, tipoDeficienciaIds, "tipoDeficienciaIds", TipoDeficienciaDuplicado);
        return recusas;
    }

    private static void RecusarOfertaRepetida(
        List<FieldError> recusas, IReadOnlyList<Guid> origemIds, string campo, DomainError recusa)
    {
        if (origemIds.Distinct().Count() != origemIds.Count)
        {
            recusas.Add(new(campo, recusa));
        }
    }

    /// <summary>Confere que tipos de deficiência só são ofertados com a condição PcD, sem depender dos recursos.</summary>
    public static List<FieldError> ValidarDeficienciaComCondicaoPcd(
        IReadOnlyList<CondicaoOfertada> condicoes, IReadOnlyList<DeficienciaAtendida> tiposDeficiencia)
    {
        ArgumentNullException.ThrowIfNull(condicoes);
        ArgumentNullException.ThrowIfNull(tiposDeficiencia);

        return tiposDeficiencia.Count > 0 && !OfertaCondicaoPcd(condicoes)
            ? [new("tipoDeficienciaIds", TipoDeficienciaSemCondicaoPcd)]
            : [];
    }

    private static bool OfertaCondicaoPcd(IReadOnlyList<CondicaoOfertada> condicoes) =>
        condicoes.Any(c => string.Equals(c.CondicaoCodigo, CodigoCondicaoPcd, StringComparison.Ordinal));

    private void OfertarCondicoes(IReadOnlyList<CondicaoOfertada> condicoes)
    {
        foreach (CondicaoOfertada condicao in condicoes)
        {
            condicao.VincularAtendimentoEspecializado(Id);
            _condicoes.Add(condicao);
        }
    }

    private void OfertarRecursos(IReadOnlyList<RecursoOfertado> recursos)
    {
        foreach (RecursoOfertado recurso in recursos)
        {
            recurso.VincularAtendimentoEspecializado(Id);
            _recursos.Add(recurso);
        }
    }

    private void AtenderDeficiencias(IReadOnlyList<DeficienciaAtendida> tiposDeficiencia)
    {
        foreach (DeficienciaAtendida deficiencia in tiposDeficiencia)
        {
            deficiencia.VincularAtendimentoEspecializado(Id);
            _tiposDeficiencia.Add(deficiencia);
        }
    }

    void IParteDoProcesso.VincularProcesso(Guid processoSeletivoId) =>
        ProcessoSeletivoId = processoSeletivoId;
}
