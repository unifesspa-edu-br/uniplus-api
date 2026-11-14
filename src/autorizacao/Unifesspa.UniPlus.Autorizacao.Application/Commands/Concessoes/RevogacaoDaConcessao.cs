namespace Unifesspa.UniPlus.Autorizacao.Application.Commands.Concessoes;

using Unifesspa.UniPlus.Application.Abstractions.Interfaces;
using Unifesspa.UniPlus.Autorizacao.Domain.Entities;
using Unifesspa.UniPlus.Autorizacao.Domain.Enums;
using Unifesspa.UniPlus.Autorizacao.Domain.Interfaces;
using Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// As regras que toda revogação de concessão cumpre, qualquer que seja o caminho que a pede (a revogação direta, o
/// atendimento da providência e a revogação do item da revisão de acessos): a concessão de gestor de acessos só se revoga
/// se sobrarem ao menos dois gestores distintos com concessão ativa (ADR-0152), e a de Representante do controlador só se
/// a instituição não ficar sem nenhuma pessoa com concessão vigente dele (UNI-REQ-0394).
/// </summary>
/// <remarks>
/// Pré-condição: quem chama já tomou a trava do papel de Representante antes de revogar a concessão dele, na ordem da
/// ADR-0162. A contagem dos Representantes roda sob essa trava e não a retoma aqui: retomá-la inverteria a ordem para quem
/// a tivesse esquecido. Duas revogações simultâneas dos dois últimos Representantes se serializam por ela, e a segunda
/// lê a primeira já gravada.
/// </remarks>
public sealed class RevogacaoDaConcessao
{
    private const int MinimoDeGestores = 2;

    private readonly IConcessaoRepository _concessoes;
    private readonly IPapelDoRepresentante _papel;
    private readonly IUnitOfWork _unitOfWork;

    public RevogacaoDaConcessao(IConcessaoRepository concessoes, IPapelDoRepresentante papel, IUnitOfWork unitOfWork)
    {
        ArgumentNullException.ThrowIfNull(concessoes);
        ArgumentNullException.ThrowIfNull(papel);
        ArgumentNullException.ThrowIfNull(unitOfWork);

        _concessoes = concessoes;
        _papel = papel;
        _unitOfWork = unitOfWork;
    }

    /// <summary>
    /// Aplica a revogação e confere o mínimo do papel que ela reduz, sem gravar: quem chama grava, junto do que mais a
    /// revogação altera. Na recusa o rastreamento é descartado, porque a concessão já foi mutada em memória e a gravação que
    /// o outbox dispara depois do handler a persistiria.
    /// </summary>
    /// <param name="revogar">Aplica a revogação à <paramref name="concessao"/>; falha sem tê-la mutado.</param>
    internal async Task<Result> AplicarAsync(Concessao concessao, DateTimeOffset agora, Func<Result> revogar, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(concessao);
        ArgumentNullException.ThrowIfNull(revogar);

        // O que a concessão conta se lê antes de revogá-la, porque depois ela já não está ativa.
        Conta conta = await ContarAsync(concessao, agora, cancellationToken).ConfigureAwait(false);

        Result revogada = revogar();
        if (revogada.IsFailure)
        {
            return revogada;
        }

        DomainError? recusa = await RecusaAsync(concessao, agora, conta, cancellationToken).ConfigureAwait(false);
        return recusa is null ? Result.Success() : Recusar(recusa);
    }

    // Revogar uma concessão vencida ou fora da instituição não muda a conta de nenhum papel.
    private async Task<Conta> ContarAsync(Concessao concessao, DateTimeOffset agora, CancellationToken cancellationToken) =>
        new(concessao.EstaAtiva(agora) && concessao.EscopoTipo == TipoDeEscopo.Instituicao,
            await DeixariaSemRepresentanteAsync(concessao, agora, cancellationToken).ConfigureAwait(false));

    private async Task<DomainError?> RecusaAsync(Concessao concessao, DateTimeOffset agora, Conta conta, CancellationToken cancellationToken)
    {
        if (conta.SemRepresentante)
        {
            return ConcessaoHandlerErros.UltimoRepresentante();
        }

        return conta.ContaComoGestor ? await RecusaDosGestoresAsync(concessao, agora, cancellationToken).ConfigureAwait(false) : null;
    }

    private Result Recusar(DomainError recusa)
    {
        _unitOfWork.DescartarAlteracoesNaoSalvas();
        return Result.Failure(recusa);
    }

    private async Task<bool> DeixariaSemRepresentanteAsync(Concessao concessao, DateTimeOffset agora, CancellationToken cancellationToken)
    {
        if (!ContinuidadeDoRepresentante.EDesignacaoVigente(concessao, agora))
        {
            return false;
        }

        Identidade beneficiario = new(concessao.BeneficiarioEmissor, concessao.BeneficiarioSub);
        bool haOutra = await _papel.HaOutroVigenteAsync(beneficiario, agora, cancellationToken).ConfigureAwait(false);

        return ContinuidadeDoRepresentante.RevogacaoDeixariaSemRepresentante(concessao, agora, haOutra);
    }

    private async Task<DomainError?> RecusaDosGestoresAsync(Concessao concessao, DateTimeOffset agora, CancellationToken cancellationToken) =>
        await _concessoes.ObterGovernancaDoPerfilAsync(concessao.PerfilId, cancellationToken).ConfigureAwait(false) == TipoDeGovernanca.GestorDeAcessos
            ? await RecusaDoMinimoDeGestoresAsync(concessao, agora, cancellationToken).ConfigureAwait(false)
            : null;

    private async Task<DomainError?> RecusaDoMinimoDeGestoresAsync(Concessao concessao, DateTimeOffset agora, CancellationToken cancellationToken)
    {
        // A contagem roda sob a trava, depois que ela é obtida: duas revogações simultâneas de
        // gestores diferentes (de três para um) não podem passar as duas.
        await _concessoes.TravarGovernancaParaEscritaAsync(cancellationToken).ConfigureAwait(false);

        int restantes = await _concessoes
            .ContarGestoresAtivosAsync(concessao.PerfilId, concessao.Id, agora, cancellationToken)
            .ConfigureAwait(false);

        return restantes < MinimoDeGestores ? ConcessaoHandlerErros.MinimoDeGestores() : null;
    }

    private readonly record struct Conta(bool ContaComoGestor, bool SemRepresentante);
}
