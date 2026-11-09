namespace Unifesspa.UniPlus.Autorizacao.Domain.Entities;

using Unifesspa.UniPlus.Autorizacao.Domain.Enums;
using Unifesspa.UniPlus.Autorizacao.Domain.Errors;
using Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>A remoção de permissões, que não espera aprovação: a contenção vale na hora.</summary>
public sealed partial class Perfil
{
    /// <summary>
    /// Retira permissões da versão em vigor. A versão que sai é substituída por uma de remoção, sem aprovação; a
    /// pendente vencida expira e a que ainda vale é cancelada por contenção. A remoção pode zerar as permissões: a regra
    /// de ao menos uma é só da proposta.
    /// </summary>
    public DesfechoDaVersao Remover(PedidoDeRemocao pedido)
    {
        ArgumentNullException.ThrowIfNull(pedido);

        DomainError? recusa = RecusaDaRemocao(pedido);

        return recusa is null ? RemoverDaVigente(pedido) : DesfechoDaVersao.Recusa(recusa);
    }

    private DomainError? RecusaDaRemocao(PedidoDeRemocao pedido)
    {
        DomainError? estado = Governanca is not null ? PerfilErros.GovernancaImutavel()
            : !Ativo ? PerfilErros.Inativo()
            : VersaoVigente is null ? PerfilErros.SemVersaoEmVigor()
            : null;

        return estado
            ?? (CodigosValidos(pedido.Codigos) ? null : PerfilErros.RemocaoInvalida())
            ?? (pedido.Motivo is null ? PerfilErros.MotivoInvalido(MotivosDaVersao.MaxLength) : null);
    }

    private bool CodigosValidos(IReadOnlyList<string> codigos) =>
        codigos.Count > 0 && codigos.All(c => VersaoVigente!.Codigos().Contains(c, StringComparer.Ordinal));

    private DesfechoDaVersao RemoverDaVigente(PedidoDeRemocao pedido)
    {
        VersaoDoPerfil? cancelada = EncerrarPendenteParaARemocao(pedido);
        VersaoDoPerfil remocao = VersaoDoPerfil.DaRemocao(VersaoVigente!, ProximoNumero(), pedido);
        _versoes.Add(remocao);

        return DesfechoDaVersao.ConcluidoPelaRemocao(TrocarVigencia(remocao, pedido.Instante), cancelada);
    }

    // A vencida expira sem aviso; a que ainda vale é cancelada pela contenção, e quem a criou é avisado.
    private VersaoDoPerfil? EncerrarPendenteParaARemocao(PedidoDeRemocao pedido)
    {
        if (MaterializarExpiracao(pedido.Instante) || VersaoPendente is not { } pendente)
        {
            return null;
        }

        pendente.Cancelar(pedido.Autoria.Pessoa, null, FormaDoCancelamento.Contencao, pedido.Instante);

        return pendente;
    }
}
