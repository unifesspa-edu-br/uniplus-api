namespace Unifesspa.UniPlus.Configuracao.IntegrationTests.Sementes;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

using Npgsql;

using Unifesspa.UniPlus.Configuracao.Application.Persistence;
using Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence.Seed;
using Unifesspa.UniPlus.Configuracao.IntegrationTests.Infrastructure;
using Unifesspa.UniPlus.Infrastructure.Core.Persistence;

using static Unifesspa.UniPlus.Configuracao.IntegrationTests.Sementes.BancoDaSementeParaTeste;

/// <summary>
/// As sementes do módulo inteiras, contra Postgres real, sobre um banco em que a administração já ocupou códigos ou
/// editou o que elas gravaram. Em todos os cenários a aplicação não falha e nenhum modelo gravado cita um termo ou uma
/// versão que não existe, que não é do termo ou que foi removido. Cada teste apaga o que as sementes gravaram dentro de
/// uma transação e a desfaz no fim, porque o banco da coleção é compartilhado.
/// </summary>
[Collection(ConfiguracaoDbCollection.Name)]
public sealed class SementeSobreBancoOcupadoTests
{
    private static readonly Guid IdDoTermoDaAdministracao = Guid.Parse("7e570001-0000-7000-8000-0000000000a1");
    private static readonly Guid IdDoModeloDaAdministracao = Guid.Parse("7e570001-0000-7000-8000-0000000000b1");

    private readonly ConfiguracaoDbFixture _fixture;

    public SementeSobreBancoOcupadoTests(ConfiguracaoDbFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact(DisplayName = "No banco vazio, entram os 5 termos, as 5 versões, os 4 modelos, o tipo PSR e os fatos, sem citação ausente")]
    public async Task BancoVazio_GravaTudo()
    {
        await using UniPlusDbContext ctx = _fixture.CreateDbContext(userId: null);
        await using IDbContextTransaction transacao = await ctx.Database.BeginTransactionAsync();
        await ApagarSementeAsync(ctx);
        RegistroDeLogDeTeste log = new();

        await AplicarAsync(ctx, log);

        (await ResumoAsync(ctx)).Should().Be(ResumoCompleto);
        (await CitacoesDosModelosAsync(ctx, soAusentes: false)).Should().Be(7, "as inscrições citam dois termos cada, a habilitação um e a isenção dois");
        await ConfirmarSemCitacaoAusenteAsync(ctx);
        log.Mensagens.Should().BeEmpty("nada foi pulado");
        await transacao.RollbackAsync();
    }

    [Theory(DisplayName = "O código do termo da consulta ao Banco Central ocupado por outro termo, removido ou vivo, pula o termo, a versão e o modelo de habilitação")]
    [InlineData(true)]
    [InlineData(false)]
    public async Task CodigoDoTermoDoBancoCentralOcupado_PulaOTermoAVersaoEOModeloDeHabilitacao(bool ocupanteRemovido)
    {
        TermoDaSemente consulta = SementePsrMedicina2027.Termos[2];
        await using UniPlusDbContext ctx = _fixture.CreateDbContext(userId: null);
        await using IDbContextTransaction transacao = await ctx.Database.BeginTransactionAsync();
        RegistroDeLogDeTeste log = new();

        await AplicarComTermoOcupadoAsync(ctx, consulta, ocupanteRemovido, log);

        (await ResumoAsync(ctx)).Should().Be(ResumoSemOModelo(SementePsrMedicina2027.ModeloDeHabilitacao));
        await ConfirmarSemCitacaoAusenteAsync(ctx);
        ConferirItensPulados(log, CodigoDoTermo(consulta), SementePsrMedicina2027.ModeloDeHabilitacao);
        await transacao.RollbackAsync();
    }

    [Fact(DisplayName = "O termo da isenção pulado deixa de fora só o modelo de isenção, e o outro termo e a versão dele entram")]
    public async Task TermoDaIsencaoPulado_DeixaDeForaSoOModeloDeIsencao()
    {
        TermoDaSemente veracidade = SementeIsencaoDaTaxa.Termos[0];
        await using UniPlusDbContext ctx = _fixture.CreateDbContext(userId: null);
        await using IDbContextTransaction transacao = await ctx.Database.BeginTransactionAsync();
        RegistroDeLogDeTeste log = new();

        await AplicarComTermoOcupadoAsync(ctx, veracidade, ocupanteRemovido: true, log);

        (await ResumoAsync(ctx)).Should().Be(ResumoSemOModelo(SementeIsencaoDaTaxa.ModeloDeIsencao));
        await ConfirmarSemCitacaoAusenteAsync(ctx);
        ConferirItensPulados(log, CodigoDoTermo(veracidade), SementeIsencaoDaTaxa.ModeloDeIsencao);
        await transacao.RollbackAsync();
    }

