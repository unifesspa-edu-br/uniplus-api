namespace Unifesspa.UniPlus.Selecao.IntegrationTests.ProcessosSeletivos;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;

using Npgsql;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Repositories;

/// <summary>
/// Integração real (Testcontainers Postgres) do rascunho da publicação: o que o operador
/// transcreveu do Diário Oficial volta byte a byte, pertence a quem o escreveu, e some quando
/// tem de sumir.
/// </summary>
public sealed class RascunhoDePublicacaoPersistenciaTests : IClassFixture<ProcessoSeletivoDbFixture>
{
    private const string Operador = "sub-operador-1";
    private const string Colega = "sub-operador-2";
    private static readonly DateTimeOffset Agora = new(2026, 9, 14, 12, 0, 0, TimeSpan.Zero);

    private readonly ProcessoSeletivoDbFixture _fixture;

    public RascunhoDePublicacaoPersistenciaTests(ProcessoSeletivoDbFixture fixture)
    {
        ArgumentNullException.ThrowIfNull(fixture);
        _fixture = fixture;
    }

    [Fact(DisplayName = "O documento volta exatamente como foi gravado — inclusive tipos e acentuação")]
    public async Task RoundTripPreservaOsBytes()
    {
        Guid processoId = await NovoProcessoAsync();
        // Número como string e ano como inteiro: se a coluna reinterpretasse o documento, o ano
        // voltaria como texto e a tela reidrataria um campo numérico com aspas.
        const string conteudo = """{"numero":"07","ato":{"ano":2027,"orgao":"REITORIA — CEPS","assinante":"João D'Ávila"}}""";

        await GravarAsync(processoId, Operador, conteudo, versao: 3);

        await using SelecaoDbContext leitura = _fixture.CreateDbContext();
        RascunhoDePublicacao? lido = await new RascunhoDePublicacaoRepository(leitura)
            .ObterDoOperadorAsync(processoId, Operador);

        lido.Should().NotBeNull();
        lido!.Conteudo.Should().Be(conteudo);
        lido.Versao.Should().Be(3);
    }

    [Fact(DisplayName = "Cada operador tem o seu — um não enxerga nem sobrescreve o do outro")]
    public async Task RascunhoTemDono()
    {
        Guid processoId = await NovoProcessoAsync();

        await GravarAsync(processoId, Operador, """{"numero":"do primeiro"}""");
        await GravarAsync(processoId, Colega, """{"numero":"do segundo"}""");

        await using SelecaoDbContext leitura = _fixture.CreateDbContext();
        RascunhoDePublicacaoRepository repositorio = new(leitura);

        (await repositorio.ObterDoOperadorAsync(processoId, Operador))!.Conteudo
            .Should().Be("""{"numero":"do primeiro"}""");
        (await repositorio.ObterDoOperadorAsync(processoId, Colega))!.Conteudo
            .Should().Be("""{"numero":"do segundo"}""");
    }

    [Fact(DisplayName = "O banco recusa um segundo rascunho do mesmo operador no mesmo processo")]
    public async Task UnicidadePorOperadorEhGarantidaPeloBanco()
    {
        Guid processoId = await NovoProcessoAsync();
        await GravarAsync(processoId, Operador, """{"a":1}""");

        // Não passa pela fábrica de substituição de propósito: o que se prova aqui é a guarda do
        // BANCO, que continua de pé mesmo se um caminho futuro esquecer de ler antes de gravar.
        await using SelecaoDbContext contexto = _fixture.CreateDbContext();
        RascunhoDePublicacao duplicado = RascunhoDePublicacao.Criar(
            processoId, Operador, """{"a":2}""", 1, Agora, RascunhoDePublicacao.Prazo).Value!;
        contexto.RascunhosDePublicacao.Add(duplicado);

        Func<Task> gravar = () => contexto.SaveChangesAsync();

        await gravar.Should().ThrowAsync<DbUpdateException>()
            .WithInnerException<DbUpdateException, PostgresException>();
    }

    [Fact(DisplayName = "Apagar do processo leva os rascunhos de todos os operadores")]
    public async Task ApagarDoProcessoLevaTodos()
    {
        Guid processoId = await NovoProcessoAsync();
        await GravarAsync(processoId, Operador, """{"a":1}""");
        await GravarAsync(processoId, Colega, """{"a":2}""");

        await using SelecaoDbContext contexto = _fixture.CreateDbContext();
        int apagados = await new RascunhoDePublicacaoRepository(contexto).ApagarDoProcessoAsync(processoId);

        apagados.Should().Be(2);
        (await contexto.RascunhosDePublicacao.CountAsync(r => r.ProcessoSeletivoId == processoId))
            .Should().Be(0);
    }

