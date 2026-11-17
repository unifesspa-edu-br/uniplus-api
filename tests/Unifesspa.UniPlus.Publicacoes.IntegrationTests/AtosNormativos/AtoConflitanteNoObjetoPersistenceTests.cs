namespace Unifesspa.UniPlus.Publicacoes.IntegrationTests.AtosNormativos;

using AwesomeAssertions;

using Unifesspa.UniPlus.Infrastructure.Core.Persistence;
using Unifesspa.UniPlus.Publicacoes.Domain.Entities;
using Unifesspa.UniPlus.Publicacoes.Domain.ValueObjects;
using Unifesspa.UniPlus.Publicacoes.Infrastructure.Persistence;
using Unifesspa.UniPlus.Publicacoes.Infrastructure.Persistence.Repositories;
using Unifesspa.UniPlus.Publicacoes.IntegrationTests.Infrastructure;
using Unifesspa.UniPlus.Testes.Compartilhado;

/// <summary>
/// O ato que já trata do objeto, contra o Postgres real: só conta o ato do tipo pedido, vinculado ao mesmo
/// objeto e fora da cadeia de retificação informada. Um ato de outro tipo no mesmo objeto e um ato da própria
/// cadeia não conflitam.
/// </summary>
[Collection(PublicacoesDbCollection.Name)]
public sealed class AtoConflitanteNoObjetoPersistenceTests
{
    private const string TipoPedido = "EDITAL_ABERTURA";
    private const string OutroTipo = "AVISO_PRAZO";
    private const string Rotulo = "PROCESSO_SELETIVO";

    private readonly PublicacoesDbFixture _fixture;

    public AtoConflitanteNoObjetoPersistenceTests(PublicacoesDbFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact(DisplayName = "O ato vivo do tipo pedido, fora da cadeia, é o conflitante; o de outro tipo e o da própria cadeia não contam")]
    public async Task ObterAtoConflitante_ComElegivel_DevolveSoOElegivel()
    {
        EntidadeVinculada objeto = new(Rotulo, Guid.CreateVersion7());
        AtoNormativo elegivel = await GravarAsync(TipoPedido, objeto);
        await GravarAsync(OutroTipo, objeto);
        AtoNormativo daPropriaCadeia = await GravarAsync(TipoPedido, objeto);

        AtoNormativo? conflitante = await ObterConflitanteAsync(objeto, TipoPedido, [daPropriaCadeia.Id, elegivel.Id]);
        AtoNormativo? soAPropriaCadeia = await ObterConflitanteAsync(objeto, TipoPedido, [daPropriaCadeia.Id]);

        conflitante.Should().BeNull("o elegível também está na cadeia informada");
        soAPropriaCadeia.Should().NotBeNull();
        soAPropriaCadeia!.Id.Should().Be(elegivel.Id);
    }

    [Fact(DisplayName = "Sem ato elegível, o de outro tipo e o da própria cadeia não conflitam")]
    public async Task ObterAtoConflitante_SemElegivel_DevolveNulo()
    {
        EntidadeVinculada objeto = new(Rotulo, Guid.CreateVersion7());
        await GravarAsync(OutroTipo, objeto);
        AtoNormativo daPropriaCadeia = await GravarAsync(TipoPedido, objeto);

        AtoNormativo? conflitante = await ObterConflitanteAsync(objeto, TipoPedido, [daPropriaCadeia.Id]);

        conflitante.Should().BeNull();
    }

    [Fact(DisplayName = "O ato elegível de um objeto não conflita com outro rótulo nem com outro identificador")]
    public async Task ObterAtoConflitante_OutroObjeto_DevolveNulo()
    {
        EntidadeVinculada objeto = new(Rotulo, Guid.CreateVersion7());
        await GravarAsync(TipoPedido, objeto);

        (await ObterConflitanteAsync(new EntidadeVinculada("CHAMADA", objeto.Id), TipoPedido, [])).Should().BeNull();
        (await ObterConflitanteAsync(new EntidadeVinculada(Rotulo, Guid.CreateVersion7()), TipoPedido, [])).Should().BeNull();
    }

    private async Task<AtoNormativo> GravarAsync(string tipoCodigo, EntidadeVinculada objeto)
    {
        AtoNormativo ato = new AtoNormativoDeTeste
        {
            Numero = null,
            TipoCodigo = tipoCodigo,
            Vinculos = [new(objeto.Tipo, objeto.Id)],
        }.Registrar();

        await using UniPlusDbContext ctx = _fixture.CreateDbContext(userId: null);
        ctx.AtosNormativos.Add(ato);
        await ctx.SaveChangesAsync();

        return ato;
    }

    private async Task<AtoNormativo?> ObterConflitanteAsync(
        EntidadeVinculada objeto, string tipoCodigo, IReadOnlyCollection<Guid> idsDaPropriaCadeia)
    {
        await using UniPlusDbContext ctx = _fixture.CreateDbContext(userId: null);
        return await new AtoNormativoRepository(ctx)
            .ObterAtoConflitanteNoObjetoAsync(objeto, tipoCodigo, idsDaPropriaCadeia, CancellationToken.None);
    }
}
