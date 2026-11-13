namespace Unifesspa.UniPlus.Autorizacao.Domain.Entities;

using Unifesspa.UniPlus.Autorizacao.Domain.Enums;
using Unifesspa.UniPlus.Autorizacao.Domain.Errors;
using Unifesspa.UniPlus.Autorizacao.Domain.Interfaces;
using Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>Quem é o beneficiário do pedido e o e-mail institucional que o identifica.</summary>
public sealed partial class PedidoDeConcessao
{
    /// <summary>
    /// O beneficiário aceita o pedido que o nomeou e, no pedido protegido, o termo fixado nele. O beneficiário já conhecido
    /// (o próprio solicitante) é conferido pela identidade do token; o ainda desconhecido, pelo e-mail verificado do pedido. O
    /// aceite grava a identidade e o nome do token e, havendo termo, a versão e o resumo aceitos. O pedido que aguarda o
    /// documento declarado não aceita: o aceite espera a verificação.
    /// </summary>
    /// <param name="termoApresentado">A versão do termo que o cliente apresentou; obrigatória no pedido com termo e proibida nos demais.</param>
    public Result Aceitar(PessoaNoToken pessoa, Guid? termoApresentado, DateTimeOffset instante)
    {
        ArgumentNullException.ThrowIfNull(pessoa);

        Result? recusa = RecusaDoAceite(pessoa) is { } erro ? Result.Failure(erro) : RecusaDoTermoNoAceite(termoApresentado);
        if (recusa is not null)
        {
            return recusa;
        }

        DefinirBeneficiario(pessoa.Identidade, NomeDeExibicao.Normalizar(pessoa.Nome));
        BeneficiarioAceitouEm = instante;
        RegistrarAceiteDoTermo(instante);
        Situacao = SituacaoDoPedido.AguardandoGestor;

        return Result.Success();
    }

    /// <summary>A pessoa é uma das partes do pedido: o solicitante ou o beneficiário.</summary>
    public bool EhParte(PessoaNoToken pessoa)
    {
        ArgumentNullException.ThrowIfNull(pessoa);

        return pessoa.Identidade == Solicitante
            || (Beneficiario is { } beneficiario && pessoa.Identidade == beneficiario)
            || (Beneficiario is null && EhOBeneficiarioPeloEmail(pessoa));
    }

    private DomainError? RecusaDoAceite(PessoaNoToken pessoa)
    {
        if (RecusaDaSituacaoNoAceite() is { } recusa)
        {
            return recusa;
        }

        if (!EhOBeneficiario(pessoa))
        {
            return new DomainError(
                PedidoErrorCodes.NaoEOBeneficiario,
                "Só a pessoa a quem o pedido se destina, com o e-mail institucional verificado, o aceita.");
        }

        // O aprovador julga a pessoa pelo nome: sem ele no token, o pedido não avança à aprovação.
        return NomeDeExibicao.Normalizar(pessoa.Nome) is null ? SemNome() : null;
    }

    private DomainError? RecusaDaSituacaoNoAceite() => Situacao switch
    {
        SituacaoDoPedido.AguardandoBeneficiario => null,
        SituacaoDoPedido.AguardandoDocumento => new DomainError(
            PedidoErrorCodes.AnexoNaoVerificado,
            "O documento declarado ainda não foi verificado; o aceite e a decisão esperam a verificação."),
        _ => SituacaoInvalida("O pedido não aguarda o aceite do beneficiário."),
    };

    private bool EhOBeneficiario(PessoaNoToken pessoa) =>
        Beneficiario is { } conhecido ? pessoa.Identidade == conhecido : EhOBeneficiarioPeloEmail(pessoa);

    private bool EhOBeneficiarioPeloEmail(PessoaNoToken pessoa) =>
        pessoa.EmailVerificado
        && EmailNormalizado(pessoa.Email) is { } email
        && string.Equals(email, BeneficiarioEmail, StringComparison.Ordinal);