    [Fact(DisplayName = "O termo da semente removido e sem versão não recebe a versão, e o modelo que o cita também não entra")]
    public async Task TermoDaSementeRemovidoSemVersao_NaoRecebeVersaoENemModelo()
    {
        TermoDaSemente consulta = SementePsrMedicina2027.Termos[2];
        await using UniPlusDbContext ctx = _fixture.CreateDbContext(userId: null);
        await using IDbContextTransaction transacao = await ctx.Database.BeginTransactionAsync();
        await ApagarSementeAsync(ctx);
        await CadastrarTermoAsync(ctx, consulta.TermoId, CodigoDoTermo(consulta), consulta.Nome, removido: true);
        RegistroDeLogDeTeste log = new();

        await AplicarAsync(ctx, log);

        (await ResumoAsync(ctx)).Should().Be(ResumoSemOModelo(SementePsrMedicina2027.ModeloDeHabilitacao) with { Termos = 5 });
        await ConfirmarSemCitacaoAusenteAsync(ctx);
        ConferirItensPulados(log, CodigoDoTermo(consulta), SementePsrMedicina2027.ModeloDeHabilitacao, motivoDaVersao: "o termo foi removido", termoPulado: false);
        await transacao.RollbackAsync();
    }

    [Fact(DisplayName = "O código de modelo ocupado por modelo da administração pula o modelo da semente e deixa o da administração intacto")]
    public async Task CodigoDeModeloOcupado_PulaOModeloDaSementeEPreservaODaAdministracao()
    {
        await using UniPlusDbContext ctx = _fixture.CreateDbContext(userId: null);
        await using IDbContextTransaction transacao = await ctx.Database.BeginTransactionAsync();
        string antes = await PrepararModeloDaAdministracaoAsync(ctx);
        RegistroDeLogDeTeste log = new();

        await AplicarAsync(ctx, log);

        (await RetratoAsync(ctx)).Should().Be(antes, "o modelo da administração e o resto ficam como estavam");
        await ConfirmarSemCitacaoAusenteAsync(ctx);
        log.Mensagens.Should().ContainSingle(static m => m.Contains(SementePsrMedicina2027.ModeloDeHabilitacao, StringComparison.Ordinal))
            .Which.Should().Contain("o código já está ocupado");
        await transacao.RollbackAsync();
    }

    [Fact(DisplayName = "Reaplicar sobre banco já preparado e editado pela administração não altera nenhuma linha")]
    public async Task Reaplicar_SobreBancoPreparadoEEditado_NaoAlteraNada()
    {
        await using UniPlusDbContext ctx = _fixture.CreateDbContext(userId: null);
        await using IDbContextTransaction transacao = await ctx.Database.BeginTransactionAsync();
        string antes = await PrepararBancoEditadoAsync(ctx);
        RegistroDeLogDeTeste log = new();

        await AplicarAsync(ctx, log);

        (await RetratoAsync(ctx)).Should().Be(antes);
        (await ResumoAsync(ctx)).Should().Be(ResumoCompleto);
        await ConfirmarSemCitacaoAusenteAsync(ctx);
        log.Mensagens.Should().BeEmpty("o item que já está gravado é o caso normal e não é registrado");
        await transacao.RollbackAsync();
    }

    [Fact(DisplayName = "O fato com o código ocupado continua falhando pelo índice único, sem o texto do termo na mensagem")]
    public async Task FatoComCodigoOcupado_FalhaPeloIndiceUnico()
    {
        await using UniPlusDbContext ctx = _fixture.CreateDbContext(userId: null);
        await using IDbContextTransaction transacao = await ctx.Database.BeginTransactionAsync();
        await ApagarSementeAsync(ctx);
        await AplicarAsync(ctx, new RegistroDeLogDeTeste());
        await ctx.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE configuracao.dados_do_candidato SET id = {Guid.Parse("7e570001-0000-7000-8000-0000000000c1")} WHERE codigo = 'SOLICITA_FATOR_ATENUACAO'");

        Func<Task> aplicar = () => AplicarAsync(ctx, new RegistroDeLogDeTeste());

        PostgresException violacao = (await aplicar.Should().ThrowAsync<PostgresException>()).Which;
        violacao.ConstraintName.Should().Be(RestricoesDoBanco.FatoDoCandidatoCodigoUnico);
        violacao.MessageText.Should().Contain(RestricoesDoBanco.FatoDoCandidatoCodigoUnico);
        violacao.MessageText.Should().NotContain(SementePsrMedicina2027.Termos[0].Texto).And.NotContain(SementePsrMedicina2027.Termos[0].Nome);
        await transacao.RollbackAsync();
    }

