namespace Unifesspa.UniPlus.Configuracao.Domain.Entities;

using System.Collections.Generic;

using Unifesspa.UniPlus.Configuracao.Domain.Enums;
using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Configuracao.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Domain.Cidades;
using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Kernel.Domain.Entities;
using Unifesspa.UniPlus.Kernel.Domain.Interfaces;
using Unifesspa.UniPlus.Kernel.Results;

public sealed class BaseLegalDoFada : SoftDeletableEntity, IAuditableEntity
{
    private const int IdentificacaoMinLength = 3;
    private const int IdentificacaoMaxLength = 500;
    private const int DescricaoMinLength = 3;
    private const int DescricaoMaxLength = 2000;

    private static readonly CampoTextual CampoIdentificacao = new(
        "Identificação", IdentificacaoMaxLength,
        BaseLegalDoFadaErrorCodes.IdentificacaoTamanho, BaseLegalDoFadaErrorCodes.IdentificacaoCaractereInvalido,
        FormaDoTexto.UmaLinha)
    { Minimo = IdentificacaoMinLength, Feminino = true };

    private static readonly CampoTextual CampoDescricao = new(
        "Descrição", DescricaoMaxLength,
        BaseLegalDoFadaErrorCodes.DescricaoTamanho, BaseLegalDoFadaErrorCodes.DescricaoCaractereInvalido,
        FormaDoTexto.VariasLinhas)
    { Minimo = DescricaoMinLength, Feminino = true };

    private readonly List<MunicipioDaBaseLegalDoFada> _municipios = [];

    /// <summary>Código estável do ato, formado na criação e nunca alterado (o documento de configuração referencia por ele).</summary>
    public CodigoDaBaseLegalDoFada Codigo { get; private set; } = null!;

    public TipoInstrumentoNormativo TipoInstrumento { get; private set; }
    public string Identificacao { get; private set; } = string.Empty;
    public string Descricao { get; private set; } = string.Empty;
    public IReadOnlyList<MunicipioDaBaseLegalDoFada> Municipios => _municipios.AsReadOnly();

    public string? CreatedBy { get; private set; }
    public string? UpdatedBy { get; private set; }

    private BaseLegalDoFada() { }

    private static Result<CamposDaBaseLegalDoFada> ValidarCampos(
        string? tipoInstrumentoCodigo, string? identificacao, string? descricao,
        IEnumerable<(string? CodigoIbge, string? Nome, string? Uf)>? municipios)
    {
        List<FieldError> erros = [];
        TipoInstrumentoNormativo? tipo = ConferirTipoInstrumento(tipoInstrumentoCodigo, erros);
        string? identNorm = CampoIdentificacao.Obrigatorio(identificacao, "identificacao", BaseLegalDoFadaErrorCodes.IdentificacaoObrigatoria, erros);
        string? descNorm = CampoDescricao.Obrigatorio(descricao, "descricao", BaseLegalDoFadaErrorCodes.DescricaoObrigatoria, erros);
        IReadOnlyList<MunicipioDaBaseLegalDoFada> validos = ConferirMunicipios(municipios, erros);

        return erros.Count > 0
            ? Result<CamposDaBaseLegalDoFada>.ValidationFailure(erros)
            : Result<CamposDaBaseLegalDoFada>.Success(new(tipo!.Value, identNorm!, descNorm!, validos));
    }

    private static TipoInstrumentoNormativo? ConferirTipoInstrumento(string? codigo, List<FieldError> erros)
    {
        if (CatalogoDe<TipoInstrumentoNormativo>.TentarObter(codigo, out TipoInstrumentoNormativo tipo))
        {
            return tipo;
        }

        string aceitos = string.Join(", ", CatalogoDe<TipoInstrumentoNormativo>.Todos().Select(static d => d.Codigo));
        erros.Add(new("tipoInstrumento", new DomainError(
            BaseLegalDoFadaErrorCodes.TipoInstrumentoInvalido,
            $"Tipo de instrumento normativo inválido ou não informado. Os tipos aceitos são {aceitos}.")));
        return null;
    }

