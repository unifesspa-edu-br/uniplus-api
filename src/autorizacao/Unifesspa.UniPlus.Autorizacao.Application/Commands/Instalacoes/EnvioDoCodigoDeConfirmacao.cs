namespace Unifesspa.UniPlus.Autorizacao.Application.Commands.Instalacoes;

using Unifesspa.UniPlus.Application.Abstractions.Interfaces;
using Unifesspa.UniPlus.Autorizacao.Application.Abstractions;
using Unifesspa.UniPlus.Autorizacao.Application.DTOs;
using Unifesspa.UniPlus.Autorizacao.Application.Mappings;
using Unifesspa.UniPlus.Autorizacao.Domain.Entities;
using Unifesspa.UniPlus.Autorizacao.Domain.Enums;
using Unifesspa.UniPlus.Autorizacao.Domain.Interfaces;
using Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Grava o resumo, o prazo e o estado e devolve o e-mail como mensagem de saída: os dois entram juntos no outbox
/// (ADR-0004/0005), sem código sem resumo nem estado sem envio. O destinatário vem do token no momento do envio, e a
/// instalação não grava e-mail. O código só existe aqui e no e-mail. Com a instalação já aguardando código, o mesmo
/// executor pede um novo: o resumo anterior deixa de valer e o reenvio entra na trilha.
/// </summary>
public sealed class EnvioDoCodigoDeConfirmacao
{
    private readonly IInstalacaoRepository _instalacoes;
    private readonly IResumoDeCodigoDeConfirmacao _resumo;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IDominiosInstitucionais _dominios;
    private readonly TimeProvider _timeProvider;

    public EnvioDoCodigoDeConfirmacao(
        IInstalacaoRepository instalacoes,
        IResumoDeCodigoDeConfirmacao resumo,
        IUnitOfWork unitOfWork,
        IDominiosInstitucionais dominios,
        TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(instalacoes);
        ArgumentNullException.ThrowIfNull(resumo);
        ArgumentNullException.ThrowIfNull(unitOfWork);
        ArgumentNullException.ThrowIfNull(dominios);
        ArgumentNullException.ThrowIfNull(timeProvider);

        _instalacoes = instalacoes;
        _resumo = resumo;
        _unitOfWork = unitOfWork;
        _dominios = dominios;
        _timeProvider = timeProvider;
    }

    internal async Task<(Result<InstalacaoDto> Resposta, IEnumerable<object> Mensagens)> EnviarAsync(
        Guid instalacaoId, PessoaNoToken executor, CancellationToken cancellationToken)
    {
        Result<EnvioConferido> conferido = await ConferirAsync(instalacaoId, executor, cancellationToken).ConfigureAwait(false);
        if (conferido.IsFailure)
        {
            return Recusa(conferido.Error!);
        }

        string codigo = CodigoDeConfirmacao.Gerar();
        Instalacao instalacao = conferido.Value!.Instalacao;
        Result gravado = await RegistrarAsync(instalacao, executor.Identidade, codigo, cancellationToken).ConfigureAwait(false);

        return gravado.IsFailure
            ? Recusa(gravado.Error!)
            : (Result<InstalacaoDto>.Success(InstalacaoMapping.ToDto(instalacao)), [MensagemDoCodigoDeConfirmacao.Para(conferido.Value.Destinatario, codigo)]);
    }

    /// <summary>
    /// A instalação do executor e o destinatário do código. A situação vem antes do e-mail: com o documento ainda sem
    /// veredicto, corrigir o e-mail não leva a nada.
    /// </summary>
    private async Task<Result<EnvioConferido>> ConferirAsync(Guid instalacaoId, PessoaNoToken executor, CancellationToken cancellationToken)
    {
        Instalacao? instalacao = await _instalacoes.ObterDoExecutorAsync(instalacaoId, executor.Identidade, cancellationToken).ConfigureAwait(false);
        if (instalacao is null)
        {
            return Result<EnvioConferido>.Failure(InstalacaoHandlerErros.NaoEncontrada());
        }

        if (!instalacao.AceitaEnvioDoCodigo)
        {
            return Result<EnvioConferido>.Failure(InstalacaoHandlerErros.EnvioDoCodigoForaDaSituacao());
        }

        return DestinatarioInstitucional(executor) is { } destinatario
            ? Result<EnvioConferido>.Success(new EnvioConferido(instalacao, destinatario))
            : Result<EnvioConferido>.Failure(InstalacaoHandlerErros.EmailNaoInstitucional());
    }

    private async Task<Result> RegistrarAsync(
        Instalacao instalacao, Identidade executor, string codigo, CancellationToken cancellationToken)
    {
        byte[] resumo = await _resumo.ResumirAsync(instalacao.Id, codigo, cancellationToken).ConfigureAwait(false);
        DateTimeOffset agora = _timeProvider.GetUtcNow();
        bool reenvio = instalacao.CodigoJaEnviado;
        Result passo = instalacao.EnviarCodigo(resumo, agora);
        if (passo.IsSuccess && reenvio)
        {
            TentativaDeInstalacao reenviado = TentativaDeInstalacao.Registrar(instalacao.Id, executor, ResultadoDaTentativaDeInstalacao.CodigoReenviado, agora);
            await _instalacoes.AdicionarTentativaAsync(reenviado, cancellationToken).ConfigureAwait(false);
        }

        return await GravacaoDaInstalacao.GravarPassoAsync(passo, _unitOfWork, cancellationToken).ConfigureAwait(false);
    }

    // O e-mail do token é o do gov.br e pode ser pessoal: o código só vai a e-mail verificado de domínio institucional.
    private Email? DestinatarioInstitucional(PessoaNoToken executor)
    {
        if (!executor.EmailVerificado)
        {
            return null;
        }

        Result<Email> email = Email.Criar(executor.Email);

        return email.IsSuccess && EmailDaPessoa.Institucional(email.Value!.Valor, _dominios) ? email.Value : null;
    }

    private static (Result<InstalacaoDto> Resposta, IEnumerable<object> Mensagens) Recusa(DomainError erro) =>
        (Result<InstalacaoDto>.Failure(erro), []);

    private sealed record EnvioConferido(Instalacao Instalacao, Email Destinatario);
}
