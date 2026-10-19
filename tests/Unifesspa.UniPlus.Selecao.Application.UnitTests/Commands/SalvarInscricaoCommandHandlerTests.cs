namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands;

using System.Text;
using System.Text.Json;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Application.Abstractions.Authentication;
using Unifesspa.UniPlus.Kernel.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Application.Commands.Inscricoes;
using Unifesspa.UniPlus.Selecao.Application.DTOs;
using Unifesspa.UniPlus.Selecao.Application.UnitTests.TestSupport;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Errors;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

/// <summary>
/// Salvar o rascunho (issue #1859): a validação é estrutural — só recusa por campo a resposta que
/// viola uma restrição do formulário, nunca por obrigatoriedade. As regras vêm da versão FIXADA
/// pela inscrição ao iniciar, resolvida por <c>IProcessoSeletivoRepository.ObterVersoesAsync</c>,
/// nunca pela divulgação atual — uma retificação publicada depois não pode mudar a validação de
/// uma inscrição já em curso (UNI-REQ-0084).
/// </summary>
public sealed class SalvarInscricaoCommandHandlerTests
{
    private static readonly Guid ProcessoId = Guid.CreateVersion7();

    private static Inscricao NovaInscricao(Cpf cpf, string sub, Guid versaoFormularioId) =>
        Inscricao.Iniciar(ProcessoId, cpf, sub, "https://issuer", versaoFormularioId).Value!;

    private static IUserContext UsuarioComCpf(Cpf cpf, string sub = "sub-1")
    {
        IUserContext contexto = Substitute.For<IUserContext>();
        contexto.Cpf.Returns(cpf.Valor);
        contexto.NomeCompleto.Returns(PersonasDeTeste.NomeFernandaDrumond);
        contexto.UserId.Returns(sub);
        contexto.Issuer.Returns("https://issuer");
        return contexto;
    }