    private static IReadOnlyList<MunicipioDaBaseLegalDoFada> ConferirMunicipios(
        IEnumerable<(string? CodigoIbge, string? Nome, string? Uf)>? municipios, List<FieldError> erros)
    {
        if (municipios is null || !municipios.Any())
        {
            erros.Add(new("municipios", new DomainError(
                BaseLegalDoFadaErrorCodes.SemMunicipios,
                "A base legal deve abranger pelo menos um município.")));
            return [];
        }

        return [.. municipios.Select((m, i) => ConferirMunicipio(m, i, erros)).OfType<MunicipioDaBaseLegalDoFada>()];
    }

    private static MunicipioDaBaseLegalDoFada? ConferirMunicipio(
        (string? CodigoIbge, string? Nome, string? Uf) municipio, int indice, List<FieldError> erros)
    {
        Result resultado = ReferenciaCidadeGeo.Validar(municipio.CodigoIbge, municipio.Nome, municipio.Uf);
        erros.AddRange(resultado.Errors.Select(erro => new FieldError(
            $"municipios[{indice}]", new DomainError(BaseLegalDoFadaErrorCodes.MunicipioInvalido, erro.Error.Message))));
        return resultado.IsFailure
            ? null
            : new MunicipioDaBaseLegalDoFada(municipio.CodigoIbge!, municipio.Nome!, municipio.Uf!);
    }

    public static Result<BaseLegalDoFada> Criar(
        string? tipoInstrumentoCodigo,
        string? identificacao,
        string? descricao,
        IEnumerable<(string? CodigoIbge, string? Nome, string? Uf)>? municipios)
    {
        Result<CamposDaBaseLegalDoFada> validacao = ValidarCampos(tipoInstrumentoCodigo, identificacao, descricao, municipios);
        Result<CodigoDaBaseLegalDoFada>? codigo = FormarCodigo(validacao, tipoInstrumentoCodigo, identificacao);

        IReadOnlyList<FieldError> erros = RecusasDaCriacao(validacao, codigo);
        if (erros.Count > 0)
        {
            return Result<BaseLegalDoFada>.ValidationFailure(erros);
        }

        BaseLegalDoFada baseLegal = new BaseLegalDoFada { Codigo = codigo!.Value! };
        baseLegal.AplicarCampos(validacao.Value!);
        return Result<BaseLegalDoFada>.Success(baseLegal);
    }

    private static IReadOnlyList<FieldError> RecusasDaCriacao(
        Result<CamposDaBaseLegalDoFada> validacao,
        Result<CodigoDaBaseLegalDoFada>? codigo) =>
        codigo is { IsFailure: true } ? [.. validacao.Errors, new FieldError("identificacao", codigo.Error!)] : validacao.Errors;

    // O código só depende do tipo e da identificação: se algum dos dois já foi recusado, o motivo está na validação e não há
    // código a formar.
    private static Result<CodigoDaBaseLegalDoFada>? FormarCodigo(
        Result<CamposDaBaseLegalDoFada> validacao,
        string? tipoInstrumentoCodigo,
        string? identificacao) =>
        validacao.Errors.Any(e => e.Field is "tipoInstrumento" or "identificacao")
            ? null
            : CodigoDaBaseLegalDoFada.Gerar(CatalogoDe<TipoInstrumentoNormativo>.Obter(tipoInstrumentoCodigo!), identificacao?.Trim() ?? string.Empty);

    public Result Atualizar(
        string? tipoInstrumentoCodigo,
        string? identificacao,
        string? descricao,
        IEnumerable<(string? CodigoIbge, string? Nome, string? Uf)>? municipios)
    {
        Result<CamposDaBaseLegalDoFada> validacao = ValidarCampos(tipoInstrumentoCodigo, identificacao, descricao, municipios);
        if (validacao.IsFailure)
        {
            return Result.ValidationFailure(validacao.Errors);
        }

        AplicarCampos(validacao.Value!);
        return Result.Success();
    }

    private void AplicarCampos(CamposDaBaseLegalDoFada campos)
    {
        TipoInstrumento = campos.TipoInstrumento;
        Identificacao = campos.Identificacao;
        Descricao = campos.Descricao;
        _municipios.Clear();
        _municipios.AddRange(campos.Municipios);
    }
}
