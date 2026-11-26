namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Queries;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Application.Abstractions.Authentication;
using Unifesspa.UniPlus.Application.Abstractions.Respostas;
using Unifesspa.UniPlus.Kernel.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Application.DTOs;
using Unifesspa.UniPlus.Selecao.Application.Queries.Inscricoes;
using Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands;
using Unifesspa.UniPlus.Selecao.Application.UnitTests.TestSupport;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Errors;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

/// <summary>
/// "Minhas inscrições" (<c>GET /api/selecao/minhas-inscricoes</c>): todas as do dono
/// do token, em qualquer processo, com os cursos que escolheram na versão em que estão.
/// </summary>
public sealed class MinhasInscricoesQueryHandlerTests
{
    private readonly ApoioDaListaDeInscricoes _apoio = new(DivulgacaoDeTeste.Relogio);

    [Fact(DisplayName = "Sem o claim cpf no token, recusa sem consultar repositório nenhum")]
    public async Task Handle_SemCpfNoToken_Recusa()
    {
        IUserContext userContext = Substitute.For<IUserContext>();
        userContext.Cpf.Returns((string?)null);
        IInscricaoRepository inscricaoRepository = Substitute.For<IInscricaoRepository>();

        Result<IReadOnlyList<MinhaInscricaoDto>> resultado = await MinhasInscricoesQueryHandler.Handle(new MinhasInscricoesQuery(), inscricaoRepository, SubmeterDeTeste.ComprovantesDe(inscricaoRepository), _apoio.ProcessosSemDivulgacao(), userContext, CancellationToken.None);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("Inscricao.CpfAusenteNoToken");
        await inscricaoRepository.DidNotReceive().ObterTodasPorCpfAsync(Arg.Any<Cpf>(), Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Sem o claim sub no token, recusa com ContaIdObrigatorio em vez de listar vazio")]
    public async Task Handle_SemContaIdNoToken_Recusa()
    {
        Cpf cpf = PersonasDeTeste.FernandaDrumond;
        IInscricaoRepository inscricaoRepository = Substitute.For<IInscricaoRepository>();
        inscricaoRepository.ObterTodasPorCpfAsync(cpf, Arg.Any<CancellationToken>())
            .Returns((IReadOnlyList<Inscricao>)[Inscricao.Iniciar(Guid.CreateVersion7(), cpf, "conta-1", "https://issuer", Guid.CreateVersion7()).Value!]);

        Result<IReadOnlyList<MinhaInscricaoDto>> resultado = await MinhasInscricoesQueryHandler.Handle(new MinhasInscricoesQuery(), inscricaoRepository, SubmeterDeTeste.ComprovantesDe(inscricaoRepository), _apoio.ProcessosSemDivulgacao(), ApoioDaListaDeInscricoes.UsuarioComCpf(cpf, contaId: ""), CancellationToken.None);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(InscricaoErrorCodes.ContaIdObrigatorio);
    }

    [Fact(DisplayName = "Nenhuma inscrição do CPF devolve lista vazia, não falha")]
    public async Task Handle_SemInscricoes_DevolveListaVazia()
    {
        Cpf cpf = PersonasDeTeste.FernandaDrumond;
        IInscricaoRepository inscricaoRepository = Substitute.For<IInscricaoRepository>();
        inscricaoRepository.ObterTodasPorCpfAsync(cpf, Arg.Any<CancellationToken>()).Returns((IReadOnlyList<Inscricao>)[]);

        Result<IReadOnlyList<MinhaInscricaoDto>> resultado = await MinhasInscricoesQueryHandler.Handle(new MinhasInscricoesQuery(), inscricaoRepository, SubmeterDeTeste.ComprovantesDe(inscricaoRepository), _apoio.ProcessosSemDivulgacao(), ApoioDaListaDeInscricoes.UsuarioComCpf(cpf), CancellationToken.None);

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        resultado.Value!.Should().BeEmpty();
    }

    [Fact(DisplayName = "Inscrição de outra conta para o mesmo CPF não aparece na lista")]
    public async Task Handle_InscricaoDeOutroContaId_NaoAparece()
    {
        Cpf cpf = PersonasDeTeste.KevinCarvalho;
        Inscricao deOutroContaId = Inscricao.Iniciar(
            Guid.CreateVersion7(), cpf, "conta-outro", "https://issuer", Guid.CreateVersion7()).Value!;

        IInscricaoRepository inscricaoRepository = Substitute.For<IInscricaoRepository>();
        inscricaoRepository.ObterTodasPorCpfAsync(cpf, Arg.Any<CancellationToken>()).Returns((IReadOnlyList<Inscricao>)[deOutroContaId]);

        Result<IReadOnlyList<MinhaInscricaoDto>> resultado = await MinhasInscricoesQueryHandler.Handle(new MinhasInscricoesQuery(), inscricaoRepository, SubmeterDeTeste.ComprovantesDe(inscricaoRepository), _apoio.ProcessosSemDivulgacao(), ApoioDaListaDeInscricoes.UsuarioComCpf(cpf, contaId: "conta-1"), CancellationToken.None);

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        resultado.Value!.Should().BeEmpty();
    }

    [Fact(DisplayName = "Cada inscrição vem com o nome divulgado do processo e os comprovantes numerados na ordem de emissão")]
    public async Task Handle_TrazONomeDoProcessoEONumeroDoUltimoComprovante()
    {
        Cpf cpf = PersonasDeTeste.FernandaDrumond;
        Guid processoId = Guid.CreateVersion7();
        DateTimeOffset instante = new(2026, 12, 10, 9, 0, 0, TimeSpan.Zero);
        Inscricao inscricao = ApoioDaListaDeInscricoes.InscricaoComNome(cpf, processoId);
        IReadOnlyList<EmissaoDoComprovante> emissoes = [new(inscricao.Id, instante), new(inscricao.Id, instante.AddHours(1))];

        Result<IReadOnlyList<MinhaInscricaoDto>> resultado = await _apoio.ListarAsync([inscricao],
            divulgados: ApoioDaListaDeInscricoes.DivulgacaoDoProcesso(processoId, instante), emissoes: emissoes);

        MinhaInscricaoDto item = resultado.Value!.Should().ContainSingle().Subject;
        item.NomeDoProcesso.Should().Be("PSS Medicina 2027");
        item.NumeroDoUltimoComprovante.Should().Be(2);
    }

    [Fact(DisplayName = "A cancelada e a nova inscrição do mesmo processo aparecem, cada uma com o próprio id")]
    public async Task Handle_CanceladaENova_AparecemAsDuasComIdProprio()
    {
        Cpf cpf = PersonasDeTeste.KevinCarvalho;
        Guid processoId = Guid.CreateVersion7();
        Inscricao cancelada = ApoioDaListaDeInscricoes.InscricaoCancelada(cpf, processoId);
        Inscricao nova = Inscricao.Iniciar(processoId, cpf, "conta-1", "https://issuer", Guid.CreateVersion7()).Value!;
        Result<IReadOnlyList<MinhaInscricaoDto>> resultado = await _apoio.ListarAsync([cancelada, nova],
            emissoes: [new EmissaoDoComprovante(cancelada.Id, DivulgacaoDeTeste.DentroDoPeriodo)]);

        resultado.Value!.Select(static i => i.InscricaoId).Should().BeEquivalentTo([cancelada.Id, nova.Id]);
        MinhaInscricaoDto daCancelada = resultado.Value!.Single(i => i.InscricaoId == cancelada.Id);
        daCancelada.Situacao.Should().Be(CodigoComRotulo<SituacaoInscricao>.De(SituacaoInscricao.Cancelada));
        daCancelada.MotivoDoCancelamento.Should().Be("a pedido do candidato");
        daCancelada.CanceladaEm.Should().NotBeNull();
        daCancelada.NumeroDoUltimoComprovante.Should().BeNull("a cancelada tem comprovante emitido, mas a rota o recusa");
        daCancelada.Acoes.Should().Be(new AcoesDaInscricaoDto(false, false, false, false));
    }

    [Fact]
    public void De_InscricaoEEmissoesNulas_RecusaInscricaoPrimeiro()
    {
        Action acao = () => MinhaInscricaoDto.De(null!, null, null, null!, null!);

        acao.Should().Throw<ArgumentNullException>().Which.ParamName.Should().Be("inscricao");
    }

    [Fact]
    public void De_InscricaoCanceladaEEmissoesNulas_RecusaEmissoesAntesDasOfertas()
    {
        Inscricao cancelada = ApoioDaListaDeInscricoes.InscricaoCancelada(PersonasDeTeste.KevinCarvalho, Guid.CreateVersion7());
        Action acao = () => MinhaInscricaoDto.De(cancelada, null, null, null!, null!);

        acao.Should().Throw<ArgumentNullException>().Which.ParamName.Should().Be("emissoes");
    }

    [Fact(DisplayName = "As opções de curso saem na ordem da inscrição, com o nome que a versão dela congelou")]
    public async Task Handle_TrazOsCursosEscolhidosEmOrdem()
    {
        Cpf cpf = PersonasDeTeste.FernandaDrumond;
        Guid processoId = Guid.CreateVersion7();
        Guid medicina = Guid.CreateVersion7();
        Guid direito = Guid.CreateVersion7();
        VersaoConfiguracao versao = ApoioDaListaDeInscricoes.NovaVersao(processoId);
        IRegistroCodecsEnvelope registro = ApoioDaListaDeInscricoes.RegistroDas((versao, ApoioDaListaDeInscricoes.EnvelopeComOfertas(
            OfertasDeTeste.DistribuicaoDoCurso(medicina, "Medicina", "AC"), OfertasDeTeste.DistribuicaoDoCurso(direito, "Direito", "AC"))));
        // A 1ª opção é a que tem o maior id: a ordem sai da escolha, não da ordem das ofertas na versão.
        Inscricao inscricao = ApoioDaListaDeInscricoes.ComOpcoes(cpf, processoId, versao.Id, primeira: direito, segunda: medicina);
        Result<IReadOnlyList<MinhaInscricaoDto>> resultado = await _apoio.ListarAsync([inscricao], [versao], registro);

        resultado.Value!.Single().OpcoesDeCurso.Should().Equal(
            new OpcaoDeCursoDaInscricaoDto(1, "Direito", "Bacharelado"), new OpcaoDeCursoDaInscricaoDto(2, "Medicina", "Bacharelado"));
    }

    [Fact(DisplayName = "Rascunho que ainda não escolheu curso vem sem opções, e uma versão que não se lê não derruba a lista")]
    public async Task Handle_SemCursoEscolhidoOuVersaoIlegivel_VemSemOpcoes()
    {
        Cpf cpf = PersonasDeTeste.FernandaDrumond;
        Guid processoId = Guid.CreateVersion7();
        Guid medicina = Guid.CreateVersion7();
        VersaoConfiguracao versao = ApoioDaListaDeInscricoes.NovaVersao(processoId);
        IRegistroCodecsEnvelope registro = ApoioDaListaDeInscricoes.RegistroDas((versao, ApoioDaListaDeInscricoes.EnvelopeComOfertas(OfertasDeTeste.DistribuicaoDoCurso(medicina, "Medicina", "AC"))));
        Inscricao semCurso = ApoioDaListaDeInscricoes.ComOpcoes(cpf, processoId, versao.Id, primeira: null, segunda: null);
        Inscricao versaoSumida = ApoioDaListaDeInscricoes.ComOpcoes(cpf, processoId, Guid.CreateVersion7(), primeira: medicina, segunda: null);
        Result<IReadOnlyList<MinhaInscricaoDto>> resultado = await _apoio.ListarAsync([semCurso, versaoSumida], [versao], registro);

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        resultado.Value!.Should().HaveCount(2).And.OnlyContain(static i => i.OpcoesDeCurso.Count == 0);
    }

    [Fact(DisplayName = "O nome do curso vem da versão em que a inscrição está, não da versão mais recente do processo")]
    public async Task Handle_NomeDoCursoEhODaVersaoDaInscricao()
    {
        Cpf cpf = PersonasDeTeste.FernandaDrumond;
        Guid processoId = Guid.CreateVersion7();
        Guid medicina = Guid.CreateVersion7();
        VersaoConfiguracao daInscricao = ApoioDaListaDeInscricoes.NovaVersao(processoId);
        VersaoConfiguracao retificada = ApoioDaListaDeInscricoes.NovaVersao(processoId);
        IRegistroCodecsEnvelope registro = ApoioDaListaDeInscricoes.RegistroDas(
            (daInscricao, ApoioDaListaDeInscricoes.EnvelopeComOfertas(OfertasDeTeste.DistribuicaoDoCurso(medicina, "Medicina", "AC"))),
            (retificada, ApoioDaListaDeInscricoes.EnvelopeComOfertas(OfertasDeTeste.DistribuicaoDoCurso(medicina, "Medicina (retificado)", "AC"))));
        Inscricao inscricao = ApoioDaListaDeInscricoes.ComOpcoes(cpf, processoId, daInscricao.Id, primeira: medicina, segunda: null);
        Result<IReadOnlyList<MinhaInscricaoDto>> resultado = await _apoio.ListarAsync([inscricao], [daInscricao, retificada], registro);

        resultado.Value!.Single().OpcoesDeCurso.Should().Equal(new OpcaoDeCursoDaInscricaoDto(1, "Medicina", "Bacharelado"));
    }

    [Fact(DisplayName = "Sem a 1ª opção nomeada pela versão, a 2ª não sobe para o lugar dela")]
    public async Task Handle_SemAPrimeiraOpcaoNaVersao_NaoListaASegundaComoPrimeira()
    {
        Cpf cpf = PersonasDeTeste.FernandaDrumond;
        Guid processoId = Guid.CreateVersion7();
        Guid direito = Guid.CreateVersion7();
        VersaoConfiguracao versao = ApoioDaListaDeInscricoes.NovaVersao(processoId);
        IRegistroCodecsEnvelope registro = ApoioDaListaDeInscricoes.RegistroDas((versao, ApoioDaListaDeInscricoes.EnvelopeComOfertas(OfertasDeTeste.DistribuicaoDoCurso(direito, "Direito", "AC"))));
        Inscricao inscricao = ApoioDaListaDeInscricoes.ComOpcoes(cpf, processoId, versao.Id, primeira: Guid.CreateVersion7(), segunda: direito);
        Result<IReadOnlyList<MinhaInscricaoDto>> resultado = await _apoio.ListarAsync([inscricao], [versao], registro);

        resultado.Value!.Single().OpcoesDeCurso.Should().BeEmpty();
    }
}
