namespace Unifesspa.UniPlus.Autorizacao.Application.Queries.Reclassificacoes;

using Unifesspa.UniPlus.Autorizacao.Application.Commands.Reclassificacoes;
using Unifesspa.UniPlus.Autorizacao.Application.DTOs;
using Unifesspa.UniPlus.Autorizacao.Domain.Entities;
using Unifesspa.UniPlus.Autorizacao.Domain.Enums;
using Unifesspa.UniPlus.Autorizacao.Domain.Interfaces;

/// <summary>
/// A fila de uma etapa: as reclassificações vigentes de cada perfil e permissão que ainda têm concessão suspensa à
/// espera da etapa, da mais antiga para a mais nova. A entrada vem do que ainda se exige contra a base de cada concessão,
/// não da direção da última mudança: a descida que não leva a classe abaixo da base de uma concessão suspensa mantém essa
/// concessão na fila, e é a reclassificação vigente que se decide.
/// </summary>
public sealed class FilaDeReclassificacoes
{
    private readonly IReclassificacaoRepository _reclassificacoes;
    private readonly ConcessoesDaReclassificacao _concessoes;
    private readonly TimeProvider _clock;

    public FilaDeReclassificacoes(IReclassificacaoRepository reclassificacoes, ConcessoesDaReclassificacao concessoes, TimeProvider clock)
    {
        ArgumentNullException.ThrowIfNull(reclassificacoes);
        ArgumentNullException.ThrowIfNull(concessoes);
        ArgumentNullException.ThrowIfNull(clock);

        _reclassificacoes = reclassificacoes;
        _concessoes = concessoes;
        _clock = clock;
    }

    public async Task<IReadOnlyList<ItemDaFilaDeReclassificacoesDto>> MontarAsync(EtapaDaVersao etapa, CancellationToken cancellationToken)
    {
        IReadOnlyList<ReclassificacaoDaPermissao> candidatas = await _reclassificacoes
            .ListarVigentesQueSuspendemAsync(cancellationToken).ConfigureAwait(false);
        IReadOnlyDictionary<Guid, string> nomes = await _reclassificacoes
            .ObterNomesDosPerfisAsync([.. candidatas.Select(static r => r.PerfilId).Distinct()], cancellationToken).ConfigureAwait(false);
        List<ItemDaFilaDeReclassificacoesDto> fila = [];

        foreach (ReclassificacaoDaPermissao reclassificacao in candidatas)
        {
            int aguardando = await ContarAsync(reclassificacao, etapa, cancellationToken).ConfigureAwait(false);
            if (aguardando > 0)
            {
                fila.Add(Item(reclassificacao, nomes.GetValueOrDefault(reclassificacao.PerfilId, string.Empty), aguardando));
            }
        }

        return fila;
    }

    private static ItemDaFilaDeReclassificacoesDto Item(ReclassificacaoDaPermissao reclassificacao, string perfilNome, int aguardando) =>
        new(reclassificacao.Id, reclassificacao.PerfilId, perfilNome, reclassificacao.Codigo,
            DetalheDaReclassificacao.Mudanca(reclassificacao), reclassificacao.Instante, aguardando);

    private async Task<int> ContarAsync(ReclassificacaoDaPermissao reclassificacao, EtapaDaVersao etapa, CancellationToken cancellationToken) =>
        (await _concessoes.LerSuspensasAsync(reclassificacao, _clock.GetUtcNow(), cancellationToken).ConfigureAwait(false))
            .Count(c => c.Aguarda(reclassificacao, etapa));
}
