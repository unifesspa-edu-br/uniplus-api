namespace Unifesspa.UniPlus.Configuracao.IntegrationTests.Modalidades;

using System.Text.Json;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;

using Unifesspa.UniPlus.Configuracao.Application.Persistence;
using Unifesspa.UniPlus.Configuracao.Contracts;
using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Enums;
using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Configuracao.Domain.Services;
using Unifesspa.UniPlus.Configuracao.Domain.ValueObjects;
using Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence;
using Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence.Repositories;
using Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence.Seed;
using Unifesspa.UniPlus.Configuracao.Infrastructure.Readers;
using Unifesspa.UniPlus.Configuracao.IntegrationTests.Infrastructure;
using Unifesspa.UniPlus.Infrastructure.Core.Persistence;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Serializacao;
using Unifesspa.UniPlus.Regras.ValueObjects;

using static Unifesspa.UniPlus.Configuracao.IntegrationTests.Modalidades.ApoioDaPersistenciaDeModalidades;

[Collection(ConfiguracaoDbCollection.Name)]
public sealed class ModalidadePersistenceTests(ConfiguracaoDbFixture fixture)
{
    private readonly ConfiguracaoDbFixture _fixture = fixture;

    [Fact(DisplayName = "Criar persiste os campos da modalidade")]
    public async Task Insert_PersisteOsCampos()
    {
        string codigo = CodigoUnico();
        Modalidade modalidade = Cota(codigo);
        await GravarAsync(_fixture, modalidade);
        await using UniPlusDbContext context = _fixture.CreateDbContext(userId: null);
        Modalidade persistida = await context.Modalidades.SingleAsync(m => m.Id == modalidade.Id);
        persistida.Codigo.Valor.Should().Be(codigo);
        persistida.NaturezaLegal.Should().Be(Domain.Enums.NaturezaLegal.CotaReservada);
        persistida.RegraRemanejamento.Should().Be(Domain.Enums.RegraRemanejamento.SegueOrdemDeRemanejamento);
        persistida.CreatedBy.Should().Be(AdminA);
        persistida.IsDeleted.Should().BeFalse();
    }

    [Fact(DisplayName = "As colunas da natureza, da composição e da regra de remanejamento gravam o código de contrato")]
    public async Task Insert_ColunasDosEnums_GravamOCodigoDeContrato()
    {
        Modalidade modalidade = Cota(CodigoUnico());
        await GravarAsync(_fixture, modalidade);

        await using UniPlusDbContext leitura = _fixture.CreateDbContext(userId: null);
        List<string> gravados = await leitura.Database
            .SqlQuery<string>($"""
                SELECT concat_ws('|', natureza_legal, composicao_vagas, regra_remanejamento, acao_quando_indeferido) AS "Value"
                FROM configuracao.modalidades WHERE id = {modalidade.Id}
                """)
            .ToListAsync();

        gravados.Should().Equal("COTA_RESERVADA|DENTRO_DA_RESERVA_LEGAL|SEGUE_ORDEM_DE_REMANEJAMENTO");
    }

    [Fact(DisplayName = "Modalidade criada fica visível pelo leitor")]
    public async Task Insert_FicaVisivelPeloReader()
    {
        string codigo = CodigoUnico();
        Modalidade modalidade = Cota(codigo);
        await GravarAsync(_fixture, modalidade);
        await using UniPlusDbContext context = _fixture.CreateDbContext(userId: null);
        ModalidadeView? view = await new ModalidadeReader(context).ObterPorIdAsync(modalidade.Id);
        view.Should().NotBeNull();
        view!.Codigo.Should().Be(codigo);
        view.NaturezaLegal.Should().Be("COTA_RESERVADA");
        view.RegraRemanejamento.Should().Be("SEGUE_ORDEM_DE_REMANEJAMENTO");
    }

    [Fact(DisplayName = "UNIQUE parcial do código rejeita segunda modalidade viva com mesmo código")]
    public async Task UniquePartial_Codigo_RejeitaDuplicataAtiva()
    {
        string codigo = CodigoUnico();
        await GravarAsync(_fixture, Ampla(codigo));

        await using UniPlusDbContext ctx2 = _fixture.CreateDbContext(AdminA);
        ctx2.Modalidades.Add(Ampla(codigo));

        Func<Task> act = async () => await ctx2.SaveChangesAsync();

        // Confere as constantes usadas para informar o conflito entre gravações simultâneas
        // (UniqueConstraintViolation) em CodigoJaExiste/409.
        DbUpdateException ex = (await act.Should().ThrowAsync<DbUpdateException>()).Which;
        Npgsql.PostgresException pg = ex.InnerException.Should().BeOfType<Npgsql.PostgresException>().Which;
        pg.SqlState.Should().Be("23505");
        pg.ConstraintName.Should().Be(RestricoesDoBanco.ModalidadeCodigoVivo);
    }

