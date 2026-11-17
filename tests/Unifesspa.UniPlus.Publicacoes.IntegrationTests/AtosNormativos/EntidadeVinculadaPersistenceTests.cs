namespace Unifesspa.UniPlus.Publicacoes.IntegrationTests.AtosNormativos;

using AwesomeAssertions;

using Unifesspa.UniPlus.Infrastructure.Core.Persistence;
using Unifesspa.UniPlus.Kernel.Pagination;
using Unifesspa.UniPlus.Publicacoes.Domain.Entities;
using Unifesspa.UniPlus.Publicacoes.Domain.ValueObjects;
using Unifesspa.UniPlus.Publicacoes.Infrastructure.Persistence;
using Unifesspa.UniPlus.Publicacoes.Infrastructure.Persistence.Repositories;
using Unifesspa.UniPlus.Publicacoes.IntegrationTests.Infrastructure;
using Unifesspa.UniPlus.Testes.Compartilhado;

/// <summary>
/// As leituras do repositório que recebem a entidade vinculada como par, contra o Postgres real: os
/// vínculos de um ato, em ordem, e a vaga que um objeto reservou para um tipo de ato.
/// </summary>
[Collection(PublicacoesDbCollection.Name)]
public sealed class EntidadeVinculadaPersistenceTests
{
    private static readonly Guid Primeiro = Guid.Parse("00000000-0000-0000-0000-000000000001");
    private static readonly Guid Segundo = Guid.Parse("00000000-0000-0000-0000-000000000002");
    private static readonly AtoNormativoDeTeste Base = new() { Numero = null, CongelaConfiguracao = true };

    private readonly PublicacoesDbFixture _fixture;

    public EntidadeVinculadaPersistenceTests(PublicacoesDbFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact(DisplayName = "Os vínculos de um ato saem ordenados pelo rótulo e depois pelo identificador, sem os de outro ato")]
    public async Task ListarVinculosDoAto_DevolveSoOsDoAtoEmOrdem()
    {
        AtoNormativo ato = (Base with
        {
            Vinculos = [new("CHAMADA", Segundo), new("PROCESSO_SELETIVO", Primeiro), new("CHAMADA", Primeiro)],
        }).Registrar();
        AtoNormativo outro = (Base with { Vinculos = [new("CHAMADA", Guid.CreateVersion7())] }).Registrar();
        await Gravar(ato);
        await Gravar(outro);

        IReadOnlyList<EntidadeVinculada> vinculos = await ListarVinculosDoAtoAsync(ato.Id);

        vinculos.Should().Equal(
            new EntidadeVinculada("CHAMADA", Primeiro),
            new EntidadeVinculada("CHAMADA", Segundo),
            new EntidadeVinculada("PROCESSO_SELETIVO", Primeiro));
    }

    [Fact(DisplayName = "A vaga do objeto é achada pelo trio (rótulo, identificador, tipo de ato)")]
    public async Task ObterVagaDoObjeto_PeloTrio_AchaAVaga()
    {
        Guid processo = Guid.CreateVersion7();
        AtoNormativo ato = await GravarComVagaAsync(processo);

        VagaDeLinhagem? vaga = await ObterVagaAsync(new EntidadeVinculada("PROCESSO_SELETIVO", processo), ato.TipoCodigo);

        vaga.Should().NotBeNull();
        vaga!.AtoId.Should().Be(ato.Id);
    }

    [Fact(DisplayName = "A vaga não é achada com outro rótulo, outro identificador ou outro tipo de ato")]
    public async Task ObterVagaDoObjeto_ComOutroTrio_NaoAchaAVaga()
    {
        Guid processo = Guid.CreateVersion7();
        AtoNormativo ato = await GravarComVagaAsync(processo);

        (await ObterVagaAsync(new EntidadeVinculada("CHAMADA", processo), ato.TipoCodigo)).Should().BeNull();
        (await ObterVagaAsync(new EntidadeVinculada("PROCESSO_SELETIVO", Guid.CreateVersion7()), ato.TipoCodigo)).Should().BeNull();
        (await ObterVagaAsync(new EntidadeVinculada("PROCESSO_SELETIVO", processo), "OUTRO_TIPO")).Should().BeNull();
    }

