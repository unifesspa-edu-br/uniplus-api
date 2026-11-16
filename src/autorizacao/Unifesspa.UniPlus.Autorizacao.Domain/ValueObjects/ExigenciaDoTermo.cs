namespace Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;

using Unifesspa.UniPlus.Autorizacao.Domain.Entities;
using Unifesspa.UniPlus.Autorizacao.Domain.Interfaces;

/// <summary>Quando o perfil do pedido exige o termo de responsabilidade (ADR-0162).</summary>
public static class ExigenciaDoTermo
{
    /// <summary>
    /// O perfil exige o termo: a composição em vigor tem permissão pessoal, identificadora ou sensível, ou o perfil é de
    /// governança, que exige o termo mesmo que uma recomposição futura o deixe sem permissão pessoal (UNI-REQ-0377).
    /// </summary>
    public static bool Do(Perfil perfil, ICatalogoDePermissoesConsultavel catalogo)
    {
        ArgumentNullException.ThrowIfNull(perfil);

        return perfil.Governanca is not null || perfil.Protegido(catalogo);
    }

    /// <summary>O perfil que o aprovador avaliou exige o termo: a segunda etapa de aprovação ou a governança.</summary>
    public static bool Do(PerfilAvaliado perfil)
    {
        ArgumentNullException.ThrowIfNull(perfil);

        return perfil.ExigeAprovacaoDeProtecaoDeDados || perfil.Governanca is not null;
    }
}
