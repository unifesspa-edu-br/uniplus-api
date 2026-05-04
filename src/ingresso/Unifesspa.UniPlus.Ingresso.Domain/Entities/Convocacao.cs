namespace Unifesspa.UniPlus.Ingresso.Domain.Entities;

using Enums;
using Events;
using ValueObjects;
using Unifesspa.UniPlus.Kernel.Domain.Entities;

public sealed class Convocacao : EntityBase
{
    public Guid ChamadaId { get; private set; }
    public Guid InscricaoId { get; private set; }
    public Guid CandidatoId { get; private set; }
    public ProtocoloConvocacao Protocolo { get; private set; } = null!;
    public StatusConvocacao Status { get; private set; }
    public int Posicao { get; private set; }
    public string CodigoCurso { get; private set; } = string.Empty;
    public DateTimeOffset? DataManifestacao { get; private set; }

    private Convocacao() { }

    public static Convocacao Criar(Guid chamadaId, Guid inscricaoId, Guid candidatoId, ProtocoloConvocacao protocolo, int posicao, string codigoCurso)
    {
        ArgumentNullException.ThrowIfNull(protocolo);

        var convocacao = new Convocacao
        {
            ChamadaId = chamadaId,
            InscricaoId = inscricaoId,
            CandidatoId = candidatoId,
            Protocolo = protocolo,
            Status = StatusConvocacao.Pendente,
            Posicao = posicao,
            CodigoCurso = codigoCurso
        };

        convocacao.AddDomainEvent(new CandidatoConvocadoEvent(inscricaoId, candidatoId, chamadaId, protocolo.Valor));

        return convocacao;
    }

    public void Aceitar()
    {
        Status = StatusConvocacao.Aceita;
        DataManifestacao = DateTimeOffset.UtcNow;
    }

    public void Recusar()
    {
        Status = StatusConvocacao.Recusada;
        DataManifestacao = DateTimeOffset.UtcNow;
    }

    public void MarcarComoNaoCompareceu() =>
        Status = StatusConvocacao.NaoCompareceu;

    public void Expirar() =>
        Status = StatusConvocacao.Expirada;
}
