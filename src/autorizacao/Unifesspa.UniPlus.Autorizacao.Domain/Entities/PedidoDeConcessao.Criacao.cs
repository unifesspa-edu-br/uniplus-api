namespace Unifesspa.UniPlus.Autorizacao.Domain.Entities;

using Unifesspa.UniPlus.Autorizacao.Domain.Enums;
using Unifesspa.UniPlus.Autorizacao.Domain.Errors;
using Unifesspa.UniPlus.Autorizacao.Domain.Interfaces;
using Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>A criação do pedido e as conferências que a antecedem.</summary>
public sealed partial class PedidoDeConcessao
{
    /// <summary>
    /// Cria o pedido. Acumula toda violação independente (ADR-0125). Que o pedido ativo igual não exista é do
    /// índice único, traduzido por quem grava; que a unidade exista é do handler.
    /// </summary>
    /// <param name="solicitante">Quem pede, como o token o traz.</param>
    /// <param name="validaAteSugerida">Fim de vigência que o solicitante sugere; o perfil de governança não admite prazo.</param>
    /// <param name="emailDoBeneficiario">E-mail institucional do beneficiário; ausente, ou igual ao e-mail verificado do solicitante, o pedido é para si mesmo.</param>
    public static Result<PedidoDeConcessao> Criar(
        PessoaNoToken solicitante,
        string? emailDoBeneficiario,
        Perfil perfil,
        EscopoDaConcessao escopo,
        string? justificativa,
        IDominiosInstitucionais dominios,
        ICatalogoDePermissoesConsultavel catalogo,
        DateTimeOffset instante,
        DateTimeOffset? validaAteSugerida = null)
    {
        ArgumentNullException.ThrowIfNull(solicitante);
        ArgumentNullException.ThrowIfNull(catalogo);
        ArgumentNullException.ThrowIfNull(perfil);
        ArgumentNullException.ThrowIfNull(escopo);
        ArgumentNullException.ThrowIfNull(dominios);

        if (RecusaDoSolicitante(solicitante, emailDoBeneficiario, dominios) is { } recusa)
        {
            return Result<PedidoDeConcessao>.Failure(recusa);
        }

        (List<FieldError> erros, string? justificativaNormalizada) = ValidarCampos(perfil, escopo, catalogo, justificativa, validaAteSugerida, instante);
        (string? email, bool paraSiMesmo) = ResolverBeneficiario(solicitante, emailDoBeneficiario, dominios, erros);

        return erros.Count > 0
            ? Result<PedidoDeConcessao>.ValidationFailure(erros)
            : Result<PedidoDeConcessao>.Success(
                Novo(solicitante, perfil, escopo, justificativaNormalizada!, validaAteSugerida, instante, (email!, paraSiMesmo)));
    }

    private static DomainError? RecusaDoSolicitante(PessoaNoToken solicitante, string? emailDoBeneficiario, IDominiosInstitucionais dominios) =>
        !ParaSiMesmo(solicitante, emailDoBeneficiario) && !SolicitanteInstitucional(solicitante, dominios)
            ? new DomainError(
                PedidoErrorCodes.SolicitanteNaoInstitucional,
                "Para pedir acesso a outra pessoa, o e-mail da sessão deve ser institucional e verificado.")
            : null;

    private static PedidoDeConcessao Novo(
        PessoaNoToken solicitante,
        Perfil perfil,
        EscopoDaConcessao escopo,
        string justificativa,
        DateTimeOffset? validaAteSugerida,
        DateTimeOffset instante,
        (string Email, bool ParaSiMesmo) beneficiario)
    {
        PedidoDeConcessao pedido = new()
        {
            BeneficiarioEmail = beneficiario.Email,
            PerfilId = perfil.Id,
            UnidadeId = escopo.UnidadeId,
            Justificativa = justificativa,
            ValidaAteSugerida = validaAteSugerida?.ToUniversalTime(),
            Situacao = SituacaoInicial(perfil, beneficiario.ParaSiMesmo),
            CriadoEm = instante,
        };
        pedido.DefinirPartes(solicitante, beneficiario.ParaSiMesmo);

        return pedido;
    }

    private void DefinirPartes(PessoaNoToken solicitante, bool paraSiMesmo)
    {
        SolicitanteEmissor = solicitante.Identidade.Emissor;
        SolicitanteSub = solicitante.Identidade.Sub;
        SolicitanteNome = NomeDeExibicao.Normalizar(solicitante.Nome);
        SolicitanteEmail = solicitante.EmailVerificado ? EmailNormalizado(solicitante.Email) : null;

        if (paraSiMesmo)
        {
            DefinirBeneficiario(solicitante.Identidade, SolicitanteNome);
        }
    }

    private static SituacaoDoPedido SituacaoInicial(Perfil perfil, bool paraSiMesmo) =>
        perfil.Governanca is not null ? SituacaoDoPedido.AguardandoDocumento : EtapaSeguinteAoDocumento(paraSiMesmo);

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

    private static (List<FieldError> Erros, string? Justificativa) ValidarCampos(
        Perfil perfil,
        EscopoDaConcessao escopo,
        ICatalogoDePermissoesConsultavel catalogo,
        string? justificativa,
        DateTimeOffset? validaAteSugerida,
        DateTimeOffset instante)
    {
        List<FieldError> erros = [.. ErrosDoPerfil(perfil), .. ErrosDoEscopo(perfil, escopo, catalogo)];
        string? normalizada = NormalizarTexto(justificativa, JustificativaMinLength, JustificativaMaxLength);

        if (normalizada is null)
        {
            erros.Add(new("justificativa", new DomainError(
                PedidoErrorCodes.JustificativaInvalida,
                $"A justificativa é obrigatória e deve ter entre {JustificativaMinLength} e {JustificativaMaxLength} caracteres.")));
        }

        ConferirValidade(validaAteSugerida, perfil.AdmitePrazo, instante, "validaAteSugerida", erros);

        return (erros, normalizada);
    }

    private static void ConferirValidade(
        DateTimeOffset? data, bool admitePrazo, DateTimeOffset instante, string campo, List<FieldError> erros)
    {
        string? mensagem = data is { } fim ? MensagemDaValidadeInvalida(fim, admitePrazo, instante) : null;
        if (mensagem is not null)
        {
            erros.Add(new(campo, new DomainError(PedidoErrorCodes.ValidadeInvalida, mensagem)));
        }
    }

    private static string? MensagemDaValidadeInvalida(DateTimeOffset fim, bool admitePrazo, DateTimeOffset instante)
    {
        if (!admitePrazo)
        {
            return "Perfil de governança não admite concessão com prazo: gestor de acessos e proteção de dados não têm vigência limitada.";
        }

        return fim <= instante ? "O fim da vigência deve ser posterior ao instante atual." : null;
    }

    // O pedido para si mesmo não tem aceite: o beneficiário já é conhecido e a decisão cabe ao gestor.
    private static SituacaoDoPedido EtapaSeguinteAoDocumento(bool paraSiMesmo) =>
        paraSiMesmo ? SituacaoDoPedido.AguardandoGestor : SituacaoDoPedido.AguardandoBeneficiario;
}
