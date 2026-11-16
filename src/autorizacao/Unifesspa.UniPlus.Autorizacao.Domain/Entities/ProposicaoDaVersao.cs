namespace Unifesspa.UniPlus.Autorizacao.Domain.Entities;

using Unifesspa.UniPlus.Autorizacao.Domain.Enums;
using Unifesspa.UniPlus.Autorizacao.Domain.Errors;
using Unifesspa.UniPlus.Autorizacao.Domain.Interfaces;
using Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// A proposta de versão de um perfil e a remoção de permissões, que não espera aprovação: a contenção vale na hora,
/// sozinha ou com a proposta que a traz.
/// </summary>
public static class ProposicaoDaVersao
{
    /// <summary>
    /// Aplica a proposta de versão. Sem permissão retirada, cria a versão pendente. A permissão retirada vale na hora,
    /// como a remoção avulsa, e cancela a pendente que ainda valia; o que sobra da proposta, acréscimo ou mudança de
    /// nome, descrição ou documento, vira a versão pendente, conferida contra a versão que a remoção deixou em vigor.
    /// Nada muda quando alguma recusa se aplica: a remoção e a pendente entram juntas ou não entram.
    /// </summary>
    public static DesfechoDaVersao Propor(
        Perfil perfil, PropostaDeVersao proposta, ContextoDaVersao contexto, ICatalogoDePermissoesConsultavel catalogo)
    {
        ArgumentNullException.ThrowIfNull(perfil);
        ArgumentNullException.ThrowIfNull(proposta);
        ArgumentNullException.ThrowIfNull(contexto);
        ArgumentNullException.ThrowIfNull(catalogo);

        DiferencaDaVersao diferenca = DiferencaDaVersao.Calcular(proposta.Conteudo.Codigos(), perfil.VersaoVigente?.Codigos(), catalogo);

        return diferenca.TemRemocao
            ? ProporComRemocao(perfil, proposta, contexto, diferenca, catalogo)
            : DaCriacao(CriarVersao(perfil, proposta, contexto, catalogo));
    }

    /// <summary>
    /// Retira permissões da versão em vigor. A versão que sai é substituída por uma de remoção, sem aprovação; a
    /// pendente vencida expira e a que ainda vale é cancelada por contenção. A remoção pode zerar as permissões: a regra
    /// de ao menos uma é só da proposta.
    /// </summary>
    public static DesfechoDaVersao Remover(Perfil perfil, PedidoDeRemocao pedido, ICatalogoDePermissoesConsultavel catalogo)
    {
        ArgumentNullException.ThrowIfNull(perfil);
        ArgumentNullException.ThrowIfNull(pedido);
        ArgumentNullException.ThrowIfNull(catalogo);

        DomainError? recusa = RecusaDaRemocao(perfil, pedido);

        return recusa is null ? RemoverDaVigente(perfil, pedido, catalogo) : DesfechoDaVersao.Recusa(recusa);
    }

    internal static ClasseDaVersao ClasseDe(
        ConteudoDaVersao conteudo, VersaoDoPerfil? vigente, ICatalogoDePermissoesConsultavel catalogo)
    {
        IReadOnlyList<string> codigos = conteudo.Codigos();
        DiferencaDaVersao diferenca = DiferencaDaVersao.Calcular(codigos, vigente?.Codigos(), catalogo);

        return new ClasseDaVersao(AlcanceDasPermissoes.De(codigos, catalogo) ?? TipoDeEscopo.Instituicao, diferenca.ExigeProtecaoDeDados);
    }

    private static DesfechoDaVersao ProporComRemocao(
        Perfil perfil, PropostaDeVersao proposta, ContextoDaVersao contexto, DiferencaDaVersao diferenca, ICatalogoDePermissoesConsultavel catalogo)
    {
        PedidoDeRemocao remocao = proposta.DaRemocao(diferenca.Remocao);

        return MudaAlemDaRemocao(perfil.VersaoVigente!, proposta.Conteudo, diferenca)
            ? RemoverEPropor(perfil, proposta, remocao, contexto, catalogo)
            : Remover(perfil, remocao, catalogo);
    }

