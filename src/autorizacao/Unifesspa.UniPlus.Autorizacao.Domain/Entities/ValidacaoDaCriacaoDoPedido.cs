namespace Unifesspa.UniPlus.Autorizacao.Domain.Entities;

using Unifesspa.UniPlus.Autorizacao.Domain.Errors;
using Unifesspa.UniPlus.Autorizacao.Domain.Interfaces;
using Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>As conferências que antecedem a criação do pedido: o perfil, o escopo, a justificativa e a validade.</summary>
internal static class ValidacaoDaCriacaoDoPedido
{
    internal static (List<FieldError> Erros, string? Justificativa) ValidarCampos(
        Perfil perfil, EscopoDaConcessao escopo, ICatalogoDePermissoesConsultavel catalogo, DeclaracaoDoPedido declaracao, DateTimeOffset instante)
    {
        List<FieldError> erros = [.. ErrosDoPerfil(perfil), .. ErrosDoEscopo(perfil, escopo, catalogo)];
        string? normalizada = TextoLivre.Normalizar(declaracao.Justificativa, PedidoDeConcessao.JustificativaMinLength, PedidoDeConcessao.JustificativaMaxLength);

        if (normalizada is null)
        {
            erros.Add(JustificativaInvalida());
        }

        ConferirValidade(declaracao.ValidaAteSugerida, perfil.AdmitePrazo, instante, "validaAteSugerida", erros);

        return (erros, normalizada);
    }

    internal static void ConferirValidade(
        DateTimeOffset? data, bool admitePrazo, DateTimeOffset instante, string campo, List<FieldError> erros)
    {
        string? mensagem = data is { } fim ? MensagemDaValidadeInvalida(fim, admitePrazo, instante) : null;
        if (mensagem is not null)
        {
            erros.Add(new(campo, new DomainError(PedidoErrorCodes.ValidadeInvalida, mensagem)));
        }
    }

    private static FieldError JustificativaInvalida() => new("justificativa", new DomainError(
        PedidoErrorCodes.JustificativaInvalida,
        $"A justificativa é obrigatória e deve ter entre {PedidoDeConcessao.JustificativaMinLength} e {PedidoDeConcessao.JustificativaMaxLength} caracteres."));

    private static IEnumerable<FieldError> ErrosDoPerfil(Perfil perfil)
    {
        if (!perfil.Ativo)
        {
            yield return new("perfilId", new DomainError(
                PedidoErrorCodes.PerfilInativo,
                "O perfil de acesso está desativado e não aceita pedido novo."));
        }

        if (!perfil.TemComposicaoEmVigor)
        {
            yield return new("perfilId", new DomainError(
                PedidoErrorCodes.PerfilSemVersaoEmVigor,
                "O perfil de acesso não tem versão em vigor e não aceita pedido."));
        }
    }

    private static IEnumerable<FieldError> ErrosDoEscopo(Perfil perfil, EscopoDaConcessao escopo, ICatalogoDePermissoesConsultavel catalogo)
    {
        string? incoerencia = Concessao.ConferirEscopo(perfil, escopo, catalogo);
        if (incoerencia is not null)
        {
            yield return new("escopo", new DomainError(PedidoErrorCodes.EscopoInvalido, incoerencia));
        }
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
