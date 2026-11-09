namespace Unifesspa.UniPlus.Autorizacao.Infrastructure.Persistence.Repositories;

using Microsoft.EntityFrameworkCore;

using Unifesspa.UniPlus.Authorization.Abstractions;
using Unifesspa.UniPlus.Authorization.Contracts;
using Unifesspa.UniPlus.Authorization.ValueObjects;
using Unifesspa.UniPlus.Autorizacao.Domain.Enums;

/// <summary>
/// Concessões gravadas de uma pessoa, lidas para a decisão de acesso. O módulo Autorização é o
/// dono das tabelas (ADR-0087), então a leitura mora aqui e o ponto de decisão só consome a porta.
/// </summary>
/// <remarks>
/// Sem cache entre requisições: a revogação vale na requisição seguinte. Dentro da mesma
/// requisição a pessoa é lida uma vez, porque o aprovador de um pedido consulta mais de uma
/// permissão. O perfil desativado continua concedendo as concessões que já existem: desativar
/// só impede concessão nova, e retirar acesso é revogar.
/// </remarks>
public sealed class ConcessoesAtivasDoSujeito : IConcessoesAtivasDoSujeito
{
    private readonly AutorizacaoDbContext _context;

    private UsuarioRef? _pessoaLida;
    private IReadOnlyList<ConcessaoAtiva>? _concessoesLidas;

    public ConcessoesAtivasDoSujeito(AutorizacaoDbContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        _context = context;
    }

    public async Task<IReadOnlyList<ConcessaoAtiva>> ListarAsync(UsuarioRef pessoa, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(pessoa);

        if (_concessoesLidas is not null && pessoa == _pessoaLida)
        {
            return _concessoesLidas;
        }

        IReadOnlyList<ConcessaoLida> lidas = await LerConcessoesAsync(pessoa, cancellationToken).ConfigureAwait(false);
        _concessoesLidas = await ComporAsync(lidas, cancellationToken).ConfigureAwait(false);
        _pessoaLida = pessoa;

        return _concessoesLidas;
    }

    private async Task<IReadOnlyList<ConcessaoLida>> LerConcessoesAsync(UsuarioRef pessoa, CancellationToken cancellationToken)
    {
        string emissor = pessoa.Emissor;
        string sub = pessoa.Subject;

        return await (
            from c in _context.Concessoes.AsNoTracking()
            join p in _context.Perfis.AsNoTracking() on c.PerfilId equals p.Id
            where c.BeneficiarioEmissor == emissor && c.BeneficiarioSub == sub && c.RevogadaEm == null
            orderby c.ConcedidaEm, c.Id
            select new ConcessaoLida(c.Id, c.PerfilId, p.Nome, c.UnidadeId, c.EscopoTipo, c.ValidaAte))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    // A concessão vale pela composição em vigor do perfil: o perfil comum sem versão vigente não concede nada.
    private async Task<IReadOnlyList<ConcessaoAtiva>> ComporAsync(
        IReadOnlyList<ConcessaoLida> lidas, CancellationToken cancellationToken)
    {
        Dictionary<Guid, List<string>> permissoes = await _context
            .PermissoesEmVigorAsync([.. lidas.Select(c => c.PerfilId).Distinct()], cancellationToken)
            .ConfigureAwait(false);

        return
        [
            .. lidas.Select(c => new ConcessaoAtiva(
                c.Id,
                c.PerfilId,
                c.PerfilNome,
                c.UnidadeId,
                c.EscopoTipo == TipoDeEscopo.Instituicao,
                c.ValidaAte,
                permissoes.GetValueOrDefault(c.PerfilId) ?? [])),
        ];
    }

    private sealed record ConcessaoLida(
        Guid Id, Guid PerfilId, string PerfilNome, Guid? UnidadeId, TipoDeEscopo EscopoTipo, DateTimeOffset? ValidaAte);
}
