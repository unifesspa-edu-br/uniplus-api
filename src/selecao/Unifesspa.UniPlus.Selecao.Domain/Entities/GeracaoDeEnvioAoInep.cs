namespace Unifesspa.UniPlus.Selecao.Domain.Entities;

using Unifesspa.UniPlus.Kernel.Domain.Interfaces;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// O registro de que o arquivo de envio ao INEP foi gerado: quem, quando, para qual edição e com
/// qual leiaute. Nem o arquivo nem os CPFs são guardados.
/// </summary>
public sealed class GeracaoDeEnvioAoInep : IForensicEntity
{
    public Guid Id { get; private init; } = Guid.CreateVersion7();

    public Guid ProcessoSeletivoId { get; private init; }

    public Guid EtapaProcessoId { get; private init; }

    public string EdicaoCodigo { get; private init; } = null!;

    public string LeiauteCodigo { get; private init; } = null!;

    public string LeiauteVersao { get; private init; } = null!;

    public string LeiauteHash { get; private init; } = null!;

    public string GeradoPor { get; private init; } = null!;

    public DateTimeOffset GeradoEm { get; private init; }

    public int TotalDeCpfs { get; private init; }

    // Construtor de materialização do EF Core.
    private GeracaoDeEnvioAoInep()
    {
    }

    public static GeracaoDeEnvioAoInep Registrar(
        Guid processoSeletivoId,
        Guid etapaProcessoId,
        string edicaoCodigo,
        string leiauteCodigo,
        string leiauteVersao,
        string leiauteHash,
        string geradoPor,
        DateTimeOffset geradoEm,
        int totalDeCpfs)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(processoSeletivoId, Guid.Empty);
        ArgumentOutOfRangeException.ThrowIfEqual(etapaProcessoId, Guid.Empty);
        ArgumentException.ThrowIfNullOrWhiteSpace(edicaoCodigo);
        ArgumentException.ThrowIfNullOrWhiteSpace(leiauteCodigo);
        ArgumentException.ThrowIfNullOrWhiteSpace(leiauteVersao);
        ArgumentException.ThrowIfNullOrWhiteSpace(geradoPor);
        ArgumentOutOfRangeException.ThrowIfNegative(totalDeCpfs);

        if (!HashCanonicalComputer.IsValidHashShape(leiauteHash))
        {
            throw new ArgumentException("O hash do leiaute deve ser um SHA-256 em hexadecimal minúsculo.", nameof(leiauteHash));
        }

        return new GeracaoDeEnvioAoInep
        {
            ProcessoSeletivoId = processoSeletivoId,
            EtapaProcessoId = etapaProcessoId,
            EdicaoCodigo = edicaoCodigo,
            LeiauteCodigo = leiauteCodigo,
            LeiauteVersao = leiauteVersao,
            LeiauteHash = leiauteHash,
            GeradoPor = geradoPor,
            GeradoEm = geradoEm,
            TotalDeCpfs = totalDeCpfs,
        };
    }
}
