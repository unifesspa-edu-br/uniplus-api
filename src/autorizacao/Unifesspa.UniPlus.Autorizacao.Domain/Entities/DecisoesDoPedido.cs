namespace Unifesspa.UniPlus.Autorizacao.Domain.Entities;

using System.Collections.ObjectModel;

using Unifesspa.UniPlus.Autorizacao.Domain.Enums;
using Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;

/// <summary>
/// As decisões de um pedido e o que se pergunta a elas: quem aprovou como gestor, qual foi a última aprovação dele e se o
/// perfil que ele avaliou é o de agora. É o campo que o agregado guarda e o EF preenche.
/// </summary>
internal sealed class DecisoesDoPedido : Collection<DecisaoDoPedido>
{
    internal bool AprovouComoGestor(Identidade decisor) => Items.Any(d =>
        d.Etapa == EtapaDoPedido.Gestor && d.Resultado == ResultadoDaDecisao.Aprovado && d.Decisor == decisor);

    internal bool MesmoPerfilQueOGestorAvaliou(PerfilAvaliado perfil) =>
        UltimaAprovacaoDoGestor is { } gestor
        && gestor.PermissoesAvaliadas.SequenceEqual(perfil.Permissoes)
        && gestor.SensibilidadeAvaliada == perfil.Sensibilidade;

    // A mais recente é a de maior instante e, no mesmo instante, a registrada por último; relógio que regride vale pelo instante.
    private DecisaoDoPedido? UltimaAprovacaoDoGestor => Items
        .Where(d => d.Etapa == EtapaDoPedido.Gestor && d.Resultado == ResultadoDaDecisao.Aprovado)
        .OrderBy(d => d.Instante)
        .ThenBy(d => d.PosicaoNoInstante)
        .LastOrDefault();
}
