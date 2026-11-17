namespace Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

using Unifesspa.UniPlus.Selecao.Domain.Enums;

/// <summary>De quem é o processo: de onde vêm os candidatos, o identificador legível, o tipo e a unidade que o administra.</summary>
public sealed class IdentidadeDoProcessoDeclarada
{
    internal IdentidadeDoProcessoDeclarada(
        OrigemCandidatos origemCandidatos,
        IdentificadorLegivel? identificadorLegivel,
        TipoProcessoSnapshot tipoProcesso,
        UnidadeAdministradoraSnapshot unidadeAdministradora)
    {
        OrigemCandidatos = origemCandidatos;
        IdentificadorLegivel = identificadorLegivel;
        TipoProcesso = tipoProcesso;
        UnidadeAdministradora = unidadeAdministradora;
    }

    public OrigemCandidatos OrigemCandidatos { get; }

    public IdentificadorLegivel? IdentificadorLegivel { get; }

    public TipoProcessoSnapshot TipoProcesso { get; }

    public UnidadeAdministradoraSnapshot UnidadeAdministradora { get; }
}
