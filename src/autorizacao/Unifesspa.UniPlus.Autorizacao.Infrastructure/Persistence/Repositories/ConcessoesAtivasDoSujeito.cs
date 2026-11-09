namespace Unifesspa.UniPlus.Autorizacao.Infrastructure.Persistence.Repositories;

using Microsoft.EntityFrameworkCore;

using Unifesspa.UniPlus.Authorization.Abstractions;
using Unifesspa.UniPlus.Authorization.Contracts;
using Unifesspa.UniPlus.Authorization.ValueObjects;
using Unifesspa.UniPlus.Autorizacao.Domain.Entities;
using Unifesspa.UniPlus.Autorizacao.Domain.Enums;

/// <summary>
/// Concessões gravadas de uma pessoa, lidas para a decisão de acesso. O módulo Autorização é o
/// dono das tabelas (ADR-0087), então a leitura mora aqui e o ponto de decisão só consome a porta.
/// </summary>
/// <remarks>
/// Sem cache entre requisições: a revogação vale na requisição seguinte. Dentro da mesma
/// requisição a pessoa é lida uma vez, porque o aprovador de um pedido consulta mais de uma
/// permissão. A permissão que cada concessão dá é calculada a cada requisição, pela regra de <c>ComposicaoEmVigorNoBanco</c>;
/// um cache futuro terá de respeitar a versão da concessão. O perfil desativado continua concedendo as concessões que já existem: desativar
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

        List<LinhaDaConcessao> linhas = await LerLinhasAsync(pessoa, cancellationToken).ConfigureAwait(false);
        _concessoesLidas = Agrupar(linhas);
        _pessoaLida = pessoa;

        return _concessoesLidas;
    }

    // Uma única instrução SQL: o retrato das concessões e dos códigos que elas dão é consistente sob READ COMMITTED.
    private Task<List<LinhaDaConcessao>> LerLinhasAsync(UsuarioRef pessoa, CancellationToken cancellationToken)
    {
        string emissor = pessoa.Emissor;
        string sub = pessoa.Subject;

        IQueryable<Concessao> daPessoa = _context.Concessoes.AsNoTracking()
            .Where(c => c.BeneficiarioEmissor == emissor && c.BeneficiarioSub == sub && c.RevogadaEm == null);

        return _context.ComCodigosEmVigor(daPessoa).ToListAsync(cancellationToken);
    }

    // A concessão sem permissão continua listada, com a lista vazia; só o código nulo é descartado.
    private static List<ConcessaoAtiva> Agrupar(List<LinhaDaConcessao> linhas) =>
    [
        .. linhas
            .GroupBy(l => l.Concessao)
            .Select(g => ConcessaoAtivaDe(g.Key, [.. g.Select(l => l.Codigo).OfType<string>()])),
    ];

    private static ConcessaoAtiva ConcessaoAtivaDe(ConcessaoLida c, IReadOnlyList<string> permissoes) =>
        new(c.Id, c.PerfilId, c.VersaoId, c.PerfilNome, c.UnidadeId, c.EscopoTipo == TipoDeEscopo.Instituicao, c.ValidaAte, permissoes);
}