    private void DefinirBeneficiario(Identidade identidade, string? nome)
    {
        BeneficiarioEmissor = identidade.Emissor;
        BeneficiarioSub = identidade.Sub;
        BeneficiarioNome = nome;
    }

    private static DomainError SemNome() => new(
        PedidoErrorCodes.BeneficiarioSemNome,
        "A sessão do beneficiário não traz o nome completo, que o aprovador precisa para julgar o pedido.");

    // O beneficiário é o próprio solicitante quando o e-mail vem em branco ou é o do token dele, verificado:
    // quem usa o próprio e-mail já provou que é ele, e não há ninguém a quem esperar.
    private static (string? Email, bool ParaSiMesmo) ResolverBeneficiario(
        PartesDoPedido partes, IDominiosInstitucionais dominios, List<FieldError> erros) =>
        ParaSiMesmo(partes.Solicitante, partes.EmailDoBeneficiario)
            ? (ResolverParaSiMesmo(partes.Solicitante, dominios, erros), true)
            : (ResolverParaOutraPessoa(partes.EmailDoBeneficiario, dominios, erros), false);

    private static string? ResolverParaSiMesmo(PessoaNoToken solicitante, IDominiosInstitucionais dominios, List<FieldError> erros)
    {
        string? doToken = solicitante.EmailVerificado ? EmailNormalizado(solicitante.Email) : null;
        if (doToken is null || !DominioInstitucional(doToken, dominios))
        {
            erros.Add(new("beneficiarioEmail", new DomainError(
                PedidoErrorCodes.BeneficiarioInvalido,
                "Para pedir para si, o e-mail da sessão deve ser institucional e verificado; para outra pessoa, informe o e-mail institucional dela.")));
        }

        if (NomeDeExibicao.Normalizar(solicitante.Nome) is null)
        {
            erros.Add(new("beneficiario", SemNome()));
        }

        return doToken;
    }

    private static string? ResolverParaOutraPessoa(string? emailInformado, IDominiosInstitucionais dominios, List<FieldError> erros)
    {
        string? informado = EmailNormalizado(emailInformado);
        if (informado is null || !DominioInstitucional(informado, dominios))
        {
            erros.Add(new("beneficiarioEmail", new DomainError(
                PedidoErrorCodes.BeneficiarioInvalido,
                "O beneficiário é identificado pelo e-mail institucional dele.")));
        }

        return informado;
    }

    private static bool ParaSiMesmo(PessoaNoToken solicitante, string? emailInformado)
    {
        string? informado = EmailNormalizado(emailInformado);
        string? doToken = solicitante.EmailVerificado ? EmailNormalizado(solicitante.Email) : null;

        return string.IsNullOrWhiteSpace(emailInformado) || (informado is not null && informado == doToken);
    }

    /// <summary>O e-mail da sessão é verificado e de domínio institucional: quem pede por outra pessoa responde por ela.</summary>
    public static bool SolicitanteInstitucional(PessoaNoToken pessoa, IDominiosInstitucionais dominios)
    {
        ArgumentNullException.ThrowIfNull(pessoa);
        ArgumentNullException.ThrowIfNull(dominios);

        return pessoa.EmailVerificado
            && EmailNormalizado(pessoa.Email) is { } email
            && DominioInstitucional(email, dominios);
    }

    private static bool DominioInstitucional(string email, IDominiosInstitucionais dominios) =>
        dominios.Contem(email[(email.LastIndexOf('@') + 1)..]);

    /// <summary>O e-mail como o pedido o guarda e compara (minúsculas, formato válido); nulo se inválido.</summary>
    public static string? EmailNormalizado(string? email)
    {
        Result<Email> resultado = Email.Criar(email);

        return resultado.IsSuccess && resultado.Value!.Valor.Length <= EmailMaxLength && !resultado.Value.Valor.Contains('\0', StringComparison.Ordinal)
            ? resultado.Value.Valor
            : null;
    }
}
