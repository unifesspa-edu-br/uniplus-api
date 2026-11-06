namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Classificacao;

using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Edicao;

/// <summary>O que a troca da classificação faz além de gravá-la: as edições do ENEM que deixam de existir.</summary>
internal sealed record PlanoDaClassificacao(
    ConfiguracaoClassificacao Classificacao,
    IReadOnlyList<OpcaoDeclaradaFato> EdicoesDoEnemARetirar)
{
    internal void RetirarEdicoes(ColecoesDoProcesso colecoes)
    {
        ArgumentNullException.ThrowIfNull(colecoes);

        foreach (OpcaoDeclaradaFato edicao in EdicoesDoEnemARetirar)
        {
            colecoes.OpcoesDeclaradas.Remove(edicao);
        }
    }
}
