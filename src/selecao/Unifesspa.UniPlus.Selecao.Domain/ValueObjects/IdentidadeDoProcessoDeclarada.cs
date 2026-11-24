namespace Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

using Unifesspa.UniPlus.Selecao.Domain.Enums;

/// <summary>
/// De quem é o processo: de onde vêm os candidatos, o identificador legível, o tipo e a unidade que o administra. A declaração que
/// vem de um documento de rascunho pode estar sem o tipo, sem a unidade administradora ou com a origem dos candidatos ausente:
/// a identificação incompleta existe como declaração, e a regra de completude a recusa ao publicar.
/// </summary>
public sealed class IdentidadeDoProcessoDeclarada
{
    /// <summary>O campo do tipo de processo, pelo nome que o documento da configuração lhe dá.</summary>
    public const string CampoDoTipoDeProcesso = "identificacao.tipoProcessoCodigo";

    /// <summary>O campo da unidade administradora, pelo nome que o documento da configuração lhe dá.</summary>
    public const string CampoDaUnidadeAdministradora = "identificacao.unidadeAdministradoraCodigo";

    /// <summary>O campo da origem dos candidatos, pelo nome que o documento da configuração lhe dá.</summary>
    public const string CampoDaOrigemDosCandidatos = "identificacao.origemCandidatos";

    /// <summary>O campo da localidade que rege a contagem dos prazos, pelo nome que o documento da configuração lhe dá.</summary>
    public const string CampoDaLocalidade = "identificacao.localidadeCodigoIbge";

    /// <summary>O campo do identificador legível, pelo nome que o documento da configuração lhe dá.</summary>
    public const string CampoDoIdentificadorLegivel = "identificacao.identificadorLegivel";

    /// <summary>O campo da convenção de contagem dos prazos, pelo nome que o documento da configuração lhe dá.</summary>
    public const string CampoDaConvencaoDeContagem = "identificacao.algoritmoContagemPrazo";

    internal IdentidadeDoProcessoDeclarada(
        string? nome,
        OrigemCandidatos? origemCandidatos,
        IdentificadorLegivel? identificadorLegivel,
        TipoProcessoSnapshot? tipoProcesso,
        UnidadeAdministradoraSnapshot? unidadeAdministradora)
    {
        Nome = nome;
        OrigemCandidatos = origemCandidatos;
        IdentificadorLegivel = identificadorLegivel;
        TipoProcesso = tipoProcesso;
        UnidadeAdministradora = unidadeAdministradora;
    }

    /// <summary>O nome do processo como o documento o traz; a leitura o copia, sem conferi-lo.</summary>
    public string? Nome { get; }

    public OrigemCandidatos? OrigemCandidatos { get; }

    public IdentificadorLegivel? IdentificadorLegivel { get; }

    public TipoProcessoSnapshot? TipoProcesso { get; }

    public UnidadeAdministradoraSnapshot? UnidadeAdministradora { get; }
}
