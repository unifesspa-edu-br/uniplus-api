namespace Unifesspa.UniPlus.Selecao.Application.Services;

using Unifesspa.UniPlus.Authorization.Abstractions;
using Unifesspa.UniPlus.Authorization.Contracts;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// O acesso de quem opera o processo a uma inscrição enviada, em duas fases que cada rota compõe na sua ordem: conferir
/// que a inscrição existe, é deste processo e não é rascunho, sem decidir; e decidir a permissão sobre o resource que a
/// rota monta com o que o vínculo revelou. O que não existe e o que a pessoa não pode ler respondem o mesmo erro da rota,
/// para que nenhum revele o outro.
/// </summary>
public static class AcessoAInscricao
{
    /// <summary>
    /// Confere o vínculo da inscrição com o processo, sem decidir nem decifrar nada: a inscrição inexistente, a de outro
    /// processo e a em rascunho são <see cref="NaoEncontrada"/>, porque não há resource sobre o qual decidir.
    /// </summary>
    public static async Task<ConferenciaDoVinculo> ConferirVinculoAsync(
        IProcessoSeletivoRepository processos,
        IInscricaoRepository inscricoes,
        Guid processoId,
        Guid inscricaoId,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(processos);
        ArgumentNullException.ThrowIfNull(inscricoes);

        Guid? unidade = await processos.ObterUnidadeAdministradoraAsync(processoId, cancellationToken).ConfigureAwait(false);
        VinculoDaInscricao? vinculo = await inscricoes.ObterVinculoAsync(inscricaoId, cancellationToken).ConfigureAwait(false);

        return unidade is { } dona
            && vinculo is not null
            && vinculo.ProcessoSeletivoId == processoId
            && vinculo.Situacao != SituacaoInscricao.Rascunho
            ? new Vinculo(dona, vinculo)
            : NaoEncontrada.Instancia;
    }

    /// <summary>Decide a permissão sobre o resource montado a partir do vínculo conferido.</summary>
    public static async Task<AcessoNaInscricao> DecidirAsync(
        IVerificadorDeAcesso verificador,
        Vinculo vinculo,
        PermissionRequirement requisito,
        ResourceContext resource,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(verificador);
        ArgumentNullException.ThrowIfNull(vinculo);
        ArgumentNullException.ThrowIfNull(requisito);

        ResultadoDoAcesso resultado = await verificador.ExigirAsync(requisito, resource, cancellationToken).ConfigureAwait(false);

        return resultado == ResultadoDoAcesso.Permitido ? Permitido.Instancia : new Recusada(resultado);
    }

    /// <summary>
    /// O erro com que a rota recusa a decisão: a identidade incompleta e o impedimento respondem o que são, e a falta de
    /// permissão responde o mesmo erro de <paramref name="naoEncontrada"/>.
    /// </summary>
    public static DomainError Erro(Recusada recusada, DomainError naoEncontrada)
    {
        ArgumentNullException.ThrowIfNull(recusada);
        ArgumentNullException.ThrowIfNull(naoEncontrada);

        return recusada.Resultado is ResultadoDoAcesso.IdentidadeIncompleta or ResultadoDoAcesso.Impedido
            ? AcessoAoProcesso.Recusa(recusada.Resultado)
            : naoEncontrada;
    }
}

/// <summary>O que a conferência do vínculo encontrou.</summary>
public abstract record ConferenciaDoVinculo
{
    private protected ConferenciaDoVinculo()
    {
    }
}

/// <summary>A inscrição enviada deste processo, com a unidade dona dele.</summary>
/// <param name="UnidadeId">A unidade que administra o processo.</param>
/// <param name="Inscricao">O vínculo da inscrição com o processo e a versão fixada.</param>
public sealed record Vinculo(Guid UnidadeId, VinculoDaInscricao Inscricao) : ConferenciaDoVinculo;

/// <summary>A inscrição inexistente, de outro processo ou em rascunho.</summary>
public sealed record NaoEncontrada : ConferenciaDoVinculo
{
    private NaoEncontrada()
    {
    }

    /// <summary>A única instância: o caso não carrega dado.</summary>
    public static NaoEncontrada Instancia { get; } = new();
}

/// <summary>O desfecho da decisão sobre a inscrição.</summary>
public abstract record AcessoNaInscricao
{
    private protected AcessoNaInscricao()
    {
    }
}

/// <summary>A pessoa pode.</summary>
public sealed record Permitido : AcessoNaInscricao
{
    private Permitido()
    {
    }

    /// <summary>A única instância: o caso não carrega dado.</summary>
    public static Permitido Instancia { get; } = new();
}

/// <summary>A pessoa não pode, ou não se identificou, ou está impedida por inscrição no processo.</summary>
/// <param name="Resultado">O desfecho que não foi permitido.</param>
public sealed record Recusada(ResultadoDoAcesso Resultado) : AcessoNaInscricao;
