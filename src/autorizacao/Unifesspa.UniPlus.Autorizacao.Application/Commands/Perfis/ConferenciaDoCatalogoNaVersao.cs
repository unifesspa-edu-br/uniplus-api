namespace Unifesspa.UniPlus.Autorizacao.Application.Commands.Perfis;

using Unifesspa.UniPlus.Authorization.Abstractions;
using Unifesspa.UniPlus.Authorization.Catalogo;
using Unifesspa.UniPlus.Autorizacao.Domain.Entities;
using Unifesspa.UniPlus.Autorizacao.Domain.Interfaces;
using Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;

/// <summary>
/// A conferência da versão com o catálogo gravado no banco, sob a trava do perfil. A instância cujo catálogo não é o do banco
/// é anterior à implantação que o trocou e não decide aprovação. A versão que entra em vigor com permissão que o catálogo
/// atual reclassificou para cima, ou de escopo diferente, ganha o fato da reclassificação do perfil, se ainda não há: a
/// concessão nessa versão fica suspensa pela mesma regra da base e tem item na fila. O retrato da versão não muda.
/// </summary>
public sealed class ConferenciaDoCatalogoNaVersao
{
    private readonly IReclassificacaoRepository _reclassificacoes;
    private readonly CatalogoDaAplicacao _catalogo;
    private readonly TimeProvider _clock;

    public ConferenciaDoCatalogoNaVersao(IReclassificacaoRepository reclassificacoes, CatalogoDaAplicacao catalogo, TimeProvider clock)
    {
        ArgumentNullException.ThrowIfNull(reclassificacoes);
        ArgumentNullException.ThrowIfNull(catalogo);
        ArgumentNullException.ThrowIfNull(clock);

        _reclassificacoes = reclassificacoes;
        _catalogo = catalogo;
        _clock = clock;
    }

    /// <summary>Recusa decidir quando o catálogo do banco não é o desta instância.</summary>
    /// <exception cref="CatalogoDesatualizadoException">A impressão do banco difere da da instância, ou não há catálogo gravado.</exception>
    public async Task ExigirCatalogoAtualAsync(CancellationToken cancellationToken)
    {
        string? impressao = await _reclassificacoes.ObterImpressaoAtualAsync(cancellationToken).ConfigureAwait(false);
        if (!string.Equals(impressao, _catalogo.Impressao, StringComparison.Ordinal))
        {
            throw new CatalogoDesatualizadoException();
        }
    }

    /// <summary>Registra a reclassificação das permissões da versão que entra em vigor que o catálogo atual suspende.</summary>
    public async Task RegistrarNaEntradaAsync(Perfil perfil, VersaoDoPerfil entrante, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(perfil);
        ArgumentNullException.ThrowIfNull(entrante);

        ImplantacaoNoBanco? atual = await _reclassificacoes.ObterImplantacaoAtualAsync(entrante.Codigos(), cancellationToken).ConfigureAwait(false);
        if (atual is null)
        {
            return;
        }

        IReadOnlySet<string> jaReclassificados = await _reclassificacoes
            .ObterCodigosReclassificadosAsync(atual.Id, perfil.Id, cancellationToken).ConfigureAwait(false);
        DateTimeOffset agora = _clock.GetUtcNow();

        foreach (PermissaoDaVersao permissao in entrante.Permissoes.Where(p => !jaReclassificados.Contains(p.Codigo)))
        {
            RegistrarSeSuspende(perfil, atual, permissao, agora);
        }
    }

    private void RegistrarSeSuspende(Perfil perfil, ImplantacaoNoBanco atual, PermissaoDaVersao permissao, DateTimeOffset agora)
    {
        BaseDaConcessao retrato = new(permissao.Classificacao, permissao.DeUnidade);
        if (atual.Linhas.TryGetValue(permissao.Codigo, out LinhaDoRetratoDoCatalogo? linha)
            && retrato.SuspendeFrenteA(linha.Classificacao, linha.DeUnidade)
            && ComparacaoComOCatalogo.MudancaDe(retrato, linha) is { } mudanca)
        {
            _reclassificacoes.Adicionar(ReclassificacaoDaPermissao.Registrar(atual.Id, perfil.Id, permissao.Codigo, mudanca, agora, 0));
        }
    }
}
