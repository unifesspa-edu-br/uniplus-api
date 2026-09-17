namespace Unifesspa.UniPlus.Selecao.IntegrationTests.Outbox.Cascading;

using System.Text;

using Application.Commands.ProcessosSeletivos;

using AwesomeAssertions;

using Domain.Entities;

using Kernel.Results;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence;

using Wolverine;

/// <summary>
/// O rascunho da publicação guarda a transcrição do Diário Oficial que o operador ainda não
/// publicou — órgão, série, ano, data, assinante, tipo e número. Uma publicação que o sistema
/// recusa não pode levá-lo junto: o operador recebe "nada foi publicado" e precisa reencontrar
/// o que digitou.
/// </summary>
[Collection(CascadingCollection.Name)]
public sealed class RascunhoSobreviveAoAtoRecusadoTests
{
    private const string Operador = "operador-do-rascunho";

    private readonly CascadingFixture _fixture;

    public RascunhoSobreviveAoAtoRecusadoTests(CascadingFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact(DisplayName =
        "Publicação recusada pelo índice de numeração das versões não apaga o rascunho do ato")]
    public async Task PublicacaoRecusada_NaoApagaORascunho()
    {
        CascadingApiFactory api = _fixture.Factory;
        await TiposDeAtoSeeder.SemearAsync(api.Services);

        (Guid processoId, Guid documentoId) = await SemearProcessoPublicavelAsync(api);

        // Ocupa a versão 1 do processo antes de publicar. `VersaoConfiguracao.Abrir` fixa
        // numeroVersao: 1, então a publicação seguinte deriva o mesmo número e colide em
        // ux_versoes_configuracao_processo_numero — a recusa que interessa aqui, obtida sem
        // depender da janela entre duas requisições concorrentes.
        await OcuparAVersaoUmAsync(api, processoId);
        await SemearRascunhoAsync(api, processoId);

        var publicarCommand = new PublicarProcessoSeletivoCommand(
            processoId,
            Numero: null,
            PeriodoInscricaoInicio: null,
            PeriodoInscricaoFim: null,
            DocumentoEditalId: documentoId,
            Ato: DadosDoAtoDeTeste.Padrao);

        Result resultado;
        await using (AsyncServiceScope scope = api.Services.CreateAsyncScope())
        {
            IMessageBus bus = scope.ServiceProvider.GetRequiredService<IMessageBus>();
            resultado = await bus.InvokeAsync<Result>(publicarCommand);
        }

        resultado.IsFailure.Should().BeTrue("a versão 1 do processo já estava ocupada");
        resultado.HasErrorCode("VersaoConfiguracao.NumeroDuplicado").Should().BeTrue(
            $"a recusa precisa vir do índice de numeração para exercer o caminho em questão — veio '{resultado.Error?.Code}'");

        await using AsyncServiceScope leituraScope = api.Services.CreateAsyncScope();
        SelecaoDbContext leitura = leituraScope.ServiceProvider.GetRequiredService<SelecaoDbContext>();
        List<RascunhoDePublicacao> rascunhos = await leitura.RascunhosDePublicacao.AsNoTracking()
            .Where(r => r.ProcessoSeletivoId == processoId)
            .ToListAsync();

        rascunhos.Should().ContainSingle(
            "a publicação foi recusada e o operador precisa reencontrar a transcrição que fez");
    }

    private static async Task OcuparAVersaoUmAsync(CascadingApiFactory api, Guid processoId)
    {
        await using AsyncServiceScope scope = api.Services.CreateAsyncScope();
        SelecaoDbContext db = scope.ServiceProvider.GetRequiredService<SelecaoDbContext>();

        db.VersoesConfiguracao.Add(VersaoConfiguracao.Abrir(
            processoId,
            Encoding.UTF8.GetBytes("{}"),
            schemaVersion: "0.0.1",
            algoritmoHash: "SHA-256",
            atoCriadorId: Guid.CreateVersion7(),
            atoCriadorHash: new string('a', 64),
            atorUsuarioSub: "semeadura",
            instante: DateTimeOffset.UtcNow));

        await db.SaveChangesAsync();
    }

    private static async Task SemearRascunhoAsync(CascadingApiFactory api, Guid processoId)
    {
        await using AsyncServiceScope scope = api.Services.CreateAsyncScope();
        SelecaoDbContext db = scope.ServiceProvider.GetRequiredService<SelecaoDbContext>();

        Result<RascunhoDePublicacao> rascunho = RascunhoDePublicacao.Criar(
            processoId,
            Operador,
            conteudo: """{"ato":{"assinante":"Fulano de Tal"}}""",
            versao: 1,
            DateTimeOffset.UtcNow,
            RascunhoDePublicacao.Prazo);

        rascunho.IsSuccess.Should().BeTrue(rascunho.Error?.Message);

        db.RascunhosDePublicacao.Add(rascunho.Value!);
        await db.SaveChangesAsync();
    }

    private static async Task<(Guid ProcessoId, Guid DocumentoId)> SemearProcessoPublicavelAsync(
        CascadingApiFactory api)
    {
        await using AsyncServiceScope seedScope = api.Services.CreateAsyncScope();
        SelecaoDbContext seedDb = seedScope.ServiceProvider.GetRequiredService<SelecaoDbContext>();
        (ProcessoSeletivo processo, DocumentoEdital documento) = await ProcessoSeletivoPublicavelSeeder
            .SemearAsync(seedDb, $"Rascunho preservado {Guid.CreateVersion7()}");
        return (processo.Id, documento.Id);
    }
}
