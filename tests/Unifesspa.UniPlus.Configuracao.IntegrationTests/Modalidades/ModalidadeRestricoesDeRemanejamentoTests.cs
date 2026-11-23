namespace Unifesspa.UniPlus.Configuracao.IntegrationTests.Modalidades;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;

using Npgsql;

using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.IntegrationTests.Infrastructure;
using Unifesspa.UniPlus.Infrastructure.Core.Persistence;

using static Unifesspa.UniPlus.Configuracao.IntegrationTests.Modalidades.ApoioDaPersistenciaDeModalidades;

[Collection(ConfiguracaoDbCollection.Name)]
public sealed class ModalidadeRestricoesDeRemanejamentoTests(ConfiguracaoDbFixture fixture)
{
    [Theory]
    [InlineData("DESTINO_UNICO", "DESTINO", "PAR", null, "ck_modalidades_remanejamento_destino_unico")]
    [InlineData("CRUZADO", "DESTINO", "PAR", null, "ck_modalidades_remanejamento_cruzado")]
    [InlineData(null, "DESTINO", null, null, "ck_modalidades_remanejamento_sem_parametros")]
    [InlineData("SEGUE_CASCATA", null, null, "DESTINO", "ck_modalidades_remanejamento_sem_parametros")]
    public async Task AlterarParametros_ComCombinacaoInvalida_BancoRecusa(
        string? regra, string? destino, string? par, string? destinoFinal, string restricao)
    {
        await using UniPlusDbContext contexto = fixture.CreateDbContext(AdminA);
        Modalidade modalidade = Ampla(CodigoUnico());
        contexto.Set<Modalidade>().Add(modalidade);
        await contexto.SaveChangesAsync();
        Func<Task> alterar = () => contexto.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE configuracao.modalidades SET regra_remanejamento = {regra}, remanejamento_destino = {destino},
                remanejamento_par = {par}, remanejamento_destino_final = {destinoFinal} WHERE id = {modalidade.Id}
            """);
        (await alterar.Should().ThrowAsync<PostgresException>()).Which.ConstraintName.Should().Be(restricao);
    }

    [Fact]
    public async Task AlterarParametros_CruzadoSemDestinoFinal_BancoAceita()
    {
        await using UniPlusDbContext contexto = fixture.CreateDbContext(AdminA);
        Modalidade modalidade = Ampla(CodigoUnico());
        contexto.Set<Modalidade>().Add(modalidade);
        await contexto.SaveChangesAsync();
        int alteradas = await contexto.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE configuracao.modalidades SET regra_remanejamento = 'CRUZADO', remanejamento_par = 'PAR'
            WHERE id = {modalidade.Id}
            """);
        alteradas.Should().Be(1);
    }
}
