namespace Unifesspa.UniPlus.Selecao.Application.Services.InscricaoAdministrativa;

using Unifesspa.UniPlus.Authorization.Projecao;
using Unifesspa.UniPlus.Selecao.Application.DTOs.Administrativos;

/// <summary>
/// O que a pessoa pode ver da inscrição: as decisões por classe de dado e de arquivo e se pode revelar o identificador. Os campos fixos da inscrição saem pela permissão da própria propriedade.
/// </summary>
/// <param name="Decisoes">As decisões das classes permitidas, já registradas.</param>
/// <param name="PodeRevelar">Se, além de consultar o identificador mascarado, a pessoa pode revelar o valor completo.</param>
public sealed record ProjecaoDaInscricao(DecisoesDaProjecao Decisoes, bool PodeRevelar)
{
    /// <summary>Se a pessoa pode ver a propriedade fixa da inscrição.</summary>
    public bool Permite(string propriedade) => Decisoes.PermitePropriedade<InscricaoAdministrativaDto>(propriedade);

    /// <summary>O campo fixo de valor, ou nulo quando a pessoa não pode ver a propriedade.</summary>
    public T? Fixo<T>(string propriedade, T? valor)
        where T : struct => Permite(propriedade) ? valor : null;

    /// <summary>O campo fixo de texto, ou nulo quando a pessoa não pode ver a propriedade.</summary>
    public string? Fixo(string propriedade, string? valor) => Permite(propriedade) ? valor : null;
}
