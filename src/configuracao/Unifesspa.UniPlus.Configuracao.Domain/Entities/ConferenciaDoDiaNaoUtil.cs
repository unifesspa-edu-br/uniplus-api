namespace Unifesspa.UniPlus.Configuracao.Domain.Entities;

using Unifesspa.UniPlus.Configuracao.Domain.Enums;
using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Kernel.Domain.Cidades;
using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Confere, sem I/O, um dia não útil declarado e devolve os campos já normalizados, ou todas as recusas de uma vez, cada
/// uma rotulada pelo campo declarado. A conferência que depende de outra (município e UF contra a abrangência) só roda
/// quando a abrangência foi reconhecida: do contrário a recusa seria derivada de um valor inválido, e não uma
/// incoerência independente.
/// </summary>
internal sealed class ConferenciaDoDiaNaoUtil
{
    private const int DescricaoMaxLength = 200;

    private readonly DiaNaoUtilCriacao _dia;
    private readonly string _campo;
    private readonly List<FieldError> _erros = [];
    private readonly string? _municipioIbge;
    private readonly string? _municipioNome;
    private readonly string? _municipioUf;
    private readonly string? _uf;
    private readonly string _descricao;
    private readonly Abrangencia? _abrangencia;

    private ConferenciaDoDiaNaoUtil(DiaNaoUtilCriacao dia, string campo)
    {
        _dia = dia;
        _campo = campo;
        _abrangencia = CatalogoDe<Abrangencia>.TentarObter(dia.Abrangencia, out Abrangencia abrangencia) ? abrangencia : null;
        _municipioIbge = AparadoOuAusente(dia.MunicipioIbge);
        _municipioNome = AparadoOuAusente(dia.MunicipioNome);
        _municipioUf = AparadoOuAusente(dia.MunicipioUf)?.ToUpperInvariant();
        _uf = AparadoOuAusente(dia.Uf)?.ToUpperInvariant();
        _descricao = (dia.Descricao ?? string.Empty).Trim();
    }

    /// <summary>
    /// Confere o dia declarado. <paramref name="campo"/> é o rótulo do item nas recusas (<c>"diasNaoUteis[i]"</c> dentro
    /// do calendário, <c>"item"</c> na inclusão pontual); os subcampos vão em camelCase (ADR-0023).
    /// </summary>
    public static Result<DiaNaoUtilResolvido> Conferir(DiaNaoUtilCriacao? dia, string campo)
    {
        if (dia is null)
        {
            return Result<DiaNaoUtilResolvido>.ValidationFailure([new FieldError(campo, new DomainError(
                CalendarioDiasUteisErrorCodes.DiaNaoUtilNulo, "Item de dia não útil não pode ser nulo."))]);
        }

        return new ConferenciaDoDiaNaoUtil(dia, campo).Resolver();
    }

    private Result<DiaNaoUtilResolvido> Resolver()
    {
        ConferirData();
        ConferirAbrangencia();
        ConferirReferenciasDaAbrangencia();
        ConferirDescricao();

        return _erros.Count > 0
            ? Result<DiaNaoUtilResolvido>.ValidationFailure(_erros)
            : Result<DiaNaoUtilResolvido>.Success(new DiaNaoUtilResolvido(
                _abrangencia!.Value, _municipioIbge, _municipioNome, _municipioUf, _uf, _dia.Data, _descricao));
    }

    private void ConferirData()
    {
        // O model binding do ASP.NET Core não recusa "data" ausente no JSON
        // (RespectRequiredConstructorParameters desabilitado): um payload sem
        // o campo materializa DateOnly.MinValue (0001-01-01) em vez de falhar
        // no bind. Sem esta checagem, essa data passaria para o agregado, e um
        // calendário vigente com ela seria irreconstruível na publicação —
        // DiaNaoUtilCongelado.Criar (Selecao) recusa o mesmo default (mesma
        // razão), então o defeito só apareceria ali, longe de onde nasceu.
        if (_dia.Data == default)
        {
            Recusar("data", CalendarioDiasUteisErrorCodes.DataAusente,
                "A data do dia não útil não pode ser o valor default (0001-01-01) — é assim que uma data omitida se materializa.");
        }
    }

