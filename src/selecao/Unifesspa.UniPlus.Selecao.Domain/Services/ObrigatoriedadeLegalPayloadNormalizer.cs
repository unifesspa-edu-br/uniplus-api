namespace Unifesspa.UniPlus.Selecao.Domain.Services;

using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

using Unifesspa.UniPlus.Governance.Contracts;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;

/// <summary>
/// Domain service responsável pela validação e normalização do payload
/// usado para construir/atualizar <see cref="ObrigatoriedadeLegal"/>.
/// Separa a responsabilidade de "garantir um payload válido" da
/// responsabilidade de "manter o estado da regra" — a entidade permanece
/// focada nos invariantes do agregado.
/// </summary>
/// <remarks>
/// <para>
/// A validação aqui é a primeira linha de defesa do domínio: shape,
/// presença, tamanho, vigência consistente, governance per ADR-0057
/// (Invariante 1: <c>Proprietario ∈ AreasDeInteresse</c>). A validação
/// de fronteira HTTP (FluentValidation) acontece antes — esta camada
/// existe para garantir que mesmo construções diretas (factory, seeds,
/// testes) respeitem os mesmos contratos.
/// </para>
/// <para>
/// O <see cref="NormalizedPayload"/> é internal por design: representa
/// um estado já validado e só faz sentido como input do construtor de
/// <see cref="ObrigatoriedadeLegal"/>.
/// </para>
/// </remarks>
[SuppressMessage(
    "Design",
    "CA1054:URI-like parameters should not be strings",
    Justification = "AtoNormativoUrl é payload textual de citação normativa (DOI, URN, IRI) — preserva "
        + "o valor original informado pelo admin sem normalização forçada por System.Uri.")]
public static class ObrigatoriedadeLegalPayloadNormalizer
{
    internal const int TipoEditalCodigoMaxLength = 64;
    internal const int RegraCodigoMaxLength = 128;
    internal const int BaseLegalMaxLength = 500;
    internal const int DescricaoHumanaMaxLength = 1000;
    internal const int PortariaInternaCodigoMaxLength = 128;
    internal const int AtoNormativoUrlMaxLength = 1000;

    /// <summary>
    /// Orquestra a normalização do payload em pipeline com early-return
    /// no primeiro <see cref="Result.IsFailure"/>. Os códigos de erro
    /// retornados estão mapeados em <c>SelecaoDomainErrorRegistration</c>.
    /// </summary>
    public static Result<NormalizedPayload> Normalizar(
        string tipoEditalCodigo,
        CategoriaObrigatoriedade categoria,
        string regraCodigo,
        string descricaoHumana,
        string baseLegal,
        string? atoNormativoUrl,
        string? portariaInternaCodigo,
        DateOnly vigenciaInicio,
        DateOnly? vigenciaFim,
        AreaCodigo? proprietario,
        IReadOnlySet<AreaCodigo>? areasDeInteresse)
    {
        Result<string> tipoEdital = NormalizarTipoEditalCodigo(tipoEditalCodigo);
        if (tipoEdital.IsFailure)
        {
            return Result<NormalizedPayload>.Failure(tipoEdital.Error!);
        }

        Result categoriaValida = ValidarCategoria(categoria);
        if (categoriaValida.IsFailure)
        {
            return Result<NormalizedPayload>.Failure(categoriaValida.Error!);
        }

        Result<string> regra = NormalizarRegraCodigo(regraCodigo);
        if (regra.IsFailure)
        {
            return Result<NormalizedPayload>.Failure(regra.Error!);
        }

        Result<string> descricao = NormalizarDescricaoHumana(descricaoHumana);
        if (descricao.IsFailure)
        {
            return Result<NormalizedPayload>.Failure(descricao.Error!);
        }

        Result<string> legal = NormalizarBaseLegal(baseLegal);
        if (legal.IsFailure)
        {
            return Result<NormalizedPayload>.Failure(legal.Error!);
        }

        Result<string?> ato = NormalizarOpcional(
            atoNormativoUrl,
            AtoNormativoUrlMaxLength,
            "ObrigatoriedadeLegal.AtoNormativoUrlInvalido",
            $"AtoNormativoUrl deve ter no máximo {AtoNormativoUrlMaxLength} caracteres.");
        if (ato.IsFailure)
        {
            return Result<NormalizedPayload>.Failure(ato.Error!);
        }

        Result<string?> portaria = NormalizarOpcional(
            portariaInternaCodigo,
            PortariaInternaCodigoMaxLength,
            "ObrigatoriedadeLegal.PortariaInternaCodigoInvalido",
            $"PortariaInternaCodigo deve ter no máximo {PortariaInternaCodigoMaxLength} caracteres.");
        if (portaria.IsFailure)
        {
            return Result<NormalizedPayload>.Failure(portaria.Error!);
        }

        Result vigencia = ValidarVigencia(vigenciaInicio, vigenciaFim);
        if (vigencia.IsFailure)
        {
            return Result<NormalizedPayload>.Failure(vigencia.Error!);
        }

        IReadOnlySet<AreaCodigo> areas = areasDeInteresse ?? (IReadOnlySet<AreaCodigo>)new HashSet<AreaCodigo>();
        Result governanca = ValidarGovernanca(proprietario, areas);
        if (governanca.IsFailure)
        {
            return Result<NormalizedPayload>.Failure(governanca.Error!);
        }

        return Result<NormalizedPayload>.Success(new NormalizedPayload(
            tipoEdital.Value!,
            categoria,
            regra.Value!,
            descricao.Value!,
            legal.Value!,
            ato.Value,
            portaria.Value,
            vigenciaInicio,
            vigenciaFim,
            proprietario,
            areas));
    }

