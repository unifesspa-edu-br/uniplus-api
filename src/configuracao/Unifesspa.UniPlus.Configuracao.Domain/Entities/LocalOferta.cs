namespace Unifesspa.UniPlus.Configuracao.Domain.Entities;

using Unifesspa.UniPlus.Kernel.Domain.Cidades;
using Unifesspa.UniPlus.Configuracao.Domain.Enums;
using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Kernel.Domain.Enderecos;
using Unifesspa.UniPlus.Kernel.Domain.Entities;
using Unifesspa.UniPlus.Kernel.Domain.Interfaces;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Local de oferta de vagas (UNI-REQ #587, módulo Configuração). Modelo
/// <strong>flat</strong> (ADR-0065): em vez de uma hierarquia de tipos, o
/// <see cref="Tipo"/> classifica a modalidade e <see cref="CampusResponsavelId"/>
/// (FK intra-banco opcional → <c>campus</c>) liga o local ao campus responsável
/// quando aplicável. Referencia a cidade do <c>Geo</c> por código + display
/// cache (ADR-0090), sem FK cross-banco.
/// </summary>
/// <remarks>
/// <para>O <see cref="Endereco"/> é uma referência de endereço estruturado ao
/// Geo via CEP, opcional (<see cref="ReferenciaEnderecoGeo"/>, ADR-0096) — sucede
/// o antigo <c>Endereco</c> texto-livre. Quando presente, seu snapshot de cidade
/// deve coincidir com a referência de cidade do local (CA-04).</para>
/// <para>O congelamento (snapshot RN08) é responsabilidade do Processo Seletivo
/// (módulo Selecao, via oferta de curso congelada — ADR-0061); não há colunas
/// de snapshot aqui.</para>
/// </remarks>
public sealed class LocalOferta : SoftDeletableEntity, IAuditableEntity
{
    private const int CodigoEmecMaxLength = 20;

    public TipoLocalOferta Tipo { get; private set; }
    public Guid? CampusResponsavelId { get; private set; }

    // Referência de cidade do Geo (ADR-0090) — código + display cache.
    public string CidadeCodigoIbge { get; private set; } = string.Empty;
    public string CidadeNome { get; private set; } = string.Empty;
    public string CidadeUf { get; private set; } = string.Empty;
    public string? CidadeOrigem { get; private set; }
    public DateTimeOffset? CidadeDisplayAtualizadoEm { get; private set; }

    // Endereço estruturado ao Geo via CEP (ADR-0096) — opcional, owned type.
    public ReferenciaEnderecoGeo? Endereco { get; private set; }

    public string? CodigoEmec { get; private set; }

    public string? CreatedBy { get; private set; }
    public string? UpdatedBy { get; private set; }

    // EF Core materialization
    private LocalOferta()
    {
    }

    /// <summary>
    /// Cria um novo Local de Oferta. Valida formato e domínio local (tipo,
    /// referência de cidade e coerência cidade↔endereço). A existência do campus
    /// responsável (quando informado) é responsabilidade do handler. O
    /// <paramref name="endereco"/> já chega validado.
    /// </summary>
    public static Result<LocalOferta> Criar(
        TipoLocalOferta tipo,
        Guid? campusResponsavelId,
        string cidadeCodigoIbge,
        string cidadeNome,
        string cidadeUf,
        string? cidadeOrigem,
        DateTimeOffset? cidadeDisplayAtualizadoEm,
        ReferenciaEnderecoGeo? endereco,
        string? codigoEmec)
    {
        ArgumentNullException.ThrowIfNull(cidadeCodigoIbge);
        ArgumentNullException.ThrowIfNull(cidadeNome);
        ArgumentNullException.ThrowIfNull(cidadeUf);

        Result validacao = ValidarCampos(tipo, cidadeCodigoIbge, cidadeNome, cidadeUf, endereco, codigoEmec);
        if (validacao.IsFailure)
        {
            return Result<LocalOferta>.Failure(validacao.Error!);
        }

        var local = new LocalOferta();
        local.AplicarCampos(
            tipo, campusResponsavelId, cidadeCodigoIbge, cidadeNome, cidadeUf, cidadeOrigem,
            cidadeDisplayAtualizadoEm, endereco, codigoEmec);

        return Result<LocalOferta>.Success(local);
    }

    /// <summary>
    /// Atualiza os atributos do Local de Oferta. A existência do campus
    /// responsável (quando informado) é responsabilidade do handler.
    /// </summary>
    public Result Atualizar(
        TipoLocalOferta tipo,
        Guid? campusResponsavelId,
        string cidadeCodigoIbge,
        string cidadeNome,
        string cidadeUf,
        string? cidadeOrigem,
        DateTimeOffset? cidadeDisplayAtualizadoEm,
        ReferenciaEnderecoGeo? endereco,
        string? codigoEmec)
    {
        ArgumentNullException.ThrowIfNull(cidadeCodigoIbge);
        ArgumentNullException.ThrowIfNull(cidadeNome);
        ArgumentNullException.ThrowIfNull(cidadeUf);

        Result validacao = ValidarCampos(tipo, cidadeCodigoIbge, cidadeNome, cidadeUf, endereco, codigoEmec);
        if (validacao.IsFailure)
        {
            return validacao;
        }

        AplicarCampos(
            tipo, campusResponsavelId, cidadeCodigoIbge, cidadeNome, cidadeUf, cidadeOrigem,
            cidadeDisplayAtualizadoEm, endereco, codigoEmec);

        return Result.Success();
    }

    private void AplicarCampos(
        TipoLocalOferta tipo,
        Guid? campusResponsavelId,
        string cidadeCodigoIbge,
        string cidadeNome,
        string cidadeUf,
        string? cidadeOrigem,
        DateTimeOffset? cidadeDisplayAtualizadoEm,
        ReferenciaEnderecoGeo? endereco,
        string? codigoEmec)
    {
        Tipo = tipo;
        CampusResponsavelId = campusResponsavelId;
        CidadeCodigoIbge = cidadeCodigoIbge.Trim();
        CidadeNome = cidadeNome.Trim();
        CidadeUf = cidadeUf.Trim().ToUpperInvariant();
        CidadeOrigem = NormalizarOpcional(cidadeOrigem);
        CidadeDisplayAtualizadoEm = cidadeDisplayAtualizadoEm;
        Endereco = endereco;
        CodigoEmec = NormalizarOpcional(codigoEmec);
    }

    private static string? NormalizarOpcional(string? valor) =>
        string.IsNullOrWhiteSpace(valor) ? null : valor.Trim();

    private static Result ValidarCampos(
        TipoLocalOferta tipo,
        string cidadeCodigoIbge,
        string cidadeNome,
        string cidadeUf,
        ReferenciaEnderecoGeo? endereco,
        string? codigoEmec)
    {
        if (!Enum.IsDefined(tipo) || tipo == TipoLocalOferta.Nenhum)
        {
            return Result.Failure(new DomainError(
                LocalOfertaErrorCodes.TipoInvalido,
                "Tipo de Local de Oferta inválido — use um valor definido em TipoLocalOferta, diferente de Nenhum."));
        }

        Result cidade = ReferenciaCidadeGeo.Validar(cidadeCodigoIbge, cidadeNome, cidadeUf);
        if (cidade.IsFailure)
        {
            return cidade;
        }

        // CA-04: o snapshot de cidade do endereço deve coincidir com a referência
        // de cidade do local (que aqui é sempre obrigatória).
        Result coerencia = ReferenciaEnderecoGeo.ValidarCoerencia(
            endereco?.CidadeCodigoIbge, endereco?.CidadeUf, cidadeCodigoIbge, cidadeUf);
        if (coerencia.IsFailure)
        {
            return coerencia;
        }

        if (codigoEmec is not null && codigoEmec.Trim().Length > CodigoEmecMaxLength)
        {
            return Result.Failure(new DomainError(
                LocalOfertaErrorCodes.CodigoEmecTamanho,
                $"Código e-MEC do Local de Oferta deve ter no máximo {CodigoEmecMaxLength} caracteres."));
        }

        return Result.Success();
    }
}
