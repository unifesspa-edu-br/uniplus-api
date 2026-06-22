namespace Unifesspa.UniPlus.Configuracao.Domain.Entities;

using Unifesspa.UniPlus.Kernel.Domain.Cidades;
using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Kernel.Domain.Enderecos;
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
/// <para>O <see cref="Endereco"/> é uma referência de endereço estruturado ao
/// Geo via CEP, opcional (<see cref="ReferenciaEnderecoGeo"/>, ADR-0096) — sucede
/// o antigo trio texto-livre <c>Endereco</c>/<c>Cep</c>/coordenada. Quando
/// presente, seu snapshot de cidade deve ser coerente com a referência de cidade
/// do campus (CA-04).</para>
/// <para>O congelamento (snapshot RN08) é responsabilidade do Processo Seletivo
/// (módulo Selecao, ADR-0061) — não há colunas de snapshot aqui.</para>
/// </remarks>
public sealed class Campus : SoftDeletableEntity, IAuditableEntity
{
    private const int SiglaMinLength = 1;
    private const int SiglaMaxLength = 20;
    private const int NomeMinLength = 2;
    private const int NomeMaxLength = 200;
    private const int CodigoEmecMaxLength = 20;

    public string Sigla { get; private set; } = string.Empty;
    public string Nome { get; private set; } = string.Empty;

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
    private Campus()
    {
    }

    /// <summary>
    /// Cria um novo Campus. Valida formato e domínio local (incluindo a
    /// referência de cidade via <see cref="ReferenciaCidadeGeo"/> e a coerência
    /// cidade↔endereço). A unicidade de <paramref name="sigla"/> entre campi
    /// vivos é responsabilidade do handler. O <paramref name="endereco"/> já
    /// chega validado (construído pelo handler via <see cref="ReferenciaEnderecoGeo.Criar"/>).
    /// </summary>
    public static Result<Campus> Criar(
        string sigla,
        string nome,
        string cidadeCodigoIbge,
        string cidadeNome,
        string cidadeUf,
        string? cidadeOrigem,
        DateTimeOffset? cidadeDisplayAtualizadoEm,
        ReferenciaEnderecoGeo? endereco,
        string? codigoEmec)
    {
        ArgumentNullException.ThrowIfNull(sigla);
        ArgumentNullException.ThrowIfNull(nome);
        ArgumentNullException.ThrowIfNull(cidadeCodigoIbge);
        ArgumentNullException.ThrowIfNull(cidadeNome);
        ArgumentNullException.ThrowIfNull(cidadeUf);

        Result validacao = ValidarCampos(sigla, nome, cidadeCodigoIbge, cidadeNome, cidadeUf, endereco, codigoEmec);
        if (validacao.IsFailure)
        {
            return Result<Campus>.Failure(validacao.Error!);
        }

        var campus = new Campus();
        campus.AplicarCampos(
            sigla, nome, cidadeCodigoIbge, cidadeNome, cidadeUf, cidadeOrigem, cidadeDisplayAtualizadoEm,
            endereco, codigoEmec);

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
        ReferenciaEnderecoGeo? endereco,
        string? codigoEmec)
    {
        ArgumentNullException.ThrowIfNull(sigla);
        ArgumentNullException.ThrowIfNull(nome);
        ArgumentNullException.ThrowIfNull(cidadeCodigoIbge);
        ArgumentNullException.ThrowIfNull(cidadeNome);
        ArgumentNullException.ThrowIfNull(cidadeUf);

        Result validacao = ValidarCampos(sigla, nome, cidadeCodigoIbge, cidadeNome, cidadeUf, endereco, codigoEmec);
        if (validacao.IsFailure)
        {
            return validacao;
        }

        AplicarCampos(
            sigla, nome, cidadeCodigoIbge, cidadeNome, cidadeUf, cidadeOrigem, cidadeDisplayAtualizadoEm,
            endereco, codigoEmec);

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
        ReferenciaEnderecoGeo? endereco,
        string? codigoEmec)
    {
        Sigla = sigla.Trim().ToUpperInvariant();
        Nome = nome.Trim();
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
        string sigla,
        string nome,
        string cidadeCodigoIbge,
        string cidadeNome,
        string cidadeUf,
        ReferenciaEnderecoGeo? endereco,
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

        // CA-04: o snapshot de cidade do endereço deve coincidir com a referência
        // de cidade do campus (que aqui é sempre obrigatória).
        Result coerencia = ReferenciaEnderecoGeo.ValidarCoerencia(
            endereco?.CidadeCodigoIbge, endereco?.CidadeUf, cidadeCodigoIbge, cidadeUf);
        if (coerencia.IsFailure)
        {
            return coerencia;
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