    private static Result<string> NormalizarTipoEditalCodigo(string? valor) =>
        NormalizarObrigatorio(
            valor,
            TipoEditalCodigoMaxLength,
            "ObrigatoriedadeLegal.TipoEditalCodigoObrigatorio",
            "TipoEditalCodigo é obrigatório — use \"*\" para regras universais.",
            "ObrigatoriedadeLegal.TipoEditalCodigoInvalido",
            $"TipoEditalCodigo deve ter no máximo {TipoEditalCodigoMaxLength} caracteres.");

    private static Result<string> NormalizarRegraCodigo(string? valor) =>
        NormalizarObrigatorio(
            valor,
            RegraCodigoMaxLength,
            "ObrigatoriedadeLegal.RegraCodigoObrigatorio",
            "RegraCodigo é obrigatório.",
            "ObrigatoriedadeLegal.RegraCodigoInvalido",
            $"RegraCodigo deve ter no máximo {RegraCodigoMaxLength} caracteres.");

    private static Result<string> NormalizarDescricaoHumana(string? valor) =>
        NormalizarObrigatorio(
            valor,
            DescricaoHumanaMaxLength,
            "ObrigatoriedadeLegal.DescricaoHumanaObrigatoria",
            "DescricaoHumana é obrigatória.",
            "ObrigatoriedadeLegal.DescricaoHumanaInvalida",
            $"DescricaoHumana deve ter no máximo {DescricaoHumanaMaxLength} caracteres.");

    private static Result<string> NormalizarBaseLegal(string? valor) =>
        NormalizarObrigatorio(
            valor,
            BaseLegalMaxLength,
            "ObrigatoriedadeLegal.BaseLegalObrigatoria",
            "BaseLegal é obrigatória.",
            "ObrigatoriedadeLegal.BaseLegalInvalida",
            $"BaseLegal deve ter no máximo {BaseLegalMaxLength} caracteres.");

