namespace Unifesspa.UniPlus.Configuracao.Domain.Entities;

using System.Collections.Generic;

using Unifesspa.UniPlus.Configuracao.Domain.Enums;
using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Configuracao.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Domain.Cidades;
using Unifesspa.UniPlus.Kernel.Domain.Entities;
using Unifesspa.UniPlus.Kernel.Domain.Interfaces;
using Unifesspa.UniPlus.Kernel.Results;

public sealed class BaseLegalBonusRegional : SoftDeletableEntity, IAuditableEntity
{
    private const int IdentificacaoMinLength = 3;
    private const int IdentificacaoMaxLength = 500;
    private const int DescricaoMinLength = 3;
    private const int DescricaoMaxLength = 2000;

    private static readonly CampoTextual CampoIdentificacao = new(
        "Identificação", IdentificacaoMaxLength,
        BaseLegalBonusRegionalErrorCodes.IdentificacaoTamanho, BaseLegalBonusRegionalErrorCodes.IdentificacaoCaractereInvalido,
        FormaDoTexto.UmaLinha)
    { Minimo = IdentificacaoMinLength, Feminino = true };

    private static readonly CampoTextual CampoDescricao = new(
        "Descrição", DescricaoMaxLength,
        BaseLegalBonusRegionalErrorCodes.DescricaoTamanho, BaseLegalBonusRegionalErrorCodes.DescricaoCaractereInvalido,
        FormaDoTexto.VariasLinhas)
    { Minimo = DescricaoMinLength, Feminino = true };

    private readonly List<BaseLegalBonusRegionalMunicipio> _municipios = [];

    public TipoInstrumentoNormativo TipoInstrumento { get; private set; }
    public string Identificacao { get; private set; } = string.Empty;
    public string Descricao { get; private set; } = string.Empty;
    public IReadOnlyList<BaseLegalBonusRegionalMunicipio> Municipios => _municipios.AsReadOnly();

    public string? CreatedBy { get; private set; }
    public string? UpdatedBy { get; private set; }

    private BaseLegalBonusRegional() { }

    private static Result<CamposDaBaseLegalDoBonus> ValidarCampos(
        string? tipoInstrumentoCodigo, string? identificacao, string? descricao,
        IEnumerable<(string? CodigoIbge, string? Nome, string? Uf)>? municipios)
    {
        List<FieldError> erros = [];
        TipoInstrumentoNormativo tipo = ConferirTipoInstrumento(tipoInstrumentoCodigo, erros);
        string? identNorm = CampoIdentificacao.Obrigatorio(identificacao, "identificacao", BaseLegalBonusRegionalErrorCodes.IdentificacaoObrigatoria, erros);
        string? descNorm = CampoDescricao.Obrigatorio(descricao, "descricao", BaseLegalBonusRegionalErrorCodes.DescricaoObrigatoria, erros);
        IReadOnlyList<BaseLegalBonusRegionalMunicipio> validos = ConferirMunicipios(municipios, erros);

        return erros.Count > 0
            ? Result<CamposDaBaseLegalDoBonus>.ValidationFailure(erros)
            : Result<CamposDaBaseLegalDoBonus>.Success(new(tipo, identNorm!, descNorm!, validos));
    }

    private static TipoInstrumentoNormativo ConferirTipoInstrumento(string? codigo, List<FieldError> erros)
    {
        TipoInstrumentoNormativo tipo = TipoInstrumentoNormativoCodigo.FromCodigo(codigo);
        if (tipo == TipoInstrumentoNormativo.Nenhum)
        {
            erros.Add(new("tipoInstrumento", new DomainError(
                BaseLegalBonusRegionalErrorCodes.TipoInstrumentoInvalido,
                "Tipo de instrumento normativo inválido ou não informado.")));
        }

        return tipo;
    }

    private static IReadOnlyList<BaseLegalBonusRegionalMunicipio> ConferirMunicipios(
        IEnumerable<(string? CodigoIbge, string? Nome, string? Uf)>? municipios, List<FieldError> erros)
    {
        if (municipios is null || !municipios.Any())
        {
            erros.Add(new("municipios", new DomainError(
                BaseLegalBonusRegionalErrorCodes.SemMunicipios,
                "A base legal deve abranger pelo menos um município.")));
            return [];
        }

        return [.. municipios.Select((m, i) => ConferirMunicipio(m, i, erros)).OfType<BaseLegalBonusRegionalMunicipio>()];
    }

    private static BaseLegalBonusRegionalMunicipio? ConferirMunicipio(
        (string? CodigoIbge, string? Nome, string? Uf) municipio, int indice, List<FieldError> erros)
    {
        Result resultado = ReferenciaCidadeGeo.Validar(municipio.CodigoIbge, municipio.Nome, municipio.Uf);
        erros.AddRange(resultado.Errors.Select(erro => new FieldError(
            $"municipios[{indice}]", new DomainError(BaseLegalBonusRegionalErrorCodes.MunicipioInvalido, erro.Error.Message))));
        return resultado.IsFailure
            ? null
            : new BaseLegalBonusRegionalMunicipio(municipio.CodigoIbge!, municipio.Nome!, municipio.Uf!);
    }

    public static Result<BaseLegalBonusRegional> Criar(
        string? tipoInstrumentoCodigo,
        string? identificacao,
        string? descricao,
        IEnumerable<(string? CodigoIbge, string? Nome, string? Uf)>? municipios)
    {
        Result<CamposDaBaseLegalDoBonus> validacao = ValidarCampos(tipoInstrumentoCodigo, identificacao, descricao, municipios);
        if (validacao.IsFailure)
        {
            return Result<BaseLegalBonusRegional>.ValidationFailure(validacao.Errors);
        }

        BaseLegalBonusRegional baseLegal = new BaseLegalBonusRegional();
        baseLegal.AplicarCampos(validacao.Value!);
        return Result<BaseLegalBonusRegional>.Success(baseLegal);
    }

    public Result Atualizar(
        string? tipoInstrumentoCodigo,
        string? identificacao,
        string? descricao,
        IEnumerable<(string? CodigoIbge, string? Nome, string? Uf)>? municipios)
    {
        Result<CamposDaBaseLegalDoBonus> validacao = ValidarCampos(tipoInstrumentoCodigo, identificacao, descricao, municipios);
        if (validacao.IsFailure)
        {
            return Result.ValidationFailure(validacao.Errors);
        }

        AplicarCampos(validacao.Value!);
        return Result.Success();
    }

    private void AplicarCampos(CamposDaBaseLegalDoBonus campos)
    {
        TipoInstrumento = campos.TipoInstrumento;
        Identificacao = campos.Identificacao;
        Descricao = campos.Descricao;
        _municipios.Clear();
        _municipios.AddRange(campos.Municipios);
    }
}
