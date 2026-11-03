namespace Unifesspa.UniPlus.Selecao.Domain.Entities;

/// <summary>
/// Uma parte da configuração que pertence a um processo seletivo e guarda a chave dele. Só a raiz
/// vincula, e por isso a implementação é explícita nas filhas: o vínculo não aparece na superfície
/// delas.
/// </summary>
internal interface IParteDoProcesso
{
    void VincularProcesso(Guid processoSeletivoId);
}
