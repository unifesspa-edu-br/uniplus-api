namespace Unifesspa.UniPlus.Autorizacao.Application.Commands.Instalacoes;

using Unifesspa.UniPlus.Application.Abstractions.Authentication;
using Unifesspa.UniPlus.Application.Abstractions.Email;
using Unifesspa.UniPlus.Autorizacao.Application.Abstractions;
using Unifesspa.UniPlus.Autorizacao.Application.DTOs;
using Unifesspa.UniPlus.Autorizacao.Application.Mappings;
using Unifesspa.UniPlus.Autorizacao.Domain.Entities;
using Unifesspa.UniPlus.Autorizacao.Domain.Enums;
using Unifesspa.UniPlus.Autorizacao.Domain.Errors;
using Unifesspa.UniPlus.Autorizacao.Domain.Interfaces;
using Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Grava o resumo, o prazo e o estado e devolve o <see cref="EnviarEmail"/> como mensagem de saída: os dois entram
/// juntos no outbox (ADR-0004/0005), sem código sem resumo nem estado sem envio. O destinatário vem do token no
/// momento do envio, e a instalação não grava e-mail. O código só existe aqui e no e-mail. Com a instalação já
/// aguardando código, o mesmo executor pede um novo: o resumo anterior deixa de valer e o reenvio entra na trilha.
/// </summary>
public static class EnviarCodigoDaInstalacaoCommandHandler
{
    public static async Task<(Result<InstalacaoDto> Resposta, IEnumerable<object> Mensagens)> Handle(
        EnviarCodigoDaInstalacaoCommand command,
        IInstalacaoRepository repository,
        IResumoDeCodigoDeConfirmacao resumo,
        IAutorizacaoUnitOfWork unitOfWork,
        IUserContext userContext,
        IDominiosInstitucionais dominios,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(resumo);
        ArgumentNullException.ThrowIfNull(unitOfWork);
        ArgumentNullException.ThrowIfNull(userContext);
        ArgumentNullException.ThrowIfNull(dominios);
        ArgumentNullException.ThrowIfNull(timeProvider);

        if (IdentidadeDoToken.De(userContext) is not { } executor)
        {
            return Recusa(InstalacaoHandlerErros.AutorNaoIdentificado());
        }

        Instalacao? instalacao = await repository
            .ObterDoExecutorAsync(command.Id, executor, cancellationToken)
            .ConfigureAwait(false);
        if (instalacao is null)
        {
            return Recusa(InstalacaoHandlerErros.NaoEncontrada());
        }

        // A situação vem antes do e-mail: com o documento ainda sem veredicto, corrigir o e-mail não leva a nada.
        if (instalacao.Situacao is not (SituacaoDaInstalacao.DocumentoVerificado or SituacaoDaInstalacao.AguardandoCodigo))
        {
            return Recusa(new DomainError(
                InstalacaoErrorCodes.SituacaoInvalida,
                "O código só é enviado depois de o documento de designação ser verificado, e reenviado enquanto a instalação o aguarda."));
        }

        Email? destinatario = DestinatarioInstitucional(userContext, dominios);
        if (destinatario is null)
        {
            return Recusa(InstalacaoHandlerErros.EmailNaoInstitucional());
        }

        string codigo = CodigoDeConfirmacao.Gerar();
        byte[] resumoDoCodigo = await resumo.ResumirAsync(instalacao.Id, codigo, cancellationToken).ConfigureAwait(false);

        DateTimeOffset agora = timeProvider.GetUtcNow();
        bool reenvio = instalacao.CodigoJaEnviado;
        Result enviado = instalacao.EnviarCodigo(resumoDoCodigo, agora);
        if (enviado.IsFailure)
        {
            return Recusa(enviado.Error!);
        }

        if (reenvio)
        {
            await repository.AdicionarTentativaAsync(
                TentativaDeInstalacao.Registrar(instalacao.Id, executor, ResultadoDaTentativaDeInstalacao.CodigoReenviado, agora), cancellationToken)
                .ConfigureAwait(false);
        }

        try
        {
            await unitOfWork.SalvarAlteracoesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ViolacoesDeGravacao.EhConflitoDeConcorrencia(ex))
        {
            unitOfWork.DescartarAlteracoesNaoSalvas();
            return Recusa(InstalacaoHandlerErros.ConflitoDeConcorrencia());
        }

        EnviarEmail mensagem = EnviarEmail.Para(
            destinatario,
            "Código de confirmação da instalação dos gestores de acessos",
            $"Seu código de confirmação é {codigo}. Ele vale por {Instalacao.PrazoDoCodigo.TotalHours:0} horas, aceita até {Instalacao.MaximoDeTentativas} tentativas e só pode ser usado uma vez. "
                + "Se você não iniciou a instalação dos gestores de acessos do Uni+, ignore esta mensagem e avise a equipe de infraestrutura.",
            $"<p>Seu código de confirmação é <b>{codigo}</b>.</p>"
                + $"<p>Ele vale por {Instalacao.PrazoDoCodigo.TotalHours:0} horas, aceita até {Instalacao.MaximoDeTentativas} tentativas e só pode ser usado uma vez.</p>"
                + "<p>Se você não iniciou a instalação dos gestores de acessos do Uni+, ignore esta mensagem e avise a equipe de infraestrutura.</p>",
            "instalacao.codigo");

        return (Result<InstalacaoDto>.Success(InstalacaoMapping.ToDto(instalacao)), [mensagem]);
    }

    // O e-mail do token é o do gov.br e pode ser pessoal: o código só vai a e-mail verificado de domínio institucional.
    private static Email? DestinatarioInstitucional(IUserContext userContext, IDominiosInstitucionais dominios)
    {
        if (!userContext.EmailVerificado)
        {
            return null;
        }

        Result<Email> email = Email.Criar(userContext.Email);
        if (email.IsFailure)
        {
            return null;
        }

        string dominio = email.Value!.Valor[(email.Value.Valor.LastIndexOf('@') + 1)..];

        return dominios.Contem(dominio) ? email.Value : null;
    }

    private static (Result<InstalacaoDto> Resposta, IEnumerable<object> Mensagens) Recusa(DomainError erro) =>
        (Result<InstalacaoDto>.Failure(erro), []);
}