    private static async Task AplicarComTermoOcupadoAsync(
        UniPlusDbContext ctx, TermoDaSemente termo, bool ocupanteRemovido, RegistroDeLogDeTeste log)
    {
        await ApagarSementeAsync(ctx);
        await CadastrarTermoAsync(ctx, IdDoTermoDaAdministracao, CodigoDoTermo(termo), "Termo fictício da administração", ocupanteRemovido);
        await AplicarAsync(ctx, log);
    }

    private static ResumoDaSemente ResumoSemOModelo(string codigoDoModelo) => ResumoCompleto with
    {
        Termos = 4,
        Versoes = 4,
        Modelos = ModelosOrdenados(CodigosDosModelos.Except([codigoDoModelo])),
    };

    // O banco com as sementes gravadas e o modelo de habilitação já substituído por outro, da administração, de mesmo código.
    private static async Task<string> PrepararModeloDaAdministracaoAsync(UniPlusDbContext ctx)
    {
        await ApagarSementeAsync(ctx);
        await AplicarAsync(ctx, new RegistroDeLogDeTeste());
        await ctx.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE configuracao.modelos_formulario SET id = {IdDoModeloDaAdministracao}, nome = 'Modelo fictício da administração' WHERE codigo = {SementePsrMedicina2027.ModeloDeHabilitacao}");
        return await RetratoAsync(ctx);
    }

    private static async Task<string> PrepararBancoEditadoAsync(UniPlusDbContext ctx)
    {
        await ApagarSementeAsync(ctx);
        await AplicarAsync(ctx, new RegistroDeLogDeTeste());
        await AdministracaoEditaAsync(ctx);
        return await RetratoAsync(ctx);
    }

    private static async Task ConfirmarSemCitacaoAusenteAsync(UniPlusDbContext ctx) =>
        (await CitacoesDosModelosAsync(ctx, soAusentes: true)).Should().Be(0, "nenhum modelo gravado cita termo ou versão que não existe");

    // O termo, a versão dele e o modelo que o cita são pulados, cada um com o código e o motivo, e sem o texto do termo. O termo
    // que já está gravado, mesmo removido, não é registrado: é o caso normal de toda reaplicação.
    private static void ConferirItensPulados(
        RegistroDeLogDeTeste log, string codigoDoTermo, string codigoDoModelo, string motivoDaVersao = "o termo não foi gravado", bool termoPulado = true)
    {
        List<string> esperadas =
        [
            $"A semente não gravou o item versão do termo {codigoDoTermo}: {motivoDaVersao}.",
            $"A semente não gravou o item modelo {codigoDoModelo}: um termo ou uma versão citada não existe, não é do termo, ou o termo foi removido.",
        ];
        if (termoPulado)
        {
            esperadas.Add($"A semente não gravou o item termo {codigoDoTermo}: o código já está ocupado.");
        }

        log.Mensagens.Should().BeEquivalentTo(esperadas);
    }

    private static async Task AdministracaoEditaAsync(UniPlusDbContext ctx)
    {
        await ctx.Database.ExecuteSqlRawAsync(
            """
            UPDATE configuracao.termos_de_consentimento SET nome = 'Termo fictício editado', texto_rascunho = 'Texto fictício editado.', revisado = FALSE WHERE id = {0};
            UPDATE configuracao.modelos_formulario SET nome = 'Modelo fictício editado', descricao = 'Descrição fictícia editada.' WHERE codigo = {1};
            UPDATE configuracao.dados_do_candidato SET descricao = 'Descrição fictícia editada.' WHERE codigo = 'SOLICITA_FATOR_ATENUACAO';
            UPDATE configuracao.opcoes_dos_dados SET descricao = 'Descrição fictícia editada.' WHERE id::text LIKE '5e3d%' AND codigo = 'RESIDENCIA';
            UPDATE configuracao.tipos_processo SET nome = 'Tipo fictício editado' WHERE codigo = {2};
            """,
            SementePsrMedicina2027.Termos[0].TermoId, SementeIsencaoDaTaxa.ModeloDeIsencao, CodigoDoTipoDeProcesso);
    }
}
