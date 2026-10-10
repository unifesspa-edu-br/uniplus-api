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
using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Application.Commands.Inscricoes;
using Unifesspa.UniPlus.Selecao.Application.DTOs;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
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
        IGeradorDeNumeroInscricao? geradorDeNumeroInscricao = null) =>
        SubmeterInscricaoCommandHandler.Handle(
            command, inscricaoRepository, processoSeletivoRepository, registroCodecs,
            conferenciaEnderecoGeo ?? ConferenciaQueAceitaTudo(),
            geradorDeNumeroInscricao ?? GeradorQueDevolve("00000001"),
            unitOfWork, userContext, TimeProvider.System, CancellationToken.None);

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
    /// As respostas de TODO o conjunto básico do candidato (24 fatos, UNI-REQ-0144): toda inscrição
    /// coleta esses fatos na seção reservada, então o veredito de submissão só passa quando todos
    /// os obrigatórios estão preenchidos — não só o item que o teste declara além do básico.
    /// Escolhas que mantêm os condicionais fora de obrigatoriedade: nacionalidade NATO (sem
    /// documento de estrangeiro), não deseja nome social, tipo de endereço urbano (sem comunidade).
    /// </summary>
    private static Dictionary<string, JsonElement> RespostasDoConjuntoBasico(Cpf cpf) => new(StringComparer.Ordinal)
    {
        ["NOME"] = JsonSerializer.SerializeToElement("Fernanda Drumond"),
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

        IProcessoSeletivoRepository processoRepository = Substitute.For<IProcessoSeletivoRepository>();
        processoRepository.ObterVersoesAsync(ProcessoId, Arg.Any<CancellationToken>()).Returns([]);

        ISelecaoUnitOfWork unitOfWork = Substitute.For<ISelecaoUnitOfWork>();

        Result<ComprovanteInscricaoDto> resultado = await SubmeterAsync(
            new SubmeterInscricaoCommand(ProcessoId, new InscricaoRequest(null, null, null, null), PrecondicaoIfMatch.DeTags([existente.ETag])),
            inscricaoRepository, processoRepository, Substitute.For<IRegistroCodecsEnvelope>(), unitOfWork, UsuarioComCpf(cpf));

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("Snapshot.VigenteAusente");
        await inscricaoRepository.DidNotReceive().AtualizarAsync(Arg.Any<Inscricao>(), Arg.Any<CancellationToken>());
        await unitOfWork.DidNotReceive().SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    /// <summary>Um item obrigatório na seção do formulário, mais um termo obrigatório exigido na mesma finalidade.</summary>
    private static (VersaoConfiguracao Versao, GrafoConfiguracao Grafo, VersaoTermoEscolhida Termo) MontarVersaoComItemObrigatorioETermo()
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
        processoBase.DefinirItens([item], PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();
        processoBase.DefinirTermosDoFormulario(FinalidadeFormulario.Inscricao, [termo], PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue(
            "o termo não cita fato nenhum, então não tem citação pendente de garantir");

        GrafoConfiguracao grafo = new(
            [.. processoBase.Etapas], processoBase.OfertaAtendimento!, [.. processoBase.DistribuicaoVagas],
            processoBase.BonusRegional, [.. processoBase.CriteriosDesempate], processoBase.Classificacao!,
            [.. processoBase.CronogramaFases], [.. processoBase.DocumentosExigidos], [], null,
            fatosColetados: [.. processoBase.FatosColetados],
            formularios: [.. processoBase.Formularios],
            termosExigidos: [.. processoBase.TermosExigidos]);

        // documentosExigidos.dataReferenciaFatos congelada: o conjunto básico traz DATA_NASCIMENTO,
        // que FAIXA_ETARIA apura contra esta data, nunca a de hoje.
        VersaoConfiguracao versao = VersaoConfiguracao.Abrir(
            ProcessoId, Encoding.UTF8.GetBytes("""{"documentosExigidos":{"dataReferenciaFatos":"2026-12-10"}}"""), "0.0.22", "canonical-json/sha256@v1",
            Guid.CreateVersion7(), new string('a', 64), "user-sub-1", new DateTimeOffset(2026, 10, 10, 12, 0, 0, TimeSpan.Zero));

        return (versao, grafo, versaoDoTermo);
    }

    private static void ConfigurarCodecs(IRegistroCodecsEnvelope registroCodecs, VersaoConfiguracao versao, GrafoConfiguracao grafo)
    {
        EnvelopeReidratado envelopeReidratado = new(
            grafo, DadosEdital.Criar("001/2026", new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero), new DateTimeOffset(2026, 1, 31, 0, 0, 0, TimeSpan.Zero), Guid.CreateVersion7()).Value!,
            new string('a', 64), "America/Belem", retificacao: null, conformidade: null);

        registroCodecs.Capacidades.Returns([new CapacidadeCodec("0.0.22", TemEncoder: true, TemDecoder: true, MotivoDaRecusa: null)]);
        registroCodecs.Reidratar(versao).Returns(Result<EnvelopeReidratado>.Success(envelopeReidratado));
    }

    private static InscricaoRequest RequestComApelidoETermoAceito(
        Cpf cpf, VersaoTermoEscolhida termo, GrafoConfiguracao grafo, string apelido = "Nanda")
    {
        Dictionary<string, JsonElement> respostas = RespostasDoConjuntoBasico(cpf);
        respostas["APELIDO"] = JsonSerializer.SerializeToElement(apelido);
        return new(OfertaDo(grafo), respostas, null, [new AceiteDeTermoInput("TERMO_VERACIDADE", termo.VersaoId, termo.Hash)]);
    }

    private static Guid OfertaDo(GrafoConfiguracao grafo) => grafo.DistribuicaoVagas[0].OfertaCursoOrigemId;

    [Fact(DisplayName = "Item obrigatório respondido e termo obrigatório aceito na versão fixada: submete, gera número e grava o comprovante")]
    public async Task Handle_ItemRespondidoETermoAceito_Submete()
    {
        Cpf cpf = PersonasDeTeste.FernandaDrumond;
        (VersaoConfiguracao versao, GrafoConfiguracao grafo, VersaoTermoEscolhida termo) = MontarVersaoComItemObrigatorioETermo();
        Inscricao existente = NovaInscricao(cpf, "sub-1", versao.Id);

        IInscricaoRepository inscricaoRepository = Substitute.For<IInscricaoRepository>();
        inscricaoRepository.ObterPorProcessoECpfAsync(ProcessoId, cpf, Arg.Any<CancellationToken>()).Returns(existente);

        IProcessoSeletivoRepository processoRepository = Substitute.For<IProcessoSeletivoRepository>();
        processoRepository.ObterVersoesAsync(ProcessoId, Arg.Any<CancellationToken>()).Returns([versao]);

        IRegistroCodecsEnvelope registroCodecs = Substitute.For<IRegistroCodecsEnvelope>();
        ConfigurarCodecs(registroCodecs, versao, grafo);

        ISelecaoUnitOfWork unitOfWork = Substitute.For<ISelecaoUnitOfWork>();
        IGeradorDeNumeroInscricao gerador = GeradorQueDevolve("00000042");

        Result<ComprovanteInscricaoDto> resultado = await SubmeterAsync(
            new SubmeterInscricaoCommand(ProcessoId, RequestComApelidoETermoAceito(cpf, termo, grafo), PrecondicaoIfMatch.DeTags([existente.ETag])),
            inscricaoRepository, processoRepository, registroCodecs, unitOfWork, UsuarioComCpf(cpf), geradorDeNumeroInscricao: gerador);

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        resultado.Value!.NumeroInscricao.Should().Be("00000042");
        resultado.Value!.CpfMascarado.Should().Be(cpf.Mascarado);
        resultado.Value!.Respostas.Should().ContainKey("APELIDO").WhoseValue.GetString().Should().Be("Nanda");
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
        processoRepository.ObterVersoesAsync(ProcessoId, Arg.Any<CancellationToken>()).Returns([versao]);

        IRegistroCodecsEnvelope registroCodecs = Substitute.For<IRegistroCodecsEnvelope>();
        ConfigurarCodecs(registroCodecs, versao, grafo);

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
        processoRepository.ObterVersoesAsync(ProcessoId, Arg.Any<CancellationToken>()).Returns([versao]);

        IRegistroCodecsEnvelope registroCodecs = Substitute.For<IRegistroCodecsEnvelope>();
        ConfigurarCodecs(registroCodecs, versao, grafo);

        ISelecaoUnitOfWork unitOfWork = Substitute.For<ISelecaoUnitOfWork>();

        Dictionary<string, JsonElement> respostas = RespostasDoConjuntoBasico(cpf);
        respostas["APELIDO"] = JsonSerializer.SerializeToElement("Nanda");
        InscricaoRequest request = new(null, respostas, null, null);

        Result<ComprovanteInscricaoDto> resultado = await SubmeterAsync(
            new SubmeterInscricaoCommand(ProcessoId, request, PrecondicaoIfMatch.DeTags([existente.ETag])),
            inscricaoRepository, processoRepository, registroCodecs, unitOfWork, UsuarioComCpf(cpf));

        string campoDoTermo = DefinicaoDoProcesso.CodigoDoTermo(FinalidadeFormulario.Inscricao, "TERMO_VERACIDADE");
        resultado.IsFailure.Should().BeTrue();
        resultado.Errors.Should().ContainSingle(e => e.Field == campoDoTermo && e.Error.Code == "Inscricao.TermoNaoAceito");
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
        processoRepository.ObterVersoesAsync(ProcessoId, Arg.Any<CancellationToken>()).Returns([versao]);

        IRegistroCodecsEnvelope registroCodecs = Substitute.For<IRegistroCodecsEnvelope>();
        ConfigurarCodecs(registroCodecs, versao, grafo);

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
            Unifesspa.UniPlus.Selecao.Application.Services.ContextoDaVersaoFixadaResolver.Admitidas(grafo), PrecondicaoIfMatch.DeTags([existente.ETag]));
        salvo.IsSuccess.Should().BeTrue(salvo.Error?.Message);

        IInscricaoRepository inscricaoRepository = Substitute.For<IInscricaoRepository>();
        inscricaoRepository.ObterPorProcessoECpfAsync(ProcessoId, cpf, Arg.Any<CancellationToken>()).Returns(existente);

        IProcessoSeletivoRepository processoRepository = Substitute.For<IProcessoSeletivoRepository>();
        processoRepository.ObterVersoesAsync(ProcessoId, Arg.Any<CancellationToken>()).Returns([versao]);

        IRegistroCodecsEnvelope registroCodecs = Substitute.For<IRegistroCodecsEnvelope>();
        ConfigurarCodecs(registroCodecs, versao, grafo);

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

    [Fact(DisplayName = "DbUpdateConcurrencyException (xmin) no commit descarta o rastreamento e devolve conflito, sem deixar a exceção propagar")]
    public async Task Handle_ConflitoDeConcorrenciaNoCommit_DescartaEDevolveConflito()
    {
        Cpf cpf = PersonasDeTeste.FernandaDrumond;
        (VersaoConfiguracao versao, GrafoConfiguracao grafo, VersaoTermoEscolhida termo) = MontarVersaoComItemObrigatorioETermo();
        Inscricao existente = NovaInscricao(cpf, "sub-1", versao.Id);

        IInscricaoRepository inscricaoRepository = Substitute.For<IInscricaoRepository>();
        inscricaoRepository.ObterPorProcessoECpfAsync(ProcessoId, cpf, Arg.Any<CancellationToken>()).Returns(existente);

        IProcessoSeletivoRepository processoRepository = Substitute.For<IProcessoSeletivoRepository>();
        processoRepository.ObterVersoesAsync(ProcessoId, Arg.Any<CancellationToken>()).Returns([versao]);

        IRegistroCodecsEnvelope registroCodecs = Substitute.For<IRegistroCodecsEnvelope>();
        ConfigurarCodecs(registroCodecs, versao, grafo);

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
}
