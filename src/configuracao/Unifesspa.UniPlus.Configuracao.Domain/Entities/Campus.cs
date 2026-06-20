namespace Unifesspa.UniPlus.Configuracao.Domain.Entities;

using Unifesspa.UniPlus.Kernel.Domain.Cidades;
using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Kernel.Domain.Entities;
using Unifesspa.UniPlus.Kernel.Domain.Interfaces;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Campus institucional — unidade física onde a instituição oferta cursos
/// (UNI-REQ #587, módulo Configuração). Referencia a cidade do módulo
/// <c>Geo</c> por <c>CidadeCodigoIbge</c> (código IBGE de 7 dígitos) + display
/// cache (<c>CidadeNome</c>, <c>CidadeUf</c>), preenchido pelo frontend via
/// composição no cliente (ADR-0090) — sem FK cross-banco nem chamada ao Geo.
/// </summary>
/// <remarks>
/// <para>A <c>Sigla</c> é única entre campi vivos (não soft-deleted); a
/// unicidade é validada pelo handler antes da factory e reforçada por índice
/// único parcial de banco (<c>WHERE is_deleted = false</c>).</para>
/// <para>O congelamento (snapshot RN08) é responsabilidade do Processo Seletivo
/// (módulo Selecao, ADR-0061) — não há colunas de snapshot aqui.</para>
/// </remarks>
public sealed class Campus : SoftDeletableEntity, IAuditableEntity
{
    private const int SiglaMinLength = 1;
    private const int SiglaMaxLength = 20;
    private const int NomeMinLength = 2;
    private const int NomeMaxLength = 200;
    private const int EnderecoMaxLength = 500;
    private const int CepLength = 8;
    private const int CodigoEmecMaxLength = 20;
    private const decimal LatitudeMin = -90m;
    private const decimal LatitudeMax = 90m;
    private const decimal LongitudeMin = -180m;
    private const decimal LongitudeMax = 180m;

    public string Sigla { get; private set; } = string.Empty;
    public string Nome { get; private set; } = string.Empty;

    // Referência de cidade do Geo (ADR-0090) — código + display cache.
    public string CidadeCodigoIbge { get; private set; } = string.Empty;
    public string CidadeNome { get; private set; } = string.Empty;
    public string CidadeUf { get; private set; } = string.Empty;
    public string? CidadeOrigem { get; private set; }
    public DateTimeOffset? CidadeDisplayAtualizadoEm { get; private set; }

    public string? Endereco { get; private set; }
    public string? Cep { get; private set; }
    public decimal? Latitude { get; private set; }
    public decimal? Longitude { get; private set; }
    public string? CodigoEmec { get; private set; }

    public string? CreatedBy { get; private set; }
    public string? UpdatedBy { get; private set; }

    // EF Core materialization
    private Campus()
    {
    }

    /// <summary>
    /// Cria um novo Campus. Valida formato e domínio local (incluindo a
    /// referência de cidade via <see cref="ReferenciaCidadeGeo"/>). A unicidade
    /// de <paramref name="sigla"/> entre campi vivos é responsabilidade do handler.
    /// </summary>
    public static Result<Campus> Criar(
        string sigla,
        string nome,
        string cidadeCodigoIbge,
        string cidadeNome,
        string cidadeUf,
        string? cidadeOrigem,
        DateTimeOffset? cidadeDisplayAtualizadoEm,
        string? endereco,
        string? cep,
        decimal? latitude,
        decimal? longitude,
        string? codigoEmec)
    {
        ArgumentNullException.ThrowIfNull(sigla);
        ArgumentNullException.ThrowIfNull(nome);
        ArgumentNullException.ThrowIfNull(cidadeCodigoIbge);
        ArgumentNullException.ThrowIfNull(cidadeNome);
        ArgumentNullException.ThrowIfNull(cidadeUf);

        Result validacao = ValidarCampos(
            sigla, nome, cidadeCodigoIbge, cidadeNome, cidadeUf, endereco, cep, latitude, longitude, codigoEmec);
        if (validacao.IsFailure)
        {
            return Result<Campus>.Failure(validacao.Error!);
        }

        var campus = new Campus();
        campus.AplicarCampos(
            sigla, nome, cidadeCodigoIbge, cidadeNome, cidadeUf, cidadeOrigem, cidadeDisplayAtualizadoEm,
            endereco, cep, latitude, longitude, codigoEmec);

        return Result<Campus>.Success(campus);
    }

