namespace Unifesspa.UniPlus.Autorizacao.Application.Commands.Instalacoes;

using Unifesspa.UniPlus.Autorizacao.Domain.Entities;
using Unifesspa.UniPlus.Autorizacao.Domain.Interfaces;
using Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// O que o início da instalação confere antes de gravar: o e-mail institucional de cada gestor, que é o que a revogação da
/// concessão pede de volta, e que ainda não haja gestor ativo, para poupar o trabalho do documento. A conferência definitiva
/// do gestor é a da confirmação, sob a trava de governança. O emissor aceito é o do próprio token, que a validação já
/// confrontou com o emissor configurado.
/// </summary>
public sealed class ConferenciaDaNovaInstalacao
{
    private readonly IConcessaoRepository _concessoes;
    private readonly IDominiosInstitucionais _dominios;

    public ConferenciaDaNovaInstalacao(IConcessaoRepository concessoes, IDominiosInstitucionais dominios)
    {
        ArgumentNullException.ThrowIfNull(concessoes);
        ArgumentNullException.ThrowIfNull(dominios);

        _concessoes = concessoes;
        _dominios = dominios;
    }

    internal async Task<Result<Instalacao>> ConferirAsync(
        IniciarInstalacaoCommand command, Identidade executor, DateTimeOffset agora, CancellationToken cancellationToken)
    {
        if (GestoresDesignados.AlgumEmailForaDoDominioInstitucional(command.Gestores, _dominios))
        {
            return Result<Instalacao>.ValidationFailure([new("gestores", InstalacaoHandlerErros.GestorComEmailNaoInstitucional())]);
        }

        return await HaGestorAtivoAsync(agora, cancellationToken).ConfigureAwait(false)
            ? Result<Instalacao>.Failure(InstalacaoHandlerErros.JaHaGestorAoIniciar())
            : Instalacao.Iniciar(executor, command.Gestores, executor.Emissor, command.NumeroDoDocumento, command.TamanhoDoPdf, agora);
    }

    private async Task<bool> HaGestorAtivoAsync(DateTimeOffset agora, CancellationToken cancellationToken) =>
        await _concessoes.ContarGestoresAtivosAsync(PerfisDeSistema.GestorDeAcessosId, Guid.Empty, agora, cancellationToken).ConfigureAwait(false) > 0;
}
