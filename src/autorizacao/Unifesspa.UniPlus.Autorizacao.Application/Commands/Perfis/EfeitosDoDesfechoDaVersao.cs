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
    private readonly CatalogoDaAplicacao _catalogo;

    public EfeitosDoDesfechoDaVersao(EfeitosDaTrocaDeVigencia efeitos, AvisosDaVersao avisos, CatalogoDaAplicacao catalogo)
    {
        ArgumentNullException.ThrowIfNull(efeitos);
        ArgumentNullException.ThrowIfNull(avisos);
        ArgumentNullException.ThrowIfNull(catalogo);

        _efeitos = efeitos;
        _avisos = avisos;
        _catalogo = catalogo;
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
            : Result<PerfilDto>.Success(PerfilMapping.ToDto(perfil, _catalogo));

        return new DesfechoDaOperacao<PerfilDto>(resposta, desfecho.Gravar, avisos);
    }

    private async Task<IReadOnlyList<EnviarEmail>> AvisosAsync(
        Perfil perfil, DesfechoDaVersao desfecho, Identidade autor, CancellationToken cancellationToken)
    {
        List<EnviarEmail> avisos = [];
        if (desfecho.Troca is { } troca)
        {
            avisos.AddRange(await DaTrocaAsync(perfil, troca, autor, cancellationToken).ConfigureAwait(false));
        }

        if (desfecho.Cancelada is { } cancelada)
        {
            avisos.AddRange(await _avisos.DoCancelamentoPorContencaoAsync(perfil, cancelada, cancellationToken).ConfigureAwait(false));
        }

        if (desfecho.Recalculo is { } recalculo)
        {
            VersaoDoPerfil versao = perfil.Versoes.First(v => v.Id == recalculo.VersaoId);
            avisos.AddRange(await _avisos.DasAprovacoesDescartadasAsync(perfil, versao, desfecho, cancellationToken).ConfigureAwait(false));
        }

        return avisos;
    }

    private async Task<IReadOnlyList<EnviarEmail>> DaTrocaAsync(
        Perfil perfil, TrocaDeVigencia troca, Identidade autor, CancellationToken cancellationToken)
    {
        if (troca.Anterior is null)
        {
            return [];
        }

        IReadOnlyList<EnviarEmail> dosEfeitos = await _efeitos.AplicarAsync(perfil, troca, cancellationToken).ConfigureAwait(false);
        IReadOnlyList<EnviarEmail> daRemocao = troca.Nova.Origem == OrigemDaVersao.Remocao
            ? await _avisos.DaRemocaoAsync(perfil, troca, autor, cancellationToken).ConfigureAwait(false)
            : [];

        return [.. dosEfeitos, .. daRemocao];
    }
}
