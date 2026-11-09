namespace Unifesspa.UniPlus.Autorizacao.Domain.Entities;

using Unifesspa.UniPlus.Autorizacao.Domain.Enums;

/// <summary>A versão do perfil que o pedido segue e o que acontece a ele quando outra versão entra em vigor.</summary>
public sealed partial class PedidoDeConcessao
{
    private readonly List<TrocaDeVersaoDoPedido> _trocasDeVersao = [];

    /// <summary>A versão do perfil que o pedido segue; nula só no perfil de governança, que não tem versão.</summary>
    public Guid? VersaoId { get; private set; }

    public IReadOnlyList<TrocaDeVersaoDoPedido> TrocasDeVersao => _trocasDeVersao;

    /// <summary>
    /// Aplica ao pedido a entrada em vigor de uma versão nova do perfil. A versão com acréscimo devolve o pedido, que não
    /// pode seguir com permissão que ninguém aprovou. Sem acréscimo, a aprovação do gestor que já tinha sido dada perde
    /// o efeito e o pedido volta a ele; nas demais etapas o pedido só passa para a versão nova. Cada efeito grava uma troca.
    /// </summary>
    /// <param name="nova">A versão que entrou em vigor.</param>
    /// <param name="numeroAnterior">O número da versão que deixou de ser a em vigor.</param>
    /// <param name="comAcrescimo">A versão nova acrescenta permissão à anterior.</param>
    public DesfechoDaTrocaDeVersao AoEntrarEmVigor(VersaoDoPerfil nova, int numeroAnterior, bool comAcrescimo, DateTimeOffset instante)
    {
        ArgumentNullException.ThrowIfNull(nova);

        if (!Ativo)
        {
            return DesfechoDaTrocaDeVersao.Inalterado;
        }

        DesfechoDaTrocaDeVersao desfecho = DesfechoDaTroca(comAcrescimo);
        Aplicar(desfecho, nova, instante);
        _trocasDeVersao.Add(TrocaDeVersaoDoPedido.Registrar(Id, numeroAnterior, nova.Numero, desfecho, instante));

        return desfecho;
    }

    /// <summary>
    /// Confere que o pedido ativo segue a versão em vigor do perfil. A troca de vigência atualiza ou devolve todo pedido
    /// ativo sob a trava do perfil, então o pedido ativo que chega à decisão em outra versão é defeito, não situação de
    /// negócio. O pedido encerrado, como o devolvido, mantém a versão que seguia e não é conferido.
    /// </summary>
    public void ExigirVersaoEmVigorDe(Perfil perfil)
    {
        ArgumentNullException.ThrowIfNull(perfil);

        if (Ativo && VersaoId != perfil.VersaoVigente?.Id)
        {
            throw new InvalidOperationException(
                $"O pedido {Id} segue a versão {VersaoId} e o perfil {perfil.Id} tem outra em vigor.");
        }
    }

    private DesfechoDaTrocaDeVersao DesfechoDaTroca(bool comAcrescimo)
    {
        if (comAcrescimo)
        {
            return DesfechoDaTrocaDeVersao.Devolvido;
        }

        return Situacao == SituacaoDoPedido.AguardandoProtecaoDeDados
            ? DesfechoDaTrocaDeVersao.VoltouAoGestor
            : DesfechoDaTrocaDeVersao.VersaoAtualizada;
    }

    private void Aplicar(DesfechoDaTrocaDeVersao desfecho, VersaoDoPerfil nova, DateTimeOffset instante)
    {
        if (desfecho == DesfechoDaTrocaDeVersao.Devolvido)
        {
            Encerrar(SituacaoDoPedido.Devolvido, instante);
            return;
        }

        VersaoId = nova.Id;

        if (desfecho == DesfechoDaTrocaDeVersao.VoltouAoGestor)
        {
            Situacao = SituacaoDoPedido.AguardandoGestor;
            ValidaAte = null;
        }
    }
}
