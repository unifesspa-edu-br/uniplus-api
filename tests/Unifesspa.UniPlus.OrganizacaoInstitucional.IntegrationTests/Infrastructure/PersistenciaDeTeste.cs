namespace Unifesspa.UniPlus.OrganizacaoInstitucional.IntegrationTests.Infrastructure;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;

using Unifesspa.UniPlus.Infrastructure.Core.Persistence;
using Unifesspa.UniPlus.Kernel.Domain.Entities;

/// <summary>
/// Cria contextos com os interceptors de produção para um usuário (ou para o fallback
/// <c>"system"</c>, quando nulo). Implementada pelas fixtures de banco do módulo.
/// </summary>
internal interface IFabricaDeContextoDeTeste
{
    UniPlusDbContext CreateDbContext(string? userId);
}

/// <summary>
/// Operações de persistência que os testes de integração do módulo repetem, cada uma
/// num contexto próprio, descartado ao fim, como um request faria.
/// </summary>
internal static class PersistenciaDeTeste
{
    /// <summary>
    /// Grava as entidades em ordem, um <c>SaveChanges</c> por entidade no mesmo contexto:
    /// saves sequenciais respeitam FK auto-referencial (pai antes do filho).
    /// </summary>
    public static async Task GravarAsync<TEntidade>(
        this IFabricaDeContextoDeTeste banco,
        string usuario,
        params TEntidade[] entidades)
        where TEntidade : class
    {
        await using UniPlusDbContext ctx = banco.CreateDbContext(usuario);
        foreach (TEntidade entidade in entidades)
        {
            ctx.Add(entidade);
            await ctx.SaveChangesAsync();
        }
    }

    /// <summary>Remove pelo EF; o SoftDeleteInterceptor converte em soft-delete.</summary>
    public static async Task RemoverAsync<TEntidade>(
        this IFabricaDeContextoDeTeste banco,
        string usuario,
        Guid id)
        where TEntidade : EntityBase
    {
        await using UniPlusDbContext ctx = banco.CreateDbContext(usuario);
        TEntidade rastreada = await ctx.Set<TEntidade>().SingleAsync(e => e.Id == id);
        ctx.Remove(rastreada);
        await ctx.SaveChangesAsync();
    }

    /// <summary>Lê de volta, num contexto limpo, a entidade viva.</summary>
    public static async Task<TEntidade> RecarregarAsync<TEntidade>(
        this IFabricaDeContextoDeTeste banco,
        Guid id)
        where TEntidade : EntityBase
    {
        await using UniPlusDbContext readCtx = banco.CreateDbContext(userId: null);
        return await readCtx.Set<TEntidade>().SingleAsync(e => e.Id == id);
    }

    /// <summary>Lê de volta, num contexto limpo e sem o filtro de soft-delete, a entidade excluída.</summary>
    public static async Task<TEntidade> RecarregarExcluidaAsync<TEntidade>(
        this IFabricaDeContextoDeTeste banco,
        Guid id)
        where TEntidade : EntityBase
    {
        await using UniPlusDbContext readCtx = banco.CreateDbContext(userId: null);
        return await readCtx.Set<TEntidade>()
            .IgnoreQueryFilters()
            .SingleAsync(e => e.Id == id);
    }

    /// <summary>Confere que a gravação da entidade, num contexto novo, não lança.</summary>
    public static async Task ConferirQueAGravacaoEhAceitaAsync<TEntidade>(
        this IFabricaDeContextoDeTeste banco,
        string usuario,
        TEntidade entidade,
        string motivo)
        where TEntidade : class
    {
        await using UniPlusDbContext ctx = banco.CreateDbContext(usuario);
        ctx.Add(entidade);
        Func<Task> act = async () => await ctx.SaveChangesAsync();

        await act.Should().NotThrowAsync(motivo);
    }

    /// <summary>
    /// Confere que o banco recusa uma exclusão crua (SQL parametrizado, fora do domínio) pela chave estrangeira
    /// esperada, declarada com <c>ON DELETE RESTRICT</c>: o PostgreSQL recusa com <c>restrict_violation</c> (23001),
    /// e não com a violação genérica de chave estrangeira. É a prova de que a restrição vive no banco, não só no agregado.
    /// </summary>
    public static async Task ConferirQueOBancoRestringeAExclusaoAsync(
        this IFabricaDeContextoDeTeste banco,
        FormattableString escritaCrua,
        string restricaoEsperada,
        string motivo)
    {
        await using UniPlusDbContext rawCtx = banco.CreateDbContext(userId: null);
        Func<Task> act = async () => await rawCtx.Database.ExecuteSqlAsync(escritaCrua);

        Npgsql.PostgresException violacao = (await act.Should().ThrowAsync<Npgsql.PostgresException>(motivo)).Which;
        violacao.SqlState.Should().Be(Npgsql.PostgresErrorCodes.RestrictViolation);
        violacao.ConstraintName.Should().Be(restricaoEsperada);
    }

    /// <summary>
    /// Confere que o banco recusa uma escrita crua (SQL parametrizado, fora do domínio) por violação de
    /// CHECK, e da restrição esperada: é a prova de que a restrição vive no banco, não só no agregado.
    /// </summary>
    public static async Task ConferirQueOBancoRecusaPorCheckAsync(
        this IFabricaDeContextoDeTeste banco,
        FormattableString escritaCrua,
        string restricaoEsperada,
        string motivo)
    {
        await using UniPlusDbContext rawCtx = banco.CreateDbContext(userId: null);
        Func<Task> act = async () => await rawCtx.Database.ExecuteSqlAsync(escritaCrua);

        Npgsql.PostgresException violacao = (await act.Should().ThrowAsync<Npgsql.PostgresException>(motivo)).Which;
        violacao.SqlState.Should().Be(Npgsql.PostgresErrorCodes.CheckViolation);
        violacao.ConstraintName.Should().Be(restricaoEsperada);
    }
}
