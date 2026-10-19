namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Queries;

using System.Text.Json;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Application.Abstractions.Authentication;
using Unifesspa.UniPlus.Kernel.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Application.DTOs;
using Unifesspa.UniPlus.Selecao.Application.Queries.Inscricoes;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Errors;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

/// <summary>
/// "Minhas inscrições" (issue #1860, <c>GET /api/selecao/minhas-inscricoes</c>): todas as do dono
/// do token, em qualquer processo, com o nome preferido — o social, só quando declarado.
/// </summary>
public sealed class MinhasInscricoesQueryHandlerTests
{
    private static IUserContext UsuarioComCpf(Cpf cpf, string sub = "sub-1")
    {
        IUserContext contexto = Substitute.For<IUserContext>();
        contexto.Cpf.Returns(cpf.Valor);
        contexto.UserId.Returns(sub);
        return contexto;
    }

    private static Inscricao ComRespostas(Cpf cpf, string sub, Guid processoId, Dictionary<string, JsonElement> respostas)
    {
        Inscricao inscricao = Inscricao.Iniciar(
            processoId, cpf, sub, "https://issuer", Guid.CreateVersion7()).Value!;
        inscricao.Salvar(
            null, null, respostas, new Dictionary<string, IReadOnlyList<OcorrenciaDeGrupoInscricao>>(StringComparer.Ordinal), [],
            TestSupport.DefinicoesDeTeste.Das(respostas) with { NomeCivilDoLogin = cpf.Valor == PersonasDeTeste.KevinCarvalho.Valor ? PersonasDeTeste.NomeKevinCarvalho : PersonasDeTeste.NomeFernandaDrumond, NomeSocialDoLogin = respostas.TryGetValue("NOME_SOCIAL", out JsonElement nomeSocial) ? nomeSocial.GetString() : null },
            Unifesspa.UniPlus.Selecao.Domain.ValueObjects.PrecondicaoIfMatch.DeTags([inscricao.ETag])).IsSuccess.Should().BeTrue();
        return inscricao;
    }

    /// <summary>Nenhum processo divulgado: o nome do processo sai nulo e a lista não depende dele.</summary>
    private static ICertameDivulgadoRepository CertamesDivulgados() => Substitute.For<ICertameDivulgadoRepository>();

