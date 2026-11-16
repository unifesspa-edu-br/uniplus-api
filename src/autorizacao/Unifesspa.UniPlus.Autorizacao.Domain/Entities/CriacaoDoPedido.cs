namespace Unifesspa.UniPlus.Autorizacao.Domain.Entities;

using Unifesspa.UniPlus.Autorizacao.Domain.Errors;
using Unifesspa.UniPlus.Autorizacao.Domain.Interfaces;
using Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// A criação do pedido de concessão: confere o perfil, o escopo, a justificativa, a validade e o beneficiário, acumulando toda
/// violação independente num só conjunto de erros (ADR-0125), e só então cria o pedido. Que o pedido ativo igual não exista é
/// do índice único, traduzido por quem grava; que a unidade exista é do handler; o termo de responsabilidade se fixa em
/// seguida, por <see cref="PedidoDeConcessao.FixarTermo"/>.
/// </summary>
public static class CriacaoDoPedido
{
    /// <param name="partes">Quem pede e a quem o pedido se destina.</param>
    /// <param name="declaracao">Justificativa, validade sugerida e intenção de anexar documento; os perfis de gestor de acessos e de Representante do controlador não admitem prazo.</param>
    public static Result<PedidoDeConcessao> Criar(
        PartesDoPedido partes,
        Perfil perfil,
        EscopoDaConcessao escopo,
        DeclaracaoDoPedido declaracao,
        IDominiosInstitucionais dominios,
        ICatalogoDePermissoesConsultavel catalogo,
        DateTimeOffset instante)
    {
        ArgumentNullException.ThrowIfNull(partes);
        ArgumentNullException.ThrowIfNull(perfil);
        ArgumentNullException.ThrowIfNull(escopo);
        ArgumentNullException.ThrowIfNull(declaracao);
        ArgumentNullException.ThrowIfNull(dominios);
        ArgumentNullException.ThrowIfNull(catalogo);

        DomainError? recusa = partes.RecusaDoSolicitante(dominios);
        (List<FieldError> erros, string? justificativa) = ValidarCampos(perfil, escopo, catalogo, declaracao, instante);
        BeneficiarioIndicado beneficiario = partes.ResolverBeneficiario(dominios, erros);

        return recusa is not null ? Result<PedidoDeConcessao>.Failure(recusa)
            : erros.Count > 0 ? Result<PedidoDeConcessao>.ValidationFailure(erros)
            : Result<PedidoDeConcessao>.Success(new PedidoDeConcessao(partes.Solicitante, beneficiario, perfil, escopo, declaracao.Consolidada(justificativa!, perfil), instante));
    }

    private static (List<FieldError> Erros, string? Justificativa) ValidarCampos(
        Perfil perfil, EscopoDaConcessao escopo, ICatalogoDePermissoesConsultavel catalogo, DeclaracaoDoPedido declaracao, DateTimeOffset instante)
    {
        List<FieldError> erros = [.. ErrosDoPerfil(perfil), .. ErrosDoEscopo(perfil, escopo, catalogo)];
        string? normalizada = TextoLivre.Normalizar(declaracao.Justificativa, DeclaracaoDoPedido.JustificativaMinLength, DeclaracaoDoPedido.JustificativaMaxLength);

        if (normalizada is null)
        {
            erros.Add(JustificativaInvalida());
        }

        ValidadeDoPedido.Conferir(declaracao.ValidaAteSugerida, perfil.AdmitePrazo, instante, "validaAteSugerida", erros);

        return (erros, normalizada);
    }

    private static FieldError JustificativaInvalida() => new("justificativa", new DomainError(
        PedidoErrorCodes.JustificativaInvalida,
        $"A justificativa é obrigatória e deve ter entre {DeclaracaoDoPedido.JustificativaMinLength} e {DeclaracaoDoPedido.JustificativaMaxLength} caracteres."));

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
}
