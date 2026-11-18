namespace Unifesspa.UniPlus.Discentes.Domain.Entities;

using Unifesspa.UniPlus.Discentes.Domain.ValueObjects;

public sealed class VinculoDiscente
{
    public Guid Id { get; private set; }
    public VinculoDiscenteCopiado DadosCopiados { get; private set; }

    private VinculoDiscente(Guid id, VinculoDiscenteCopiado dadosCopiados)
    {
        if (id == Guid.Empty)
            throw new ArgumentException("O identificador não pode ser vazio.", nameof(id));

        ArgumentNullException.ThrowIfNull(dadosCopiados);

        Id = id;
        DadosCopiados = dadosCopiados;
    }

    /// <summary>
    /// Cria um novo vínculo discente com identificador UUIDv7 gerado aqui — nunca
    /// fornecido de fora.
    /// </summary>
    public static VinculoDiscente Criar(VinculoDiscenteCopiado dadosCopiados) =>
        new(Guid.CreateVersion7(), dadosCopiados);

    /// <summary>
    /// Reconstrói o vínculo com os dados persistidos, preservando o <paramref name="id"/>.
    /// O construtor confere os dados novamente na leitura.
    /// </summary>
    public static VinculoDiscente Reidratar(Guid id, VinculoDiscenteCopiado dadosCopiados) =>
        new(id, dadosCopiados);

    /// <summary>
    /// Retorna uma representação técnica e opaca sem exposição de PII (Nome, Matrícula, CPF).
    /// </summary>
    public override string ToString() => $"[VinculoDiscente Id={Id}, IdDiscenteSigaa={DadosCopiados.Identificacao.IdDiscenteSigaa.Valor}]";
}
