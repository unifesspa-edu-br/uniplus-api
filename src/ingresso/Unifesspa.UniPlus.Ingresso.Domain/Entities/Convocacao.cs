namespace Unifesspa.UniPlus.Ingresso.Domain.Entities;

using Enums;

using Events;

using Unifesspa.UniPlus.Kernel.Domain.Entities;
using Unifesspa.UniPlus.Kernel.Domain.OfertasDeCurso;

using ValueObjects;

public sealed class Convocacao : SoftDeletableEntity
{
    public Guid ChamadaId { get; private set; }
    public Guid InscricaoId { get; private set; }
    public Guid CandidatoId { get; private set; }
    public ProtocoloConvocacao Protocolo { get; private set; } = null!;
    public StatusConvocacao Status { get; private set; }
    public int Posicao { get; private set; }
    public CopiaDaOfertaDeCurso OfertaCurso { get; private set; } = null!;
    public DateTimeOffset? DataManifestacao { get; private set; }

    private Convocacao() { }

    private Convocacao(
        Guid chamadaId,
        Guid inscricaoId,
        Guid candidatoId,
        ProtocoloConvocacao protocolo,
        int posicao,
        CopiaDaOfertaDeCurso ofertaCurso,
        DateTimeOffset convocadaEm)
    {
        ChamadaId = chamadaId;
        InscricaoId = inscricaoId;
        CandidatoId = candidatoId;
        Protocolo = protocolo;
        Status = StatusConvocacao.Pendente;
        Posicao = posicao;
        OfertaCurso = ofertaCurso.ParaNovoDono();

        AddDomainEvent(new CandidatoConvocadoEvent(inscricaoId, candidatoId, chamadaId, protocolo.Valor, convocadaEm));
    }

    public static Convocacao Criar(Guid chamadaId, Guid inscricaoId, Guid candidatoId, ProtocoloConvocacao protocolo, int posicao, CopiaDaOfertaDeCurso ofertaCurso, TimeProvider clock)
    {
        ArgumentNullException.ThrowIfNull(protocolo);
        ArgumentNullException.ThrowIfNull(ofertaCurso);
        ArgumentNullException.ThrowIfNull(clock);

        return new Convocacao(chamadaId, inscricaoId, candidatoId, protocolo, posicao, ofertaCurso, clock.GetUtcNow());
    }

    public void Aceitar(TimeProvider clock)
    {
        ArgumentNullException.ThrowIfNull(clock);
        Status = StatusConvocacao.Aceita;
        DataManifestacao = clock.GetUtcNow();
    }

    public void Recusar(TimeProvider clock)
    {
        ArgumentNullException.ThrowIfNull(clock);
        Status = StatusConvocacao.Recusada;
        DataManifestacao = clock.GetUtcNow();
    }

    public void MarcarComoNaoCompareceu() =>
        Status = StatusConvocacao.NaoCompareceu;

    public void Expirar() =>
        Status = StatusConvocacao.Expirada;
}
