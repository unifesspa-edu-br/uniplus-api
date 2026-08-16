namespace Unifesspa.UniPlus.Configuracao.Domain.Entities;

using System.Diagnostics.CodeAnalysis;

using Unifesspa.UniPlus.Configuracao.Domain.Enums;
using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Kernel.Domain.Cidades;
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
/// deve coincidir com a referência de cidade do local.</para>
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
    /// <paramref name="endereco"/> já chega validado (construído pelo handler via
    /// <see cref="ReferenciaEnderecoGeo.Criar"/>).
    /// </summary>
    [SuppressMessage(
        "Design",
        "CA1062:Validate arguments of public methods",
        Justification = "Nulo em cidadeCodigoIbge/cidadeNome/cidadeUf é violação de campo (\"obrigatório\"), " +
            "não bug de contrato — ValidarCampos trata via IsNullOrWhiteSpace e devolve " +
            "Result.ValidationFailure, não ArgumentNullException (ADR-0125: domínio é fonte única de " +
            "validação, sem validator FluentValidation garantindo não-nulo a montante).")]
    public static Result<LocalOferta> Criar(
        TipoLocalOferta tipo,
        Guid? campusResponsavelId,
        string? cidadeCodigoIbge,
        string? cidadeNome,
        string? cidadeUf,
        string? cidadeOrigem,
        DateTimeOffset? cidadeDisplayAtualizadoEm,
        ReferenciaEnderecoGeo? endereco,
        string? codigoEmec)
    {
        Result validacao = ValidarCampos(tipo, cidadeCodigoIbge, cidadeNome, cidadeUf, endereco, codigoEmec);
        if (validacao.IsFailure)
        {
            return Result<LocalOferta>.ValidationFailure(validacao.Errors);
        }

        var local = new LocalOferta();
        local.AplicarCampos(
            tipo, campusResponsavelId, cidadeCodigoIbge!, cidadeNome!, cidadeUf!, cidadeOrigem,
            cidadeDisplayAtualizadoEm, endereco, codigoEmec);

        return Result<LocalOferta>.Success(local);
    }

    /// <summary>
    /// Atualiza os atributos do Local de Oferta. A existência do campus
    /// responsável (quando informado) é responsabilidade do handler.
    /// </summary>
    [SuppressMessage(
        "Design",
        "CA1062:Validate arguments of public methods",
        Justification = "Ver justificativa equivalente em Criar.")]
    public Result Atualizar(
        TipoLocalOferta tipo,
        Guid? campusResponsavelId,
        string? cidadeCodigoIbge,
        string? cidadeNome,
        string? cidadeUf,
        string? cidadeOrigem,
        DateTimeOffset? cidadeDisplayAtualizadoEm,
        ReferenciaEnderecoGeo? endereco,
        string? codigoEmec)
    {
        Result validacao = ValidarCampos(tipo, cidadeCodigoIbge, cidadeNome, cidadeUf, endereco, codigoEmec);
        if (validacao.IsFailure)
        {
            return validacao;
        }

        AplicarCampos(
            tipo, campusResponsavelId, cidadeCodigoIbge!, cidadeNome!, cidadeUf!, cidadeOrigem,
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

    /// <summary>
    /// Valida tipo, referência de cidade, coerência cidade↔endereço e código
    /// e-MEC, acumulando toda violação independente em vez de parar na primeira
    /// (ADR-0125) — sem mutar nada e sem depender de I/O. Público para o handler
    /// de atualização poder validar o payload inteiro antes de buscar o registro
    /// por Id (validação sempre vence 404); o <paramref name="endereco"/> já
    /// chega resolvido (ou nulo, quando a resolução falhou — a coerência não é
    /// checada nesse caso, para não mascarar a causa raiz).
    /// </summary>
    public static Result ValidarCampos(
        TipoLocalOferta tipo,
        string? cidadeCodigoIbge,
        string? cidadeNome,
        string? cidadeUf,
        ReferenciaEnderecoGeo? endereco,
        string? codigoEmec)
    {
        List<FieldError> erros = [];

        if (!Enum.IsDefined(tipo) || tipo == TipoLocalOferta.Nenhum)
        {
            erros.Add(new("tipo", new DomainError(
                LocalOfertaErrorCodes.TipoInvalido,
                "Tipo de Local de Oferta inválido — use um valor definido em TipoLocalOferta, diferente de Nenhum.")));
        }

        Result cidade = ReferenciaCidadeGeo.Validar(cidadeCodigoIbge, cidadeNome, cidadeUf);
        if (cidade.IsFailure)
        {
            // ReferenciaCidadeGeo.Validar acumula toda violação independente do
            // trio de cidade — cada uma mapeada para o campo real que descreve.
            foreach (FieldError erroCidade in cidade.Errors)
            {
                erros.Add(new(CampoDaCidade(erroCidade.Error.Code), erroCidade.Error));
            }
        }
        else
        {
            // Só faz sentido checar coerência cidade↔endereço quando a referência
            // de cidade em si já é válida — senão a causa raiz (formato) fica
            // mascarada por "incoerente".
            Result coerencia = ReferenciaEnderecoGeo.ValidarCoerencia(
                endereco?.CidadeCodigoIbge, endereco?.CidadeUf, cidadeCodigoIbge, cidadeUf);
            if (coerencia.IsFailure)
            {
                erros.Add(new("endereco", coerencia.Error!));
            }
        }

        if (codigoEmec is not null && codigoEmec.Trim().Length > CodigoEmecMaxLength)
        {
            erros.Add(new("codigoEmec", new DomainError(
                LocalOfertaErrorCodes.CodigoEmecTamanho,
                $"Código e-MEC do Local de Oferta deve ter no máximo {CodigoEmecMaxLength} caracteres.")));
        }

        return erros.Count == 0 ? Result.Success() : Result.ValidationFailure(erros);
    }

    /// <summary>
    /// Mapeia o código interno de <see cref="ReferenciaCidadeGeo.Validar"/> para o
    /// campo do payload (camelCase, ADR-0023) a que ele se refere de fato — sem
    /// isso, todo erro de cidade seria rotulado com o mesmo campo, mesmo quando a
    /// causa é o nome ou a UF, não o código IBGE.
    /// </summary>
    private static string CampoDaCidade(string codigoErro) => codigoErro switch
    {
        CidadeReferenciaErrorCodes.NomeObrigatorio
            or CidadeReferenciaErrorCodes.NomeCaractereNulo
            or CidadeReferenciaErrorCodes.NomeTamanho => "cidadeNome",
        CidadeReferenciaErrorCodes.UfObrigatoria
            or CidadeReferenciaErrorCodes.UfIncoerente => "cidadeUf",
        _ => "cidadeCodigoIbge",
    };
}
