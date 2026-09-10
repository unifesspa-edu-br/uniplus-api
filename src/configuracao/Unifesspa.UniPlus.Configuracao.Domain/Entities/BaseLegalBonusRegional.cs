namespace Unifesspa.UniPlus.Configuracao.Domain.Entities;

using System.Collections.Generic;

using Unifesspa.UniPlus.Configuracao.Domain.Enums;
using Unifesspa.UniPlus.Configuracao.Domain.Errors;
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

    private readonly List<BaseLegalBonusRegionalMunicipio> _municipios = [];

    public TipoInstrumentoNormativo TipoInstrumento { get; private set; }
    public string Identificacao { get; private set; } = string.Empty;
    public string Descricao { get; private set; } = string.Empty;
    public IReadOnlyList<BaseLegalBonusRegionalMunicipio> Municipios => _municipios.AsReadOnly();

    public string? CreatedBy { get; private set; }
    public string? UpdatedBy { get; private set; }

    private BaseLegalBonusRegional() { }

    public static Result<(TipoInstrumentoNormativo TipoInstrumento, string Identificacao, string Descricao, IReadOnlyList<BaseLegalBonusRegionalMunicipio> Municipios)> ValidarCampos(
        string? tipoInstrumentoCodigo,
        string? identificacao,
        string? descricao,
        IEnumerable<(string? CodigoIbge, string? Nome, string? Uf)>? municipios)
    {
        List<FieldError> erros = [];

        TipoInstrumentoNormativo tipoInstrumento = TipoInstrumentoNormativoCodigo.FromCodigo(tipoInstrumentoCodigo);
        if (tipoInstrumento == TipoInstrumentoNormativo.Nenhum)
        {
            erros.Add(new("tipoInstrumentoCodigo", new DomainError(
                BaseLegalBonusRegionalErrorCodes.TipoInstrumentoInvalido,
                "Tipo de instrumento normativo inválido ou não informado.")));
        }

        string? identNorm = null;
        if (string.IsNullOrWhiteSpace(identificacao))
        {
            erros.Add(new("identificacao", new DomainError(
                BaseLegalBonusRegionalErrorCodes.IdentificacaoObrigatoria,
                "Identificação é obrigatória.")));
        }
        else
        {
            identNorm = identificacao.Trim();
            if (identNorm.Contains('\0'))
            {
                erros.Add(new("identificacao", new DomainError(
                    BaseLegalBonusRegionalErrorCodes.IdentificacaoCaractereNulo,
                    "Identificação não pode conter o caractere nulo (U+0000).")));
                identNorm = null;
            }
            else if (identNorm.Length is < IdentificacaoMinLength or > IdentificacaoMaxLength)
            {
                erros.Add(new("identificacao", new DomainError(
                    BaseLegalBonusRegionalErrorCodes.IdentificacaoTamanho,
                    $"Identificação deve ter entre {IdentificacaoMinLength} e {IdentificacaoMaxLength} caracteres.")));
                identNorm = null;
            }
        }

        string? descNorm = null;
        if (string.IsNullOrWhiteSpace(descricao))
        {
            erros.Add(new("descricao", new DomainError(
                BaseLegalBonusRegionalErrorCodes.DescricaoObrigatoria,
                "Descrição é obrigatória.")));
        }
        else
        {
            descNorm = descricao.Trim();
            if (descNorm.Contains('\0'))
            {
                erros.Add(new("descricao", new DomainError(
                    BaseLegalBonusRegionalErrorCodes.DescricaoCaractereNulo,
                    "Descrição não pode conter o caractere nulo (U+0000).")));
                descNorm = null;
            }
            else if (descNorm.Length is < DescricaoMinLength or > DescricaoMaxLength)
            {
                erros.Add(new("descricao", new DomainError(
                    BaseLegalBonusRegionalErrorCodes.DescricaoTamanho,
                    $"Descrição deve ter entre {DescricaoMinLength} e {DescricaoMaxLength} caracteres.")));
                descNorm = null;
            }
        }

        List<BaseLegalBonusRegionalMunicipio> municipiosValidos = new List<BaseLegalBonusRegionalMunicipio>();
        if (municipios is null || !municipios.Any())
        {
            erros.Add(new("municipios", new DomainError(
                BaseLegalBonusRegionalErrorCodes.SemMunicipios,
                "A base legal deve abranger pelo menos um município.")));
        }
        else
        {
            int i = 0;
            foreach ((string? CodigoIbge, string? Nome, string? Uf) m in municipios)
            {
                Result valResult = ReferenciaCidadeGeo.Validar(m.CodigoIbge, m.Nome, m.Uf);
                if (valResult.IsFailure)
                {
                    foreach (FieldError err in valResult.Errors)
                    {
                        erros.Add(new($"municipios[{i}]", new DomainError(
                            BaseLegalBonusRegionalErrorCodes.MunicipioInvalido,
                            err.Error.Message)));
                    }
                }
                else
                {
                    municipiosValidos.Add(new BaseLegalBonusRegionalMunicipio(m.CodigoIbge!, m.Nome!, m.Uf!));
                }
                i++;
            }
        }

        if (erros.Count > 0)
        {
            return Result<(TipoInstrumentoNormativo, string, string, IReadOnlyList<BaseLegalBonusRegionalMunicipio>)>.ValidationFailure(erros);
        }

        return Result<(TipoInstrumentoNormativo, string, string, IReadOnlyList<BaseLegalBonusRegionalMunicipio>)>.Success(
            (tipoInstrumento, identNorm!, descNorm!, municipiosValidos));
    }

    public static Result<BaseLegalBonusRegional> Criar(
        string? tipoInstrumentoCodigo,
        string? identificacao,
        string? descricao,
        IEnumerable<(string? CodigoIbge, string? Nome, string? Uf)>? municipios)
    {
        Result<(TipoInstrumentoNormativo TipoInstrumento, string Identificacao, string Descricao, IReadOnlyList<BaseLegalBonusRegionalMunicipio> Municipios)> validacao = ValidarCampos(tipoInstrumentoCodigo, identificacao, descricao, municipios);
        if (validacao.IsFailure)
        {
            return Result<BaseLegalBonusRegional>.ValidationFailure(validacao.Errors);
        }

        BaseLegalBonusRegional baseLegal = new BaseLegalBonusRegional();
        baseLegal.AplicarCampos(validacao.Value);
        return Result<BaseLegalBonusRegional>.Success(baseLegal);
    }

    public Result Atualizar(
        string? tipoInstrumentoCodigo,
        string? identificacao,
        string? descricao,
        IEnumerable<(string? CodigoIbge, string? Nome, string? Uf)>? municipios)
    {
        Result<(TipoInstrumentoNormativo TipoInstrumento, string Identificacao, string Descricao, IReadOnlyList<BaseLegalBonusRegionalMunicipio> Municipios)> validacao = ValidarCampos(tipoInstrumentoCodigo, identificacao, descricao, municipios);
        if (validacao.IsFailure)
        {
            return Result.ValidationFailure(validacao.Errors);
        }

        AplicarCampos(validacao.Value);
        return Result.Success();
    }

    private void AplicarCampos((TipoInstrumentoNormativo TipoInstrumento, string Identificacao, string Descricao, IReadOnlyList<BaseLegalBonusRegionalMunicipio> Municipios) campos)
    {
        TipoInstrumento = campos.TipoInstrumento;
        Identificacao = campos.Identificacao;
        Descricao = campos.Descricao;
        _municipios.Clear();
        _municipios.AddRange(campos.Municipios);
    }
}
