namespace Unifesspa.UniPlus.ArchTests.Fixtures.Modulos.Alfa.Infrastructure.Persistence;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using Unifesspa.UniPlus.Application.Abstractions.Interfaces;
using Unifesspa.UniPlus.ArchTests.Fixtures.Modulos.Alfa.Domain;
using Unifesspa.UniPlus.ArchTests.Fixtures.Modulos.Beta.Contracts;
using Unifesspa.UniPlus.Infrastructure.Core.Persistence;

/// <summary>Repositório de Alfa: a infraestrutura do módulo é dona do contexto, e a regra o permite.</summary>
internal sealed class RepositorioDeAlfa(UniPlusDbContext contexto)
{
    public UniPlusDbContext Contexto => contexto;
}

/// <summary>Mapeamento do EF no lugar certo: a persistência da infraestrutura do módulo.</summary>
internal sealed class MapeamentoNaPersistencia : IEntityTypeConfiguration<EntidadeDeAlfa>
{
    public void Configure(EntityTypeBuilder<EntidadeDeAlfa> builder) => builder.HasKey(e => e.Id);
}

/// <summary>Leitor que só lê: sem rastreio e sem gravação.</summary>
internal sealed class LeitorQueSoLe(UniPlusDbContext contexto) : IContratoDeBeta
{
    public async Task<int> ContarAsync(CancellationToken cancellationToken) =>
        await contexto.Set<EntidadeDeAlfa>().AsNoTracking().CountAsync(cancellationToken);
}

/// <summary>Leitor que grava pelo contexto.</summary>
internal sealed class LeitorQueSalvaPeloContexto(UniPlusDbContext contexto) : IContratoDeBeta
{
    public async Task<int> ContarAsync(CancellationToken cancellationToken) =>
        await contexto.SaveChangesAsync(cancellationToken);
}

/// <summary>Leitor que grava pela unidade de trabalho.</summary>
internal sealed class LeitorQueSalvaPelaUnidadeDeTrabalho(IUnitOfWork unidade) : IContratoDeBeta
{
    public async Task<int> ContarAsync(CancellationToken cancellationToken) =>
        await unidade.SalvarAlteracoesAsync(cancellationToken);
}

/// <summary>Leitor que mexe no rastreador de alterações.</summary>
internal sealed class LeitorQueUsaORastreador(UniPlusDbContext contexto) : IContratoDeBeta
{
    public Task<int> ContarAsync(CancellationToken cancellationToken) =>
        Task.FromResult(contexto.ChangeTracker.Entries().Count());
}

/// <summary>Leitor que apaga em massa.</summary>
internal sealed class LeitorQueApagaEmMassa(UniPlusDbContext contexto) : IContratoDeBeta
{
    public async Task<int> ContarAsync(CancellationToken cancellationToken) =>
        await contexto.Set<EntidadeDeAlfa>().ExecuteDeleteAsync(cancellationToken);
}

/// <summary>Leitor que atualiza em massa.</summary>
internal sealed class LeitorQueAtualizaEmMassa(UniPlusDbContext contexto) : IContratoDeBeta
{
    public async Task<int> ContarAsync(CancellationToken cancellationToken) =>
        await contexto.Set<EntidadeDeAlfa>().ExecuteUpdateAsync(
            alteracoes => alteracoes.SetProperty(entidade => entidade.Nome, "alterado"),
            cancellationToken);
}

/// <summary>Leitor que executa SQL de escrita.</summary>
internal sealed class LeitorQueExecutaSql(UniPlusDbContext contexto) : IContratoDeBeta
{
    public async Task<int> ContarAsync(CancellationToken cancellationToken) =>
        await contexto.Database.ExecuteSqlRawAsync("DELETE FROM alfa.entidades", cancellationToken);
}