    [Fact(DisplayName = "Descartar apaga só o do operador, e não o do colega")]
    public async Task DescartarNaoAlcancaOColega()
    {
        Guid processoId = await NovoProcessoAsync();
        await GravarAsync(processoId, Operador, """{"a":1}""");
        await GravarAsync(processoId, Colega, """{"a":2}""");

        await using SelecaoDbContext contexto = _fixture.CreateDbContext();
        RascunhoDePublicacaoRepository repositorio = new(contexto);
        await repositorio.ApagarDoOperadorAsync(processoId, Operador);

        (await repositorio.ObterDoOperadorAsync(processoId, Operador)).Should().BeNull();
        (await repositorio.ObterDoOperadorAsync(processoId, Colega)).Should().NotBeNull();
    }

    [Fact(DisplayName = "O vencido é apagado; o que foi regravado no meio do caminho sobrevive")]
    public async Task ApagarSeVencidoRespeitaARenovacao()
    {
        Guid processoId = await NovoProcessoAsync();
        Guid rascunhoId = await GravarAsync(processoId, Operador, """{"a":1}""");

        await using SelecaoDbContext contexto = _fixture.CreateDbContext();
        RascunhoDePublicacaoRepository repositorio = new(contexto);

        // O veredito de vencimento é de ANTES do prazo: a linha renovada não é a que ele
        // descreve, e apagá-la destruiria o trabalho que o rascunho existe para preservar.
        (await repositorio.ApagarSeVencidoAsync(rascunhoId, Agora)).Should().Be(0);

        (await repositorio.ApagarSeVencidoAsync(rascunhoId, Agora.Add(RascunhoDePublicacao.Prazo)))
            .Should().Be(1);
        (await repositorio.ObterDoOperadorAsync(processoId, Operador)).Should().BeNull();
    }

    [Fact(DisplayName = "A varredura alcança o rascunho abandonado, que a expiração na leitura nunca pegaria")]
    public async Task ApagarVencidosAlcancaOAbandonado()
    {
        Guid abandonado = await NovoProcessoAsync();
        Guid vivo = await NovoProcessoAsync();

        // O abandonado venceu e ninguém mais o abre — é exatamente o caso em que expirar só na
        // leitura não expira nunca, porque leitura nenhuma acontece.
        await GravarAsync(abandonado, Operador, """{"a":1}""");
        await VencerAsync(abandonado);
        await GravarAsync(vivo, Colega, """{"a":2}""");

        await using SelecaoDbContext contexto = _fixture.CreateDbContext();
        RascunhoDePublicacaoRepository repositorio = new(contexto);

        int apagados = await repositorio.ApagarVencidosAsync(Agora.AddDays(1));

        apagados.Should().Be(1);
        (await repositorio.ObterDoOperadorAsync(abandonado, Operador)).Should().BeNull();
        (await repositorio.ObterDoOperadorAsync(vivo, Colega))
            .Should().NotBeNull("o prazo do que está em uso não venceu");
    }

    /// <summary>Empurra o vencimento para trás, sem passar pelo domínio — o teste precisa de um
    /// rascunho vencido, e envelhecer trinta dias de relógio não é opção.</summary>
    private async Task VencerAsync(Guid processoId)
    {
        await using SelecaoDbContext contexto = _fixture.CreateDbContext();
        await contexto.RascunhosDePublicacao
            .Where(r => r.ProcessoSeletivoId == processoId)
            .ExecuteUpdateAsync(set => set.SetProperty(r => r.ExpiraEm, Agora.AddDays(-1)));
    }

    private async Task<Guid> GravarAsync(Guid processoId, string usuarioSub, string conteudo, int versao = 1)
    {
        await using SelecaoDbContext contexto = _fixture.CreateDbContext();
        RascunhoDePublicacaoRepository repositorio = new(contexto);

        RascunhoDePublicacao? existente = await repositorio.ObterDoOperadorAsync(processoId, usuarioSub);
        if (existente is null)
        {
            Result<RascunhoDePublicacao> criacao = RascunhoDePublicacao.Criar(
                processoId, usuarioSub, conteudo, versao, Agora, RascunhoDePublicacao.Prazo);
            criacao.IsSuccess.Should().BeTrue();
            existente = criacao.Value!;
            await repositorio.AdicionarAsync(existente);
        }
        else
        {
            existente.Substituir(conteudo, versao, Agora, RascunhoDePublicacao.Prazo).IsSuccess.Should().BeTrue();
        }

        await contexto.SaveChangesAsync();
        return existente.Id;
    }

    private async Task<Guid> NovoProcessoAsync()
    {
        await using SelecaoDbContext contexto = _fixture.CreateDbContext();
        ProcessoSeletivo processo = ProcessoSeletivo.Criar(
            $"Certame de teste {Guid.CreateVersion7()}",
            TipoProcesso.SiSU,
            OrigemCandidatos.InscricaoPropria,
            Guid.CreateVersion7(),
            UnidadeAdministradoraSnapshot.Criar("CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!,
            LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!);
        contexto.ProcessosSeletivos.Add(processo);
        await contexto.SaveChangesAsync();
        return processo.Id;
    }
}
