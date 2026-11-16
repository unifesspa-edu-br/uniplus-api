namespace Unifesspa.UniPlus.Autorizacao.Domain.Entities;

using Unifesspa.UniPlus.Autorizacao.Domain.Enums;
using Unifesspa.UniPlus.Autorizacao.Domain.Errors;
using Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>A aprovação que um decisor tenta na etapa corrente do pedido, com o perfil que ele avaliou.</summary>
internal sealed record AprovacaoNaEtapa(
    Identidade Decisor,
    EtapaDoPedido Etapa,
    PerfilAvaliado Perfil,
    ContextoDaAprovacao Contexto,
    DateTimeOffset Instante,
    DateTimeOffset? ValidaAte)
{
    internal AtoDoAgente Ato => new(Decisor, Instante);

    /// <summary>O motivo de a segunda aprovação ser dispensada, dado só na etapa do gestor.</summary>
    internal string? Dispensa => Etapa == EtapaDoPedido.Gestor ? Contexto.MotivoDaDispensa(Perfil) : null;

    /// <summary>Esta é a última aprovação exigida: a da etapa seguinte não existe, ou foi dispensada, ou o perfil não a exige.</summary>
    internal bool Conclui => Etapa != EtapaDoPedido.Gestor || Dispensa is not null || !Perfil.ExigeAprovacaoDeProtecaoDeDados;

    internal DesfechoDaAprovacao Desfecho => Conclui ? DesfechoDaAprovacao.Concluido : DesfechoDaAprovacao.AguardaProtecaoDeDados;

    internal List<FieldError> Recusas(SegregacaoDoPedido segregacao, DateTimeOffset? validaAteDoPedido)
    {
        List<FieldError> erros = [];
        segregacao.Conferir(Decisor, Etapa, erros);

        if (!Perfil.Ativo)
        {
            erros.Add(new("perfilId", new DomainError(
                PedidoErrorCodes.PerfilInativo,
                "O perfil de acesso está desativado e não aceita aprovação.")));
        }

        ValidadeDoPedido.ConferirNaEtapa(this, validaAteDoPedido, erros);
        return erros;
    }

    // Nenhuma etapa concede o que a anterior não avaliou: o gestor decide de novo sobre o perfil de agora, e o prazo que
    // venceu volta ao gestor porque só ele o define. No perfil que passou a exigir o termo depois da criação, o pedido que
    // não tem termo fixado é devolvido.
    internal DevolucaoNaAprovacao? Devolucao(DecisoesDoPedido decisoes, AndamentoDoPedido andamento, TermoFixado? termo) =>
        Etapa == EtapaDoPedido.Gestor
            ? DevolucaoPeloTermo(termo)
            : DevolucaoPeloPerfilOuPeloPrazo(decisoes, andamento);

    private DevolucaoNaAprovacao? DevolucaoPeloTermo(TermoFixado? termo) =>
        termo is null && ExigenciaDoTermo.Do(Perfil) ? DevolucaoNaAprovacao.TermoExigido() : null;

    private DevolucaoNaAprovacao? DevolucaoPeloPerfilOuPeloPrazo(DecisoesDoPedido decisoes, AndamentoDoPedido andamento)
    {
        if (!decisoes.MesmoPerfilQueOGestorAvaliou(Perfil))
        {
            return DevolucaoNaAprovacao.PerfilAlterado();
        }

        return andamento.ValidaAte is { } fim && fim <= Instante ? DevolucaoNaAprovacao.PrazoVencido() : null;
    }
}