    private static DesfechoDaVersao DaCriacao(Result<VersaoDoPerfil> criada) =>
        criada.IsSuccess ? DesfechoDaVersao.ConcluidoComProposta(criada.Value!) : DesfechoDaVersao.Recusa(criada.Error!);

    // Só retira permissão quando o nome, a descrição e o protocolo do documento são os da versão em vigor e nada se acrescenta.
    private static bool MudaAlemDaRemocao(VersaoDoPerfil vigente, ConteudoDaVersao conteudo, DiferencaDaVersao diferenca) =>
        diferenca.TemAcrescimo
        || !string.Equals(conteudo.Nome, vigente.Nome, StringComparison.Ordinal)
        || !string.Equals(conteudo.Descricao, vigente.Descricao, StringComparison.Ordinal)
        || !string.Equals(conteudo.Protocolo, vigente.Protocolo, StringComparison.Ordinal);

    private static DesfechoDaVersao RemoverEPropor(
        Perfil perfil, PropostaDeVersao proposta, PedidoDeRemocao pedido, ContextoDaVersao contexto, ICatalogoDePermissoesConsultavel catalogo)
    {
        DomainError? recusa = RecusaDaRemocao(perfil, pedido) ?? RecusaDoContexto(contexto);
        if (recusa is not null)
        {
            return DesfechoDaVersao.Recusa(recusa);
        }

        VersaoDoPerfil remocao = VersaoDoPerfil.DaRemocao(perfil.VersaoVigente!, perfil.ProximoNumero(), pedido);
        Result<VersaoDoPerfil> pendente = CriarSobre(perfil.Id, remocao, remocao.Numero + 1, proposta, catalogo);

        return pendente.IsFailure ? DesfechoDaVersao.Recusa(pendente.Error!) : EfetivarARemocao(perfil, pedido, remocao, pendente.Value!, catalogo);
    }

    // Cria uma versão nova, pendente, com o documento esperando o envio, para a proposta que não retira permissão. Sem
    // versão vigente (a primeira foi cancelada, recusada ou expirou) é uma nova primeira versão: não há o que comparar e
    // o nome pode mudar. A pendente que já passou do prazo expira antes e não bloqueia.
    private static Result<VersaoDoPerfil> CriarVersao(
        Perfil perfil, PropostaDeVersao proposta, ContextoDaVersao contexto, ICatalogoDePermissoesConsultavel catalogo)
    {
        perfil.MaterializarExpiracao(proposta.Instante);
        DomainError? recusa = RecusaDoEstado(perfil) ?? RecusaDoContexto(contexto);

        return recusa is null
            ? CriarEAcrescentar(perfil, proposta, catalogo)
            : Result<VersaoDoPerfil>.Failure(recusa);
    }

    private static Result<VersaoDoPerfil> CriarEAcrescentar(Perfil perfil, PropostaDeVersao proposta, ICatalogoDePermissoesConsultavel catalogo)
    {
        Result<VersaoDoPerfil> criada = CriarSobre(perfil.Id, perfil.VersaoVigente, perfil.ProximoNumero(), proposta, catalogo);
        if (criada.IsSuccess)
        {
            perfil.Acrescentar(criada.Value!);
        }

        return criada;
    }

