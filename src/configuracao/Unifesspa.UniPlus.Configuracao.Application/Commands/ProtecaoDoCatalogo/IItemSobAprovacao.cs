namespace Unifesspa.UniPlus.Configuracao.Application.Commands.ProtecaoDoCatalogo;

using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// O item do catálogo sob o circuito de aprovação da proteção de dados, seja tipo de documento, seja fato: a decisão e
/// a manifestação tratam os dois cadastros do mesmo jeito.
/// </summary>
/// <remarks>
/// Provisório: o lugar deste contrato é o domínio, implementado pelo tipo de documento e pelo fato, quando a divisão do
/// <see cref="FatoCandidato"/> por assunto permitir mexer nele.
/// </remarks>
internal interface IItemSobAprovacao
{
    ItemDaProtecao Item { get; }

    ProtecaoDeDados Protecao { get; }

    AprovacaoDaProtecao Aprovacao { get; }

    /// <summary>Passa o item pendente à solicitação enviada por <paramref name="ator"/>.</summary>
    Result Solicitar(string ator, DateTimeOffset em);

    /// <summary>O item no instante do ato, para o registro do ato.</summary>
    RetratoDoItemDaProtecao Retrato();

    /// <summary>
    /// Aplica a decisão pelas transições do próprio item. A justificativa da aprovação não entra no circuito: fica só no
    /// ato da decisão.
    /// </summary>
    Result Decidir(DecisaoFundamentada decisao, string ator, DateTimeOffset em);
}
