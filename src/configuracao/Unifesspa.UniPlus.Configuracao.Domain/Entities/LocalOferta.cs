namespace Unifesspa.UniPlus.Configuracao.Domain.Entities;

using Unifesspa.UniPlus.Configuracao.Domain.Enums;
using Unifesspa.UniPlus.Configuracao.Domain.ValueObjects;
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
/// <para>O congelamento (snapshot do congelamento dos parâmetros na publicação, UNI-REQ-0019) é responsabilidade do Processo Seletivo
/// (módulo Selecao, via oferta de curso congelada — ADR-0061); não há colunas
/// de snapshot aqui.</para>
/// </remarks>
public sealed class LocalOferta : SoftDeletableEntity, IAuditableEntity
{
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
    /// Cria um novo Local de Oferta a partir da <paramref name="declaracao"/> da gestão. Valida tipo, referência de cidade,
    /// coerência cidade↔endereço e código e-MEC, acumulando as recusas. A cidade nasce com a proveniência do cadastro de
    /// municípios e o instante <paramref name="agora"/>. A existência do campus responsável (quando informado) é
    /// responsabilidade do handler; o endereço da declaração já chega validado (construído via
    /// <see cref="ReferenciaEnderecoGeo.Criar"/>).
    /// </summary>
    public static Result<LocalOferta> Criar(DeclaracaoDoLocalDeOferta declaracao, DateTimeOffset agora)
    {
        Result<CamposDoLocalDeOferta> campos = Conferir(declaracao);
        if (campos.IsFailure)
        {
            return Result<LocalOferta>.ValidationFailure(campos.Errors);
        }

        var local = new LocalOferta();
        local.AplicarCampos(campos.Value!);
        local.RenovarProcedenciaDaCidade(agora);

        return Result<LocalOferta>.Success(local);
    }

    /// <summary>
    /// Atualiza os atributos do Local de Oferta a partir da <paramref name="declaracao"/>. A proveniência da cidade só é
    /// renovada com <paramref name="agora"/> quando o código IBGE, o nome ou a UF mudam; qualquer outro campo preserva o
    /// instante em que a cidade foi copiada. A existência do campus responsável (quando informado) é responsabilidade do
    /// handler.
    /// </summary>
    public Result Atualizar(DeclaracaoDoLocalDeOferta declaracao, DateTimeOffset agora)
    {
        Result<CamposDoLocalDeOferta> campos = Conferir(declaracao);
        if (campos.IsFailure)
        {
            return Result.ValidationFailure(campos.Errors);
        }

        if (ReferenciaDaCidadeMudou(campos.Value!))
        {
            RenovarProcedenciaDaCidade(agora);
        }

        AplicarCampos(campos.Value!);

        return Result.Success();
    }

    /// <summary>
    /// Confere a declaração sem construir nem mutar nada, para o handler de atualização recusar o payload inteiro antes de
    /// buscar o registro por Id (validação sempre vence 404). <see cref="Criar"/> e <see cref="Atualizar"/> revalidam por
    /// conta própria: este método só antecipa a mesma checagem.
    /// </summary>
    public static Result ValidarDeclaracao(DeclaracaoDoLocalDeOferta declaracao)
    {
        Result<CamposDoLocalDeOferta> campos = Conferir(declaracao);

        return campos.IsFailure ? Result.ValidationFailure(campos.Errors) : Result.Success();
    }

    private static Result<CamposDoLocalDeOferta> Conferir(DeclaracaoDoLocalDeOferta declaracao)
    {
        ArgumentNullException.ThrowIfNull(declaracao);

        return new ValidacaoDoLocalDeOferta(declaracao).Resolver();
    }

    private bool ReferenciaDaCidadeMudou(CamposDoLocalDeOferta campos) =>
        !string.Equals(campos.CidadeCodigoIbge, CidadeCodigoIbge, StringComparison.Ordinal)
        || !string.Equals(campos.CidadeNome, CidadeNome, StringComparison.Ordinal);

    private void RenovarProcedenciaDaCidade(DateTimeOffset agora)
    {
        CidadeOrigem = ReferenciaCidadeGeo.OrigemGeoApi;
        CidadeDisplayAtualizadoEm = agora;
    }

    private void AplicarCampos(CamposDoLocalDeOferta campos)
    {
        Tipo = campos.Tipo;
        CampusResponsavelId = campos.CampusResponsavelId;
        CidadeCodigoIbge = campos.CidadeCodigoIbge;
        CidadeNome = campos.CidadeNome;
        CidadeUf = campos.CidadeUf;
        Endereco = campos.Endereco;
        CodigoEmec = campos.CodigoEmec;
    }
}