    [Fact(DisplayName = "Sem o claim cpf no token, recusa sem consultar repositório nenhum")]
    public async Task Handle_SemCpfNoToken_Recusa()
    {
        IUserContext userContext = Substitute.For<IUserContext>();
        userContext.Cpf.Returns((string?)null);
        IInscricaoRepository inscricaoRepository = Substitute.For<IInscricaoRepository>();

        Result<IReadOnlyList<MinhaInscricaoDto>> resultado = await MinhasInscricoesQueryHandler.Handle(
            new MinhasInscricoesQuery(), inscricaoRepository, CertamesDivulgados(), userContext, CancellationToken.None);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("Inscricao.CpfAusenteNoToken");
        await inscricaoRepository.DidNotReceive().ObterTodasPorCpfAsync(Arg.Any<Cpf>(), Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Sem o claim sub no token, recusa com SubObrigatorio em vez de listar vazio")]
    public async Task Handle_SemSubNoToken_Recusa()
    {
        Cpf cpf = PersonasDeTeste.FernandaDrumond;
        IInscricaoRepository inscricaoRepository = Substitute.For<IInscricaoRepository>();
        inscricaoRepository.ObterTodasPorCpfAsync(cpf, Arg.Any<CancellationToken>())
            .Returns((IReadOnlyList<Inscricao>)[Inscricao.Iniciar(Guid.CreateVersion7(), cpf, "sub-1", "https://issuer", Guid.CreateVersion7()).Value!]);

        Result<IReadOnlyList<MinhaInscricaoDto>> resultado = await MinhasInscricoesQueryHandler.Handle(
            new MinhasInscricoesQuery(), inscricaoRepository, CertamesDivulgados(), UsuarioComCpf(cpf, sub: ""), CancellationToken.None);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(InscricaoErrorCodes.SubObrigatorio);
    }

    [Fact(DisplayName = "Nenhuma inscrição do CPF devolve lista vazia, não falha")]
    public async Task Handle_SemInscricoes_DevolveListaVazia()
    {
        Cpf cpf = PersonasDeTeste.FernandaDrumond;
        IInscricaoRepository inscricaoRepository = Substitute.For<IInscricaoRepository>();
        inscricaoRepository.ObterTodasPorCpfAsync(cpf, Arg.Any<CancellationToken>()).Returns((IReadOnlyList<Inscricao>)[]);

        Result<IReadOnlyList<MinhaInscricaoDto>> resultado = await MinhasInscricoesQueryHandler.Handle(
            new MinhasInscricoesQuery(), inscricaoRepository, CertamesDivulgados(), UsuarioComCpf(cpf), CancellationToken.None);

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        resultado.Value!.Should().BeEmpty();
    }

    [Fact(DisplayName = "Inscrição de outro sub para o mesmo CPF não aparece na lista")]
    public async Task Handle_InscricaoDeOutroSub_NaoAparece()
    {
        Cpf cpf = PersonasDeTeste.KevinCarvalho;
        Inscricao deOutroSub = Inscricao.Iniciar(
            Guid.CreateVersion7(), cpf, "sub-outro", "https://issuer", Guid.CreateVersion7()).Value!;

        IInscricaoRepository inscricaoRepository = Substitute.For<IInscricaoRepository>();
        inscricaoRepository.ObterTodasPorCpfAsync(cpf, Arg.Any<CancellationToken>()).Returns((IReadOnlyList<Inscricao>)[deOutroSub]);

        Result<IReadOnlyList<MinhaInscricaoDto>> resultado = await MinhasInscricoesQueryHandler.Handle(
            new MinhasInscricoesQuery(), inscricaoRepository, CertamesDivulgados(), UsuarioComCpf(cpf, sub: "sub-1"), CancellationToken.None);

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        resultado.Value!.Should().BeEmpty();
    }

    [Fact(DisplayName = "Sem desejar nome social, o nome preferido é o civil")]
    public async Task Handle_SemDesejarNomeSocial_NomePreferidoEhOCivil()
    {
        Cpf cpf = PersonasDeTeste.FernandaDrumond;
        Inscricao inscricao = ComRespostas(cpf, "sub-1", Guid.CreateVersion7(), new Dictionary<string, JsonElement>(StringComparer.Ordinal)
        {
            ["NOME"] = JsonSerializer.SerializeToElement(PersonasDeTeste.NomeFernandaDrumond),
            ["DESEJA_NOME_SOCIAL"] = JsonSerializer.SerializeToElement(false),
        });

        IInscricaoRepository inscricaoRepository = Substitute.For<IInscricaoRepository>();
        inscricaoRepository.ObterTodasPorCpfAsync(cpf, Arg.Any<CancellationToken>()).Returns((IReadOnlyList<Inscricao>)[inscricao]);

        Result<IReadOnlyList<MinhaInscricaoDto>> resultado = await MinhasInscricoesQueryHandler.Handle(
            new MinhasInscricoesQuery(), inscricaoRepository, CertamesDivulgados(), UsuarioComCpf(cpf), CancellationToken.None);

        resultado.Value!.Should().ContainSingle(i => i.NomePreferido == PersonasDeTeste.NomeFernandaDrumond && i.CpfMascarado == cpf.Mascarado);
    }

    [Fact(DisplayName = "Cada inscrição vem com o nome divulgado do processo e os comprovantes numerados na ordem de emissão")]
    public async Task Handle_TrazONomeDoProcessoEOsComprovantes()
    {
        Cpf cpf = PersonasDeTeste.FernandaDrumond;
        Guid processoId = Guid.CreateVersion7();
        DateTimeOffset instante = new(2026, 12, 10, 9, 0, 0, TimeSpan.Zero);
        Inscricao inscricao = ComRespostas(cpf, "sub-1", processoId, new Dictionary<string, JsonElement>(StringComparer.Ordinal)
        {
            ["NOME"] = JsonSerializer.SerializeToElement(PersonasDeTeste.NomeFernandaDrumond),
        });
        IReadOnlyList<ComprovanteInscricao> comprovantes =
        [
            ComprovanteInscricao.Emitir(inscricao.Id, "00000001", instante, null,
                new Dictionary<string, JsonElement>(StringComparer.Ordinal), new Dictionary<string, JsonElement>(StringComparer.Ordinal),
                new Dictionary<string, IReadOnlyList<OcorrenciaDeGrupoInscricao>>(StringComparer.Ordinal), []),
            ComprovanteInscricao.Emitir(inscricao.Id, "00000001", instante.AddHours(1), null,
                new Dictionary<string, JsonElement>(StringComparer.Ordinal), new Dictionary<string, JsonElement>(StringComparer.Ordinal),
                new Dictionary<string, IReadOnlyList<OcorrenciaDeGrupoInscricao>>(StringComparer.Ordinal), []),
        ];

        IInscricaoRepository inscricaoRepository = Substitute.For<IInscricaoRepository>();
        inscricaoRepository.ObterTodasPorCpfAsync(cpf, Arg.Any<CancellationToken>()).Returns((IReadOnlyList<Inscricao>)[inscricao]);
        inscricaoRepository.ObterComprovantesAsync(Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>()).Returns(comprovantes);
        ICertameDivulgadoRepository divulgados = Substitute.For<ICertameDivulgadoRepository>();
        divulgados.ObterParaLeituraAsync(processoId, Arg.Any<CancellationToken>()).Returns(CertameDivulgado.Criar(
            processoId, numeroVersao: 1, Guid.CreateVersion7(), new string('a', 64), versaoProjecao: "1",
            new FacetasDoCertameDivulgado("pss-medicina-2027", "PSS Medicina 2027", "001/2026", ["AC"], instante, instante.AddDays(30)),
            """{"nome":"documento"}""", instante));

        Result<IReadOnlyList<MinhaInscricaoDto>> resultado = await MinhasInscricoesQueryHandler.Handle(
            new MinhasInscricoesQuery(), inscricaoRepository, divulgados, UsuarioComCpf(cpf), CancellationToken.None);

        MinhaInscricaoDto item = resultado.Value!.Should().ContainSingle().Subject;
        item.NomeDoProcesso.Should().Be("PSS Medicina 2027");
        item.Comprovantes.Should().Equal(new ComprovanteEmitidoDto(1, instante), new ComprovanteEmitidoDto(2, instante.AddHours(1)));
    }

    [Fact(DisplayName = "Desejando nome social e com NOME_SOCIAL preenchido, o nome preferido é o social (UNI-REQ-0032)")]
    public async Task Handle_ComNomeSocialDeclarado_NomePreferidoEhOSocial()
    {
        Cpf cpf = PersonasDeTeste.KevinCarvalho;
        Inscricao inscricao = ComRespostas(cpf, "sub-1", Guid.CreateVersion7(), new Dictionary<string, JsonElement>(StringComparer.Ordinal)
        {
            ["NOME"] = JsonSerializer.SerializeToElement(PersonasDeTeste.NomeKevinCarvalho),
            ["DESEJA_NOME_SOCIAL"] = JsonSerializer.SerializeToElement(true),
            ["NOME_SOCIAL"] = JsonSerializer.SerializeToElement("Kéfera"),
        });

        IInscricaoRepository inscricaoRepository = Substitute.For<IInscricaoRepository>();
        inscricaoRepository.ObterTodasPorCpfAsync(cpf, Arg.Any<CancellationToken>()).Returns((IReadOnlyList<Inscricao>)[inscricao]);

        Result<IReadOnlyList<MinhaInscricaoDto>> resultado = await MinhasInscricoesQueryHandler.Handle(
            new MinhasInscricoesQuery(), inscricaoRepository, CertamesDivulgados(), UsuarioComCpf(cpf), CancellationToken.None);

        resultado.Value!.Should().ContainSingle(i => i.NomePreferido == "Kéfera");
    }

    [Fact(DisplayName = "Desejando nome social sem NOME_SOCIAL preenchido, cai no nome civil, nunca em branco")]
    public async Task Handle_DesejaNomeSocialSemPreencher_CaiNoCivil()
    {
        Cpf cpf = PersonasDeTeste.FernandaDrumond;
        Inscricao inscricao = ComRespostas(cpf, "sub-1", Guid.CreateVersion7(), new Dictionary<string, JsonElement>(StringComparer.Ordinal)
        {
            ["NOME"] = JsonSerializer.SerializeToElement(PersonasDeTeste.NomeFernandaDrumond),
            ["DESEJA_NOME_SOCIAL"] = JsonSerializer.SerializeToElement(true),
        });

        IInscricaoRepository inscricaoRepository = Substitute.For<IInscricaoRepository>();
        inscricaoRepository.ObterTodasPorCpfAsync(cpf, Arg.Any<CancellationToken>()).Returns((IReadOnlyList<Inscricao>)[inscricao]);

        Result<IReadOnlyList<MinhaInscricaoDto>> resultado = await MinhasInscricoesQueryHandler.Handle(
            new MinhasInscricoesQuery(), inscricaoRepository, CertamesDivulgados(), UsuarioComCpf(cpf), CancellationToken.None);

        resultado.Value!.Should().ContainSingle(i => i.NomePreferido == PersonasDeTeste.NomeFernandaDrumond);
    }
}
