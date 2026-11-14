namespace Unifesspa.UniPlus.Autorizacao.Application.Commands.Revisoes;

using Unifesspa.UniPlus.Application.Abstractions.Interfaces;
using Unifesspa.UniPlus.Authorization.Catalogo;
using Unifesspa.UniPlus.Autorizacao.Application.DTOs;
using Unifesspa.UniPlus.Autorizacao.Application.Mappings;
using Unifesspa.UniPlus.Autorizacao.Domain.Entities;
using Unifesspa.UniPlus.Autorizacao.Domain.Interfaces;
using Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// A abertura da revisão periódica: traz as concessões ativas de perfil pessoal ou sensível no instante da abertura, pelo
/// mesmo critério com que o aprovador avalia o perfil, sem fixar quem decide cada item, o que se avalia na decisão
/// (ADR-0162). Só uma revisão fica em andamento por vez, conferida sob trava; a revisão sem nenhum item nasce concluída.
/// </summary>
public sealed class AberturaDaRevisao
{
    private readonly IRevisaoDeAcessosRepository _revisoes;
    private readonly LeituraDaRevisao _leitura;
    private readonly CatalogoDaAplicacao _catalogo;
    private readonly IUnitOfWork _unitOfWork;
    private readonly TimeProvider _clock;

    public AberturaDaRevisao(
        IRevisaoDeAcessosRepository revisoes,
        LeituraDaRevisao leitura,
        CatalogoDaAplicacao catalogo,
        IUnitOfWork unitOfWork,
        TimeProvider clock)
    {
        ArgumentNullException.ThrowIfNull(revisoes);
        ArgumentNullException.ThrowIfNull(leitura);
        ArgumentNullException.ThrowIfNull(catalogo);
        ArgumentNullException.ThrowIfNull(unitOfWork);
        ArgumentNullException.ThrowIfNull(clock);

        _revisoes = revisoes;
        _leitura = leitura;
        _catalogo = catalogo;
        _unitOfWork = unitOfWork;
        _clock = clock;
    }

    /// <summary>Abre a revisão sob a trava da abertura: duas aberturas simultâneas não passam as duas.</summary>
    public async Task<Result<RevisaoDeAcessosDto>> AbrirAsync(Identidade abertaPor, CancellationToken cancellationToken)
    {
        DateTimeOffset agora = _clock.GetUtcNow();

        await _revisoes.TravarAberturaAsync(cancellationToken).ConfigureAwait(false);
        if (await _revisoes.ExisteEmAndamentoAsync(agora, cancellationToken).ConfigureAwait(false))
        {
            return Result<RevisaoDeAcessosDto>.Failure(RevisaoHandlerErros.JaHaEmAndamento());
        }

        RevisaoDeAcessos revisao = RevisaoDeAcessos.Abrir(abertaPor, await ConcessoesARevisarAsync(agora, cancellationToken).ConfigureAwait(false), agora);
        await _revisoes.AdicionarAsync(revisao, cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SalvarAlteracoesAsync(cancellationToken).ConfigureAwait(false);

        RevisaoDeAcessosDto? dto = await _leitura.LerAsync(revisao.Id, agora, abertaPor, cancellationToken).ConfigureAwait(false);
        return Result<RevisaoDeAcessosDto>.Success(dto!);
    }

    private async Task<IReadOnlyList<Guid>> ConcessoesARevisarAsync(DateTimeOffset agora, CancellationToken cancellationToken)
    {
        IReadOnlyList<Perfil> perfis = await _revisoes.ListarPerfisComConcessaoAtivaAsync(agora, cancellationToken).ConfigureAwait(false);
        Guid[] pessoaisOuSensiveis =
        [
            .. perfis.Where(p => PerfilMapping.Avaliar(p, _catalogo).ExigeAprovacaoDeProtecaoDeDados).Select(static p => p.Id),
        ];

        IReadOnlyList<(Guid ConcessaoId, Guid PerfilId, Identidade Beneficiario)> ativas = await _revisoes
            .ListarConcessoesAtivasDosPerfisAsync(pessoaisOuSensiveis, agora, cancellationToken)
            .ConfigureAwait(false);

        return [.. ativas.Select(static c => c.ConcessaoId)];
    }
}
