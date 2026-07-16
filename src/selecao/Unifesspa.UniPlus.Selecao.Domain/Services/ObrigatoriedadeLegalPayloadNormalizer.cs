namespace Unifesspa.UniPlus.Selecao.Domain.Services;

using System.Diagnostics.CodeAnalysis;

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
/// presença, tamanho e vigência consistente. A validação
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
    internal const int TipoProcessoCodigoMaxLength = 64;
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
        string tipoProcessoCodigo,
        CategoriaObrigatoriedade categoria,
        string regraCodigo,
        string descricaoHumana,
        string baseLegal,
        string? atoNormativoUrl,
        string? portariaInternaCodigo,
        DateOnly vigenciaInicio,
        DateOnly? vigenciaFim)
    {
        Result<string> tipoProcesso = NormalizarTipoProcessoCodigo(tipoProcessoCodigo);
        if (tipoProcesso.IsFailure)
        {
            return Result<NormalizedPayload>.Failure(tipoProcesso.Error!);
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

        return Result<NormalizedPayload>.Success(new NormalizedPayload(
            tipoProcesso.Value!,
            categoria,
            regra.Value!,
            descricao.Value!,
            legal.Value!,
            ato.Value,
            portaria.Value,
            vigenciaInicio,
            vigenciaFim));
    }

    private static Result<string> NormalizarTipoProcessoCodigo(string? valor)
    {
        Result<string> codigo = NormalizarObrigatorio(
            valor,
            TipoProcessoCodigoMaxLength,
            "ObrigatoriedadeLegal.TipoProcessoCodigoObrigatorio",
            "TipoProcessoCodigo é obrigatório — use \"*\" para regras universais.",
            "ObrigatoriedadeLegal.TipoProcessoCodigoInvalido",
            $"TipoProcessoCodigo deve ter no máximo {TipoProcessoCodigoMaxLength} caracteres.");
        if (codigo.IsFailure)
        {
            return codigo;
        }

        string normalizado = codigo.Value!;
        return normalizado == ObrigatoriedadeLegal.TipoProcessoUniversal
            || Enum.GetNames<TipoProcesso>().Contains(normalizado, StringComparer.Ordinal)
                && normalizado != nameof(TipoProcesso.Nenhum)
            ? codigo
            : Result<string>.Failure(new DomainError(
                "ObrigatoriedadeLegal.TipoProcessoCodigoForaDoVocabulario",
                "TipoProcessoCodigo deve ser \"*\" ou um nome válido de TipoProcesso."));
    }

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
    string TipoProcessoCodigo,
    CategoriaObrigatoriedade Categoria,
    string RegraCodigo,
    string DescricaoHumana,
    string BaseLegal,
    string? AtoNormativoUrl,
    string? PortariaInternaCodigo,
    DateOnly VigenciaInicio,
    DateOnly? VigenciaFim);
