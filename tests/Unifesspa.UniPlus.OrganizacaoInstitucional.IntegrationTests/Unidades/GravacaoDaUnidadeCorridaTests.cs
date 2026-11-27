namespace Unifesspa.UniPlus.OrganizacaoInstitucional.IntegrationTests.Unidades;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;

using NSubstitute;

using Unifesspa.UniPlus.Infrastructure.Core.Persistence;
using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Application.Abstractions;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Application.Commands.Unidades;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Domain.Entities;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Domain.Enums;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Domain.Errors;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Domain.ValueObjects;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Infrastructure.Persistence;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Infrastructure.Persistence.Repositories;
using Unifesspa.UniPlus.OrganizacaoInstitucional.IntegrationTests.Infrastructure;
using Unifesspa.UniPlus.Testes.Compartilhado;

/// <summary>
/// Prova, contra Postgres real, que a corrida entre a conferência de unicidade e a gravação da Unidade vira a
/// recusa do identificador, e não uma exceção que escaparia como HTTP 500. A corrida é simulada com a conferência
/// obsoleta: a resposta que ela deu um instante antes já não vale quando outra transação grava a Unidade conflitante.
/// </summary>
/// <remarks>
/// A tradução depende de o Npgsql preencher o nome da restrição na violação de unicidade: um repositório dublê
/// passaria mesmo que o nome viesse nulo.
/// </remarks>
public sealed class GravacaoDaUnidadeCorridaTests : IClassFixture<UnidadeDbFixture>
{
    private const string Usuario = "admin-corrida";

    private static readonly DateOnly Inicio = new(2026, 1, 1);

    private readonly UnidadeDbFixture _fixture;

    public GravacaoDaUnidadeCorridaTests(UnidadeDbFixture fixture)
    {
        _fixture = fixture;
    }

    [Theory(DisplayName = "Criar com a conferência obsoleta devolve a recusa do identificador em uso e nada além da primeira é gravado")]
    [InlineData(TipoIdentificador.IdentificadorNoEnderecoWeb, UnidadeErrorCodes.SlugJaExiste)]
    [InlineData(TipoIdentificador.Sigla, UnidadeErrorCodes.SiglaJaExiste)]
    [InlineData(TipoIdentificador.Codigo, UnidadeErrorCodes.CodigoJaExiste)]
    public async Task Criar_ConferenciaObsoleta_DevolveARecusaDoIdentificador(TipoIdentificador emUso, string codigoEsperado)
    {
        Unidade primeira = NovaUnidade("Primeira");
        await _fixture.GravarAsync(Usuario, primeira);
        string nomeDaTentativa = $"Tentativa {Guid.NewGuid():N}";
        CriarUnidadeCommand segunda = ComandoComIdentificadorDe(primeira, emUso, nomeDaTentativa);

        await using UniPlusDbContext ctx = _fixture.CreateDbContext(Usuario);
        Result<Guid> resultado = await CriarComConferenciaObsoletaAsync(ctx, segunda);

        resultado.Error!.Code.Should().Be(codigoEsperado);
        await using UniPlusDbContext leitura = _fixture.CreateDbContext(userId: null);
        (await leitura.Unidades.AnyAsync(u => u.Nome == nomeDaTentativa)).Should().BeFalse();
        (await leitura.Unidades.AnyAsync(u => u.Id == primeira.Id)).Should().BeTrue();
    }

    [Fact(DisplayName = "Atualizar com a conferência obsoleta devolve SlugJaExiste e mantém o slug da Unidade editada")]
    public async Task Atualizar_ConferenciaObsoleta_DevolveSlugJaExisteSemAlterarAUnidade()
    {
        Unidade a = NovaUnidade("Unidade A");
        Unidade b = NovaUnidade("Unidade B");
        await _fixture.GravarAsync(Usuario, a, b);

        await using UniPlusDbContext ctx = _fixture.CreateDbContext(Usuario);
        Result resultado = await AtualizarComConferenciaObsoletaAsync(ctx, ComandoDeAtualizacao(b) with { Slug = a.Slug.Valor });

        resultado.Error!.Code.Should().Be(UnidadeErrorCodes.SlugJaExiste);
        (await _fixture.RecarregarAsync<Unidade>(b.Id)).Slug.Should().Be(b.Slug);
    }

