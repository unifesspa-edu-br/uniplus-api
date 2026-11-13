namespace Unifesspa.UniPlus.Selecao.Application.Services;

using Unifesspa.UniPlus.Authorization.Abstractions;
using Unifesspa.UniPlus.Authorization.Contracts;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Errors;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// O acesso de quem opera o processo a uma inscrição enviada, em duas fases que cada rota compõe na sua ordem: conferir
/// que a inscrição existe, é deste processo e não é rascunho, sem decidir; e decidir a permissão sobre o resource que a
/// rota monta com o que o vínculo revelou. O que não existe e o que a pessoa não pode ler respondem o mesmo erro da rota,
/// para que nenhum revele o outro. A recusa por falta de permissão é registrada nos dois casos; só o registro de quem
/// pediu o que não existe fica sem o id do resource.
/// </summary>
public static class AcessoAInscricao
{
    /// <summary>O erro com que as rotas do processo respondem a inscrição que não alcançam: a inexistente, a de outro processo, a em rascunho e a sem permissão.</summary>
    public static DomainError NaoEncontradaNoProcesso { get; } = new(
        InscricaoErrorCodes.NaoEncontrada,
        "Inscrição não encontrada neste processo seletivo.");

    /// <summary>
    /// Confere o vínculo da inscrição com o processo, sem decidir nem decifrar nada: a inscrição inexistente, a de outro
    /// processo e a em rascunho são <see cref="NaoEncontrada"/>, porque não há resource sobre o qual decidir. A
    /// não encontrada leva a unidade dona do processo, quando ele existe, para o registro de quem não podia consultar.
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

        UnidadeENomeDoProcesso? processo = await processos.ObterUnidadeENomeAsync(processoId, cancellationToken).ConfigureAwait(false);
        VinculoDaInscricao? vinculo = await inscricoes.ObterVinculoAsync(inscricaoId, cancellationToken).ConfigureAwait(false);

        return processo is not null
            && vinculo is not null
            && vinculo.ProcessoSeletivoId == processoId
            && vinculo.Situacao != SituacaoInscricao.Rascunho
            ? new Vinculo(processo.UnidadeId, processo.Nome, vinculo)
            : new NaoEncontrada(processo?.UnidadeId);
    }

    /// <summary>
    /// Registra a negativa de quem pediu uma inscrição que não existe sem ter as permissões da rota na unidade dona do
    /// processo, para que a sondagem apareça na trilha. As permissões são avaliadas na ordem em que a rota as exige, e só
    /// a primeira negada é registrada. O registro nomeia o motivo real e não leva o id do resource,
    /// que não existe; a resposta ao cliente continua a mesma da inscrição sem permissão. Quem tem a permissão errou o
    /// link, e o processo inexistente não tem unidade sobre a qual decidir: nenhum dos dois gera registro.
    /// </summary>
    public static async Task RegistrarNegativaDoInexistenteAsync(
        IVerificadorDeAcesso verificador,
        ConferenciaDoVinculo conferencia,
        Guid processoId,
        IReadOnlyList<PermissionRequirement> requisitos,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(verificador);
        ArgumentNullException.ThrowIfNull(conferencia);
        ArgumentNullException.ThrowIfNull(requisitos);

        if (conferencia is not NaoEncontrada { UnidadeDoProcesso: { } unidade })
        {
            return;
        }

        ResourceContext resource = AcessoAoProcesso.ResourceDaInscricao(unidade, processoId, inscricaoId: null);
        await RegistrarAPrimeiraNegadaAsync(verificador, requisitos, resource, cancellationToken).ConfigureAwait(false);
    }

    private static async Task RegistrarAPrimeiraNegadaAsync(
        IVerificadorDeAcesso verificador,
        IReadOnlyList<PermissionRequirement> requisitos,
        ResourceContext resource,
        CancellationToken cancellationToken)
    {
        foreach (PermissionRequirement requisito in requisitos)
        {
            DecisaoDeAcesso decisao = await verificador.AvaliarAsync(requisito, resource, cancellationToken).ConfigureAwait(false);

            // O permitido sobre o que não existe não se grava: a política o gravaria como acesso a uma inscrição.
            if (decisao is DecisaoNegada)
            {
                await verificador.RegistrarDesfechoAsync([decisao], IntencaoDoRegistro.Pedido, cancellationToken).ConfigureAwait(false);
                return;
            }
        }
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
/// <param name="ProcessoNome">O nome do processo, lido na mesma consulta da unidade.</param>
/// <param name="Inscricao">O vínculo da inscrição com o processo e a versão fixada.</param>
public sealed record Vinculo(Guid UnidadeId, string ProcessoNome, VinculoDaInscricao Inscricao) : ConferenciaDoVinculo;

/// <summary>A inscrição inexistente, de outro processo ou em rascunho.</summary>
/// <param name="UnidadeDoProcesso">A unidade que administra o processo da rota; nula quando o processo não existe.</param>
public sealed record NaoEncontrada(Guid? UnidadeDoProcesso) : ConferenciaDoVinculo;

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