    [Fact(DisplayName = "Soft-delete preserva a trilha e libera o slot da UNIQUE parcial do código")]
    public async Task SoftDelete_LiberaSlot()
    {
        string codigo = CodigoUnico();
        Modalidade modalidade = Ampla(codigo);
        await GravarAsync(_fixture, modalidade);
        await RemoverAsync(_fixture, modalidade.Id);
        await ConferirRemocaoAsync(modalidade.Id);
        await GravarAsync(_fixture, Ampla(codigo));
    }

    private async Task ConferirRemocaoAsync(Guid id)
    {
        await using UniPlusDbContext context = _fixture.CreateDbContext(userId: null);
        Modalidade excluida = await context.Modalidades.IgnoreQueryFilters().SingleAsync(m => m.Id == id);
        excluida.IsDeleted.Should().BeTrue();
        excluida.DeletedBy.Should().Be(AdminB);
    }

    [Fact(DisplayName = "CHECK de banco rejeita natureza fora do domínio via SQL cru")]
    public async Task Check_RejeitaNaturezaForaDoDominioViaSqlCru()
    {
        await using UniPlusDbContext ctx = _fixture.CreateDbContext(userId: null);

        Func<Task> act = async () => await ctx.Database.ExecuteSqlAsync(
            $"INSERT INTO configuracao.modalidades (id, codigo, natureza_legal, composicao_vagas, created_at, is_deleted) VALUES ({Guid.CreateVersion7()}, {CodigoUnico()}, {"INVALIDA"}, {"RESTANTE_DAS_VAGAS_OFERTADAS"}, {DateTimeOffset.UtcNow}, {false})");

        await act.Should().ThrowAsync<Npgsql.PostgresException>(
            "o CHECK de domínio de natureza_legal impede o INSERT direto");
    }

    [Fact(DisplayName = "CHECK de banco rejeita código fora do formato via SQL cru")]
    public async Task Check_RejeitaCodigoForaDoFormatoViaSqlCru()
    {
        await using UniPlusDbContext ctx = _fixture.CreateDbContext(userId: null);

        Func<Task> act = async () => await ctx.Database.ExecuteSqlAsync(
            $"INSERT INTO configuracao.modalidades (id, codigo, natureza_legal, composicao_vagas, created_at, is_deleted) VALUES ({Guid.CreateVersion7()}, {"lb-ppi"}, {"AMPLA"}, {"RESTANTE_DAS_VAGAS_OFERTADAS"}, {DateTimeOffset.UtcNow}, {false})");

        await act.Should().ThrowAsync<Npgsql.PostgresException>(
            "o CHECK codigo ~ '^[A-Z0-9_]+$' impede o INSERT direto");
    }

    [Fact(DisplayName = "CHECK de banco rejeita RETIRADA_DE_OUTRA_MODALIDADE sem origem via SQL cru")]
    public async Task Check_RejeitaRetiradaDeOutraModalidadeSemOrigemViaSqlCru()
    {
        await using UniPlusDbContext ctx = _fixture.CreateDbContext(userId: null);

        Func<Task> act = async () => await ctx.Database.ExecuteSqlAsync(
            $"INSERT INTO configuracao.modalidades (id, codigo, natureza_legal, composicao_vagas, created_at, is_deleted) VALUES ({Guid.CreateVersion7()}, {CodigoUnico()}, {"AMPLA"}, {"RETIRADA_DE_OUTRA_MODALIDADE"}, {DateTimeOffset.UtcNow}, {false})");

        await act.Should().ThrowAsync<Npgsql.PostgresException>(
            "o CHECK (composicao_vagas = 'RETIRADA_DE_OUTRA_MODALIDADE') = (composicao_origem IS NOT NULL) impede o INSERT direto");
    }

    [Fact(DisplayName = "CHECK de banco rejeita origem preenchida sem RETIRADA_DE_OUTRA_MODALIDADE via SQL cru")]
    public async Task Check_RejeitaOrigemSemRetiradaDeOutraModalidadeViaSqlCru()
    {
        await using UniPlusDbContext ctx = _fixture.CreateDbContext(userId: null);

        Func<Task> act = async () => await ctx.Database.ExecuteSqlAsync(
            $"INSERT INTO configuracao.modalidades (id, codigo, natureza_legal, composicao_vagas, composicao_origem, created_at, is_deleted) VALUES ({Guid.CreateVersion7()}, {CodigoUnico()}, {"AMPLA"}, {"RESTANTE_DAS_VAGAS_OFERTADAS"}, {"AC"}, {DateTimeOffset.UtcNow}, {false})");

        await act.Should().ThrowAsync<Npgsql.PostgresException>(
            "o CHECK de coerência RETIRADA_DE_OUTRA_MODALIDADE⟺origem impede o INSERT direto");
    }

}
