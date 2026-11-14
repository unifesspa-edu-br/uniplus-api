namespace Unifesspa.UniPlus.Autorizacao.Infrastructure.Persistence.Repositories;

using Microsoft.EntityFrameworkCore;

using Unifesspa.UniPlus.Authorization.Abstractions;
using Unifesspa.UniPlus.Authorization.Catalogo;
using Unifesspa.UniPlus.Authorization.Contracts;
using Unifesspa.UniPlus.Authorization.ValueObjects;
using Unifesspa.UniPlus.Autorizacao.Domain.Entities;
using Unifesspa.UniPlus.Autorizacao.Domain.Enums;
using Unifesspa.UniPlus.Infrastructure.Core.Persistence;

/// <summary>
/// Concessões gravadas de uma pessoa, lidas para a decisão de acesso. O módulo Autorização é o
/// dono das tabelas (ADR-0087), então a leitura mora aqui e o ponto de decisão só consome a porta.
/// </summary>
/// <remarks>
/// Sem cache entre requisições: a revogação vale na requisição seguinte. Dentro da mesma
/// requisição a pessoa é lida uma vez, porque o aprovador de um pedido consulta mais de uma
/// permissão. A permissão que cada concessão dá é calculada a cada requisição, pela regra de <c>ComposicaoEmVigorNoBanco</c>;
/// um cache futuro terá de respeitar a versão da concessão. O perfil desativado continua concedendo as concessões que já existem: desativar
/// só impede concessão nova, e retirar acesso é revogar. O perfil suspenso não concede: as concessões continuam listadas,
/// sem permissão, até a retomada.
/// </remarks>
public sealed class ConcessoesAtivasDoSujeito : IConcessoesAtivasDoSujeito
{
    private readonly UniPlusDbContext _context;
    private readonly TimeProvider _relogio;
    private readonly CatalogoDaAplicacao _catalogo;

    private UsuarioRef? _pessoaLida;
    private IReadOnlyList<ConcessaoAtiva>? _concessoesLidas;

    public ConcessoesAtivasDoSujeito(UniPlusDbContext context, TimeProvider relogio, CatalogoDaAplicacao catalogo)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(relogio);
        ArgumentNullException.ThrowIfNull(catalogo);
        _context = context;
        _relogio = relogio;
        _catalogo = catalogo;
    }

    public async Task<IReadOnlyList<ConcessaoAtiva>> ListarAsync(UsuarioRef pessoa, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(pessoa);

        if (_concessoesLidas is not null && pessoa == _pessoaLida)
        {
            return _concessoesLidas;
        }

        List<LinhaDoAcesso> lidas = await LerLinhasAsync(pessoa, cancellationToken).ConfigureAwait(false);
        ConferirOCatalogo(lidas);
        _concessoesLidas = Agrupar([.. lidas.Select(static l => l.Linha).OfType<LinhaDaConcessao>()]);
        _pessoaLida = pessoa;

        return _concessoesLidas;
    }

    // Uma única instrução SQL: o retrato das concessões, dos códigos que elas dão e do catálogo gravado é consistente sob
    // READ COMMITTED.
    private Task<List<LinhaDoAcesso>> LerLinhasAsync(UsuarioRef pessoa, CancellationToken cancellationToken)
    {
        string emissor = pessoa.Emissor;
        string sub = pessoa.Subject;

        IQueryable<Concessao> daPessoa = _context.Concessoes.AsNoTracking()
            .Where(c => c.BeneficiarioEmissor == emissor && c.BeneficiarioSub == sub && c.RevogadaEm == null);

        return _context.LinhasDeAcessoComImpressao(daPessoa, _relogio.GetUtcNow()).ToListAsync(cancellationToken);
    }

    // O catálogo gravado que não é o desta instância, ou a falta dele, recusa a decisão: a instância é anterior à implantação
    // que o trocou, e a suspensão que ela não conhece não pode ser contornada.
    private void ConferirOCatalogo(List<LinhaDoAcesso> lidas)
    {
        if (lidas.Exists(l => !string.Equals(l.Impressao, _catalogo.Impressao, StringComparison.Ordinal)))
        {
            throw new CatalogoDesatualizadoException();
        }
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
