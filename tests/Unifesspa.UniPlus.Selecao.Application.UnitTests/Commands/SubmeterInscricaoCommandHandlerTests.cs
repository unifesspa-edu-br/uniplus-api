namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands;

using System.Text;
using System.Text.Json;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;

using NSubstitute;
using NSubstitute.ExceptionExtensions;

using Unifesspa.UniPlus.Application.Abstractions.Authentication;
using Unifesspa.UniPlus.Kernel.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Regras.ValueObjects;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Application.Commands.Inscricoes;
using Unifesspa.UniPlus.Selecao.Application.DTOs;
using Unifesspa.UniPlus.Selecao.Application.UnitTests.TestSupport;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Errors;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;
using Unifesspa.UniPlus.Selecao.Domain.Services;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

/// <summary>
/// Submeter a inscrição (issue #1859): a validação completa — obrigatórios, restrições,
/// impedimentos, grupos e termos — decide o veredito antes de qualquer gravação. Diferente de
/// <c>SalvarInscricaoCommandHandler</c>, avalia o estado FINAL (o já gravado mesclado com o corpo
/// recebido), porque a submissão exige obrigatórios do formulário inteiro (UNI-REQ-0084).
/// </summary>
public sealed class SubmeterInscricaoCommandHandlerTests
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

    private static Task<Result<ComprovanteInscricaoDto>> SubmeterAsync(
        SubmeterInscricaoCommand command,
        IInscricaoRepository inscricaoRepository,
        IProcessoSeletivoRepository processoSeletivoRepository,
        IRegistroCodecsEnvelope registroCodecs,
        ISelecaoUnitOfWork unitOfWork,
        IUserContext userContext,
        IConferenciaEnderecoGeo? conferenciaEnderecoGeo = null,
        IGeradorDeNumeroInscricao? geradorDeNumeroInscricao = null,
        TimeProvider? relogio = null) =>
        SubmeterInscricaoCommandHandler.Handle(
            command, inscricaoRepository, processoSeletivoRepository, DivulgacaoDeTeste.Certame(ProcessoId), registroCodecs,
            DivulgacaoDeTeste.Fuso(),
            conferenciaEnderecoGeo ?? ConferenciaQueAceitaTudo(),
            geradorDeNumeroInscricao ?? GeradorQueDevolve("00000001"),
            unitOfWork, userContext, relogio ?? DivulgacaoDeTeste.Relogio, CancellationToken.None);

    private static IGeradorDeNumeroInscricao GeradorQueDevolve(string numero)
    {
        IGeradorDeNumeroInscricao gerador = Substitute.For<IGeradorDeNumeroInscricao>();
        gerador.ProximoAsync(Arg.Any<CancellationToken>()).Returns(numero);
        return gerador;
    }

    private static IConferenciaEnderecoGeo ConferenciaQueAceitaTudo()
    {
        IConferenciaEnderecoGeo conferencia = Substitute.For<IConferenciaEnderecoGeo>();
        conferencia.ConferirCepAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(Result.Success());
        conferencia.ConferirMunicipioAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(Result.Success());
        return conferencia;
    }

    /// <summary>
    /// As respostas de TODO o conjunto básico do candidato (25 fatos, UNI-REQ-0144): toda inscrição
    /// coleta esses fatos na seção reservada, então o veredito de submissão só passa quando todos
    /// os obrigatórios estão preenchidos — não só o item que o teste declara além do básico.
    /// Escolhas que mantêm os condicionais fora de obrigatoriedade: nacionalidade NATO (sem
    /// documento de estrangeiro), órgão emissor da lista (sem o nome do outro órgão), não deseja nome
    /// social, tipo de endereço urbano (sem comunidade).
    /// </summary>
    private static Dictionary<string, JsonElement> RespostasDoConjuntoBasico(Cpf cpf) => new(StringComparer.Ordinal)
    {
        ["NOME"] = JsonSerializer.SerializeToElement(PersonasDeTeste.NomeFernandaDrumond),
        ["DESEJA_NOME_SOCIAL"] = JsonSerializer.SerializeToElement(false),
        ["NACIONALIDADE"] = JsonSerializer.SerializeToElement("NATO"),
        ["CPF"] = JsonSerializer.SerializeToElement(cpf.Valor),
        ["RG_NUMERO"] = JsonSerializer.SerializeToElement("1234567"),
        ["RG_ORGAO_EMISSOR"] = JsonSerializer.SerializeToElement("SSP"),
        ["RG_UF"] = JsonSerializer.SerializeToElement("PA"),
        ["RG_DATA_EMISSAO"] = JsonSerializer.SerializeToElement("2010-01-01"),
        ["DATA_NASCIMENTO"] = JsonSerializer.SerializeToElement("2000-01-01"),
        ["NATURALIDADE_UF"] = JsonSerializer.SerializeToElement("PA"),
        ["NATURALIDADE_MUNICIPIO"] = JsonSerializer.SerializeToElement("1501402"),
        ["NOME_MAE"] = JsonSerializer.SerializeToElement("Maria Drumond"),
        ["ESTADO_CIVIL"] = JsonSerializer.SerializeToElement("SOLTEIRO"),
        ["SEXO"] = JsonSerializer.SerializeToElement("FEMININO"),
        ["SEXO_CIVIL"] = JsonSerializer.SerializeToElement("FEMININO"),
        ["IDENTIDADE_GENERO"] = JsonSerializer.SerializeToElement("PREFIRO_NAO_INFORMAR"),
        ["ORIENTACAO_SEXUAL"] = JsonSerializer.SerializeToElement("PREFIRO_NAO_INFORMAR"),
        ["COR_RACA"] = JsonSerializer.SerializeToElement("PARDA"),
        ["EMAIL"] = JsonSerializer.SerializeToElement("fernanda.drumond@example.com"),
        ["TELEFONE"] = JsonSerializer.SerializeToElement("91999999999"),
        ["TIPO_ENDERECO"] = JsonSerializer.SerializeToElement("URBANO"),
        ["ENDERECO_RESIDENCIAL"] = JsonSerializer.SerializeToElement(new
        {
            cep = (string?)null,
            cidade = new { codigoIbge = "1501402", uf = "PA" },
        }),
    };

    [Fact(DisplayName = "Sem o claim cpf no token, recusa sem consultar repositório nenhum")]
    public async Task Handle_SemCpfNoToken_Recusa()
    {
        IUserContext userContext = Substitute.For<IUserContext>();
        userContext.Cpf.Returns((string?)null);

        IInscricaoRepository inscricaoRepository = Substitute.For<IInscricaoRepository>();

        Result<ComprovanteInscricaoDto> resultado = await SubmeterAsync(
            new SubmeterInscricaoCommand(ProcessoId, new InscricaoRequest(null, null, null, null), PrecondicaoIfMatch.DeTags(["\"x\""])),
            inscricaoRepository, Substitute.For<IProcessoSeletivoRepository>(), Substitute.For<IRegistroCodecsEnvelope>(),
            Substitute.For<ISelecaoUnitOfWork>(), userContext);

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

        Result<ComprovanteInscricaoDto> resultado = await SubmeterAsync(
            new SubmeterInscricaoCommand(ProcessoId, new InscricaoRequest(null, null, null, null), PrecondicaoIfMatch.DeTags(["\"x\""])),
            inscricaoRepository, Substitute.For<IProcessoSeletivoRepository>(), Substitute.For<IRegistroCodecsEnvelope>(),
            Substitute.For<ISelecaoUnitOfWork>(), UsuarioComCpf(cpf));

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

        Result<ComprovanteInscricaoDto> resultado = await SubmeterAsync(
            new SubmeterInscricaoCommand(ProcessoId, new InscricaoRequest(null, null, null, null), PrecondicaoIfMatch.DeTags([existente.ETag])),
            inscricaoRepository, Substitute.For<IProcessoSeletivoRepository>(), Substitute.For<IRegistroCodecsEnvelope>(),
            unitOfWork, UsuarioComCpf(cpf, sub: "sub-outro"));

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
        (VersaoConfiguracao divulgada, GrafoConfiguracao grafo, _) = MontarVersaoComItemObrigatorioETermo();
        DivulgacaoDeTeste.ConfigurarDivulgada(processoRepository, registroCodecs, divulgada, grafo);
        processoRepository.ObterVersoesAsync(ProcessoId, Arg.Any<CancellationToken>()).Returns([]);

        ISelecaoUnitOfWork unitOfWork = Substitute.For<ISelecaoUnitOfWork>();

        Result<ComprovanteInscricaoDto> resultado = await SubmeterAsync(
            new SubmeterInscricaoCommand(ProcessoId, new InscricaoRequest(null, null, null, null), PrecondicaoIfMatch.DeTags([existente.ETag])),
            inscricaoRepository, processoRepository, registroCodecs, unitOfWork, UsuarioComCpf(cpf));

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("Snapshot.VigenteAusente");
        await inscricaoRepository.DidNotReceive().AtualizarAsync(Arg.Any<Inscricao>(), Arg.Any<CancellationToken>());
        await unitOfWork.DidNotReceive().SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Fora do período da inscrição, enviar é recusado sem gerar número nem comprovante")]
    public async Task Handle_ForaDoPeriodo_RecusaSemComprovante()
    {
        Cpf cpf = PersonasDeTeste.FernandaDrumond;
        (VersaoConfiguracao versao, GrafoConfiguracao grafo, _) = MontarVersaoComItemObrigatorioETermo();
        Inscricao existente = NovaInscricao(cpf, "sub-1", versao.Id);

        IInscricaoRepository inscricaoRepository = Substitute.For<IInscricaoRepository>();
        inscricaoRepository.ObterPorProcessoECpfAsync(ProcessoId, cpf, Arg.Any<CancellationToken>()).Returns(existente);
        IProcessoSeletivoRepository processoRepository = Substitute.For<IProcessoSeletivoRepository>();
        IRegistroCodecsEnvelope registroCodecs = Substitute.For<IRegistroCodecsEnvelope>();
        DivulgacaoDeTeste.ConfigurarVersao(processoRepository, registroCodecs, versao, grafo);
        IGeradorDeNumeroInscricao gerador = GeradorQueDevolve("00000001");

        Result<ComprovanteInscricaoDto> resultado = await SubmeterAsync(
            new SubmeterInscricaoCommand(ProcessoId, new InscricaoRequest(null, null, null, null), PrecondicaoIfMatch.DeTags([existente.ETag])),
            inscricaoRepository, processoRepository, registroCodecs, Substitute.For<ISelecaoUnitOfWork>(), UsuarioComCpf(cpf),
            geradorDeNumeroInscricao: gerador, relogio: new RelogioFixo(DivulgacaoDeTeste.DentroDoPeriodo.AddMonths(1)));

        resultado.Error!.Code.Should().Be(InscricaoErrorCodes.ForaDoPeriodo);
        await gerador.DidNotReceive().ProximoAsync(Arg.Any<CancellationToken>());
        await inscricaoRepository.DidNotReceive().AdicionarComprovanteAsync(Arg.Any<ComprovanteInscricao>(), Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "A retificação que prorroga o período vale para quem já iniciou: envia depois do fim da versão fixada")]
    public async Task Handle_PeriodoProrrogadoPelaRetificacao_Submete()
    {
        Cpf cpf = PersonasDeTeste.FernandaDrumond;
        (VersaoConfiguracao fixada, GrafoConfiguracao grafo, VersaoTermoEscolhida termo) = MontarVersaoComItemObrigatorioETermo();
        VersaoConfiguracao divulgada = VersaoConfiguracao.Abrir(
            ProcessoId, Encoding.UTF8.GetBytes("""{"documentosExigidos":{"dataReferenciaFatos":"2026-12-10"}}"""), "0.0.22", "canonical-json/sha256@v1",
            Guid.CreateVersion7(), new string('c', 64), "user-sub-1", new DateTimeOffset(2026, 10, 11, 12, 0, 0, TimeSpan.Zero));
        Inscricao existente = NovaInscricao(cpf, "sub-1", fixada.Id);

        IInscricaoRepository inscricaoRepository = Substitute.For<IInscricaoRepository>();
        inscricaoRepository.ObterPorProcessoECpfAsync(ProcessoId, cpf, Arg.Any<CancellationToken>()).Returns(existente);
        IProcessoSeletivoRepository processoRepository = Substitute.For<IProcessoSeletivoRepository>();
        IRegistroCodecsEnvelope registroCodecs = Substitute.For<IRegistroCodecsEnvelope>();
        DivulgacaoDeTeste.ConfigurarFixadaEDivulgada(
            processoRepository, registroCodecs, grafo,
            fixada: (fixada, DivulgacaoDeTeste.FimDoPeriodo),
            divulgada: (divulgada, DivulgacaoDeTeste.FimDoPeriodo.AddDays(30)));

        Result<ComprovanteInscricaoDto> resultado = await SubmeterAsync(
            new SubmeterInscricaoCommand(ProcessoId, RequestComApelidoETermoAceito(cpf, termo, grafo), PrecondicaoIfMatch.DeTags([existente.ETag])),
            inscricaoRepository, processoRepository, registroCodecs, Substitute.For<ISelecaoUnitOfWork>(), UsuarioComCpf(cpf),
            geradorDeNumeroInscricao: GeradorQueDevolve("00000007"), relogio: new RelogioFixo(DivulgacaoDeTeste.FimDoPeriodo.AddDays(10)));

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
    }

    /// <summary>
    /// Um item obrigatório na seção do formulário, mais um termo obrigatório exigido na mesma
    /// finalidade; <paramref name="outrosItens"/> e <paramref name="grupos"/> entram na mesma seção, e
    /// <paramref name="ajustar"/> muda o processo antes de a versão o congelar.
    /// </summary>
    private static (VersaoConfiguracao Versao, GrafoConfiguracao Grafo, VersaoTermoEscolhida Termo) MontarVersaoComItemObrigatorioETermo(
        IReadOnlyList<FatoColetado>? outrosItens = null, IReadOnlyList<GrupoColetado>? grupos = null, Action<ProcessoSeletivo>? ajustar = null)
    {
        FatoColetado item = FatoColetado.Criar(
            "APELIDO", 0, "Apelido", TipoRenderizacao.Texto, Obrigatoriedade.Sempre, null,
            classificacaoProtecao: "PESSOAL", formato: "LIVRE", restricoes: []).Value!;

        VersaoTermoEscolhida versaoDoTermo = new(
            Guid.CreateVersion7(), Guid.CreateVersion7(), "Termo de veracidade", "Declaro que as informações são verdadeiras.",
            "Lei 9.784/1999", "ACEITE_EXPLICITO", new string('b', 64));
        TermoExigidoFormulario termo = TermoExigidoFormulario.Criar(
            "TERMO_VERACIDADE", 0, versaoDoTermo, exibicao: null, Obrigatoriedade.Sempre, FinalidadeFormulario.Inscricao).Value!;

        ProcessoSeletivo processoBase = ProcessoSeletivoConformeBuilder.Criar("PS 2026 — Submeter inscrição");
        Result itensDefinidos = processoBase.DefinirItens([item, .. outrosItens ?? []], PrecondicaoIfMatch.Ausente, grupos: grupos);
        itensDefinidos.IsSuccess.Should().BeTrue(itensDefinidos.Error?.Message);
        processoBase.DefinirTermosDoFormulario(FinalidadeFormulario.Inscricao, [termo], PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue(
            "o termo não cita fato nenhum, então não tem citação pendente de garantir");
        ajustar?.Invoke(processoBase);

        GrafoConfiguracao grafo = new(
            [.. processoBase.Etapas], processoBase.OfertaAtendimento!, [.. processoBase.DistribuicaoVagas],
            processoBase.BonusRegional, [.. processoBase.CriteriosDesempate], processoBase.Classificacao!,
            [.. processoBase.CronogramaFases], [.. processoBase.DocumentosExigidos], [], null,
            fatosColetados: [.. processoBase.FatosColetados],
            formularios: [.. processoBase.Formularios],
            termosExigidos: [.. processoBase.TermosExigidos],
            gruposColetados: [.. processoBase.GruposColetados]);

        // documentosExigidos.dataReferenciaFatos congelada: o conjunto básico traz DATA_NASCIMENTO,
        // que FAIXA_ETARIA apura contra esta data, nunca a de hoje.
        VersaoConfiguracao versao = VersaoConfiguracao.Abrir(
            ProcessoId, Encoding.UTF8.GetBytes("""{"documentosExigidos":{"dataReferenciaFatos":"2026-12-10"}}"""), "0.0.22", "canonical-json/sha256@v1",
            Guid.CreateVersion7(), new string('a', 64), "user-sub-1", new DateTimeOffset(2026, 10, 10, 12, 0, 0, TimeSpan.Zero));

        return (versao, grafo, versaoDoTermo);
    }

    private static InscricaoRequest RequestComApelidoETermoAceito(
        Cpf cpf, VersaoTermoEscolhida termo, GrafoConfiguracao grafo, string apelido = "Nanda")
    {
        Dictionary<string, JsonElement> respostas = RespostasDoConjuntoBasico(cpf);
        respostas["APELIDO"] = JsonSerializer.SerializeToElement(apelido);
        return new(OfertaDo(grafo), respostas, null, [new AceiteDeTermoInput("TERMO_VERACIDADE", termo.VersaoId, termo.Hash)]);
    }

    private static Guid OfertaDo(GrafoConfiguracao grafo) => grafo.DistribuicaoVagas[0].OfertaCursoOrigemId;

    [Fact(DisplayName = "O envio com a oferta A concorre só às modalidades da oferta A, mesmo com o rascunho gravado na oferta B")]
    public async Task Handle_OfertaDoCorpo_ConcorreSoAsModalidadesDaOfertaEnviada()
    {
        Cpf cpf = PersonasDeTeste.FernandaDrumond;
        (VersaoConfiguracao versao, GrafoConfiguracao grafoBase, VersaoTermoEscolhida termo) = MontarVersaoComItemObrigatorioETermo();
        Guid ofertaA = Guid.CreateVersion7();
        Guid ofertaB = Guid.CreateVersion7();
        GrafoConfiguracao grafo = OfertasDeTeste.ComDistribuicao(
            grafoBase, OfertasDeTeste.Distribuicao(ofertaA, "AC"), OfertasDeTeste.Distribuicao(ofertaB, "AC", "AC_FEM"));
        Inscricao existente = NovaInscricao(cpf, "sub-1", versao.Id);
        existente.Salvar(
            ofertaB, new Dictionary<string, JsonElement>(), new Dictionary<string, IReadOnlyList<OcorrenciaDeGrupoInscricao>>(), [],
            Unifesspa.UniPlus.Selecao.Application.Services.ContextoDaVersaoFixadaResolver.Admitidas(grafo) with { NomeCivilDoLogin = PersonasDeTeste.NomeFernandaDrumond }, PrecondicaoIfMatch.DeTags([existente.ETag])).IsSuccess.Should().BeTrue();

        Result<ComprovanteInscricaoDto> daOfertaA = await SubmeterNaOfertaAsync(cpf, versao, grafo, termo, existente, ofertaA);

        daOfertaA.IsSuccess.Should().BeTrue(daOfertaA.Error?.Message);
        daOfertaA.Value!.OfertaCursoId.Should().Be(ofertaA);
        daOfertaA.Value!.Modalidades!.Select(static m => m.Codigo).Should().Equal(
            ["AC"], "a modalidade que só a oferta B tem não vale para quem enviou na oferta A");
    }

    [Fact(DisplayName = "O envio com a oferta B concorre também à modalidade que só a oferta B tem")]
    public async Task Handle_OfertaDoCorpo_ConcorreAsModalidadesDaPropriaOferta()
    {
        Cpf cpf = PersonasDeTeste.FernandaDrumond;
        (VersaoConfiguracao versao, GrafoConfiguracao grafoBase, VersaoTermoEscolhida termo) = MontarVersaoComItemObrigatorioETermo();
        Guid ofertaA = Guid.CreateVersion7();
        Guid ofertaB = Guid.CreateVersion7();
        GrafoConfiguracao grafo = OfertasDeTeste.ComDistribuicao(
            grafoBase, OfertasDeTeste.Distribuicao(ofertaA, "AC"), OfertasDeTeste.Distribuicao(ofertaB, "AC", "AC_FEM"));

        Result<ComprovanteInscricaoDto> daOfertaB = await SubmeterNaOfertaAsync(cpf, versao, grafo, termo, NovaInscricao(cpf, "sub-1", versao.Id), ofertaB);

        daOfertaB.IsSuccess.Should().BeTrue(daOfertaB.Error?.Message);
        daOfertaB.Value!.Modalidades!.Select(static m => m.Codigo).Should().Equal("AC", "AC_FEM");
    }

    private static async Task<Result<ComprovanteInscricaoDto>> SubmeterNaOfertaAsync(
        Cpf cpf, VersaoConfiguracao versao, GrafoConfiguracao grafo, VersaoTermoEscolhida termo, Inscricao inscricao, Guid ofertaEnviada)
    {
        IInscricaoRepository inscricaoRepository = Substitute.For<IInscricaoRepository>();
        inscricaoRepository.ObterPorProcessoECpfAsync(ProcessoId, cpf, Arg.Any<CancellationToken>()).Returns(inscricao);
        IProcessoSeletivoRepository processoRepository = Substitute.For<IProcessoSeletivoRepository>();
        IRegistroCodecsEnvelope registroCodecs = Substitute.For<IRegistroCodecsEnvelope>();
        DivulgacaoDeTeste.ConfigurarVersao(processoRepository, registroCodecs, versao, grafo);

        // A candidata responde o que faz valer a modalidade de ação afirmativa da oferta B.
        InscricaoRequest request = RequestComApelidoETermoAceito(cpf, termo, grafo) with { OfertaCursoId = ofertaEnviada };
        return await SubmeterAsync(
            new SubmeterInscricaoCommand(ProcessoId, request, PrecondicaoIfMatch.DeTags([inscricao.ETag])),
            inscricaoRepository, processoRepository, registroCodecs, Substitute.For<ISelecaoUnitOfWork>(), UsuarioComCpf(cpf));
    }

    [Fact(DisplayName = "Item obrigatório respondido e termo obrigatório aceito na versão fixada: submete, gera número e grava o comprovante, com 'Prefiro não informar' em identidade de gênero e orientação sexual")]
    public async Task Handle_ItemRespondidoETermoAceito_Submete()
    {
        Cpf cpf = PersonasDeTeste.FernandaDrumond;
        (VersaoConfiguracao versao, GrafoConfiguracao grafo, VersaoTermoEscolhida termo) = MontarVersaoComItemObrigatorioETermo();
        Inscricao existente = NovaInscricao(cpf, "sub-1", versao.Id);

        IInscricaoRepository inscricaoRepository = Substitute.For<IInscricaoRepository>();
        inscricaoRepository.ObterPorProcessoECpfAsync(ProcessoId, cpf, Arg.Any<CancellationToken>()).Returns(existente);

        IProcessoSeletivoRepository processoRepository = Substitute.For<IProcessoSeletivoRepository>();

        IRegistroCodecsEnvelope registroCodecs = Substitute.For<IRegistroCodecsEnvelope>();
        DivulgacaoDeTeste.ConfigurarVersao(processoRepository, registroCodecs, versao, grafo);

        ISelecaoUnitOfWork unitOfWork = Substitute.For<ISelecaoUnitOfWork>();
        IGeradorDeNumeroInscricao gerador = GeradorQueDevolve("00000042");

        Result<ComprovanteInscricaoDto> resultado = await SubmeterAsync(
            new SubmeterInscricaoCommand(ProcessoId, RequestComApelidoETermoAceito(cpf, termo, grafo), PrecondicaoIfMatch.DeTags([existente.ETag])),
            inscricaoRepository, processoRepository, registroCodecs, unitOfWork, UsuarioComCpf(cpf), geradorDeNumeroInscricao: gerador);

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        resultado.Value!.NumeroInscricao.Should().Be("00000042");
        resultado.Value!.CpfMascarado.Should().Be(cpf.Mascarado);
        resultado.Value!.Respostas.Should().ContainKey("APELIDO").WhoseValue.GetString().Should().Be("Nanda");
        resultado.Value!.Respostas["IDENTIDADE_GENERO"].GetString().Should().Be("PREFIRO_NAO_INFORMAR", "'Prefiro não informar' é resposta válida e volta ao titular no comprovante");
        resultado.Value!.Respostas["ORIENTACAO_SEXUAL"].GetString().Should().Be("PREFIRO_NAO_INFORMAR");
        HashCanonicalComputer.IsValidHashShape(resultado.Value!.HashIntegridade).Should().BeTrue();
        await inscricaoRepository.Received(1).AtualizarAsync(existente, Arg.Any<CancellationToken>());
        await inscricaoRepository.Received(1).AdicionarComprovanteAsync(Arg.Any<ComprovanteInscricao>(), Arg.Any<CancellationToken>());
        await unitOfWork.Received(1).SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Item obrigatório sem resposta recusa por ausência, sem gerar número nem gravar nada")]
    public async Task Handle_ItemObrigatorioSemResposta_RecusaSemGerarNumero()
    {
        Cpf cpf = PersonasDeTeste.KevinCarvalho;
        (VersaoConfiguracao versao, GrafoConfiguracao grafo, VersaoTermoEscolhida termo) = MontarVersaoComItemObrigatorioETermo();
        Inscricao existente = NovaInscricao(cpf, "sub-1", versao.Id);

        IInscricaoRepository inscricaoRepository = Substitute.For<IInscricaoRepository>();
        inscricaoRepository.ObterPorProcessoECpfAsync(ProcessoId, cpf, Arg.Any<CancellationToken>()).Returns(existente);

        IProcessoSeletivoRepository processoRepository = Substitute.For<IProcessoSeletivoRepository>();

        IRegistroCodecsEnvelope registroCodecs = Substitute.For<IRegistroCodecsEnvelope>();
        DivulgacaoDeTeste.ConfigurarVersao(processoRepository, registroCodecs, versao, grafo);

        ISelecaoUnitOfWork unitOfWork = Substitute.For<ISelecaoUnitOfWork>();
        IGeradorDeNumeroInscricao gerador = Substitute.For<IGeradorDeNumeroInscricao>();

        // Todo o conjunto básico está respondido, exceto APELIDO — nem agora, nem numa chamada
        // anterior de Salvar.
        Dictionary<string, JsonElement> respostas = RespostasDoConjuntoBasico(cpf);
        InscricaoRequest request = new(
            null, respostas, null, [new AceiteDeTermoInput("TERMO_VERACIDADE", termo.VersaoId, termo.Hash)]);

        Result<ComprovanteInscricaoDto> resultado = await SubmeterAsync(
            new SubmeterInscricaoCommand(ProcessoId, request, PrecondicaoIfMatch.DeTags([existente.ETag])),
            inscricaoRepository, processoRepository, registroCodecs, unitOfWork, UsuarioComCpf(cpf), geradorDeNumeroInscricao: gerador);

        resultado.IsFailure.Should().BeTrue();
        resultado.Errors.Should().ContainSingle(e => e.Field == "APELIDO" && e.Error.Code == "Inscricao.ItemObrigatorioAusente");
        await gerador.DidNotReceive().ProximoAsync(Arg.Any<CancellationToken>());
        await inscricaoRepository.DidNotReceive().AtualizarAsync(Arg.Any<Inscricao>(), Arg.Any<CancellationToken>());
        await unitOfWork.DidNotReceive().SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Termo obrigatório não aceito recusa a submissão, mesmo com o item obrigatório respondido")]
    public async Task Handle_TermoObrigatorioNaoAceito_Recusa()
    {
        Cpf cpf = PersonasDeTeste.FernandaDrumond;
        (VersaoConfiguracao versao, GrafoConfiguracao grafo, _) = MontarVersaoComItemObrigatorioETermo();
        Inscricao existente = NovaInscricao(cpf, "sub-1", versao.Id);

        IInscricaoRepository inscricaoRepository = Substitute.For<IInscricaoRepository>();
        inscricaoRepository.ObterPorProcessoECpfAsync(ProcessoId, cpf, Arg.Any<CancellationToken>()).Returns(existente);

        IProcessoSeletivoRepository processoRepository = Substitute.For<IProcessoSeletivoRepository>();

        IRegistroCodecsEnvelope registroCodecs = Substitute.For<IRegistroCodecsEnvelope>();
        DivulgacaoDeTeste.ConfigurarVersao(processoRepository, registroCodecs, versao, grafo);

        ISelecaoUnitOfWork unitOfWork = Substitute.For<ISelecaoUnitOfWork>();

        Dictionary<string, JsonElement> respostas = RespostasDoConjuntoBasico(cpf);
        respostas["APELIDO"] = JsonSerializer.SerializeToElement("Nanda");
        InscricaoRequest request = new(null, respostas, null, null);

        Result<ComprovanteInscricaoDto> resultado = await SubmeterAsync(
            new SubmeterInscricaoCommand(ProcessoId, request, PrecondicaoIfMatch.DeTags([existente.ETag])),
            inscricaoRepository, processoRepository, registroCodecs, unitOfWork, UsuarioComCpf(cpf));

        resultado.IsFailure.Should().BeTrue();
        resultado.Errors.Should().ContainSingle(e => e.Field == "TERMO_VERACIDADE" && e.Error.Code == "Inscricao.TermoNaoAceito",
            "a recusa sai no código do aceite que o candidato envia");
        await unitOfWork.DidNotReceive().SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Theory(DisplayName = "Estrangeiro que informa só um dos documentos, passaporte ou RNM, é recusado pela falta do outro")]
    [InlineData("PASSAPORTE_NUMERO", "RNM_NUMERO")]
    [InlineData("RNM_NUMERO", "PASSAPORTE_NUMERO")]
    public async Task Handle_EstrangeiroComUmSoDosDocumentos_Recusa(string informado, string faltante)
    {
        Cpf cpf = PersonasDeTeste.KevinCarvalho;
        (VersaoConfiguracao versao, GrafoConfiguracao grafo, VersaoTermoEscolhida termo) = MontarVersaoComItemObrigatorioETermo();
        Inscricao existente = NovaInscricao(cpf, "sub-1", versao.Id);

        IInscricaoRepository inscricaoRepository = Substitute.For<IInscricaoRepository>();
        inscricaoRepository.ObterPorProcessoECpfAsync(ProcessoId, cpf, Arg.Any<CancellationToken>()).Returns(existente);

        IProcessoSeletivoRepository processoRepository = Substitute.For<IProcessoSeletivoRepository>();

        IRegistroCodecsEnvelope registroCodecs = Substitute.For<IRegistroCodecsEnvelope>();
        DivulgacaoDeTeste.ConfigurarVersao(processoRepository, registroCodecs, versao, grafo);

        ISelecaoUnitOfWork unitOfWork = Substitute.For<ISelecaoUnitOfWork>();

        // O estrangeiro não responde o RG nem a naturalidade, que ficam ocultos para ele.
        Dictionary<string, JsonElement> respostas = RespostasDoConjuntoBasico(cpf);
        foreach (string doBrasileiro in new[] { "RG_NUMERO", "RG_ORGAO_EMISSOR", "RG_UF", "RG_DATA_EMISSAO", "NATURALIDADE_UF", "NATURALIDADE_MUNICIPIO" })
        {
            respostas.Remove(doBrasileiro);
        }

        respostas["NACIONALIDADE"] = JsonSerializer.SerializeToElement("ESTRANGEIRO");
        respostas[informado] = JsonSerializer.SerializeToElement("FX1234567");
        respostas["APELIDO"] = JsonSerializer.SerializeToElement("Kevin");
        InscricaoRequest request = new(
            null, respostas, null, [new AceiteDeTermoInput("TERMO_VERACIDADE", termo.VersaoId, termo.Hash)]);

        Result<ComprovanteInscricaoDto> resultado = await SubmeterAsync(
            new SubmeterInscricaoCommand(ProcessoId, request, PrecondicaoIfMatch.DeTags([existente.ETag])),
            inscricaoRepository, processoRepository, registroCodecs, unitOfWork, UsuarioComCpf(cpf));

        resultado.IsFailure.Should().BeTrue();
        resultado.Errors.Should().ContainSingle().Which.Should().Match<FieldError>(
            e => e.Field == faltante && e.Error.Code == "Inscricao.ItemObrigatorioAusente");
        await unitOfWork.DidNotReceive().SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Item respondido numa chamada anterior de Salvar e não reenviado na submissão continua preenchido, porque o veredito avalia o estado mesclado")]
    public async Task Handle_ItemRespondidoAntesNaoReenviado_PermaneceGravado()
    {
        Cpf cpf = PersonasDeTeste.KevinCarvalho;
        (VersaoConfiguracao versao, GrafoConfiguracao grafo, VersaoTermoEscolhida termo) = MontarVersaoComItemObrigatorioETermo();
        Inscricao existente = NovaInscricao(cpf, "sub-1", versao.Id);
        Dictionary<string, JsonElement> respostasDoRascunho = RespostasDoConjuntoBasico(cpf);
        respostasDoRascunho["APELIDO"] = JsonSerializer.SerializeToElement("Nanda");
        Result salvo = existente.Salvar(
            null, respostasDoRascunho,
            new Dictionary<string, IReadOnlyList<OcorrenciaDeGrupoInscricao>>(StringComparer.Ordinal), [],
            Unifesspa.UniPlus.Selecao.Application.Services.ContextoDaVersaoFixadaResolver.Admitidas(grafo) with { NomeCivilDoLogin = PersonasDeTeste.NomeFernandaDrumond }, PrecondicaoIfMatch.DeTags([existente.ETag]));
        salvo.IsSuccess.Should().BeTrue(salvo.Error?.Message);

        IInscricaoRepository inscricaoRepository = Substitute.For<IInscricaoRepository>();
        inscricaoRepository.ObterPorProcessoECpfAsync(ProcessoId, cpf, Arg.Any<CancellationToken>()).Returns(existente);

        IProcessoSeletivoRepository processoRepository = Substitute.For<IProcessoSeletivoRepository>();

        IRegistroCodecsEnvelope registroCodecs = Substitute.For<IRegistroCodecsEnvelope>();
        DivulgacaoDeTeste.ConfigurarVersao(processoRepository, registroCodecs, versao, grafo);

        ISelecaoUnitOfWork unitOfWork = Substitute.For<ISelecaoUnitOfWork>();

        // Só o aceite do termo vem nesta chamada — APELIDO já estava gravado pela chamada de Salvar.
        InscricaoRequest request = new(
            OfertaDo(grafo), new Dictionary<string, JsonElement>(StringComparer.Ordinal), null,
            [new AceiteDeTermoInput("TERMO_VERACIDADE", termo.VersaoId, termo.Hash)]);

        Result<ComprovanteInscricaoDto> resultado = await SubmeterAsync(
            new SubmeterInscricaoCommand(ProcessoId, request, PrecondicaoIfMatch.DeTags([existente.ETag])),
            inscricaoRepository, processoRepository, registroCodecs, unitOfWork, UsuarioComCpf(cpf));

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        resultado.Value!.Respostas.Should().ContainKey("APELIDO").WhoseValue.GetString().Should().Be("Nanda");
    }

    /// <summary>Um booleano que, respondido "não", esconde o que depende dele.</summary>
    private static FatoColetado Pergunta(string fato, int ordem) => FatoColetado.Criar(
        fato, ordem, fato, TipoRenderizacao.Booleano, Obrigatoriedade.Sempre, null, classificacaoProtecao: "SENSIVEL").Value!;

    /// <summary>Um texto opcional que só aparece com <paramref name="pergunta"/> respondida "sim".</summary>
    private static FatoColetado SoComSim(string fato, int ordem, string pergunta) => FatoColetado.Criar(
        fato, ordem, fato, TipoRenderizacao.Texto, Obrigatoriedade.Nunca,
        [CondicaoPrecondicaoFato.Criar(0, pergunta, Operador.Igual, JsonSerializer.SerializeToElement(true)).Value!],
        classificacaoProtecao: "SENSIVEL", formato: "LIVRE").Value!;

    private static async Task<(Result<ComprovanteInscricaoDto> Resultado, Inscricao Inscricao)> SubmeterComAsync(
        IReadOnlyList<FatoColetado> itens,
        IReadOnlyList<GrupoColetado>? grupos,
        IReadOnlyDictionary<string, JsonElement> respostas,
        IReadOnlyDictionary<string, IReadOnlyList<OcorrenciaDeGrupoInscricaoInput>>? respostasDosGrupos = null,
        Action<ProcessoSeletivo>? ajustar = null)
    {
        Cpf cpf = PersonasDeTeste.FernandaDrumond;
        (VersaoConfiguracao versao, GrafoConfiguracao grafo, VersaoTermoEscolhida termo) = MontarVersaoComItemObrigatorioETermo(itens, grupos, ajustar);
        Inscricao existente = NovaInscricao(cpf, "sub-1", versao.Id);

        IInscricaoRepository inscricaoRepository = Substitute.For<IInscricaoRepository>();
        inscricaoRepository.ObterPorProcessoECpfAsync(ProcessoId, cpf, Arg.Any<CancellationToken>()).Returns(existente);
        IProcessoSeletivoRepository processoRepository = Substitute.For<IProcessoSeletivoRepository>();
        IRegistroCodecsEnvelope registroCodecs = Substitute.For<IRegistroCodecsEnvelope>();
        DivulgacaoDeTeste.ConfigurarVersao(processoRepository, registroCodecs, versao, grafo);

        InscricaoRequest comApelido = RequestComApelidoETermoAceito(cpf, termo, grafo);
        Dictionary<string, JsonElement> todas = new(comApelido.Respostas!, StringComparer.Ordinal);
        foreach ((string fato, JsonElement valor) in respostas)
        {
            todas[fato] = valor;
        }

        Result<ComprovanteInscricaoDto> resultado = await SubmeterAsync(
            new SubmeterInscricaoCommand(ProcessoId, comApelido with { Respostas = todas, Grupos = respostasDosGrupos }, PrecondicaoIfMatch.DeTags([existente.ETag])),
            inscricaoRepository, processoRepository, registroCodecs, Substitute.For<ISelecaoUnitOfWork>(), UsuarioComCpf(cpf));
        return (resultado, existente);
    }

    /// <summary>A ampla concorrência só para quem quer concorrer às vagas de pessoa com deficiência: a modalidade decide pela resposta.</summary>
    private static void AmplaSoComDeficiencia(ProcessoSeletivo processo) =>
        processo.DefinirDistribuicaoVagas(
            [ProcessoSeletivoConformeBuilder.DistribuicaoAmplaConcorrencia(PredicadoDnf.CriarDeCondicoesAgrupadas(
                [(0, CondicaoDnf.Criar("CONCORRER_PCD", Operador.Igual, JsonSerializer.SerializeToElement(true)).Value!)]).Value!)],
            FatosDeModalidadeDeTeste.DoCatalogo, PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

    [Fact(DisplayName = "Sem nenhuma modalidade aplicável, o envio é recusado no campo de que ela depende, sem gravar nada")]
    public async Task Handle_NenhumaModalidade_RecusaNoCampoQueADecide()
    {
        (Result<ComprovanteInscricaoDto> resultado, Inscricao inscricao) = await SubmeterComAsync(
            [Pergunta("CONCORRER_PCD", 1)], grupos: null,
            new Dictionary<string, JsonElement>(StringComparer.Ordinal) { ["CONCORRER_PCD"] = JsonSerializer.SerializeToElement(false) },
            ajustar: AmplaSoComDeficiencia);

        resultado.Errors.Should().ContainSingle().Which.Field.Should().Be("CONCORRER_PCD");
        resultado.Errors[0].Error.Code.Should().Be(InscricaoErrorCodes.NenhumaModalidade);
        inscricao.Situacao.Should().Be(Domain.Enums.SituacaoInscricao.Rascunho);
        inscricao.Comprovantes.Should().BeEmpty();
    }

    [Fact(DisplayName = "Com a modalidade aplicável, o envio a grava como derivada e o comprovante a traz com o nome congelado")]
    public async Task Handle_ComModalidade_GravaEComprova()
    {
        (Result<ComprovanteInscricaoDto> resultado, Inscricao inscricao) = await SubmeterComAsync(
            [Pergunta("CONCORRER_PCD", 1)], grupos: null,
            new Dictionary<string, JsonElement>(StringComparer.Ordinal) { ["CONCORRER_PCD"] = JsonSerializer.SerializeToElement(true) },
            ajustar: AmplaSoComDeficiencia);

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        resultado.Value!.Modalidades.Should().Equal(new ModalidadeDaInscricaoDto("AC", "AC", 0));
        inscricao.Derivados["MODALIDADE"].EnumerateArray().Select(static v => v.GetString()).Should().Equal("AC");
        inscricao.Comprovantes.Should().ContainSingle().Which.IntegridadeConfere().Should().BeTrue();
    }

    [Fact(DisplayName = "A modalidade enviada pelo candidato é recusada e nunca gravada")]
    public async Task Handle_ModalidadeEnviadaPeloCandidato_Recusa()
    {
        (Result<ComprovanteInscricaoDto> resultado, Inscricao inscricao) = await SubmeterComAsync(
            [Pergunta("CONCORRER_PCD", 1)], grupos: null,
            new Dictionary<string, JsonElement>(StringComparer.Ordinal)
            {
                ["CONCORRER_PCD"] = JsonSerializer.SerializeToElement(true),
                ["MODALIDADE"] = JsonSerializer.SerializeToElement(new[] { "AC" }),
            },
            ajustar: AmplaSoComDeficiencia);

        resultado.Errors.Should().Contain(e => e.Field == "MODALIDADE" && e.Error.Code == InscricaoErrorCodes.FatoForaDoFormulario);
        inscricao.Derivados.Should().NotContainKey("MODALIDADE");
        inscricao.Respostas.Should().NotContainKey("MODALIDADE");
    }

    [Fact(DisplayName = "O item e o termo obrigatórios de outra finalidade, como a isenção, não recusam o envio da inscrição")]
    public async Task Handle_ObrigatoriosDaIsencao_NaoRecusamOEnvio()
    {
        VersaoTermoEscolhida versaoDoTermo = new(
            Guid.CreateVersion7(), Guid.CreateVersion7(), "Termo da isenção", "Declaro a renda informada.",
            "Lei 12.799/2013", "ACEITE_EXPLICITO", new string('d', 64));

        (Result<ComprovanteInscricaoDto> resultado, _) = await SubmeterComAsync(
            [], grupos: null, new Dictionary<string, JsonElement>(StringComparer.Ordinal),
            ajustar: processo =>
            {
                FormularioDeTeste.DefinirItens(
                    processo,
                    [FatoColetado.Criar(
                        "RENDA_DECLARADA", 0, "Renda declarada", TipoRenderizacao.Texto, Obrigatoriedade.Sempre, null,
                        classificacaoProtecao: "PESSOAL", formato: "LIVRE", finalidade: FinalidadeFormulario.IsencaoTaxa).Value!],
                    finalidade: FinalidadeFormulario.IsencaoTaxa).IsSuccess.Should().BeTrue();
                FormularioDeTeste.DefinirTermos(
                    processo,
                    [TermoExigidoFormulario.Criar("TERMO_ISENCAO", 0, versaoDoTermo, exibicao: null, Obrigatoriedade.Sempre, FinalidadeFormulario.IsencaoTaxa).Value!],
                    finalidade: FinalidadeFormulario.IsencaoTaxa).IsSuccess.Should().BeTrue();
            });

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
    }

    [Fact(DisplayName = "A resposta ao item que outra resposta esconde é descartada no envio: não fica gravada nem sai no comprovante, e o envio passa")]
    public async Task Handle_RespostaDeItemOculto_EDescartadaSemRecusar()
    {
        (Result<ComprovanteInscricaoDto> resultado, Inscricao inscricao) = await SubmeterComAsync(
            [Pergunta("PRECISA_ATENDIMENTO", 1), SoComSim("CONDICAO_DESCRITA", 2, "PRECISA_ATENDIMENTO")],
            grupos: null,
            new Dictionary<string, JsonElement>(StringComparer.Ordinal)
            {
                ["PRECISA_ATENDIMENTO"] = JsonSerializer.SerializeToElement(false),
                ["CONDICAO_DESCRITA"] = JsonSerializer.SerializeToElement("Lactante"),
            });

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        resultado.Value!.Respostas.Should().ContainKey("PRECISA_ATENDIMENTO").And.NotContainKey("CONDICAO_DESCRITA");
        inscricao.Respostas.Should().NotContainKey("CONDICAO_DESCRITA", "a inscrição enviada guarda só o que se aplica");
    }

    [Fact(DisplayName = "O grupo que outra resposta esconde é descartado inteiro no envio")]
    public async Task Handle_GrupoOculto_EDescartadoInteiro()
    {
        GrupoColetado dependentes = GrupoColetado.Criar(
            "DEPENDENTES", 2, FormularioDeTeste.Secao, "Dependentes", minimo: 0, maximo: null,
            PredicadoDnf.CriarDeCondicoesAgrupadas([(0, CondicaoDnf.Criar("TEM_DEPENDENTES", Operador.Igual, JsonSerializer.SerializeToElement(true)).Value!)]).Value!,
            Obrigatoriedade.Nunca,
            [FatoColetado.Criar("NOME_DEPENDENTE", 0, "Nome", TipoRenderizacao.Texto, Obrigatoriedade.Sempre, null, classificacaoProtecao: "PESSOAL", formato: "LIVRE").Value!],
            FinalidadeFormulario.Inscricao).Value!;

        (Result<ComprovanteInscricaoDto> resultado, Inscricao inscricao) = await SubmeterComAsync(
            [Pergunta("TEM_DEPENDENTES", 1)],
            [dependentes],
            new Dictionary<string, JsonElement>(StringComparer.Ordinal) { ["TEM_DEPENDENTES"] = JsonSerializer.SerializeToElement(false) },
            new Dictionary<string, IReadOnlyList<OcorrenciaDeGrupoInscricaoInput>>(StringComparer.Ordinal)
            {
                ["DEPENDENTES"] = [new("dependente-1", new Dictionary<string, JsonElement>(StringComparer.Ordinal) { ["NOME_DEPENDENTE"] = JsonSerializer.SerializeToElement("Ana Drumond") })],
            });

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        resultado.Value!.Grupos.Should().NotContainKey("DEPENDENTES");
        inscricao.Grupos.Should().NotContainKey("DEPENDENTES");
    }

    [Fact(DisplayName = "No grupo que aparece, a resposta ao subitem que a ocorrência esconde é descartada, e a dos demais subitens fica")]
    public async Task Handle_SubitemOcultoNaOcorrencia_EDescartado()
    {
        GrupoColetado composicao = GrupoColetado.Criar(
            "COMPOSICAO_FAMILIAR", 1, FormularioDeTeste.Secao, "Composição familiar", minimo: 0, maximo: null, exibicao: null,
            Obrigatoriedade.Nunca,
            [Pergunta("MEMBRO_TEM_RENDA", 0), SoComSim("RENDA_DO_MEMBRO", 1, "MEMBRO_TEM_RENDA")],
            FinalidadeFormulario.Inscricao).Value!;

        (Result<ComprovanteInscricaoDto> resultado, Inscricao inscricao) = await SubmeterComAsync(
            [],
            [composicao],
            new Dictionary<string, JsonElement>(StringComparer.Ordinal),
            new Dictionary<string, IReadOnlyList<OcorrenciaDeGrupoInscricaoInput>>(StringComparer.Ordinal)
            {
                ["COMPOSICAO_FAMILIAR"] = [new("membro-1", new Dictionary<string, JsonElement>(StringComparer.Ordinal)
                {
                    ["MEMBRO_TEM_RENDA"] = JsonSerializer.SerializeToElement(false),
                    ["RENDA_DO_MEMBRO"] = JsonSerializer.SerializeToElement("1500"),
                })],
            });

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        resultado.Value!.Grupos["COMPOSICAO_FAMILIAR"].Single().Respostas.Should().ContainKey("MEMBRO_TEM_RENDA").And.NotContainKey("RENDA_DO_MEMBRO");
        inscricao.Grupos["COMPOSICAO_FAMILIAR"].Single().Respostas.Should().NotContainKey("RENDA_DO_MEMBRO");
    }

    [Fact(DisplayName = "DbUpdateConcurrencyException (xmin) no commit descarta o rastreamento e devolve conflito, sem deixar a exceção propagar")]
    public async Task Handle_ConflitoDeConcorrenciaNoCommit_DescartaEDevolveConflito()
    {
        Cpf cpf = PersonasDeTeste.FernandaDrumond;
        (VersaoConfiguracao versao, GrafoConfiguracao grafo, VersaoTermoEscolhida termo) = MontarVersaoComItemObrigatorioETermo();
        Inscricao existente = NovaInscricao(cpf, "sub-1", versao.Id);

        IInscricaoRepository inscricaoRepository = Substitute.For<IInscricaoRepository>();
        inscricaoRepository.ObterPorProcessoECpfAsync(ProcessoId, cpf, Arg.Any<CancellationToken>()).Returns(existente);

        IProcessoSeletivoRepository processoRepository = Substitute.For<IProcessoSeletivoRepository>();

        IRegistroCodecsEnvelope registroCodecs = Substitute.For<IRegistroCodecsEnvelope>();
        DivulgacaoDeTeste.ConfigurarVersao(processoRepository, registroCodecs, versao, grafo);

        ISelecaoUnitOfWork unitOfWork = Substitute.For<ISelecaoUnitOfWork>();
        unitOfWork.SalvarAlteracoesAsync(Arg.Any<CancellationToken>())
            .ThrowsAsync(new DbUpdateConcurrencyException("conflito sintético de teste"));

        Result<ComprovanteInscricaoDto> resultado = await SubmeterAsync(
            new SubmeterInscricaoCommand(ProcessoId, RequestComApelidoETermoAceito(cpf, termo, grafo), PrecondicaoIfMatch.DeTags([existente.ETag])),
            inscricaoRepository, processoRepository, registroCodecs, unitOfWork, UsuarioComCpf(cpf));

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("Inscricao.ConflitoDeConcorrencia");
        unitOfWork.Received(1).DescartarAlteracoesNaoSalvas();
    }

    private static readonly FatoColetado DataAteOEnvio = FatoColetado.Criar(
        "DATA_DO_EVENTO", 1, "Data do evento", TipoRenderizacao.Data, Obrigatoriedade.Sempre, null, classificacaoProtecao: "PESSOAL",
        restricoes: [new FaixaDeData(minima: null, LimiteDeData.NoEnvio(anos: 0, dias: 0))]).Value!;

    private static InscricaoRequest RequestComDataDoEvento(Cpf cpf, VersaoTermoEscolhida termo, GrafoConfiguracao grafo, string dataDoEvento)
    {
        InscricaoRequest request = RequestComApelidoETermoAceito(cpf, termo, grafo);
        Dictionary<string, JsonElement> respostas = new(request.Respostas!, StringComparer.Ordinal)
        {
            ["DATA_DO_EVENTO"] = JsonSerializer.SerializeToElement(dataDoEvento),
        };
        return request with { Respostas = respostas };
    }

    [Fact(DisplayName = "O relógio que vira o dia durante o envio não muda o dia de que a faixa de data se conta")]
    public async Task Handle_ViradaDoDiaDuranteOEnvio_AvaliaNoDiaDoInstanteUnico()
    {
        Cpf cpf = PersonasDeTeste.FernandaDrumond;
        (VersaoConfiguracao versao, GrafoConfiguracao grafo, VersaoTermoEscolhida termo) = MontarVersaoComItemObrigatorioETermo([DataAteOEnvio]);
        Inscricao existente = NovaInscricao(cpf, "sub-1", versao.Id);

        IInscricaoRepository inscricaoRepository = Substitute.For<IInscricaoRepository>();
        inscricaoRepository.ObterPorProcessoECpfAsync(ProcessoId, cpf, Arg.Any<CancellationToken>()).Returns(existente);
        IProcessoSeletivoRepository processoRepository = Substitute.For<IProcessoSeletivoRepository>();
        IRegistroCodecsEnvelope registroCodecs = Substitute.For<IRegistroCodecsEnvelope>();
        DivulgacaoDeTeste.ConfigurarVersao(processoRepository, registroCodecs, versao, grafo);
        ISelecaoUnitOfWork unitOfWork = Substitute.For<ISelecaoUnitOfWork>();

        // 23:59:59,9 de 15/01 em Belém; cada leitura seguinte do relógio já cai em 16/01.
        Result<ComprovanteInscricaoDto> resultado = await SubmeterAsync(
            new SubmeterInscricaoCommand(ProcessoId, RequestComDataDoEvento(cpf, termo, grafo, "2026-01-16"), PrecondicaoIfMatch.DeTags([existente.ETag])),
            inscricaoRepository, processoRepository, registroCodecs, unitOfWork, UsuarioComCpf(cpf),
            relogio: new RelogioQueAvanca(new DateTimeOffset(2026, 1, 16, 2, 59, 59, 900, TimeSpan.Zero), TimeSpan.FromMilliseconds(200)));

        resultado.Errors.Should().ContainSingle(static e => e.Field == "DATA_DO_EVENTO" && e.Error.Code == InscricaoErrorCodes.DataForaDaFaixa)
            .Which.Error.Message.Should().Be("Informe uma data até 15/01/2026.");
        await unitOfWork.DidNotReceive().SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "O comprovante leva o mesmo instante com que o envio foi avaliado")]
    public async Task Handle_Envio_ComprovanteLevaOInstanteDaAvaliacao()
    {
        Cpf cpf = PersonasDeTeste.FernandaDrumond;
        (VersaoConfiguracao versao, GrafoConfiguracao grafo, VersaoTermoEscolhida termo) = MontarVersaoComItemObrigatorioETermo([DataAteOEnvio]);
        Inscricao existente = NovaInscricao(cpf, "sub-1", versao.Id);

        IInscricaoRepository inscricaoRepository = Substitute.For<IInscricaoRepository>();
        inscricaoRepository.ObterPorProcessoECpfAsync(ProcessoId, cpf, Arg.Any<CancellationToken>()).Returns(existente);
        IProcessoSeletivoRepository processoRepository = Substitute.For<IProcessoSeletivoRepository>();
        IRegistroCodecsEnvelope registroCodecs = Substitute.For<IRegistroCodecsEnvelope>();
        DivulgacaoDeTeste.ConfigurarVersao(processoRepository, registroCodecs, versao, grafo);

        Result<ComprovanteInscricaoDto> resultado = await SubmeterAsync(
            new SubmeterInscricaoCommand(ProcessoId, RequestComDataDoEvento(cpf, termo, grafo, "2026-01-15"), PrecondicaoIfMatch.DeTags([existente.ETag])),
            inscricaoRepository, processoRepository, registroCodecs, Substitute.For<ISelecaoUnitOfWork>(), UsuarioComCpf(cpf),
            relogio: new RelogioQueAvanca(new DateTimeOffset(2026, 1, 16, 2, 59, 59, 900, TimeSpan.Zero), TimeSpan.FromMilliseconds(200)));

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        resultado.Value!.Instante.Should().Be(new DateTimeOffset(2026, 1, 16, 2, 59, 59, 900, TimeSpan.Zero));
    }

    [Fact(DisplayName = "A correção da inscrição enviada conta a faixa de data do instante da correção")]
    public async Task Handle_Correcao_ContaAFaixaDoNovoEnvio()
    {
        Cpf cpf = PersonasDeTeste.FernandaDrumond;
        (VersaoConfiguracao versao, GrafoConfiguracao grafo, VersaoTermoEscolhida termo) = MontarVersaoComItemObrigatorioETermo([DataAteOEnvio]);
        Inscricao existente = NovaInscricao(cpf, "sub-1", versao.Id);

        IInscricaoRepository inscricaoRepository = Substitute.For<IInscricaoRepository>();
        inscricaoRepository.ObterPorProcessoECpfAsync(ProcessoId, cpf, Arg.Any<CancellationToken>()).Returns(existente);
        IProcessoSeletivoRepository processoRepository = Substitute.For<IProcessoSeletivoRepository>();
        IRegistroCodecsEnvelope registroCodecs = Substitute.For<IRegistroCodecsEnvelope>();
        DivulgacaoDeTeste.ConfigurarVersao(processoRepository, registroCodecs, versao, grafo);

        Result<ComprovanteInscricaoDto> envio = await SubmeterAsync(
            new SubmeterInscricaoCommand(ProcessoId, RequestComDataDoEvento(cpf, termo, grafo, "2026-01-15"), PrecondicaoIfMatch.DeTags([existente.ETag])),
            inscricaoRepository, processoRepository, registroCodecs, Substitute.For<ISelecaoUnitOfWork>(), UsuarioComCpf(cpf),
            relogio: new RelogioFixo(new DateTimeOffset(2026, 1, 15, 12, 0, 0, TimeSpan.Zero)));
        envio.IsSuccess.Should().BeTrue(envio.Error?.Message);

        Result<ComprovanteInscricaoDto> correcao = await SubmeterAsync(
            new SubmeterInscricaoCommand(ProcessoId, RequestComDataDoEvento(cpf, termo, grafo, "2026-01-16"), PrecondicaoIfMatch.DeTags([existente.ETag])),
            inscricaoRepository, processoRepository, registroCodecs, Substitute.For<ISelecaoUnitOfWork>(), UsuarioComCpf(cpf),
            relogio: new RelogioFixo(new DateTimeOffset(2026, 1, 16, 12, 0, 0, TimeSpan.Zero)));

        correcao.IsSuccess.Should().BeTrue(correcao.Error?.Message);
    }

    /// <summary>Um relógio cuja leitura seguinte é sempre mais tarde que a anterior, pelo passo.</summary>
    private sealed class RelogioQueAvanca(DateTimeOffset inicio, TimeSpan passo) : TimeProvider
    {
        private int _leituras;

        public override DateTimeOffset GetUtcNow() => inicio + (passo * _leituras++);
    }
}
