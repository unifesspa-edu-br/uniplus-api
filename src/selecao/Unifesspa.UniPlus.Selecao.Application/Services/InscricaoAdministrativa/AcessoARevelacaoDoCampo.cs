namespace Unifesspa.UniPlus.Selecao.Application.Services.InscricaoAdministrativa;

using Unifesspa.UniPlus.Authorization.Abstractions;
using Unifesspa.UniPlus.Authorization.Contracts;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Application.Permissoes;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// A decisão de acesso à revelação de um campo identificador pela consulta administrativa (ADR-0153), tomada antes de
/// procurar qualquer resposta. A ordem é a que orienta a recusa: a inscrição alcançada por consultar as inscrições na
/// unidade dona do processo; a classe em que o identificador aparece, a que mostra o valor mascarado; e só então a permissão
/// de revelar. Sem a primeira, a resposta é a do campo que não existe, e a negativa é registrada. A recusa da classe
/// registra só a negativa dela, e nada é registrado como revelado.
/// </summary>
public sealed class AcessoARevelacaoDoCampo
{
    private static readonly PermissionRequirement[] PermissoesNaOrdemDaRota =
    [
        PermissaoSelecao.InscricoesConsultar.Requisito,
        PermissaoSelecao.DadosIdentificadoresConsultar.Requisito,
        PermissaoSelecao.DadosIdentificadoresRevelar.Requisito,
    ];

    private readonly IVerificadorDeAcesso _verificador;
    private readonly IProcessoSeletivoRepository _processos;
    private readonly IInscricaoRepository _inscricoes;

    public AcessoARevelacaoDoCampo(
        IVerificadorDeAcesso verificador, IProcessoSeletivoRepository processos, IInscricaoRepository inscricoes)
    {
        ArgumentNullException.ThrowIfNull(verificador);
        ArgumentNullException.ThrowIfNull(processos);
        ArgumentNullException.ThrowIfNull(inscricoes);

        _verificador = verificador;
        _processos = processos;
        _inscricoes = inscricoes;
    }

    /// <summary>
    /// O vínculo da inscrição, quando a pessoa pode revelar o campo; senão o erro com que a rota responde: o 404 do campo
    /// para o que não alcança, e a identidade incompleta e o impedimento pelo que são.
    /// </summary>
    public async Task<Result<VinculoDaInscricao>> DecidirAsync(
        Guid processoId, Guid inscricaoId, string fatoCodigo, string? ocorrencia, CancellationToken cancellationToken)
    {
        ConferenciaDoVinculo conferencia = await AcessoAInscricao
            .ConferirVinculoAsync(_processos, _inscricoes, processoId, inscricaoId, cancellationToken)
            .ConfigureAwait(false);

        return conferencia is Vinculo conferido
            ? await DecidirDoVinculoAsync(conferido, processoId, inscricaoId, fatoCodigo, ocorrencia, cancellationToken).ConfigureAwait(false)
            : await RecusarInexistenteAsync(conferencia, processoId, cancellationToken).ConfigureAwait(false);
    }

    private async Task<Result<VinculoDaInscricao>> DecidirDoVinculoAsync(
        Vinculo conferido, Guid processoId, Guid inscricaoId, string fatoCodigo, string? ocorrencia, CancellationToken cancellationToken)
    {
        Result<InscricaoAlcancada> alcancada = await AcessoDoOperadorAInscricao
            .AlcancarAsync(_verificador, conferido, processoId, inscricaoId, ErrosDoCampoDaInscricao.NaoEncontrado, cancellationToken)
            .ConfigureAwait(false);

        return alcancada.IsFailure
            ? Result<VinculoDaInscricao>.Failure(alcancada.Error!)
            : await DecidirDoCampoAsync(conferido, processoId, inscricaoId, fatoCodigo, ocorrencia, cancellationToken).ConfigureAwait(false);
    }

    private async Task<Result<VinculoDaInscricao>> DecidirDoCampoAsync(
        Vinculo conferido, Guid processoId, Guid inscricaoId, string fatoCodigo, string? ocorrencia, CancellationToken cancellationToken)
    {
        ResourceContext resource = AcessoAoProcesso.ResourceDoCampo(conferido.UnidadeId, processoId, inscricaoId, fatoCodigo, ocorrencia);
        DomainError? recusa = await RecusaDaClasseAsync(resource, cancellationToken).ConfigureAwait(false)
            ?? await RecusaDeRevelarAsync(conferido, resource, cancellationToken).ConfigureAwait(false);

        return recusa is null ? Result<VinculoDaInscricao>.Success(conferido.Inscricao) : Result<VinculoDaInscricao>.Failure(recusa);
    }

    private async Task<DomainError?> RecusaDaClasseAsync(ResourceContext resource, CancellationToken cancellationToken)
    {
        DecisaoDeAcesso daClasse = await _verificador
            .AvaliarAsync(PermissaoSelecao.DadosIdentificadoresConsultar.Requisito, resource, cancellationToken)
            .ConfigureAwait(false);
        if (daClasse is DecisaoPermitida)
        {
            return null;
        }

        if (daClasse is DecisaoNegada)
        {
            await _verificador.RegistrarDesfechoAsync([daClasse], IntencaoDoRegistro.Pedido, cancellationToken).ConfigureAwait(false);
        }

        return AcessoAInscricao.Erro(new Recusada(daClasse.Resultado), ErrosDoCampoDaInscricao.NaoEncontrado);
    }

    private async Task<DomainError?> RecusaDeRevelarAsync(Vinculo conferido, ResourceContext resource, CancellationToken cancellationToken) =>
        await AcessoAInscricao
            .DecidirAsync(_verificador, conferido, PermissaoSelecao.DadosIdentificadoresRevelar.Requisito, resource, cancellationToken)
            .ConfigureAwait(false) is Recusada recusada
            ? AcessoAInscricao.Erro(recusada, ErrosDoCampoDaInscricao.NaoEncontrado)
            : null;

    private async Task<Result<VinculoDaInscricao>> RecusarInexistenteAsync(
        ConferenciaDoVinculo conferencia, Guid processoId, CancellationToken cancellationToken)
    {
        await AcessoAInscricao
            .RegistrarNegativaDoInexistenteAsync(_verificador, conferencia, processoId, PermissoesNaOrdemDaRota, cancellationToken)
            .ConfigureAwait(false);

        return Result<VinculoDaInscricao>.Failure(ErrosDoCampoDaInscricao.NaoEncontrado);
    }
}