    /// <summary>
    /// Normaliza texto obrigatório: trim + validação de presença e tamanho.
    /// </summary>
    private static Result<string> NormalizarObrigatorio(
        string? valor,
        int maxLength,
        string codigoObrigatorio,
        string mensagemObrigatorio,
        string codigoTamanho,
        string mensagemTamanho)
    {
        if (string.IsNullOrWhiteSpace(valor))
        {
            return Result<string>.Failure(new DomainError(codigoObrigatorio, mensagemObrigatorio));
        }

        string trimmed = valor.Trim();
        return trimmed.Length > maxLength
            ? Result<string>.Failure(new DomainError(codigoTamanho, mensagemTamanho))
            : Result<string>.Success(trimmed);
    }

    /// <summary>
    /// Normaliza texto opcional: trim + tamanho. Vazio/null é sucesso com null.
    /// </summary>
    private static Result<string?> NormalizarOpcional(
        string? valor,
        int maxLength,
        string codigoTamanho,
        string mensagemTamanho)
    {
        if (string.IsNullOrWhiteSpace(valor))
        {
            return Result<string?>.Success(null);
        }

        string trimmed = valor.Trim();
        return trimmed.Length > maxLength
            ? Result<string?>.Failure(new DomainError(codigoTamanho, mensagemTamanho))
            : Result<string?>.Success(trimmed);
    }

    private static Result ValidarCategoria(CategoriaObrigatoriedade categoria)
    {
        return !Enum.IsDefined(categoria) || categoria == CategoriaObrigatoriedade.Nenhuma
            ? Result.Failure(new DomainError(
                "ObrigatoriedadeLegal.CategoriaInvalida",
                "Categoria inválida — use um valor definido em CategoriaObrigatoriedade, diferente de Nenhuma."))
            : Result.Success();
    }

    private static Result ValidarVigencia(DateOnly inicio, DateOnly? fim)
    {
        return fim is { } f && f <= inicio
            ? Result.Failure(new DomainError(
                "ObrigatoriedadeLegal.VigenciaInvalida",
                "VigenciaFim deve ser estritamente posterior a VigenciaInicio."))
            : Result.Success();
    }

    /// <summary>
    /// Invariante 1 do ADR-0057: <c>Proprietario</c> deve estar em
    /// <c>AreasDeInteresse</c>; ambos vazios são válidos para regra global.
    /// </summary>
    private static Result ValidarGovernanca(AreaCodigo? proprietario, IReadOnlySet<AreaCodigo> areas)
    {
        if (proprietario is { } prop)
        {
            return areas.Count == 0 || !areas.Contains(prop)
                ? Result.Failure(new DomainError(
                    "ObrigatoriedadeLegal.ProprietarioForaDeAreasDeInteresse",
                    "Proprietario deve estar em AreasDeInteresse — ou ambos devem ser vazios para regra global."))
                : Result.Success();
        }

        return areas.Count > 0
            ? Result.Failure(new DomainError(
                "ObrigatoriedadeLegal.ProprietarioObrigatorioComAreas",
                "Quando AreasDeInteresse não é vazio, Proprietario é obrigatório."))
            : Result.Success();
    }
}

/// <summary>
/// Payload já validado e normalizado, pronto para construir um
/// <see cref="ObrigatoriedadeLegal"/>. Internal por design: só faz
/// sentido como input do construtor da entidade.
/// </summary>
[SuppressMessage(
    "Design",
    "CA1056:URI-like properties should not be strings",
    Justification = "AtoNormativoUrl é payload textual de citação normativa — pareado com a "
        + "supressão equivalente na ObrigatoriedadeLegal.")]
[SuppressMessage(
    "Design",
    "CA1054:URI-like parameters should not be strings",
    Justification = "Construtor do record propaga o tipo string do payload — ver justificativa acima.")]
public sealed record NormalizedPayload(
    string TipoEditalCodigo,
    CategoriaObrigatoriedade Categoria,
    string RegraCodigo,
    string DescricaoHumana,
    string BaseLegal,
    string? AtoNormativoUrl,
    string? PortariaInternaCodigo,
    DateOnly VigenciaInicio,
    DateOnly? VigenciaFim,
    AreaCodigo? Proprietario,
    IReadOnlySet<AreaCodigo> AreasDeInteresse);
