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
    /// Monta a oferta validando a invariante ADR-0067: tipo de deficiência só
    /// é ofertável quando a condição PcD está entre as condições ofertadas.
    /// </summary>
    public static Result<OfertaAtendimentoEspecializado> Criar(
        IReadOnlyList<OfertaCondicao> condicoes,
        IReadOnlyList<OfertaRecurso> recursos,
        IReadOnlyList<OfertaTipoDeficiencia> tiposDeficiencia)
    {
        ArgumentNullException.ThrowIfNull(condicoes);
        ArgumentNullException.ThrowIfNull(recursos);
        ArgumentNullException.ThrowIfNull(tiposDeficiencia);

        // Recusa duplicatas aqui — não no índice único do banco (CA-06): sem
        // este guard, um request com o mesmo *_origem_id repetido passaria
        // pelo domínio e só falharia no SaveChanges como DbUpdateException
        // (500), em vez de Result 422.
        if (condicoes.Select(c => c.CondicaoOrigemId).Distinct().Count() != condicoes.Count)
        {
            return Result<OfertaAtendimentoEspecializado>.Failure(new DomainError(
                "OfertaAtendimento.CondicaoDuplicada",
                "Cada condição de atendimento só pode ser ofertada uma vez."));
        }

        if (recursos.Select(r => r.RecursoOrigemId).Distinct().Count() != recursos.Count)
        {
            return Result<OfertaAtendimentoEspecializado>.Failure(new DomainError(
                "OfertaAtendimento.RecursoDuplicado",
                "Cada recurso de acessibilidade só pode ser ofertado uma vez."));
        }

        if (tiposDeficiencia.Select(t => t.TipoDeficienciaOrigemId).Distinct().Count() != tiposDeficiencia.Count)
        {
            return Result<OfertaAtendimentoEspecializado>.Failure(new DomainError(
                "OfertaAtendimento.TipoDeficienciaDuplicado",
                "Cada tipo de deficiência só pode ser ofertado uma vez."));
        }

        bool pcdOfertada = condicoes.Any(c =>
            string.Equals(c.CondicaoCodigo, CodigoCondicaoPcd, StringComparison.OrdinalIgnoreCase));

        if (tiposDeficiencia.Count > 0 && !pcdOfertada)
        {
            return Result<OfertaAtendimentoEspecializado>.Failure(new DomainError(
                "OfertaAtendimento.TipoDeficienciaSemCondicaoPcd",
                "Tipos de deficiência só podem ser ofertados quando a condição PcD está ofertada."));
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

    internal void VincularProcesso(Guid processoSeletivoId) =>
        ProcessoSeletivoId = processoSeletivoId;
}
