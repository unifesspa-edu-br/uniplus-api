namespace Unifesspa.UniPlus.Autorizacao.IntegrationTests.Perfis;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;

using Unifesspa.UniPlus.Autorizacao.Infrastructure.Persistence;
using Unifesspa.UniPlus.Autorizacao.IntegrationTests.Infrastructure;
using Unifesspa.UniPlus.Infrastructure.Core.Persistence;

/// <summary>
/// A coluna do código da permissão não tem restrição de formato: quem garante o formato é o objeto de valor na leitura.
/// A gravação malformada roda numa transação desfeita ao fim, e nada fica no banco compartilhado pelas outras suítes.
/// </summary>
[Collection(AutorizacaoEndpointCollection.Name)]
public sealed class CodigoMalformadoNoBancoTests : PerfilEndpointTestBase
{
    private const string CodigoMalformado = "Sem-Formato";

    private const string MalformarUmaPermissaoSql =
        """
        UPDATE autorizacao.permissoes_implantadas SET codigo = {0}
        WHERE (implantacao_id, codigo) = (SELECT implantacao_id, codigo FROM autorizacao.permissoes_implantadas LIMIT 1)
        """;

    public CodigoMalformadoNoBancoTests(AutorizacaoEndpointFixture fixture)
        : base(fixture)
    {
    }

    [Fact(DisplayName = "O código malformado gravado na permissão implantada faz a leitura falhar pela regra do formato do código")]
    public async Task CodigoMalformadoGravado_FalhaAoMaterializarAPermissaoImplantada()
    {
        await using AsyncServiceScope scope = Fixture.Factory.Services.CreateAsyncScope();
        UniPlusDbContext db = scope.ServiceProvider.GetRequiredService<UniPlusDbContext>();
        await using IDbContextTransaction transacao = await db.Database.BeginTransactionAsync();
        (await db.Database.ExecuteSqlRawAsync(MalformarUmaPermissaoSql, CodigoMalformado)).Should().Be(1);

        Func<Task> materializar = () => db.PermissoesImplantadas.AsNoTracking().ToListAsync();

        Exception erro = (await materializar.Should().ThrowAsync<Exception>()).Which;
        Exception causa = erro as ArgumentException ?? erro.InnerException ?? erro;
        causa.Should().BeOfType<ArgumentException>().Which.Message.Should().Contain("O código deve seguir {módulo}:{resource}:{ação}");
    }
}
