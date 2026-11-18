namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Assinantes;

/// <summary>
/// Quem pode assinar o documento de uma unidade: a pessoa vinculada a ela ou a qualquer unidade acima.
/// A hierarquia decide só quem pode constar no documento; não concede acesso a nada. Sobe a partir da
/// unidade do documento e percorre o caminho até a raiz, mesmo depois de achar a pessoa, para que um ciclo
/// nunca autorize; nega quando a unidade do documento ou uma unidade do caminho está inativa, ou
/// quando a subida acaba na raiz sem achar a unidade da pessoa (a unidade inativa da pessoa nunca é achada). Compara por identificador.
/// </summary>
internal static class AutoridadeParaAssinar
{
    internal static ResultadoDaAutoridade Avaliar(
        Guid unidadeDaPessoa, Guid unidadeDoDocumento, EstruturaDasUnidadesAtivas estrutura)
    {
        ArgumentNullException.ThrowIfNull(estrutura);
        List<Guid> caminho = [];

        return CaminhoAteARaiz(unidadeDoDocumento, estrutura, caminho)
            ?? (caminho.Contains(unidadeDaPessoa) ? ResultadoDaAutoridade.Autorizada : ResultadoDaAutoridade.SemAutoridade);
    }

    // Preenche o caminho da unidade do documento até a raiz; devolve a recusa se ele não chega lá.
    private static ResultadoDaAutoridade? CaminhoAteARaiz(Guid origem, EstruturaDasUnidadesAtivas estrutura, List<Guid> caminho)
    {
        Guid? atual = origem;

        while (atual is { } unidadeId)
        {
            if (RecusaDoPasso(unidadeId, estrutura, caminho, out Guid? superiorId) is { } recusa)
            {
                return recusa;
            }

            caminho.Add(unidadeId);
            atual = superiorId;
        }

        return null;
    }

    private static ResultadoDaAutoridade? RecusaDoPasso(
        Guid unidadeId, EstruturaDasUnidadesAtivas estrutura, List<Guid> caminho, out Guid? superiorId)
    {
        if (!estrutura.TentaObterSuperior(unidadeId, out superiorId))
        {
            return ResultadoDaAutoridade.SemAutoridade;
        }

        return caminho.Contains(unidadeId) ? ResultadoDaAutoridade.HierarquiaComCiclo : null;
    }
}
