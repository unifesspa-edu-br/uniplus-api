namespace Unifesspa.UniPlus.Selecao.Domain.Entities;

using Unifesspa.UniPlus.Kernel.Domain.Entities;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Contêiner (um por processo) da oferta de atendimento especializado do
/// <see cref="ProcessoSeletivo"/>: as condições, recursos de acessibilidade e
/// tipos de deficiência que o certame disponibiliza aos candidatos, todos
/// congelados por snapshot-copy (ADR-0061) dos cadastros do módulo
/// Configuração.
/// </summary>
/// <remarks>
/// <see cref="EntityBase"/> puro (sem soft-delete): ver justificativa em
/// <see cref="EtapaProcesso"/>.
/// </remarks>
public sealed class OfertaAtendimentoEspecializado : EntityBase
{
    /// <summary>
    /// Código canônico da condição PcD no cadastro de condições de atendimento
    /// (linha protegida do catálogo). Âncora da invariante ADR-0067.
    /// </summary>
    public const string CodigoCondicaoPcd = "PCD";

    public Guid ProcessoSeletivoId { get; private set; }

    private readonly List<OfertaCondicao> _condicoes = [];
    public IReadOnlyCollection<OfertaCondicao> Condicoes => _condicoes.AsReadOnly();

    private readonly List<OfertaRecurso> _recursos = [];
    public IReadOnlyCollection<OfertaRecurso> Recursos => _recursos.AsReadOnly();

    private readonly List<OfertaTipoDeficiencia> _tiposDeficiencia = [];
    public IReadOnlyCollection<OfertaTipoDeficiencia> TiposDeficiencia => _tiposDeficiencia.AsReadOnly();

    private OfertaAtendimentoEspecializado() { }

    /// <summary>
    /// Acumula toda violação independente em vez de retornar na primeira (ADR-0125) — as
    /// três checagens de duplicata e a invariante ADR-0067 (tipo de deficiência só sob
    /// condição PcD) não dependem umas das outras. As checagens de duplicata só existem
    /// aqui por defesa em profundidade: o handler já confirma unicidade via
    /// <see cref="ValidarIdsUnicos"/>, sobre os IDs crus, ANTES de resolver cada dimensão nos
    /// cadastros vivos (validação sempre precede I/O) — chamar <see cref="Criar"/>
    /// diretamente sem passar por ali (ex.: em teste) ainda é seguro.
    /// </summary>
    public static Result<OfertaAtendimentoEspecializado> Criar(
        IReadOnlyList<OfertaCondicao> condicoes,
        IReadOnlyList<OfertaRecurso> recursos,
        IReadOnlyList<OfertaTipoDeficiencia> tiposDeficiencia)
    {
        ArgumentNullException.ThrowIfNull(condicoes);
        ArgumentNullException.ThrowIfNull(recursos);
        ArgumentNullException.ThrowIfNull(tiposDeficiencia);

        List<FieldError> erros = [];

        // Recusa duplicatas aqui — não no índice único do banco (CA-06): sem
        // este guard, um request com o mesmo *_origem_id repetido passaria
        // pelo domínio e só falharia no SaveChanges como DbUpdateException
        // (500), em vez de Result 422.
        if (condicoes.Select(c => c.CondicaoOrigemId).Distinct().Count() != condicoes.Count)
        {
            erros.Add(new("condicaoIds", new DomainError(
                "OfertaAtendimento.CondicaoDuplicada",
                "Cada condição de atendimento só pode ser ofertada uma vez.")));
        }

        if (recursos.Select(r => r.RecursoOrigemId).Distinct().Count() != recursos.Count)
        {
            erros.Add(new("recursoIds", new DomainError(
                "OfertaAtendimento.RecursoDuplicado",
                "Cada recurso de acessibilidade só pode ser ofertado uma vez.")));
        }

        if (tiposDeficiencia.Select(t => t.TipoDeficienciaOrigemId).Distinct().Count() != tiposDeficiencia.Count)
        {
            erros.Add(new("tipoDeficienciaIds", new DomainError(
                "OfertaAtendimento.TipoDeficienciaDuplicado",
                "Cada tipo de deficiência só pode ser ofertado uma vez.")));
        }

        bool pcdOfertada = condicoes.Any(c =>
            string.Equals(c.CondicaoCodigo, CodigoCondicaoPcd, StringComparison.OrdinalIgnoreCase));

        if (tiposDeficiencia.Count > 0 && !pcdOfertada)
        {
            erros.Add(new("tipoDeficienciaIds", new DomainError(
                "OfertaAtendimento.TipoDeficienciaSemCondicaoPcd",
                "Tipos de deficiência só podem ser ofertados quando a condição PcD está ofertada.")));
        }

        if (erros.Count > 0)
        {
            return Result<OfertaAtendimentoEspecializado>.ValidationFailure(erros);
        }

        OfertaAtendimentoEspecializado oferta = new();
        foreach (OfertaCondicao condicao in condicoes)
        {
            condicao.VincularOferta(oferta.Id);
            oferta._condicoes.Add(condicao);
        }

        foreach (OfertaRecurso recurso in recursos)
        {
            recurso.VincularOferta(oferta.Id);
            oferta._recursos.Add(recurso);
        }

        foreach (OfertaTipoDeficiencia tipo in tiposDeficiencia)
        {
            tipo.VincularOferta(oferta.Id);
            oferta._tiposDeficiencia.Add(tipo);
        }

        return Result<OfertaAtendimentoEspecializado>.Success(oferta);
    }

    /// <summary>
    /// Confirma unicidade dos IDs crus de cada dimensão — sem depender do cadastro vivo
    /// (ADR-0056) já resolvido, ao contrário das checagens equivalentes dentro de
    /// <see cref="Criar"/>. Existe para o handler poder recusar duplicatas ANTES de consultar
    /// os três leitores cross-módulo (ADR-0125 ponto 5: validação sempre precede I/O) — um
    /// payload com o mesmo <c>condicaoId</c> repetido não precisa de três chamadas de rede
    /// para descobrir que vai falhar.
    /// </summary>
    public static List<FieldError> ValidarIdsUnicos(
        IReadOnlyList<Guid> condicaoIds, IReadOnlyList<Guid> recursoIds, IReadOnlyList<Guid> tipoDeficienciaIds)
    {
        ArgumentNullException.ThrowIfNull(condicaoIds);
        ArgumentNullException.ThrowIfNull(recursoIds);
        ArgumentNullException.ThrowIfNull(tipoDeficienciaIds);

        List<FieldError> erros = [];

        if (condicaoIds.Distinct().Count() != condicaoIds.Count)
        {
            erros.Add(new("condicaoIds", new DomainError(
                "OfertaAtendimento.CondicaoDuplicada",
                "Cada condição de atendimento só pode ser ofertada uma vez.")));
        }

        if (recursoIds.Distinct().Count() != recursoIds.Count)
        {
            erros.Add(new("recursoIds", new DomainError(
                "OfertaAtendimento.RecursoDuplicado",
                "Cada recurso de acessibilidade só pode ser ofertado uma vez.")));
        }

        if (tipoDeficienciaIds.Distinct().Count() != tipoDeficienciaIds.Count)
        {
            erros.Add(new("tipoDeficienciaIds", new DomainError(
                "OfertaAtendimento.TipoDeficienciaDuplicado",
                "Cada tipo de deficiência só pode ser ofertado uma vez.")));
        }

        return erros;
    }

    internal void VincularProcesso(Guid processoSeletivoId) =>
        ProcessoSeletivoId = processoSeletivoId;
}