    private void ConferirAbrangencia()
    {
        if (_abrangencia is null)
        {
            Recusar("abrangencia", CalendarioDiasUteisErrorCodes.AbrangenciaInvalida,
                "Abrangência inválida. Deve ser uma de: "
                + string.Join(", ", CatalogoDe<Abrangencia>.Todos().Select(static declarado => declarado.Codigo)) + ".");
        }
    }

    private void ConferirReferenciasDaAbrangencia()
    {
        if (_abrangencia is not null)
        {
            ConferirMunicipio();
            ConferirUf();
        }
    }

    private void ConferirMunicipio()
    {
        if (_abrangencia == Abrangencia.Municipal)
        {
            ConferirReferenciaDoMunicipio();
        }
        else if (_municipioIbge is not null || _municipioNome is not null || _municipioUf is not null)
        {
            _erros.Add(new(_campo, new DomainError(
                CalendarioDiasUteisErrorCodes.SnapshotMunicipalApenasParaMunicipal,
                "Referência do município só se aplica a abrangência municipal.")));
        }
    }

    private void ConferirReferenciaDoMunicipio()
    {
        Result referencia = ReferenciaCidadeGeo.Validar(_municipioIbge, _municipioNome, _municipioUf);
        if (referencia.IsFailure)
        {
            foreach (FieldError recusa in referencia.Errors)
            {
                _erros.Add(new($"{_campo}.{CampoDoMunicipio(recusa.Error.Code)}", recusa.Error));
            }
        }
    }

    private void ConferirUf()
    {
        if (_abrangencia == Abrangencia.Estadual)
        {
            ConferirUfDoEstado();
        }
        else if (_uf is not null)
        {
            Recusar("uf", CalendarioDiasUteisErrorCodes.UfApenasParaEstadual, "UF só se aplica a abrangência estadual.");
        }
    }

    private void ConferirUfDoEstado()
    {
        if (_uf is null)
        {
            Recusar("uf", CalendarioDiasUteisErrorCodes.UfObrigatoriaParaEstadual,
                "UF é obrigatória para abrangência estadual — sem ela, feriados de estados "
                + "diferentes seriam indistinguíveis.");
        }
        else if (_uf.Length != ReferenciaCidadeGeo.UfLength || !ReferenciaCidadeGeo.EhUfValida(_uf))
        {
            Recusar("uf", CalendarioDiasUteisErrorCodes.UfFormatoInvalido, "UF deve ser uma das 27 siglas válidas.");
        }
    }

    private void ConferirDescricao()
    {
        if (_descricao.Length == 0)
        {
            Recusar("descricao", CalendarioDiasUteisErrorCodes.DescricaoObrigatoria, "Descrição é obrigatória.");
        }
        else if (_descricao.Length > DescricaoMaxLength)
        {
            Recusar("descricao", CalendarioDiasUteisErrorCodes.DescricaoTamanho,
                $"Descrição deve ter no máximo {DescricaoMaxLength} caracteres.");
        }
    }

    private void Recusar(string subcampo, string codigo, string mensagem) =>
        _erros.Add(new($"{_campo}.{subcampo}", new DomainError(codigo, mensagem)));

    private static string? AparadoOuAusente(string? texto) =>
        string.IsNullOrWhiteSpace(texto) ? null : texto.Trim();

    /// <summary>
    /// Mapeia o código interno de <see cref="ReferenciaCidadeGeo.Validar"/> para o subcampo (camelCase, ADR-0023) do dia
    /// não útil a que ele se refere de fato — sem isso, todo erro de município seria rotulado com o mesmo campo, mesmo
    /// quando a causa é o nome ou a UF, não o código IBGE.
    /// </summary>
    private static string CampoDoMunicipio(string codigoErro) => codigoErro switch
    {
        CidadeReferenciaErrorCodes.NomeObrigatorio
            or CidadeReferenciaErrorCodes.NomeCaractereInvalido
            or CidadeReferenciaErrorCodes.NomeTamanho => "municipioNome",
        CidadeReferenciaErrorCodes.UfObrigatoria
            or CidadeReferenciaErrorCodes.UfIncoerente => "municipioUf",
        _ => "municipioIbge",
    };
}
