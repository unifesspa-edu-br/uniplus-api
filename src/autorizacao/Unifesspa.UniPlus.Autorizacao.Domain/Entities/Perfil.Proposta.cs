namespace Unifesspa.UniPlus.Autorizacao.Domain.Entities;

using Unifesspa.UniPlus.Autorizacao.Domain.Errors;
using Unifesspa.UniPlus.Autorizacao.Domain.Interfaces;
using Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>A proposta de versão, que traz junto a remoção das permissões que ela retira.</summary>
public sealed partial class Perfil
{
    /// <summary>
    /// Aplica a proposta de versão. Sem permissão retirada, cria a versão pendente. A permissão retirada vale na hora,
    /// como a remoção avulsa, e cancela a pendente que ainda valia; o que sobra da proposta, acréscimo ou mudança de
    /// nome, descrição ou documento, vira a versão pendente, conferida contra a versão que a remoção deixou em vigor.
    /// Nada muda quando alguma recusa se aplica: a remoção e a pendente entram juntas ou não entram.
    /// </summary>
    public DesfechoDaVersao Propor(PropostaDeVersao proposta, ContextoDaVersao contexto, ICatalogoDePermissoesConsultavel catalogo)
    {
        ArgumentNullException.ThrowIfNull(proposta);
        ArgumentNullException.ThrowIfNull(contexto);
        ArgumentNullException.ThrowIfNull(catalogo);

        DiferencaDaVersao diferenca = DiferencaDaVersao.Calcular(proposta.Conteudo.Codigos(), VersaoVigente?.Codigos(), catalogo);

        if (!diferenca.TemRemocao)
        {
            return DaCriacao(CriarVersao(proposta.Conteudo, contexto, proposta.Autoria, proposta.Instante, catalogo));
        }

        PedidoDeRemocao remocao = proposta.DaRemocao(diferenca.Remocao);

        return MudaAlemDaRemocao(proposta.Conteudo, diferenca)
            ? RemoverEPropor(proposta, remocao, contexto, catalogo)
            : Remover(remocao, catalogo);
    }

    private static DesfechoDaVersao DaCriacao(Result<VersaoDoPerfil> criada) =>
        criada.IsSuccess ? DesfechoDaVersao.ConcluidoComProposta(criada.Value!) : DesfechoDaVersao.Recusa(criada.Error!);

    // Só retira permissão quando o nome, a descrição e o protocolo do documento são os da versão em vigor e nada se acrescenta.
    private bool MudaAlemDaRemocao(ConteudoDaVersao conteudo, DiferencaDaVersao diferenca)
    {
        VersaoDoPerfil vigente = VersaoVigente!;

        return diferenca.TemAcrescimo
            || !string.Equals(conteudo.Nome, vigente.Nome, StringComparison.Ordinal)
            || !string.Equals(conteudo.Descricao, vigente.Descricao, StringComparison.Ordinal)
            || !string.Equals(conteudo.Protocolo, vigente.Protocolo, StringComparison.Ordinal);
    }

    private static DomainError? RecusaDaPendente(ContextoDaVersao contexto)
    {
        if (contexto.JanelaDeMigracaoAberta)
        {
            return PerfilErros.JanelaDeMigracaoAberta();
        }

        return contexto.NomeEmUsoPorOutroPerfil ? PerfilErros.NomeDuplicado() : null;
    }

    private DesfechoDaVersao RemoverEPropor(
        PropostaDeVersao proposta, PedidoDeRemocao pedido, ContextoDaVersao contexto, ICatalogoDePermissoesConsultavel catalogo)
    {
        DomainError? recusa = RecusaDaRemocao(pedido) ?? RecusaDaPendente(contexto);
        if (recusa is not null)
        {
            return DesfechoDaVersao.Recusa(recusa);
        }

        VersaoDoPerfil remocao = VersaoDoPerfil.DaRemocao(VersaoVigente!, ProximoNumero(), pedido);
        Result<VersaoDoPerfil> pendente = CriarSobre(
            remocao, remocao.Numero + 1, proposta.Conteudo, contexto, proposta.Autoria, proposta.Instante, catalogo);

        return pendente.IsFailure ? DesfechoDaVersao.Recusa(pendente.Error!) : EfetivarARemocao(pedido, remocao, pendente.Value!, catalogo);
    }
}
