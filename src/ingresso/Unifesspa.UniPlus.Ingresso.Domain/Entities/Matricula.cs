namespace Unifesspa.UniPlus.Ingresso.Domain.Entities;

using Enums;
using Events;
using Unifesspa.UniPlus.Kernel.Domain.Entities;

public sealed class Matricula : EntityBase
{
    public Guid ConvocacaoId { get; private set; }
    public Guid CandidatoId { get; private set; }
    public StatusMatricula Status { get; private set; }
    public string CodigoCurso { get; private set; } = string.Empty;
    public string? Observacoes { get; private set; }

    private readonly List<DocumentoMatricula> _documentos = [];
    public IReadOnlyCollection<DocumentoMatricula> Documentos => _documentos.AsReadOnly();

    private Matricula() { }

    public static Matricula Criar(Guid convocacaoId, Guid candidatoId, string codigoCurso)
    {
        return new Matricula
        {
            ConvocacaoId = convocacaoId,
            CandidatoId = candidatoId,
            Status = StatusMatricula.Pendente,
            CodigoCurso = codigoCurso
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

    public void Efetivar()
    {
        Status = StatusMatricula.Efetivada;
        AddDomainEvent(new MatriculaEfetivadaEvent(Id, CandidatoId, CodigoCurso));
    }
}
