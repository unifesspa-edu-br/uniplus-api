namespace Unifesspa.UniPlus.Autorizacao.Domain.Entities;

using Unifesspa.UniPlus.Autorizacao.Domain.Enums;
using Unifesspa.UniPlus.Autorizacao.Domain.Errors;
using Unifesspa.UniPlus.Autorizacao.Domain.Interfaces;
using Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>A aprovação da versão proposta, o cancelamento dela e a troca de vigência que a aprovação completa faz.</summary>
public sealed partial class Perfil
{
    /// <summary>
    /// Decide uma etapa da aprovação da versão: aprova ou recusa. O prazo vencido e a mudança de classe gravam mesmo
    /// recusando; a decisão que completa a aprovação troca a versão em vigor, e o resultado traz a troca.
    /// </summary>
    public DesfechoDaVersao Decidir(
        PedidoDeDecisaoDaVersao pedida, ContextoDaVersao contexto, ICatalogoDePermissoesConsultavel catalogo)
    {
        ArgumentNullException.ThrowIfNull(pedida);
        ArgumentNullException.ThrowIfNull(contexto);
        ArgumentNullException.ThrowIfNull(catalogo);

        Result<VersaoDoPerfil> versao = ObterVersaoDeOperacao(pedida.VersaoId);

        return versao.IsFailure
            ? DesfechoDaVersao.Recusa(versao.Error!)
            : new PassosDaDecisaoDaVersao(this, VersaoVigente, versao.Value!, pedida, contexto, catalogo).Executar();
    }

    /// <summary>
    /// Cancela a versão pendente, com o motivo, a pedido de quem a criou ou de quem mantém perfis. A versão vigente só
    /// deixa de valer quando outra a substitui. A pendente vencida expira em vez de cancelar.
    /// </summary>
    public DesfechoDaVersao CancelarVersao(Guid versaoId, Identidade autor, string? motivo, DateTimeOffset instante)
    {
        ArgumentNullException.ThrowIfNull(autor);

        Result<VersaoDoPerfil> versao = ObterVersaoDeOperacao(versaoId);
        DesfechoDaVersao? recusa = versao.IsFailure
            ? DesfechoDaVersao.Recusa(versao.Error!)
            : RecusaAoCancelar(versao.Value!, motivo, instante);
        if (recusa is not null)
        {
            return recusa;
        }

        versao.Value!.Cancelar(autor, motivo, FormaDoCancelamento.Pessoa, instante);

        return DesfechoDaVersao.Concluido();
    }

    /// <summary>
    /// Materializa o fim do prazo da pendente, para que a trilha não dependa de a tarefa agendada ter rodado. Devolve
    /// verdadeiro quando o perfil mudou e precisa ser gravado.
    /// </summary>
    public bool MaterializarExpiracao(DateTimeOffset agora)
    {
        if (VersaoPendente is not { } pendente || !pendente.Expirou(agora))
        {
            return false;
        }

        pendente.Expirar();

        return true;
    }

    // A vigência aberta se encerra antes de a nova abrir: o banco admite uma só aberta por perfil.
    internal TrocaDeVigencia TrocarVigencia(VersaoDoPerfil nova, DateTimeOffset instante)
    {
        VersaoDoPerfil? anterior = VersaoVigente;
        _vigencias.Find(static v => v.Aberta)?.Encerrar(instante);
        anterior?.Encerrar();

        nova.EntrarEmVigor();
        _vigencias.Add(VigenciaDoPerfil.Abrir(Id, nova.Id, instante));
        Nome = nova.Nome;
        Descricao = nova.Descricao;

        return new TrocaDeVigencia(anterior, nova);
    }

    // O perfil de governança não tem versão e não se altera por esta API: a recusa é a da imutabilidade.
    private Result<VersaoDoPerfil> ObterVersaoDeOperacao(Guid versaoId) =>
        Governanca is not null
            ? Result<VersaoDoPerfil>.Failure(PerfilErros.GovernancaImutavel())
            : ObterVersao(versaoId);

    private DesfechoDaVersao? RecusaAoCancelar(VersaoDoPerfil versao, string? motivo, DateTimeOffset instante)
    {
        if (MaterializarExpiracao(instante))
        {
            return DesfechoDaVersao.RecusaGravada(PerfilErros.VersaoExpirada());
        }

        DomainError? recusa = RecusaDoCancelamento(versao, motivo);

        return recusa is null ? null : DesfechoDaVersao.Recusa(recusa);
    }

    private static DomainError? RecusaDoCancelamento(VersaoDoPerfil versao, string? motivo)
    {
        if (!versao.Pendente)
        {
            return PerfilErros.VersaoNaoPendente();
        }

        return MotivosDaVersao.Normalizar(motivo) is null ? PerfilErros.MotivoInvalido(MotivosDaVersao.MaxLength) : null;
    }
}