    // A versão proposta, conferida contra aquela sobre a qual vai valer: a vigente, ou a que a remoção da mesma proposta
    // deixa em vigor.
    private static Result<VersaoDoPerfil> CriarSobre(
        Guid perfilId, VersaoDoPerfil? sobre, int numero, PropostaDeVersao proposta, ICatalogoDePermissoesConsultavel catalogo)
    {
        Result<VersaoDoPerfil> criada = VersaoDoPerfil.Criar(
            perfilId, numero, proposta.Conteudo, proposta.Autoria, ClasseDe(proposta.Conteudo, sobre, catalogo), proposta.Instante);
        DomainError? recusa = criada.IsSuccess ? ConferenciasDaVersao.DaCriacao(criada.Value!, sobre) : null;

        return recusa is null ? criada : Result<VersaoDoPerfil>.Failure(recusa);
    }

    private static DomainError? RecusaDoEstado(Perfil perfil)
    {
        if (perfil.Governanca is not null)
        {
            return PerfilErros.GovernancaImutavel();
        }

        if (!perfil.Ativo)
        {
            return PerfilErros.Inativo();
        }

        return perfil.VersaoPendente is not null ? PerfilErros.VersaoPendenteExistente() : null;
    }

    private static DomainError? RecusaDoContexto(ContextoDaVersao contexto)
    {
        if (contexto.JanelaDeMigracaoAberta)
        {
            return PerfilErros.JanelaDeMigracaoAberta();
        }

        return contexto.NomeEmUsoPorOutroPerfil ? PerfilErros.NomeDuplicado() : null;
    }

    private static DomainError? RecusaDaRemocao(Perfil perfil, PedidoDeRemocao pedido)
    {
        DomainError? estado = perfil.Governanca is not null ? PerfilErros.GovernancaImutavel()
            : !perfil.Ativo ? PerfilErros.Inativo()
            : perfil.VersaoVigente is null ? PerfilErros.SemVersaoEmVigor()
            : null;

        return estado
            ?? (CodigosValidos(perfil.VersaoVigente!, pedido.Codigos) ? null : PerfilErros.RemocaoInvalida())
            ?? (pedido.Motivo is null ? PerfilErros.MotivoInvalido(MotivosDaVersao.MaxLength) : null);
    }

    private static bool CodigosValidos(VersaoDoPerfil vigente, IReadOnlyList<string> codigos) =>
        codigos.Count > 0 && codigos.All(c => vigente.Codigos().Contains(c, StringComparer.Ordinal));

    private static DesfechoDaVersao RemoverDaVigente(Perfil perfil, PedidoDeRemocao pedido, ICatalogoDePermissoesConsultavel catalogo) =>
        EfetivarARemocao(perfil, pedido, VersaoDoPerfil.DaRemocao(perfil.VersaoVigente!, perfil.ProximoNumero(), pedido), null, catalogo);

    // A remoção entra em vigor e a pendente que ainda valia é cancelada; a versão que a proposta criou além da remoção
    // passa a ser a única pendente.
    private static DesfechoDaVersao EfetivarARemocao(
        Perfil perfil, PedidoDeRemocao pedido, VersaoDoPerfil remocao, VersaoDoPerfil? proposta, ICatalogoDePermissoesConsultavel catalogo)
    {
        VersaoDoPerfil? cancelada = EncerrarPendenteParaARemocao(perfil, pedido);
        perfil.Acrescentar(remocao);
        TrocaDeVigencia troca = perfil.TrocarVigencia(remocao, pedido.Instante, catalogo);

        if (proposta is not null)
        {
            perfil.Acrescentar(proposta);
        }

        return DesfechoDaVersao.ConcluidoPelaRemocao(troca, cancelada, proposta);
    }

    // A vencida expira sem aviso; a que ainda vale é cancelada pela contenção, e quem a criou é avisado.
    private static VersaoDoPerfil? EncerrarPendenteParaARemocao(Perfil perfil, PedidoDeRemocao pedido)
    {
        if (perfil.MaterializarExpiracao(pedido.Instante) || perfil.VersaoPendente is not { } pendente)
        {
            return null;
        }

        pendente.Cancelar(pedido.Autoria.Pessoa, null, FormaDoCancelamento.Contencao, pedido.Instante);

        return pendente;
    }
}
