namespace Unifesspa.UniPlus.Discentes.Domain.Entities;

using Unifesspa.UniPlus.Discentes.Domain.ValueObjects;

public sealed class VinculoDiscente
{
    public Guid Id { get; private set; }
    public VinculoDiscenteSnapshot Snapshot { get; private set; }

    private VinculoDiscente(Guid id, VinculoDiscenteSnapshot snapshot)
    {
        if (id == Guid.Empty)
            throw new ArgumentException("O identificador não pode ser vazio.", nameof(id));

        ArgumentNullException.ThrowIfNull(snapshot);

        Id = id;
        Snapshot = snapshot;
    }

    /// <summary>
    /// Cria um novo vínculo discente com identificador UUIDv7 gerado aqui — nunca
    /// fornecido de fora.
    /// </summary>
    public static VinculoDiscente Criar(VinculoDiscenteSnapshot snapshot) =>
        new(Guid.CreateVersion7(), snapshot);

    /// <summary>
    /// Reidratar não é criar: reconstrói o vínculo a partir do estado já persistido,
    /// preservando o <paramref name="id"/> do banco. As guardas do construtor são a
    /// última linha de defesa contra dado corrompido na leitura.
    /// </summary>
    public static VinculoDiscente Reidratar(Guid id, VinculoDiscenteSnapshot snapshot) =>
        new(id, snapshot);

    /// <summary>
    /// Retorna uma representação técnica e opaca sem exposição de PII (Nome, Matrícula, CPF).
    /// </summary>
    public override string ToString() => $"[VinculoDiscente Id={Id}, IdDiscenteSigaa={Snapshot.IdDiscenteSigaa}]";
}
