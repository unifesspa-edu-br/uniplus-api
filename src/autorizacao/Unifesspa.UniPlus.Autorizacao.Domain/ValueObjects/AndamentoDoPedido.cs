namespace Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;

using Unifesspa.UniPlus.Autorizacao.Domain.Entities;
using Unifesspa.UniPlus.Autorizacao.Domain.Enums;
using Unifesspa.UniPlus.Autorizacao.Domain.Errors;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Onde o pedido está: a situação, o fim de vigência que o gestor definiu e, nos estados terminais, o encerramento. Cada
/// transição devolve o andamento novo, e o pedido o troca por inteiro.
/// </summary>
public sealed record AndamentoDoPedido
{
    /// <summary>As situações em que o pedido ainda pode avançar: o mesmo pedido não se repete enquanto numa delas.</summary>
    public static readonly IReadOnlyList<SituacaoDoPedido> SituacoesAtivas =
    [
        SituacaoDoPedido.AguardandoDocumento,
        SituacaoDoPedido.AguardandoBeneficiario,
        SituacaoDoPedido.AguardandoGestor,
        SituacaoDoPedido.AguardandoProtecaoDeDados,
    ];

    public SituacaoDoPedido Situacao { get; private init; }

    /// <summary>O fim de vigência que o gestor definiu ao aprovar e que a concessão herda; nulo, sem prazo.</summary>
    public DateTimeOffset? ValidaAte { get; private init; }

    public DateTimeOffset? EncerradoEm { get; private init; }

    public bool Ativo => SituacoesAtivas.Contains(Situacao);

    /// <summary>A etapa que o pedido aguarda; nula quando ele não espera aprovador.</summary>
    public EtapaDoPedido? EtapaAtual => Situacao switch
    {
        SituacaoDoPedido.AguardandoGestor => EtapaDoPedido.Gestor,
        SituacaoDoPedido.AguardandoProtecaoDeDados => EtapaDoPedido.ProtecaoDeDados,
        _ => null,
    };

    // Construtor de materialização do EF Core.
    private AndamentoDoPedido()
    {
    }

    internal static AndamentoDoPedido Inicial(SituacaoDoPedido situacao) => new() { Situacao = situacao };

    internal AndamentoDoPedido Em(SituacaoDoPedido situacao) => this with { Situacao = situacao };

    internal AndamentoDoPedido Encerrado(SituacaoDoPedido situacao, DateTimeOffset instante) =>
        this with { Situacao = situacao, EncerradoEm = instante };

    internal AndamentoDoPedido ComPrazo(DateTimeOffset? validaAte) => this with { ValidaAte = validaAte?.ToUniversalTime() };

    /// <summary>Volta a aguardar o gestor, único que define o prazo: o prazo dado antes se perde.</summary>
    internal AndamentoDoPedido DeVoltaAoGestor() => this with { Situacao = SituacaoDoPedido.AguardandoGestor, ValidaAte = null };

    internal DesfechoDaTrocaDeVersao DesfechoDaTroca(bool comAcrescimo)
    {
        if (comAcrescimo)
        {
            return DesfechoDaTrocaDeVersao.Devolvido;
        }

        return Situacao == SituacaoDoPedido.AguardandoProtecaoDeDados
            ? DesfechoDaTrocaDeVersao.VoltouAoGestor
            : DesfechoDaTrocaDeVersao.VersaoAtualizada;
    }

    internal DomainError? RecusaDoAceite() => Situacao switch
    {
        SituacaoDoPedido.AguardandoBeneficiario => null,
        SituacaoDoPedido.AguardandoDocumento => new DomainError(
            PedidoErrorCodes.AnexoNaoVerificado,
            "O documento declarado ainda não foi verificado; o aceite e a decisão esperam a verificação."),
        _ => new DomainError(PedidoErrorCodes.SituacaoInvalida, "O pedido não aguarda o aceite do beneficiário."),
    };

    /// <summary>Só o gestor define o prazo; depois da aprovação o pedido se encerra, ou passa a aguardar a proteção de dados.</summary>
    internal AndamentoDoPedido DepoisDaAprovacao(AprovacaoNaEtapa aprovacao)
    {
        AndamentoDoPedido comPrazo = aprovacao.Etapa == EtapaDoPedido.Gestor ? ComPrazo(aprovacao.ValidaAte) : this;

        return aprovacao.Conclui
            ? comPrazo.Encerrado(SituacaoDoPedido.Aprovado, aprovacao.Instante)
            : comPrazo.Em(SituacaoDoPedido.AguardandoProtecaoDeDados);
    }

    internal AndamentoDoPedido DepoisDaTroca(DesfechoDaTrocaDeVersao desfecho, DateTimeOffset instante) => desfecho switch
    {
        DesfechoDaTrocaDeVersao.Devolvido => Encerrado(SituacaoDoPedido.Devolvido, instante),
        DesfechoDaTrocaDeVersao.VoltouAoGestor => DeVoltaAoGestor(),
        _ => this,
    };
}
