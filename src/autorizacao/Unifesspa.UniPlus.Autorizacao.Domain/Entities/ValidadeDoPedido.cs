namespace Unifesspa.UniPlus.Autorizacao.Domain.Entities;

using Unifesspa.UniPlus.Autorizacao.Domain.Enums;
using Unifesspa.UniPlus.Autorizacao.Domain.Errors;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>O fim de vigência do pedido: o que o solicitante sugere na criação e o que o gestor define ao aprovar.</summary>
internal static class ValidadeDoPedido
{
    internal static void Conferir(
        DateTimeOffset? data, bool admitePrazo, DateTimeOffset instante, string campo, List<FieldError> erros)
    {
        string? mensagem = data is { } fim ? MensagemDaValidadeInvalida(fim, admitePrazo, instante) : null;
        if (mensagem is not null)
        {
            erros.Add(new(campo, new DomainError(PedidoErrorCodes.ValidadeInvalida, mensagem)));
        }
    }

    internal static void ConferirNaEtapa(AprovacaoNaEtapa aprovacao, DateTimeOffset? validaAteDoPedido, List<FieldError> erros)
    {
        if (aprovacao.Etapa == EtapaDoPedido.Gestor)
        {
            Conferir(aprovacao.ValidaAte, aprovacao.Perfil.AdmitePrazo, aprovacao.Instante, "validaAte", erros);
        }
        else if (RecusaNaSegundaEtapa(aprovacao, validaAteDoPedido) is { } recusa)
        {
            erros.Add(recusa);
        }
    }

    // Na segunda etapa o Representante não informa prazo. O pedido de perfil que não admite prazo e já traz o do gestor,
    // gravado antes de o perfil de Representante deixar de admiti-lo (UNI-REQ-0200), é recusado aqui, antes de virar
    // concessão: nada é gravado, e o pedido segue para ser recusado.
    private static FieldError? RecusaNaSegundaEtapa(AprovacaoNaEtapa aprovacao, DateTimeOffset? validaAteDoPedido)
    {
        if (aprovacao.ValidaAte is not null)
        {
            return new("validaAte", new DomainError(PedidoErrorCodes.ValidadeInvalida, "O fim da vigência é definido pelo gestor de acessos, na etapa dele."));
        }

        return validaAteDoPedido is not null && !aprovacao.Perfil.AdmitePrazo
            ? new("validaAte", new DomainError(PedidoErrorCodes.ValidadeInvalida, MensagensDoPrazo.PerfilSemPrazo))
            : null;
    }

    private static string? MensagemDaValidadeInvalida(DateTimeOffset fim, bool admitePrazo, DateTimeOffset instante)
    {
        if (!admitePrazo)
        {
            return MensagensDoPrazo.PerfilSemPrazo;
        }

        return fim <= instante ? "O fim da vigência deve ser posterior ao instante atual." : null;
    }
}