    /// <summary>
    /// Atualiza os atributos do Campus. A unicidade de <paramref name="sigla"/>
    /// (quando alterada) é responsabilidade do handler.
    /// </summary>
    public Result Atualizar(
        string sigla,
        string nome,
        string cidadeCodigoIbge,
        string cidadeNome,
        string cidadeUf,
        string? cidadeOrigem,
        DateTimeOffset? cidadeDisplayAtualizadoEm,
        string? endereco,
        string? cep,
        decimal? latitude,
        decimal? longitude,
        string? codigoEmec)
    {
        ArgumentNullException.ThrowIfNull(sigla);
        ArgumentNullException.ThrowIfNull(nome);
        ArgumentNullException.ThrowIfNull(cidadeCodigoIbge);
        ArgumentNullException.ThrowIfNull(cidadeNome);
        ArgumentNullException.ThrowIfNull(cidadeUf);

        Result validacao = ValidarCampos(
            sigla, nome, cidadeCodigoIbge, cidadeNome, cidadeUf, endereco, cep, latitude, longitude, codigoEmec);
        if (validacao.IsFailure)
        {
            return validacao;
        }

        AplicarCampos(
            sigla, nome, cidadeCodigoIbge, cidadeNome, cidadeUf, cidadeOrigem, cidadeDisplayAtualizadoEm,
            endereco, cep, latitude, longitude, codigoEmec);

        return Result.Success();
    }

    private void AplicarCampos(
        string sigla,
        string nome,
        string cidadeCodigoIbge,
        string cidadeNome,
        string cidadeUf,
        string? cidadeOrigem,
        DateTimeOffset? cidadeDisplayAtualizadoEm,
        string? endereco,
        string? cep,
        decimal? latitude,
        decimal? longitude,
        string? codigoEmec)
    {
        Sigla = sigla.Trim().ToUpperInvariant();
        Nome = nome.Trim();
        CidadeCodigoIbge = cidadeCodigoIbge.Trim();
        CidadeNome = cidadeNome.Trim();
        CidadeUf = cidadeUf.Trim().ToUpperInvariant();
        CidadeOrigem = NormalizarOpcional(cidadeOrigem);
        CidadeDisplayAtualizadoEm = cidadeDisplayAtualizadoEm;
        Endereco = NormalizarOpcional(endereco);
        Cep = NormalizarOpcional(cep);
        Latitude = latitude;
        Longitude = longitude;
        CodigoEmec = NormalizarOpcional(codigoEmec);
    }

    private static string? NormalizarOpcional(string? valor) =>
        string.IsNullOrWhiteSpace(valor) ? null : valor.Trim();

    private static Result ValidarCampos(
        string sigla,
        string nome,
        string cidadeCodigoIbge,
        string cidadeNome,
        string cidadeUf,
        string? endereco,
        string? cep,
        decimal? latitude,
        decimal? longitude,
        string? codigoEmec)
    {
        if (string.IsNullOrWhiteSpace(sigla))
        {
            return Result.Failure(new DomainError(
                CampusErrorCodes.SiglaObrigatoria,
                "Sigla do Campus é obrigatória."));
        }

        if (sigla.Trim().Length is < SiglaMinLength or > SiglaMaxLength)
        {
            return Result.Failure(new DomainError(
                CampusErrorCodes.SiglaTamanho,
                $"Sigla do Campus deve ter entre {SiglaMinLength} e {SiglaMaxLength} caracteres."));
        }

        if (string.IsNullOrWhiteSpace(nome))
        {
            return Result.Failure(new DomainError(
                CampusErrorCodes.NomeObrigatorio,
                "Nome do Campus é obrigatório."));
        }

        if (nome.Trim().Length is < NomeMinLength or > NomeMaxLength)
        {
            return Result.Failure(new DomainError(
                CampusErrorCodes.NomeTamanho,
                $"Nome do Campus deve ter entre {NomeMinLength} e {NomeMaxLength} caracteres."));
        }

        Result cidade = ReferenciaCidadeGeo.Validar(cidadeCodigoIbge, cidadeNome, cidadeUf);
        if (cidade.IsFailure)
        {
            return cidade;
        }

        if (endereco is not null && endereco.Trim().Length > EnderecoMaxLength)
        {
            return Result.Failure(new DomainError(
                CampusErrorCodes.EnderecoTamanho,
                $"Endereço do Campus deve ter no máximo {EnderecoMaxLength} caracteres."));
        }

        if (!string.IsNullOrWhiteSpace(cep))
        {
            string cepNormalizado = cep.Trim();
            if (cepNormalizado.Length != CepLength || !cepNormalizado.All(char.IsAsciiDigit))
            {
                return Result.Failure(new DomainError(
                    CampusErrorCodes.CepInvalido,
                    $"CEP do Campus deve ter exatamente {CepLength} dígitos numéricos."));
            }
        }

        if (latitude is { } lat && lat is < LatitudeMin or > LatitudeMax)
        {
            return Result.Failure(new DomainError(
                CampusErrorCodes.LatitudeForaDeFaixa,
                $"Latitude deve estar entre {LatitudeMin} e {LatitudeMax}."));
        }

        if (longitude is { } lon && lon is < LongitudeMin or > LongitudeMax)
        {
            return Result.Failure(new DomainError(
                CampusErrorCodes.LongitudeForaDeFaixa,
                $"Longitude deve estar entre {LongitudeMin} e {LongitudeMax}."));
        }

        if (codigoEmec is not null && codigoEmec.Trim().Length > CodigoEmecMaxLength)
        {
            return Result.Failure(new DomainError(
                CampusErrorCodes.CodigoEmecTamanho,
                $"Código e-MEC do Campus deve ter no máximo {CodigoEmecMaxLength} caracteres."));
        }

        return Result.Success();
    }
}