    [Theory(DisplayName = "A vaga do objeto recusa a entidade sem rótulo antes de consultar")]
    [InlineData(null)]
    [InlineData("   ")]
    public async Task ObterVagaDoObjeto_SemRotulo_Recusa(string? rotulo)
    {
        Func<Task> acao = () => ObterVagaAsync(new EntidadeVinculada(rotulo!, Guid.CreateVersion7()), "EDITAL_ABERTURA");

        (await acao.Should().ThrowAsync<ArgumentException>()).Which.ParamName.Should().Be("entidade");
    }

    [Theory(DisplayName = "O ato conflitante no objeto recusa a entidade sem rótulo antes de consultar")]
    [InlineData(null)]
    [InlineData("   ")]
    public async Task ObterAtoConflitanteNoObjeto_SemRotulo_Recusa(string? rotulo)
    {
        await using UniPlusDbContext ctx = _fixture.CreateDbContext(userId: null);
        AtoNormativoRepository atos = new(ctx);

        Func<Task> acao = () => atos.ObterAtoConflitanteNoObjetoAsync(
            new EntidadeVinculada(rotulo!, Guid.CreateVersion7()), "EDITAL_ABERTURA", [], CancellationToken.None);

        (await acao.Should().ThrowAsync<ArgumentException>()).Which.ParamName.Should().Be("entidade");
    }

    [Theory(DisplayName = "A listagem dos atos da entidade recusa a entidade sem rótulo antes de consultar")]
    [InlineData(null)]
    [InlineData("   ")]
    public async Task ListarPorEntidade_SemRotulo_Recusa(string? rotulo)
    {
        await using UniPlusDbContext ctx = _fixture.CreateDbContext(userId: null);
        AtoNormativoRepository atos = new(ctx);

        Func<Task> acao = () => atos.ListarPorEntidadeAsync(
            new EntidadeVinculada(rotulo!, Guid.CreateVersion7()), null, null, 10, PaginationDirection.Next, CancellationToken.None);

        (await acao.Should().ThrowAsync<ArgumentException>()).Which.ParamName.Should().Be("entidade");
    }

    private async Task<IReadOnlyList<EntidadeVinculada>> ListarVinculosDoAtoAsync(Guid atoId)
    {
        await using UniPlusDbContext ctx = _fixture.CreateDbContext(userId: null);
        return await new AtoNormativoRepository(ctx).ListarVinculosDoAtoAsync(atoId, CancellationToken.None);
    }

    private async Task<VagaDeLinhagem?> ObterVagaAsync(EntidadeVinculada entidade, string tipoCodigo)
    {
        await using UniPlusDbContext ctx = _fixture.CreateDbContext(userId: null);
        return await new AtoNormativoRepository(ctx).ObterVagaDoObjetoAsync(entidade, tipoCodigo, CancellationToken.None);
    }

    private async Task<AtoNormativo> GravarComVagaAsync(Guid processo)
    {
        AtoNormativo ato = (Base with { UnicoPorObjeto = true, Vinculos = [new("PROCESSO_SELETIVO", processo)] }).Registrar();

        await using UniPlusDbContext ctx = _fixture.CreateDbContext(userId: null);
        ctx.AtosNormativos.Add(ato);
        ctx.VagasDeLinhagem.Add(VagaDeLinhagem.Criar(ato, ato.Vinculos.Single(), raizId: ato.Id));
        await ctx.SaveChangesAsync();

        return ato;
    }

    private async Task Gravar(AtoNormativo ato)
    {
        await using UniPlusDbContext ctx = _fixture.CreateDbContext(userId: null);
        ctx.AtosNormativos.Add(ato);
        await ctx.SaveChangesAsync();
    }
}
