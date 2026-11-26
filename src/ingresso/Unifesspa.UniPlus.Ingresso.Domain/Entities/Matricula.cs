namespace Unifesspa.UniPlus.Ingresso.Domain.Entities;

using Enums;

using Events;

using Unifesspa.UniPlus.Kernel.Domain.Entities;
using Unifesspa.UniPlus.Kernel.Domain.OfertasDeCurso;

public sealed class Matricula : SoftDeletableEntity
{
    public Guid ConvocacaoId { get; private set; }
    public Guid CandidatoId { get; private set; }
    public StatusMatricula Status { get; private set; }
    public CopiaDaOfertaDeCurso OfertaCurso { get; private set; } = null!;
    public string? Observacoes { get; private set; }

    private readonly List<DocumentoMatricula> _documentos = [];
    public IReadOnlyCollection<DocumentoMatricula> Documentos => _documentos.AsReadOnly();

    private Matricula() { }

    public static Matricula Criar(Guid convocacaoId, Guid candidatoId, CopiaDaOfertaDeCurso ofertaCurso)
    {
        ArgumentNullException.ThrowIfNull(ofertaCurso);
        return new Matricula
        {
            ConvocacaoId = convocacaoId,
            CandidatoId = candidatoId,
            Status = StatusMatricula.Pendente,
            OfertaCurso = ofertaCurso.ParaNovoDono()
        };
    }

    public void AdicionarDocumento(DocumentoMatricula documento) =>
        _documentos.Add(documento);

    public void EnviarDocumentacao()
    {
        Status = StatusMatricula.DocumentacaoEnviada;
    }

    public void IniciarAnalise() => Status = StatusMatricula.EmAnalise;

    public void Deferir()
    {
        Status = StatusMatricula.Deferida;
    }

    public void Indeferir(string observacoes)
    {
        Status = StatusMatricula.Indeferida;
        Observacoes = observacoes;
    }

    public void Efetivar(TimeProvider clock)
    {
        ArgumentNullException.ThrowIfNull(clock);
        Status = StatusMatricula.Efetivada;
        AddDomainEvent(new MatriculaEfetivadaEvent(
            Id,
            CandidatoId,
            OfertaCurso.Codigo,
            clock.GetUtcNow()));
    }
}
