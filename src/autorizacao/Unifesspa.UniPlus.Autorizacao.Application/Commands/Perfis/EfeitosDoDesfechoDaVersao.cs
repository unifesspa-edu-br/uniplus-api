namespace Unifesspa.UniPlus.Autorizacao.Application.Commands.Perfis;

using Unifesspa.UniPlus.Application.Abstractions.Email;
using Unifesspa.UniPlus.Authorization.Catalogo;
using Unifesspa.UniPlus.Autorizacao.Application.DTOs;
using Unifesspa.UniPlus.Autorizacao.Application.Mappings;
using Unifesspa.UniPlus.Autorizacao.Domain.Entities;
using Unifesspa.UniPlus.Autorizacao.Domain.Enums;
using Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Traduz o desfecho de uma operação do agregado sobre a versão no que a camada de aplicação faz com ele: os efeitos da
/// troca de vigência sobre concessões e pedidos, os avisos do que mudou e a resposta com o perfil. Tudo roda na transação
/// e sob a trava de quem chama, e os avisos só saem se a gravação acontece.
/// </summary>
public sealed class EfeitosDoDesfechoDaVersao
{
    private readonly EfeitosDaTrocaDeVigencia _efeitos;
    private readonly AvisosDaVersao _avisos;
    private readonly AvisoDeEtapaApta _etapaApta;
    private readonly CatalogoDaAplicacao _catalogo;
    private readonly ConferenciaDoCatalogoNaVersao _conferencia;

    public EfeitosDoDesfechoDaVersao(
        EfeitosDaTrocaDeVigencia efeitos,
        AvisosDaVersao avisos,
        AvisoDeEtapaApta etapaApta,
        CatalogoDaAplicacao catalogo,
        ConferenciaDoCatalogoNaVersao conferencia)
    {
        ArgumentNullException.ThrowIfNull(efeitos);
        ArgumentNullException.ThrowIfNull(avisos);
        ArgumentNullException.ThrowIfNull(etapaApta);
        ArgumentNullException.ThrowIfNull(catalogo);
        ArgumentNullException.ThrowIfNull(conferencia);

        _efeitos = efeitos;
        _avisos = avisos;
        _etapaApta = etapaApta;
        _catalogo = catalogo;
        _conferencia = conferencia;
    }

    /// <param name="perfil">O perfil já alterado pela operação.</param>
    /// <param name="desfecho">O que a operação produziu.</param>
    /// <param name="autor">Quem praticou o ato; não recebe o aviso dele.</param>
    /// <param name="cancellationToken">Token de cancelamento.</param>
    public async Task<DesfechoDaOperacao<PerfilDto>> AplicarAsync(
        Perfil perfil, DesfechoDaVersao desfecho, Identidade autor, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(perfil);
        ArgumentNullException.ThrowIfNull(desfecho);
        ArgumentNullException.ThrowIfNull(autor);

        IReadOnlyList<EnviarEmail> avisos = await AvisosAsync(perfil, desfecho, autor, cancellationToken).ConfigureAwait(false);
        Result<PerfilDto> resposta = desfecho.Erro is { } erro
            ? Result<PerfilDto>.Failure(erro)
            : Result<PerfilDto>.Success(PerfilMapping.ToDto(perfil, _catalogo, autor));

        return new DesfechoDaOperacao<PerfilDto>(resposta, desfecho.Gravar, avisos);
    }

    private async Task<IReadOnlyList<EnviarEmail>> AvisosAsync(
        Perfil perfil, DesfechoDaVersao desfecho, Identidade autor, CancellationToken cancellationToken)
    {
        IReadOnlyList<EnviarEmail> doFato = await DoFatoAsync(perfil, desfecho, autor, cancellationToken).ConfigureAwait(false);
        IReadOnlyList<EnviarEmail> etapaApta = desfecho.Gravar
            ? await _etapaApta.EmitirAsync(perfil, cancellationToken).ConfigureAwait(false)
            : [];

        return [.. doFato, .. etapaApta];
    }

    private async Task<IReadOnlyList<EnviarEmail>> DoFatoAsync(
        Perfil perfil, DesfechoDaVersao desfecho, Identidade autor, CancellationToken cancellationToken)
    {
        List<EnviarEmail> avisos = [];
        if (desfecho.Troca is { } troca)
        {
            avisos.AddRange(await DaTrocaAsync(perfil, troca, autor, cancellationToken).ConfigureAwait(false));
        }

        if (desfecho.Cancelada is { } cancelada)
        {
            avisos.AddRange(await _avisos.DoCancelamentoPorContencaoAsync(perfil, cancelada, autor, cancellationToken).ConfigureAwait(false));
        }

        avisos.AddRange(await DoRecalculoAsync(perfil, desfecho, cancellationToken).ConfigureAwait(false));

        return avisos;
    }

    // O recálculo do ciclo descarta as aprovações dadas, e quem as deu é avisado.
    private async Task<IReadOnlyList<EnviarEmail>> DoRecalculoAsync(Perfil perfil, DesfechoDaVersao desfecho, CancellationToken cancellationToken)
    {
        if (desfecho.Recalculo is not { } recalculo)
        {
            return [];
        }

        VersaoDoPerfil versao = perfil.Versoes.First(v => v.Id == recalculo.VersaoId);

        return await _avisos.DasAprovacoesDescartadasAsync(perfil, versao, desfecho, cancellationToken).ConfigureAwait(false);
    }

    private async Task<IReadOnlyList<EnviarEmail>> DaTrocaAsync(
        Perfil perfil, TrocaDeVigencia troca, Identidade autor, CancellationToken cancellationToken)
    {
        await _conferencia.RegistrarNaEntradaAsync(perfil, troca.Nova, cancellationToken).ConfigureAwait(false);
        if (troca.Anterior is null)
        {
            return [];
        }

        IReadOnlyList<EnviarEmail> dosEfeitos = await _efeitos.AplicarAsync(perfil, troca, autor, cancellationToken).ConfigureAwait(false);
        IReadOnlyList<EnviarEmail> daRemocao = troca.Nova.Origem == OrigemDaVersao.Remocao
            ? await _avisos.DaRemocaoAsync(perfil, troca, autor, cancellationToken).ConfigureAwait(false)
            : [];

        return [.. dosEfeitos, .. daRemocao];
    }
}
