namespace Unifesspa.UniPlus.Autorizacao.IntegrationTests.Perfis;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using Unifesspa.UniPlus.Authorization.Abstractions;
using Unifesspa.UniPlus.Authorization.Contracts;
using Unifesspa.UniPlus.Authorization.ValueObjects;
using Unifesspa.UniPlus.Autorizacao.Application.Abstractions;
using Unifesspa.UniPlus.Autorizacao.Domain.Enums;
using Unifesspa.UniPlus.Autorizacao.Infrastructure.Catalogo;
using Unifesspa.UniPlus.Autorizacao.Infrastructure.Persistence;
using Unifesspa.UniPlus.Autorizacao.IntegrationTests.Infrastructure;
using Unifesspa.UniPlus.Infrastructure.Core.Persistence;
using Unifesspa.UniPlus.IntegrationTests.Fixtures.Authentication;
using Unifesspa.UniPlus.IntegrationTests.Fixtures.Hosting;
using Unifesspa.UniPlus.Kernel.Domain.Enums;

/// <summary>
/// O que os testes da reclassificação preparam e conferem no banco real: o catálogo anterior é simulado por uma linha nova de
/// <c>implantacoes_das_permissoes</c> com a classe ou o escopo de uma permissão alterados, e a conciliação compara o catálogo da
/// aplicação com ela.
/// </summary>
internal sealed class CenarioDaReclassificacao(AutorizacaoEndpointFixture fixture)
{
    internal const string PermissaoReclassificada = "selecao:leiautes-inep:consultar";
    internal const string OutraPermissaoDoPerfil = PerfilEndpointTestBase.ConsultarCatalogo;

    // Uma implantação nova do catálogo, igual à atual exceto pela classe e pelo escopo do código pedido.
    private const string CatalogoAnteriorSql =
        """
        WITH atual AS (SELECT id FROM autorizacao.implantacoes_das_permissoes ORDER BY sequencia DESC LIMIT 1),
        novo AS (INSERT INTO autorizacao.implantacoes_das_permissoes (id, impressao, implantada_em) VALUES ({0}, {1}, now()) RETURNING id)
        INSERT INTO autorizacao.permissoes_implantadas (implantacao_id, codigo, modulo, classificacao, de_unidade, reservada)
        SELECT novo.id, p.codigo, p.modulo,
               CASE WHEN p.codigo = {2} THEN {3} ELSE p.classificacao END,
               CASE WHEN p.codigo = {2} THEN {4} ELSE p.de_unidade END,
               p.reservada
        FROM autorizacao.permissoes_implantadas p, atual, novo
        WHERE p.implantacao_id = atual.id
        """;

    private const string CatalogoSemAPermissaoSql =
        """
        WITH atual AS (SELECT id FROM autorizacao.implantacoes_das_permissoes ORDER BY sequencia DESC LIMIT 1),
        novo AS (INSERT INTO autorizacao.implantacoes_das_permissoes (id, impressao, implantada_em) VALUES ({0}, {1}, now()) RETURNING id)
        INSERT INTO autorizacao.permissoes_implantadas (implantacao_id, codigo, modulo, classificacao, de_unidade, reservada)
        SELECT novo.id, p.codigo, p.modulo, p.classificacao, p.de_unidade, p.reservada
        FROM autorizacao.permissoes_implantadas p, atual, novo
        WHERE p.implantacao_id = atual.id AND p.codigo <> {2}
        """;

    private const string PermissaoDaVersaoSql =
        """
        UPDATE autorizacao.permissoes_da_versao_do_perfil SET classificacao = {0}, de_unidade = {1}
        WHERE codigo = {3}
          AND versao_id = (SELECT id FROM autorizacao.versoes_do_perfil WHERE perfil_id = {2} AND situacao = 'vigente')
        """;

    internal async Task ConciliarAsync()
    {
        await using AsyncServiceScope scope = fixture.Factory.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<ConciliacaoDoCatalogo>().ExecutarAsync(CancellationToken.None);
    }

    internal async Task<(Guid Perfil, string Pessoa)> PerfilConcedidoComCatalogoAnteriorAsync(ClassificacaoDeProtecao anterior, bool deUnidade)
    {
        Guid perfil = await ConcessoesDeTeste.CriarPerfilAsync(fixture.Factory.Services, PermissaoReclassificada, OutraPermissaoDoPerfil);
        string pessoa = $"pessoa.{Guid.NewGuid():N}";
        await ConcessoesDeTeste.ConcederAsync(fixture.Factory.Services, pessoa, perfil);
        await ExecutarAsync(PermissaoDaVersaoSql, anterior.ToString().ToLowerInvariant(), deUnidade, perfil, PermissaoReclassificada);
        await GravarCatalogoAnteriorAsync(PermissaoReclassificada, anterior, deUnidade, new string('a', 64));

        return (perfil, pessoa);
    }

    internal Task GravarCatalogoAnteriorAsync(string codigo, ClassificacaoDeProtecao classificacao, bool deUnidade, string impressao) =>
        ExecutarAsync(CatalogoAnteriorSql, Guid.CreateVersion7(), impressao, codigo, classificacao.ToString().ToLowerInvariant(), deUnidade);

    internal Task GravarCatalogoSemAPermissaoAsync(string codigo, string impressao) =>
        ExecutarAsync(CatalogoSemAPermissaoSql, Guid.CreateVersion7(), impressao, codigo);

    internal async Task<Guid> ConcessaoDoPerfilAsync(Guid perfil)
    {
        await using AsyncServiceScope scope = fixture.Factory.Services.CreateAsyncScope();

        return await scope.ServiceProvider.GetRequiredService<UniPlusDbContext>().Concessoes
            .Where(c => c.PerfilId == perfil).Select(c => c.Id).SingleAsync();
    }

    internal async Task<Guid> ReclassificacaoDoPerfilAsync(Guid perfil)
    {
        await using AsyncServiceScope scope = fixture.Factory.Services.CreateAsyncScope();

        return await scope.ServiceProvider.GetRequiredService<UniPlusDbContext>().Reclassificacoes
            .Where(r => r.PerfilId == perfil).Select(r => r.Id).SingleAsync();
    }

    internal async Task<IReadOnlyList<string>> PermissoesDaPessoaAsync(string contaId)
    {
        await using AsyncServiceScope scope = fixture.Factory.Services.CreateAsyncScope();
        UsuarioRef pessoa = UsuarioRef.From(TestAuthHandler.DefaultIssuer, contaId).Value!;
        IReadOnlyList<ConcessaoAtiva> concessoes = await scope.ServiceProvider
            .GetRequiredService<IConcessoesAtivasDoSujeito>().ListarAsync(pessoa, CancellationToken.None);

        return [.. concessoes.SelectMany(static c => c.Permissoes)];
    }

    private async Task ExecutarAsync(string sql, params object[] parametros)
    {
        await using AsyncServiceScope scope = fixture.Factory.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<UniPlusDbContext>().Database.ExecuteSqlRawAsync(sql, parametros);
    }
}