    [Fact(DisplayName = "Após o conflito da criação, o segundo SaveChanges do mesmo contexto, o do outbox, não estoura")]
    public async Task Criar_AposConflito_SegundoSaveChangesNaoEstoura()
    {
        Unidade primeira = NovaUnidade("Primeira");
        await _fixture.GravarAsync(Usuario, primeira);

        await using UniPlusDbContext ctx = _fixture.CreateDbContext(Usuario);
        (await CriarComConferenciaObsoletaAsync(ctx, ComandoComIdentificadorDe(primeira, TipoIdentificador.IdentificadorNoEnderecoWeb, "Segunda")))
            .IsFailure.Should().BeTrue();

        Func<Task> segundoSave = async () => await ctx.SaveChangesAsync();

        await segundoSave.Should().NotThrowAsync();
    }

    [Fact(DisplayName = "Após o conflito da atualização, o segundo SaveChanges do mesmo contexto, o do outbox, não estoura")]
    public async Task Atualizar_AposConflito_SegundoSaveChangesNaoEstoura()
    {
        Unidade a = NovaUnidade("Unidade A");
        Unidade b = NovaUnidade("Unidade B");
        await _fixture.GravarAsync(Usuario, a, b);

        await using UniPlusDbContext ctx = _fixture.CreateDbContext(Usuario);
        (await AtualizarComConferenciaObsoletaAsync(ctx, ComandoDeAtualizacao(b) with { Slug = a.Slug.Valor }))
            .IsFailure.Should().BeTrue();

        Func<Task> segundoSave = async () => await ctx.SaveChangesAsync();

        await segundoSave.Should().NotThrowAsync();
    }

    private static Unidade NovaUnidade(string nome)
    {
        string sufixo = Guid.NewGuid().ToString("N")[..8];
        return Unidade.Criar(
            nome,
            new IdentificadoresDaUnidade($"u-{sufixo}", sufixo, sufixo, null),
            CidadeDaUnidade.Nenhuma,
            new VigenciaDaUnidade(Inicio, null),
            null,
            TipoUnidade.Centro,
            false).Value!;
    }

    private static CriarUnidadeCommand ComandoComIdentificadorDe(Unidade existente, TipoIdentificador emUso, string nome)
    {
        string sufixo = Guid.NewGuid().ToString("N")[..8];
        return new CriarUnidadeCommand(
            nome,
            null,
            emUso == TipoIdentificador.IdentificadorNoEnderecoWeb ? existente.Slug.Valor : $"u-{sufixo}",
            emUso == TipoIdentificador.Sigla ? existente.Sigla : sufixo,
            emUso == TipoIdentificador.Codigo ? existente.Codigo : sufixo,
            null,
            TipoUnidade.Centro.Codigo(),
            false,
            Inicio,
            null);
    }

    private static AtualizarUnidadeCommand ComandoDeAtualizacao(Unidade unidade) =>
        new(
            unidade.Id,
            unidade.Nome,
            null,
            unidade.Slug.Valor,
            unidade.Sigla,
            unidade.Codigo,
            null,
            TipoUnidade.Centro.Codigo(),
            false,
            null);

    private static Task<Result<Guid>> CriarComConferenciaObsoletaAsync(UniPlusDbContext ctx, CriarUnidadeCommand command)
    {
        (ConferenciaDoCadastroDaUnidade conferencia, GravacaoDaUnidade gravacao) = MontarComConferenciaObsoleta(ctx);
        return CriarUnidadeCommandHandler.Handle(command, conferencia, gravacao, CancellationToken.None);
    }

    private static Task<Result> AtualizarComConferenciaObsoletaAsync(UniPlusDbContext ctx, AtualizarUnidadeCommand command)
    {
        (ConferenciaDoCadastroDaUnidade conferencia, GravacaoDaUnidade gravacao) = MontarComConferenciaObsoleta(ctx);
        return AtualizarUnidadeCommandHandler.Handle(command, conferencia, gravacao, new ResolvedorFusoDeTeste(), TimeProvider.System, CancellationToken.None);
    }

    private static (ConferenciaDoCadastroDaUnidade Conferencia, GravacaoDaUnidade Gravacao) MontarComConferenciaObsoleta(
        UniPlusDbContext ctx)
    {
        var unidades = new UnidadeComConsultaObsoleta(new UnidadeRepository(ctx));
        return (
            new ConferenciaDoCadastroDaUnidade(unidades, new InstituicaoRepository(ctx)),
            new GravacaoDaUnidade(unidades, ctx, Substitute.For<IUnidadeCacheInvalidator>()));
    }
}