    [Fact(DisplayName = "Sem o claim cpf no token, recusa sem consultar repositório nenhum")]
    public async Task Handle_SemCpfNoToken_Recusa()
    {
        IUserContext userContext = Substitute.For<IUserContext>();
        userContext.Cpf.Returns((string?)null);

        IInscricaoRepository inscricaoRepository = Substitute.For<IInscricaoRepository>();

        Result<InscricaoDto> resultado = await SalvarInscricaoCommandHandler.Handle(
            new SalvarInscricaoCommand(ProcessoId, new InscricaoRequest(null, null, null, null), PrecondicaoIfMatch.DeTags(["\"x\""])),
            inscricaoRepository, Substitute.For<IProcessoSeletivoRepository>(), DivulgacaoDeTeste.Certame(ProcessoId), Substitute.For<IRegistroCodecsEnvelope>(), DivulgacaoDeTeste.Fuso(), Substitute.For<IConferenciaEnderecoGeo>(), Substitute.For<IEnderecoNoAcervoPublico>(),
            Substitute.For<ISelecaoUnitOfWork>(), userContext, DivulgacaoDeTeste.Relogio, CancellationToken.None);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("Inscricao.CpfAusenteNoToken");
        await inscricaoRepository.DidNotReceive().ObterPorProcessoECpfAsync(Arg.Any<Guid>(), Arg.Any<Cpf>(), Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Sem inscrição para o CPF e o processo, recusa como não encontrada")]
    public async Task Handle_SemInscricao_RecusaComoNaoEncontrada()
    {
        Cpf cpf = PersonasDeTeste.FernandaDrumond;
        IInscricaoRepository inscricaoRepository = Substitute.For<IInscricaoRepository>();
        inscricaoRepository.ObterPorProcessoECpfAsync(ProcessoId, cpf, Arg.Any<CancellationToken>()).Returns((Inscricao?)null);

        Result<InscricaoDto> resultado = await SalvarInscricaoCommandHandler.Handle(
            new SalvarInscricaoCommand(ProcessoId, new InscricaoRequest(null, null, null, null), PrecondicaoIfMatch.DeTags(["\"x\""])),
            inscricaoRepository, Substitute.For<IProcessoSeletivoRepository>(), DivulgacaoDeTeste.Certame(ProcessoId), Substitute.For<IRegistroCodecsEnvelope>(), DivulgacaoDeTeste.Fuso(), Substitute.For<IConferenciaEnderecoGeo>(), Substitute.For<IEnderecoNoAcervoPublico>(),
            Substitute.For<ISelecaoUnitOfWork>(), UsuarioComCpf(cpf), DivulgacaoDeTeste.Relogio, CancellationToken.None);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("Inscricao.NaoEncontrada");
    }

    [Fact(DisplayName = "Sub diferente do gravado para o mesmo CPF é recusado como dono divergente, sem tentar gravar")]
    public async Task Handle_SubDivergente_RecusaComoDonoDivergente()
    {
        Cpf cpf = PersonasDeTeste.KevinCarvalho;
        Inscricao existente = NovaInscricao(cpf, "sub-original", Guid.CreateVersion7());

        IInscricaoRepository inscricaoRepository = Substitute.For<IInscricaoRepository>();
        inscricaoRepository.ObterPorProcessoECpfAsync(ProcessoId, cpf, Arg.Any<CancellationToken>()).Returns(existente);
        ISelecaoUnitOfWork unitOfWork = Substitute.For<ISelecaoUnitOfWork>();

        Result<InscricaoDto> resultado = await SalvarInscricaoCommandHandler.Handle(
            new SalvarInscricaoCommand(ProcessoId, new InscricaoRequest(null, null, null, null), PrecondicaoIfMatch.DeTags([existente.ETag])),
            inscricaoRepository, Substitute.For<IProcessoSeletivoRepository>(), DivulgacaoDeTeste.Certame(ProcessoId), Substitute.For<IRegistroCodecsEnvelope>(), DivulgacaoDeTeste.Fuso(), Substitute.For<IConferenciaEnderecoGeo>(), Substitute.For<IEnderecoNoAcervoPublico>(),
            unitOfWork, UsuarioComCpf(cpf, sub: "sub-outro"), DivulgacaoDeTeste.Relogio, CancellationToken.None);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("Inscricao.DonoDivergente");
        await unitOfWork.DidNotReceive().SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Versão fixada que não existe mais entre as versões do processo aflora como corrupção, sem gravar nada")]
    public async Task Handle_VersaoFixadaInexistente_AfloraComoCorrupcao()
    {
        Cpf cpf = PersonasDeTeste.FernandaDrumond;
        Inscricao existente = NovaInscricao(cpf, "sub-1", Guid.CreateVersion7());

        IInscricaoRepository inscricaoRepository = Substitute.For<IInscricaoRepository>();
        inscricaoRepository.ObterPorProcessoECpfAsync(ProcessoId, cpf, Arg.Any<CancellationToken>()).Returns(existente);

        // A versão divulgada existe e o período está aberto; só a versão fixada pela inscrição sumiu.
        IProcessoSeletivoRepository processoRepository = Substitute.For<IProcessoSeletivoRepository>();
        IRegistroCodecsEnvelope registroCodecs = Substitute.For<IRegistroCodecsEnvelope>();
        (VersaoConfiguracao divulgada, GrafoConfiguracao grafo) = MontarVersaoComItem(FatoColetado.Criar(
            "APELIDO", 0, "Apelido", TipoRenderizacao.Texto, Obrigatoriedade.Nunca, null,
            classificacaoProtecao: "PESSOAL", formato: "LIVRE").Value!);
        DivulgacaoDeTeste.ConfigurarDivulgada(processoRepository, registroCodecs, divulgada, grafo);
        processoRepository.ObterVersoesAsync(ProcessoId, Arg.Any<CancellationToken>()).Returns([]);

        ISelecaoUnitOfWork unitOfWork = Substitute.For<ISelecaoUnitOfWork>();

        Result<InscricaoDto> resultado = await SalvarInscricaoCommandHandler.Handle(
            new SalvarInscricaoCommand(ProcessoId, new InscricaoRequest(null, null, null, null), PrecondicaoIfMatch.DeTags([existente.ETag])),
            inscricaoRepository, processoRepository, DivulgacaoDeTeste.Certame(ProcessoId), registroCodecs, DivulgacaoDeTeste.Fuso(), Substitute.For<IConferenciaEnderecoGeo>(), Substitute.For<IEnderecoNoAcervoPublico>(), unitOfWork, UsuarioComCpf(cpf), DivulgacaoDeTeste.Relogio, CancellationToken.None);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("Snapshot.VigenteAusente");
        await inscricaoRepository.DidNotReceive().AtualizarAsync(Arg.Any<Inscricao>(), Arg.Any<CancellationToken>());
        await unitOfWork.DidNotReceive().SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    private static (VersaoConfiguracao Versao, GrafoConfiguracao Grafo) MontarVersaoComItem(
        FatoColetado item, IReadOnlyList<GrupoColetado>? grupos = null)
    {
        ProcessoSeletivo processoBase = ProcessoSeletivoConformeBuilder.Criar("PS 2026 — Salvar rascunho");
        processoBase.DefinirItens([item], PrecondicaoIfMatch.Ausente, grupos: grupos).IsSuccess.Should().BeTrue();

        // A definição avaliável só reconhece um item que esteja numa etapa de um FORMULÁRIO
        // (RecorteDaFinalidade filtra por etapa de FormularioProcesso, não por EtapaProcesso do
        // certame) — por isso o grafo leva processoBase.Formularios, que FormularioDeTeste.DefinirItens
        // já colocou o item dentro da seção "DADOS" da finalidade Inscrição.
        GrafoConfiguracao grafo = new(
            [.. processoBase.Etapas], processoBase.OfertaAtendimento!, [.. processoBase.DistribuicaoVagas],
            processoBase.BonusRegional, [.. processoBase.CriteriosDesempate], processoBase.Classificacao!,
            [.. processoBase.CronogramaFases], [.. processoBase.DocumentosExigidos], [], null,
            fatosColetados: [.. processoBase.FatosColetados],
            formularios: [.. processoBase.Formularios],
            gruposColetados: [.. processoBase.GruposColetados],
            numeroDeOpcoesDeCurso: 1);

        // O bloco documentosExigidos.dataReferenciaFatos é lido do JSON cru por
        // ContextoDaVersaoFixadaResolver (issue #1859) — presente em todo envelope real; aqui
        // nulo, porque este processo de teste não declara referência temporal.
        VersaoConfiguracao versao = VersaoConfiguracao.Abrir(
            ProcessoId, Encoding.UTF8.GetBytes("""{"documentosExigidos":{"dataReferenciaFatos":null}}"""), "0.0.22", "canonical-json/sha256@v1",
            Guid.CreateVersion7(), new string('a', 64), "user-sub-1", new DateTimeOffset(2026, 10, 10, 12, 0, 0, TimeSpan.Zero));

        return (versao, grafo);
    }

    [Fact(DisplayName = "Fora do período da inscrição, salvar é recusado sem gravar nada")]
    public async Task Handle_ForaDoPeriodo_RecusaSemGravar()
    {
        Cpf cpf = PersonasDeTeste.FernandaDrumond;
        FatoColetado item = FatoColetado.Criar(
            "APELIDO", 0, "Apelido", TipoRenderizacao.Texto, Obrigatoriedade.Nunca, null,
            classificacaoProtecao: "PESSOAL", formato: "LIVRE").Value!;
        (VersaoConfiguracao versao, GrafoConfiguracao grafo) = MontarVersaoComItem(item);
        Inscricao existente = NovaInscricao(cpf, "sub-1", versao.Id);

        IInscricaoRepository inscricaoRepository = Substitute.For<IInscricaoRepository>();
        inscricaoRepository.ObterPorProcessoECpfAsync(ProcessoId, cpf, Arg.Any<CancellationToken>()).Returns(existente);
        IProcessoSeletivoRepository processoRepository = Substitute.For<IProcessoSeletivoRepository>();
        IRegistroCodecsEnvelope registroCodecs = Substitute.For<IRegistroCodecsEnvelope>();
        DivulgacaoDeTeste.ConfigurarVersao(processoRepository, registroCodecs, versao, grafo);
        ISelecaoUnitOfWork unitOfWork = Substitute.For<ISelecaoUnitOfWork>();

        Result<InscricaoDto> resultado = await SalvarInscricaoCommandHandler.Handle(
            new SalvarInscricaoCommand(ProcessoId, new InscricaoRequest(null, null, null, null), PrecondicaoIfMatch.DeTags([existente.ETag])),
            inscricaoRepository, processoRepository, DivulgacaoDeTeste.Certame(ProcessoId), registroCodecs, DivulgacaoDeTeste.Fuso(), Substitute.For<IConferenciaEnderecoGeo>(), Substitute.For<IEnderecoNoAcervoPublico>(),
            unitOfWork, UsuarioComCpf(cpf), new RelogioFixo(DivulgacaoDeTeste.DentroDoPeriodo.AddMonths(1)), CancellationToken.None);

        resultado.Error!.Code.Should().Be(InscricaoErrorCodes.ForaDoPeriodo);
        await unitOfWork.DidNotReceive().SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "A retificação que prorroga o período vale para quem já iniciou: salva depois do fim da versão fixada")]
    public async Task Handle_PeriodoProrrogadoPelaRetificacao_Salva()
    {
        Cpf cpf = PersonasDeTeste.FernandaDrumond;
        FatoColetado item = FatoColetado.Criar(
            "APELIDO", 0, "Apelido", TipoRenderizacao.Texto, Obrigatoriedade.Nunca, null,
            classificacaoProtecao: "PESSOAL", formato: "LIVRE").Value!;
        (VersaoConfiguracao fixada, GrafoConfiguracao grafo) = MontarVersaoComItem(item);
        (VersaoConfiguracao divulgada, _) = MontarVersaoComItem(item);
        Inscricao existente = NovaInscricao(cpf, "sub-1", fixada.Id);

        IInscricaoRepository inscricaoRepository = Substitute.For<IInscricaoRepository>();
        inscricaoRepository.ObterPorProcessoECpfAsync(ProcessoId, cpf, Arg.Any<CancellationToken>()).Returns(existente);
        IProcessoSeletivoRepository processoRepository = Substitute.For<IProcessoSeletivoRepository>();
        IRegistroCodecsEnvelope registroCodecs = Substitute.For<IRegistroCodecsEnvelope>();
        DivulgacaoDeTeste.ConfigurarFixadaEDivulgada(
            processoRepository, registroCodecs, grafo,
            fixada: (fixada, DivulgacaoDeTeste.FimDoPeriodo),
            divulgada: (divulgada, DivulgacaoDeTeste.FimDoPeriodo.AddDays(30)));
        ISelecaoUnitOfWork unitOfWork = Substitute.For<ISelecaoUnitOfWork>();

        Result<InscricaoDto> resultado = await SalvarInscricaoCommandHandler.Handle(
            new SalvarInscricaoCommand(ProcessoId, new InscricaoRequest(null, null, null, null), PrecondicaoIfMatch.DeTags([existente.ETag])),
            inscricaoRepository, processoRepository, DivulgacaoDeTeste.Certame(ProcessoId), registroCodecs, DivulgacaoDeTeste.Fuso(), Substitute.For<IConferenciaEnderecoGeo>(), Substitute.For<IEnderecoNoAcervoPublico>(),
            unitOfWork, UsuarioComCpf(cpf), new RelogioFixo(DivulgacaoDeTeste.FimDoPeriodo.AddDays(10)), CancellationToken.None);

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        await unitOfWork.Received(1).SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Resposta dentro do tamanho permitido é salva, sobre a versão fixada ao iniciar")]
    public async Task Handle_RespostaValida_Salva()
    {
        Cpf cpf = PersonasDeTeste.FernandaDrumond;

        FatoColetado item = FatoColetado.Criar(
            "APELIDO", 0, "Apelido", TipoRenderizacao.Texto, Obrigatoriedade.Sempre, null,
            classificacaoProtecao: "PESSOAL", formato: "LIVRE", restricoes: [new TamanhoTexto(1, 10)]).Value!;
        (VersaoConfiguracao versao, GrafoConfiguracao grafo) = MontarVersaoComItem(item);
        Inscricao existente = NovaInscricao(cpf, "sub-1", versao.Id);

        IInscricaoRepository inscricaoRepository = Substitute.For<IInscricaoRepository>();
        inscricaoRepository.ObterPorProcessoECpfAsync(ProcessoId, cpf, Arg.Any<CancellationToken>()).Returns(existente);

        IProcessoSeletivoRepository processoRepository = Substitute.For<IProcessoSeletivoRepository>();

        IRegistroCodecsEnvelope registroCodecs = Substitute.For<IRegistroCodecsEnvelope>();
        DivulgacaoDeTeste.ConfigurarVersao(processoRepository, registroCodecs, versao, grafo);

        ISelecaoUnitOfWork unitOfWork = Substitute.For<ISelecaoUnitOfWork>();
        InscricaoRequest request = new(
            null, new Dictionary<string, JsonElement>(StringComparer.Ordinal) { ["APELIDO"] = JsonSerializer.SerializeToElement("Nanda") }, null, null);

        Result<InscricaoDto> resultado = await SalvarInscricaoCommandHandler.Handle(
            new SalvarInscricaoCommand(ProcessoId, request, PrecondicaoIfMatch.DeTags([existente.ETag])),
            inscricaoRepository, processoRepository, DivulgacaoDeTeste.Certame(ProcessoId), registroCodecs, DivulgacaoDeTeste.Fuso(), Substitute.For<IConferenciaEnderecoGeo>(), Substitute.For<IEnderecoNoAcervoPublico>(), unitOfWork, UsuarioComCpf(cpf), DivulgacaoDeTeste.Relogio, CancellationToken.None);

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        resultado.Value!.Respostas.Should().ContainKey("APELIDO").WhoseValue.GetString().Should().Be("Nanda");
        await inscricaoRepository.Received(1).AtualizarAsync(existente, Arg.Any<CancellationToken>());
        await unitOfWork.Received(1).SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Salvar com a oferta A avalia só as modalidades da oferta A, mesmo com o rascunho gravado na oferta B")]
    public async Task Handle_OfertaDoCorpo_AvaliaSoAsModalidadesDaOfertaSalva()
    {
        Cpf cpf = PersonasDeTeste.FernandaDrumond;
        (VersaoConfiguracao versao, GrafoConfiguracao grafoBase) = MontarVersaoComItem(FatoColetado.Criar(
            "APELIDO", 0, "Apelido", TipoRenderizacao.Texto, Obrigatoriedade.Nunca, null,
            classificacaoProtecao: "PESSOAL", formato: "LIVRE").Value!);
        Guid ofertaA = Guid.CreateVersion7();
        Guid ofertaB = Guid.CreateVersion7();
        GrafoConfiguracao grafo = OfertasDeTeste.ComDistribuicao(
            grafoBase, OfertasDeTeste.Distribuicao(ofertaA, "AC"), OfertasDeTeste.Distribuicao(ofertaB, "AC", "AC_FEM"));
        Inscricao existente = NovaInscricao(cpf, "sub-1", versao.Id);
        existente.Salvar(
            ofertaB, null, new Dictionary<string, JsonElement>(), new Dictionary<string, IReadOnlyList<OcorrenciaDeGrupoInscricao>>(), [],
            Unifesspa.UniPlus.Selecao.Application.Services.ContextoDaVersaoFixadaResolver.Admitidas(grafo) with { NomeCivilDoLogin = PersonasDeTeste.NomeFernandaDrumond },
            PrecondicaoIfMatch.DeTags([existente.ETag])).IsSuccess.Should().BeTrue();

        IInscricaoRepository inscricaoRepository = Substitute.For<IInscricaoRepository>();
        inscricaoRepository.ObterPorProcessoECpfAsync(ProcessoId, cpf, Arg.Any<CancellationToken>()).Returns(existente);
        IProcessoSeletivoRepository processoRepository = Substitute.For<IProcessoSeletivoRepository>();
        IRegistroCodecsEnvelope registroCodecs = Substitute.For<IRegistroCodecsEnvelope>();
        DivulgacaoDeTeste.ConfigurarVersao(processoRepository, registroCodecs, versao, grafo);

        InscricaoRequest request = new(
            ofertaA,
            new Dictionary<string, JsonElement>(StringComparer.Ordinal)
            {
                [OfertasDeTeste.FatoDaAcaoAfirmativa] = JsonSerializer.SerializeToElement(OfertasDeTeste.ValorDaAcaoAfirmativa),
            },
            null, null);

        Result<InscricaoDto> resultado = await SalvarInscricaoCommandHandler.Handle(
            new SalvarInscricaoCommand(ProcessoId, request, PrecondicaoIfMatch.DeTags([existente.ETag])),
            inscricaoRepository, processoRepository, DivulgacaoDeTeste.Certame(ProcessoId), registroCodecs, DivulgacaoDeTeste.Fuso(), Substitute.For<IConferenciaEnderecoGeo>(), Substitute.For<IEnderecoNoAcervoPublico>(),
            Substitute.For<ISelecaoUnitOfWork>(), UsuarioComCpf(cpf), DivulgacaoDeTeste.Relogio, CancellationToken.None);

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        resultado.Value!.OfertaCursoId.Should().Be(ofertaA);
        resultado.Value!.Avaliacao.Modalidades.Itens.Select(static m => m.Codigo).Should().Equal(
            ["AC"], "a modalidade que só a oferta B tem não vale para quem salvou na oferta A");
    }

    [Fact(DisplayName = "Endereço com CEP que o Geo não encontra é recusado no campo do endereço, sem gravar o rascunho")]
    public async Task Handle_EnderecoComCepNaoEncontrado_RecusaSemGravar()
    {
        IConferenciaEnderecoGeo geo = Substitute.For<IConferenciaEnderecoGeo>();
        geo.ResolverCepAsync("79096730", Arg.Any<CancellationToken>())
            .Returns(Result<CepResolvido>.Failure(new DomainError(ConferenciaEnderecoGeoErrorCodes.CepNaoResolve, "sem cobertura")));

        (Result<InscricaoDto> resultado, ISelecaoUnitOfWork unitOfWork) = await SalvarEnderecoAsync(geo);

        resultado.Errors.Should().ContainSingle(e => e.Field == "ENDERECO_RESIDENCIAL" && e.Error.Code == InscricaoErrorCodes.EnderecoCepNaoEncontrado);
        await unitOfWork.DidNotReceive().SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Geo fora do ar não impede salvar o rascunho do endereço, e a recusa nunca é de CEP não encontrado")]
    public async Task Handle_EnderecoComGeoIndisponivel_SalvaORascunho()
    {
        IConferenciaEnderecoGeo geo = Substitute.For<IConferenciaEnderecoGeo>();
        geo.ResolverCepAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Result<CepResolvido>.Failure(new DomainError(ConferenciaEnderecoGeoErrorCodes.GeoIndisponivel, "fora do ar")));

        (Result<InscricaoDto> resultado, ISelecaoUnitOfWork unitOfWork) = await SalvarEnderecoAsync(geo);

        resultado.Errors.Should().NotContain(e => e.Error.Code == InscricaoErrorCodes.EnderecoCepNaoEncontrado);
        await unitOfWork.Received(1).SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    private static async Task<(Result<InscricaoDto> Resultado, ISelecaoUnitOfWork UnitOfWork)> SalvarEnderecoAsync(IConferenciaEnderecoGeo geo)
    {
        Cpf cpf = PersonasDeTeste.FernandaDrumond;
        FatoColetado item = FatoColetado.Criar(
            "ENDERECO_RESIDENCIAL", 0, "Endereço residencial", TipoRenderizacao.Endereco, Obrigatoriedade.Sempre, null,
            classificacaoProtecao: "PESSOAL").Value!;
        (VersaoConfiguracao versao, GrafoConfiguracao grafo) = MontarVersaoComItem(item);
        Inscricao existente = NovaInscricao(cpf, "sub-1", versao.Id);

        IInscricaoRepository inscricaoRepository = Substitute.For<IInscricaoRepository>();
        inscricaoRepository.ObterPorProcessoECpfAsync(ProcessoId, cpf, Arg.Any<CancellationToken>()).Returns(existente);
        IProcessoSeletivoRepository processoRepository = Substitute.For<IProcessoSeletivoRepository>();
        IRegistroCodecsEnvelope registroCodecs = Substitute.For<IRegistroCodecsEnvelope>();
        DivulgacaoDeTeste.ConfigurarVersao(processoRepository, registroCodecs, versao, grafo);
        ISelecaoUnitOfWork unitOfWork = Substitute.For<ISelecaoUnitOfWork>();

        InscricaoRequest request = new(
            null,
            new Dictionary<string, JsonElement>(StringComparer.Ordinal)
            {
                ["ENDERECO_RESIDENCIAL"] = JsonSerializer.SerializeToElement(new
                {
                    cep = "79096-730",
                    cidade = new { codigoIbge = "5002704", nome = "Campo Grande", uf = "MS" },
                }),
            },
            null, null);

        Result<InscricaoDto> resultado = await SalvarInscricaoCommandHandler.Handle(
            new SalvarInscricaoCommand(ProcessoId, request, PrecondicaoIfMatch.DeTags([existente.ETag])),
            inscricaoRepository, processoRepository, DivulgacaoDeTeste.Certame(ProcessoId), registroCodecs, DivulgacaoDeTeste.Fuso(),
            geo, Substitute.For<IEnderecoNoAcervoPublico>(), unitOfWork, UsuarioComCpf(cpf), DivulgacaoDeTeste.Relogio, CancellationToken.None);
        return (resultado, unitOfWork);
    }

    [Fact(DisplayName = "Resposta que viola uma restrição do formulário é recusada por campo, sem gravar nada — obrigatoriedade sem resposta não é recusada")]
    public async Task Handle_RespostaViolaRestricao_RecusaPorCampoSemGravar()
    {
        Cpf cpf = PersonasDeTeste.KevinCarvalho;

        FatoColetado item = FatoColetado.Criar(
            "APELIDO", 0, "Apelido", TipoRenderizacao.Texto, Obrigatoriedade.Sempre, null,
            classificacaoProtecao: "PESSOAL", formato: "LIVRE", restricoes: [new TamanhoTexto(1, 5)]).Value!;
        (VersaoConfiguracao versao, GrafoConfiguracao grafo) = MontarVersaoComItem(item);
        Inscricao existente = NovaInscricao(cpf, "sub-1", versao.Id);

        IInscricaoRepository inscricaoRepository = Substitute.For<IInscricaoRepository>();
        inscricaoRepository.ObterPorProcessoECpfAsync(ProcessoId, cpf, Arg.Any<CancellationToken>()).Returns(existente);

        IProcessoSeletivoRepository processoRepository = Substitute.For<IProcessoSeletivoRepository>();

        IRegistroCodecsEnvelope registroCodecs = Substitute.For<IRegistroCodecsEnvelope>();
        DivulgacaoDeTeste.ConfigurarVersao(processoRepository, registroCodecs, versao, grafo);

        ISelecaoUnitOfWork unitOfWork = Substitute.For<ISelecaoUnitOfWork>();

        // "Apelido Enorme" tem mais de 5 caracteres — viola o TamanhoTexto(1, 5) da versão fixada.
        InscricaoRequest request = new(
            null, new Dictionary<string, JsonElement>(StringComparer.Ordinal) { ["APELIDO"] = JsonSerializer.SerializeToElement("Apelido Enorme") }, null, null);

        Result<InscricaoDto> resultado = await SalvarInscricaoCommandHandler.Handle(
            new SalvarInscricaoCommand(ProcessoId, request, PrecondicaoIfMatch.DeTags([existente.ETag])),
            inscricaoRepository, processoRepository, DivulgacaoDeTeste.Certame(ProcessoId), registroCodecs, DivulgacaoDeTeste.Fuso(), Substitute.For<IConferenciaEnderecoGeo>(), Substitute.For<IEnderecoNoAcervoPublico>(), unitOfWork, UsuarioComCpf(cpf), DivulgacaoDeTeste.Relogio, CancellationToken.None);

        resultado.IsFailure.Should().BeTrue();
        resultado.Errors.Should().ContainSingle(e => e.Field == "APELIDO" && e.Error.Code == "Inscricao.RespostaInvalida");
        await inscricaoRepository.DidNotReceive().AtualizarAsync(Arg.Any<Inscricao>(), Arg.Any<CancellationToken>());
        await unitOfWork.DidNotReceive().SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Data fora da faixa é recusada no rascunho com os limites do dia do salvar")]
    public async Task Handle_DataForaDaFaixa_RecusaComOsLimites()
    {
        Cpf cpf = PersonasDeTeste.KevinCarvalho;
        FatoColetado item = FatoColetado.Criar(
            "DATA_NASCIMENTO", 0, "Data de nascimento", TipoRenderizacao.Data, Obrigatoriedade.Sempre, null, classificacaoProtecao: "PESSOAL",
            restricoes: [new FaixaDeData(LimiteDeData.NoEnvio(anos: -120, dias: 0), LimiteDeData.NoEnvio(anos: 0, dias: 0))]).Value!;
        (VersaoConfiguracao versao, GrafoConfiguracao grafo) = MontarVersaoComItem(item);
        Inscricao existente = NovaInscricao(cpf, "sub-1", versao.Id);

        IInscricaoRepository inscricaoRepository = Substitute.For<IInscricaoRepository>();
        inscricaoRepository.ObterPorProcessoECpfAsync(ProcessoId, cpf, Arg.Any<CancellationToken>()).Returns(existente);
        IProcessoSeletivoRepository processoRepository = Substitute.For<IProcessoSeletivoRepository>();
        IRegistroCodecsEnvelope registroCodecs = Substitute.For<IRegistroCodecsEnvelope>();
        DivulgacaoDeTeste.ConfigurarVersao(processoRepository, registroCodecs, versao, grafo);
        ISelecaoUnitOfWork unitOfWork = Substitute.For<ISelecaoUnitOfWork>();

        InscricaoRequest request = new(
            null, new Dictionary<string, JsonElement>(StringComparer.Ordinal) { ["DATA_NASCIMENTO"] = JsonSerializer.SerializeToElement("2027-01-10") }, null, null);

        Result<InscricaoDto> resultado = await SalvarInscricaoCommandHandler.Handle(
            new SalvarInscricaoCommand(ProcessoId, request, PrecondicaoIfMatch.DeTags([existente.ETag])),
            inscricaoRepository, processoRepository, DivulgacaoDeTeste.Certame(ProcessoId), registroCodecs, DivulgacaoDeTeste.Fuso(), Substitute.For<IConferenciaEnderecoGeo>(), Substitute.For<IEnderecoNoAcervoPublico>(), unitOfWork, UsuarioComCpf(cpf), DivulgacaoDeTeste.Relogio, CancellationToken.None);

        resultado.Errors.Should().ContainSingle(static e => e.Field == "DATA_NASCIMENTO" && e.Error.Code == InscricaoErrorCodes.DataForaDaFaixa)
            .Which.Error.Message.Should().Be("Informe uma data entre 15/01/1906 e 15/01/2026.");
        await unitOfWork.DidNotReceive().SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Inscrição já enviada recusa o rascunho antes de conferir as respostas, mesmo com resposta inválida")]
    public async Task Handle_InscricaoEnviadaComRespostaInvalida_RecusaPeloEnvio()
    {
        Cpf cpf = PersonasDeTeste.KevinCarvalho;

        FatoColetado item = FatoColetado.Criar(
            "APELIDO", 0, "Apelido", TipoRenderizacao.Texto, Obrigatoriedade.Sempre, null,
            classificacaoProtecao: "PESSOAL", formato: "LIVRE", restricoes: [new TamanhoTexto(1, 5)]).Value!;
        (VersaoConfiguracao versao, GrafoConfiguracao grafo) = MontarVersaoComItem(item);
        Inscricao existente = NovaInscricao(cpf, "sub-1", versao.Id);
        Dictionary<string, JsonElement> semRespostas = new(StringComparer.Ordinal);
        existente.Submeter(
            "000001", null, null, semRespostas, new Dictionary<string, IReadOnlyList<OcorrenciaDeGrupoInscricao>>(StringComparer.Ordinal), [],
            new RespostasAdmitidas(new Dictionary<string, DefinicaoDaResposta>(StringComparer.Ordinal), new HashSet<string>(StringComparer.Ordinal), new Dictionary<string, IReadOnlySet<string>>(StringComparer.Ordinal), new HashSet<Guid>(), new Dictionary<string, TermoAdmitido>(StringComparer.Ordinal)), FormularioDeTeste.AvaliacaoSemOcultos,
            semRespostas, new Dictionary<string, DefinicaoDaResposta>(StringComparer.Ordinal),
            PrecondicaoIfMatch.DeTags([existente.ETag]), DateTimeOffset.UnixEpoch).IsSuccess.Should().BeTrue();

        IInscricaoRepository inscricaoRepository = Substitute.For<IInscricaoRepository>();
        inscricaoRepository.ObterPorProcessoECpfAsync(ProcessoId, cpf, Arg.Any<CancellationToken>()).Returns(existente);

        IProcessoSeletivoRepository processoRepository = Substitute.For<IProcessoSeletivoRepository>();

        IRegistroCodecsEnvelope registroCodecs = Substitute.For<IRegistroCodecsEnvelope>();
        DivulgacaoDeTeste.ConfigurarVersao(processoRepository, registroCodecs, versao, grafo);

        ISelecaoUnitOfWork unitOfWork = Substitute.For<ISelecaoUnitOfWork>();

        InscricaoRequest request = new(
            null, new Dictionary<string, JsonElement>(StringComparer.Ordinal) { ["APELIDO"] = JsonSerializer.SerializeToElement("Apelido Enorme") }, null, null);

        Result<InscricaoDto> resultado = await SalvarInscricaoCommandHandler.Handle(
            new SalvarInscricaoCommand(ProcessoId, request, PrecondicaoIfMatch.DeTags([existente.ETag])),
            inscricaoRepository, processoRepository, DivulgacaoDeTeste.Certame(ProcessoId), registroCodecs, DivulgacaoDeTeste.Fuso(), Substitute.For<IConferenciaEnderecoGeo>(), Substitute.For<IEnderecoNoAcervoPublico>(), unitOfWork, UsuarioComCpf(cpf), DivulgacaoDeTeste.Relogio, CancellationToken.None);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(InscricaoErrorCodes.RascunhoSoAntesDoEnvio);
        await unitOfWork.DidNotReceive().SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Inscrição já enviada, fora do período, recusa pelo período: o novo envio que a outra recusa orienta também seria recusado")]
    public async Task Handle_InscricaoEnviadaForaDoPeriodo_RecusaPeloPeriodo()
    {
        Cpf cpf = PersonasDeTeste.KevinCarvalho;

        FatoColetado item = FatoColetado.Criar(
            "APELIDO", 0, "Apelido", TipoRenderizacao.Texto, Obrigatoriedade.Sempre, null,
            classificacaoProtecao: "PESSOAL", formato: "LIVRE", restricoes: [new TamanhoTexto(1, 5)]).Value!;
        (VersaoConfiguracao versao, GrafoConfiguracao grafo) = MontarVersaoComItem(item);
        Inscricao existente = NovaInscricao(cpf, "sub-1", versao.Id);
        Dictionary<string, JsonElement> semRespostas = new(StringComparer.Ordinal);
        existente.Submeter(
            "000001", null, null, semRespostas, new Dictionary<string, IReadOnlyList<OcorrenciaDeGrupoInscricao>>(StringComparer.Ordinal), [],
            new RespostasAdmitidas(new Dictionary<string, DefinicaoDaResposta>(StringComparer.Ordinal), new HashSet<string>(StringComparer.Ordinal), new Dictionary<string, IReadOnlySet<string>>(StringComparer.Ordinal), new HashSet<Guid>(), new Dictionary<string, TermoAdmitido>(StringComparer.Ordinal)), FormularioDeTeste.AvaliacaoSemOcultos,
            semRespostas, new Dictionary<string, DefinicaoDaResposta>(StringComparer.Ordinal),
            PrecondicaoIfMatch.DeTags([existente.ETag]), DateTimeOffset.UnixEpoch).IsSuccess.Should().BeTrue();

        IInscricaoRepository inscricaoRepository = Substitute.For<IInscricaoRepository>();
        inscricaoRepository.ObterPorProcessoECpfAsync(ProcessoId, cpf, Arg.Any<CancellationToken>()).Returns(existente);

        IProcessoSeletivoRepository processoRepository = Substitute.For<IProcessoSeletivoRepository>();

        IRegistroCodecsEnvelope registroCodecs = Substitute.For<IRegistroCodecsEnvelope>();
        DivulgacaoDeTeste.ConfigurarVersao(processoRepository, registroCodecs, versao, grafo);

        ISelecaoUnitOfWork unitOfWork = Substitute.For<ISelecaoUnitOfWork>();

        InscricaoRequest request = new(
            null, new Dictionary<string, JsonElement>(StringComparer.Ordinal) { ["APELIDO"] = JsonSerializer.SerializeToElement("Apelido Enorme") }, null, null);

        Result<InscricaoDto> resultado = await SalvarInscricaoCommandHandler.Handle(
            new SalvarInscricaoCommand(ProcessoId, request, PrecondicaoIfMatch.DeTags([existente.ETag])),
            inscricaoRepository, processoRepository, DivulgacaoDeTeste.Certame(ProcessoId), registroCodecs, DivulgacaoDeTeste.Fuso(), Substitute.For<IConferenciaEnderecoGeo>(), Substitute.For<IEnderecoNoAcervoPublico>(), unitOfWork, UsuarioComCpf(cpf), new RelogioFixo(DivulgacaoDeTeste.DentroDoPeriodo.AddMonths(1)), CancellationToken.None);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(InscricaoErrorCodes.ForaDoPeriodo);
        await unitOfWork.DidNotReceive().SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Resposta de um membro de grupo repetível que viola uma restrição do formulário também é recusada por campo, sem gravar nada")]
    public async Task Handle_RespostaDeMembroDeGrupoViolaRestricao_RecusaPorCampoSemGravar()
    {
        Cpf cpf = PersonasDeTeste.FernandaDrumond;

        FatoColetado itemDoTopo = FatoColetado.Criar(
            "APELIDO", 0, "Apelido", TipoRenderizacao.Texto, Obrigatoriedade.Sempre, null,
            classificacaoProtecao: "PESSOAL", formato: "LIVRE", restricoes: [new TamanhoTexto(1, 10)]).Value!;
        GrupoColetado composicaoFamiliar = GrupoColetado.Criar(
            "COMPOSICAO_FAMILIAR", 1, etapaCodigo: FormularioDeTeste.Secao, "Composição familiar",
            minimo: 0, maximo: null, exibicao: null, Obrigatoriedade.Sempre,
            [FatoColetado.Criar(
                "RG_NUMERO_MEMBRO", 0, "RG do membro", TipoRenderizacao.Texto, Obrigatoriedade.Sempre, null,
                classificacaoProtecao: "IDENTIFICADOR", formato: "LIVRE", restricoes: [new TamanhoTexto(1, 5)]).Value!],
            FinalidadeFormulario.Inscricao).Value!;
        (VersaoConfiguracao versao, GrafoConfiguracao grafo) = MontarVersaoComItem(itemDoTopo, [composicaoFamiliar]);
        Inscricao existente = NovaInscricao(cpf, "sub-1", versao.Id);

        IInscricaoRepository inscricaoRepository = Substitute.For<IInscricaoRepository>();
        inscricaoRepository.ObterPorProcessoECpfAsync(ProcessoId, cpf, Arg.Any<CancellationToken>()).Returns(existente);

        IProcessoSeletivoRepository processoRepository = Substitute.For<IProcessoSeletivoRepository>();

        IRegistroCodecsEnvelope registroCodecs = Substitute.For<IRegistroCodecsEnvelope>();
        DivulgacaoDeTeste.ConfigurarVersao(processoRepository, registroCodecs, versao, grafo);

        ISelecaoUnitOfWork unitOfWork = Substitute.For<ISelecaoUnitOfWork>();

        // O RG do membro excede o TamanhoTexto(1, 5) do subitem do grupo — a mesma violação do
        // teste anterior, agora dentro de uma ocorrência de COMPOSICAO_FAMILIAR.
        InscricaoRequest request = new(
            null,
            new Dictionary<string, JsonElement>(StringComparer.Ordinal) { ["APELIDO"] = JsonSerializer.SerializeToElement("Nanda") },
            new Dictionary<string, IReadOnlyList<OcorrenciaDeGrupoInscricaoInput>>(StringComparer.Ordinal)
            {
                ["COMPOSICAO_FAMILIAR"] =
                [
                    new OcorrenciaDeGrupoInscricaoInput("membro-1", new Dictionary<string, JsonElement>(StringComparer.Ordinal)
                    {
                        ["RG_NUMERO_MEMBRO"] = JsonSerializer.SerializeToElement("12.345.678-9"),
                    }),
                ],
            },
            null);

        Result<InscricaoDto> resultado = await SalvarInscricaoCommandHandler.Handle(
            new SalvarInscricaoCommand(ProcessoId, request, PrecondicaoIfMatch.DeTags([existente.ETag])),
            inscricaoRepository, processoRepository, DivulgacaoDeTeste.Certame(ProcessoId), registroCodecs, DivulgacaoDeTeste.Fuso(), Substitute.For<IConferenciaEnderecoGeo>(), Substitute.For<IEnderecoNoAcervoPublico>(), unitOfWork, UsuarioComCpf(cpf), DivulgacaoDeTeste.Relogio, CancellationToken.None);

        resultado.IsFailure.Should().BeTrue();
        resultado.Errors.Should().ContainSingle(e =>
            e.Field == "COMPOSICAO_FAMILIAR.membro-1.RG_NUMERO_MEMBRO" && e.Error.Code == "Inscricao.RespostaInvalida");
        await inscricaoRepository.DidNotReceive().AtualizarAsync(Arg.Any<Inscricao>(), Arg.Any<CancellationToken>());
        await unitOfWork.DidNotReceive().SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }
}
