namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands;

using System.Text;
using System.Text.Json;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;

using NSubstitute;
using NSubstitute.ExceptionExtensions;

using Unifesspa.UniPlus.Application.Abstractions.Authentication;
using Unifesspa.UniPlus.Application.Abstractions.Interfaces;
using Unifesspa.UniPlus.Kernel.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Regras.ValueObjects;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Application.Commands.Inscricoes;
using Unifesspa.UniPlus.Selecao.Application.DTOs;
using Unifesspa.UniPlus.Selecao.Application.DTOs.Comprovantes;
using Unifesspa.UniPlus.Selecao.Application.UnitTests.TestSupport;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Errors;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;
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

    private static IUserContext UsuarioComCpf(Cpf cpf, string sub = "sub-1", string? nomeSocial = null)
    {
        IUserContext contexto = Substitute.For<IUserContext>();
        contexto.Cpf.Returns(cpf.Valor);
        contexto.NomeCompleto.Returns(PersonasDeTeste.NomeFernandaDrumond);
        contexto.NomeSocial.Returns(nomeSocial);
        contexto.UserId.Returns(sub);
        contexto.Issuer.Returns("https://issuer");
        return contexto;
    }

    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<Inscricao, ComprovanteInscricao> Emitidos = [];

    /// <summary>O comprovante que o envio gravou para a inscrição: o recibo não o repete, quem o lê é a consulta.</summary>
    private static ComprovanteInscricao Emitido(Inscricao inscricao) =>
        Emitidos.TryGetValue(inscricao, out ComprovanteInscricao? comprovante) ? comprovante : throw new InvalidOperationException("O envio não gravou comprovante.");

    private static void Guardar(Inscricao inscricao, IInscricaoRepository repositorio)
    {
        ComprovanteInscricao? gravado = repositorio.ReceivedCalls()
            .Where(static c => c.GetMethodInfo().Name == nameof(IInscricaoRepository.AdicionarComprovanteAsync))
            .Select(static c => (ComprovanteInscricao)c.GetArguments()[0]!)
            .LastOrDefault();
        if (gravado is not null)
        {
            Emitidos.AddOrUpdate(inscricao, gravado);
        }
    }

    private static Task<Result<ReciboDaInscricaoDto>> SubmeterAsync(
        SubmeterInscricaoCommand command,
        IInscricaoRepository inscricaoRepository,
        IProcessoSeletivoRepository processoSeletivoRepository,
        IRegistroCodecsEnvelope registroCodecs,
        IUnitOfWork unitOfWork,
        IUserContext userContext,
        IConferenciaEnderecoGeo? conferenciaEnderecoGeo = null,
        IGeradorDeNumeroInscricao? geradorDeNumeroInscricao = null,
        TimeProvider? relogio = null,
        IDocumentoDaInscricaoRepository? documentoRepository = null) =>
        SubmeterInscricaoCommandHandler.Handle(
            command, inscricaoRepository, processoSeletivoRepository, DivulgacaoDeTeste.Certame(ProcessoId), documentoRepository ?? Substitute.For<IDocumentoDaInscricaoRepository>(), registroCodecs,
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
        conferencia.ConferirCepAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(Result<string>.Success("Marabá"));
        conferencia.ConferirMunicipioAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(Result<string>.Success("Marabá"));
        conferencia.ResolverCepAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Result<CepResolvido>.Success(new CepResolvido("1501402", "Marabá", "PA", "logradouro")));
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
            cep = "66010000",
            cidade = new { codigoIbge = "1501402", uf = "PA" },
        }),
    };

    [Fact(DisplayName = "Sem o claim cpf no token, recusa sem consultar repositório nenhum")]
    public async Task Handle_SemCpfNoToken_Recusa()
    {
        IUserContext userContext = Substitute.For<IUserContext>();
        userContext.Cpf.Returns((string?)null);

        IInscricaoRepository inscricaoRepository = Substitute.For<IInscricaoRepository>();

        Result<ReciboDaInscricaoDto> resultado = await SubmeterAsync(
            new SubmeterInscricaoCommand(ProcessoId, new InscricaoRequest(null, null, null, null), PrecondicaoIfMatch.DeTags(["\"x\""])),
            inscricaoRepository, Substitute.For<IProcessoSeletivoRepository>(), Substitute.For<IRegistroCodecsEnvelope>(),
            Substitute.For<IUnitOfWork>(), userContext);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("Inscricao.CpfAusenteNoToken");
        await inscricaoRepository.DidNotReceive().ObterAtivaPorProcessoECpfAsync(Arg.Any<Guid>(), Arg.Any<Cpf>(), Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Sem inscrição para o CPF e o processo, recusa como não encontrada")]
    public async Task Handle_SemInscricao_RecusaComoNaoEncontrada()
    {
        Cpf cpf = PersonasDeTeste.FernandaDrumond;
        IInscricaoRepository inscricaoRepository = Substitute.For<IInscricaoRepository>();
        inscricaoRepository.ObterAtivaPorProcessoECpfAsync(ProcessoId, cpf, Arg.Any<CancellationToken>()).Returns((Inscricao?)null);

        Result<ReciboDaInscricaoDto> resultado = await SubmeterAsync(
            new SubmeterInscricaoCommand(ProcessoId, new InscricaoRequest(null, null, null, null), PrecondicaoIfMatch.DeTags(["\"x\""])),
            inscricaoRepository, Substitute.For<IProcessoSeletivoRepository>(), Substitute.For<IRegistroCodecsEnvelope>(),
            Substitute.For<IUnitOfWork>(), UsuarioComCpf(cpf));

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("Inscricao.NaoEncontrada");
    }

    [Fact(DisplayName = "Sub diferente do gravado para o mesmo CPF é recusado como dono divergente, sem tentar gravar")]
    public async Task Handle_SubDivergente_RecusaComoDonoDivergente()
    {
        Cpf cpf = PersonasDeTeste.KevinCarvalho;
        Inscricao existente = NovaInscricao(cpf, "sub-original", Guid.CreateVersion7());

        IInscricaoRepository inscricaoRepository = Substitute.For<IInscricaoRepository>();
        inscricaoRepository.ObterAtivaPorProcessoECpfAsync(ProcessoId, cpf, Arg.Any<CancellationToken>()).Returns(existente);
        IUnitOfWork unitOfWork = Substitute.For<IUnitOfWork>();

        Result<ReciboDaInscricaoDto> resultado = await SubmeterAsync(
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
        inscricaoRepository.ObterAtivaPorProcessoECpfAsync(ProcessoId, cpf, Arg.Any<CancellationToken>()).Returns(existente);

        // A versão divulgada existe e o período está aberto; só a versão fixada pela inscrição sumiu.
        IProcessoSeletivoRepository processoRepository = Substitute.For<IProcessoSeletivoRepository>();
        IRegistroCodecsEnvelope registroCodecs = Substitute.For<IRegistroCodecsEnvelope>();
        (VersaoConfiguracao divulgada, GrafoConfiguracao grafo, _) = MontarVersaoComItemObrigatorioETermo();
        DivulgacaoDeTeste.ConfigurarDivulgada(processoRepository, registroCodecs, divulgada, grafo);
        processoRepository.ObterVersoesAsync(ProcessoId, Arg.Any<CancellationToken>()).Returns([]);

        IUnitOfWork unitOfWork = Substitute.For<IUnitOfWork>();

        Result<ReciboDaInscricaoDto> resultado = await SubmeterAsync(
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
        inscricaoRepository.ObterAtivaPorProcessoECpfAsync(ProcessoId, cpf, Arg.Any<CancellationToken>()).Returns(existente);
        IProcessoSeletivoRepository processoRepository = Substitute.For<IProcessoSeletivoRepository>();
        IRegistroCodecsEnvelope registroCodecs = Substitute.For<IRegistroCodecsEnvelope>();
        DivulgacaoDeTeste.ConfigurarVersao(processoRepository, registroCodecs, versao, grafo);
        IGeradorDeNumeroInscricao gerador = GeradorQueDevolve("00000001");

        Result<ReciboDaInscricaoDto> resultado = await SubmeterAsync(
            new SubmeterInscricaoCommand(ProcessoId, new InscricaoRequest(null, null, null, null), PrecondicaoIfMatch.DeTags([existente.ETag])),
            inscricaoRepository, processoRepository, registroCodecs, Substitute.For<IUnitOfWork>(), UsuarioComCpf(cpf),
            geradorDeNumeroInscricao: gerador, relogio: new RelogioFixo(DivulgacaoDeTeste.DentroDoPeriodo.AddMonths(1)));

        resultado.Error!.Code.Should().Be(InscricaoErrorCodes.ForaDoPeriodo);
        await gerador.DidNotReceive().ProximoAsync(Arg.Any<CancellationToken>());
        await inscricaoRepository.DidNotReceive().AdicionarComprovanteAsync(Arg.Any<ComprovanteInscricao>(), Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "O rascunho adota a versão divulgada que prorroga o período: envia depois do fim da versão em que ele nasceu")]
    public async Task Handle_PeriodoProrrogadoPelaRetificacao_Submete()
    {
        Cpf cpf = PersonasDeTeste.FernandaDrumond;
        (VersaoConfiguracao fixada, GrafoConfiguracao grafo, VersaoTermoEscolhida termo) = MontarVersaoComItemObrigatorioETermo();
        VersaoConfiguracao divulgada = VersaoConfiguracao.Abrir(
            ProcessoId, Encoding.UTF8.GetBytes("""{"documentosExigidos":{"dataReferenciaFatos":"2026-12-10"}}"""), "0.0.22", "canonical-json/sha256@v1",
            Guid.CreateVersion7(), new string('c', 64), "user-sub-1", new DateTimeOffset(2026, 10, 11, 12, 0, 0, TimeSpan.Zero));
        Inscricao existente = NovaInscricao(cpf, "sub-1", fixada.Id);

        IInscricaoRepository inscricaoRepository = Substitute.For<IInscricaoRepository>();
        inscricaoRepository.ObterAtivaPorProcessoECpfAsync(ProcessoId, cpf, Arg.Any<CancellationToken>()).Returns(existente);
        IProcessoSeletivoRepository processoRepository = Substitute.For<IProcessoSeletivoRepository>();
        IRegistroCodecsEnvelope registroCodecs = Substitute.For<IRegistroCodecsEnvelope>();
        DivulgacaoDeTeste.ConfigurarFixadaEDivulgada(
            processoRepository, registroCodecs, grafo,
            fixada: (fixada, DivulgacaoDeTeste.FimDoPeriodo),
            divulgada: (divulgada, DivulgacaoDeTeste.FimDoPeriodo.AddDays(30)));

        Result<ReciboDaInscricaoDto> resultado = await SubmeterAsync(
            new SubmeterInscricaoCommand(ProcessoId, RequestComApelidoETermoAceito(cpf, termo, grafo), PrecondicaoIfMatch.DeTags([DivulgacaoDeTeste.ETagDepoisDeAdotar(existente, divulgada)])),
            inscricaoRepository, processoRepository, registroCodecs, Substitute.For<IUnitOfWork>(), UsuarioComCpf(cpf),
            geradorDeNumeroInscricao: GeradorQueDevolve("00000007"), relogio: new RelogioFixo(DivulgacaoDeTeste.FimDoPeriodo.AddDays(10)));

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
    }

    /// <summary>
    /// Um item obrigatório na seção do formulário, mais um termo obrigatório exigido na mesma
    /// finalidade; <paramref name="outrosItens"/> e <paramref name="grupos"/> entram na mesma seção, e
    /// <paramref name="ajustar"/> muda o processo antes de a versão o congelar.
    /// </summary>
    private static (VersaoConfiguracao Versao, GrafoConfiguracao Grafo, VersaoTermoEscolhida Termo) MontarVersaoComItemObrigatorioETermo(
        IReadOnlyList<FatoColetado>? outrosItens = null, IReadOnlyList<GrupoColetado>? grupos = null, Action<ProcessoSeletivo>? ajustar = null,
        Func<ProcessoSeletivo, (IReadOnlyList<DocumentoExigido> Documentos, IReadOnlyList<NoExigencia> Nos)>? exigencias = null)
    {
        FatoColetado item = FatoColetado.Criar(
            "APELIDO", 0, "Apelido", TipoRenderizacao.Texto, Obrigatoriedade.Sempre, null,
            classificacaoProtecao: "PESSOAL", formato: "LIVRE", restricoes: [], hipoteseLegal: ProtecaoDoFatoDeTeste.HipoteseLegal, finalidadeTratamento: ProtecaoDoFatoDeTeste.Finalidade, descricao: ProtecaoDoFatoDeTeste.Descricao).Value!;

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

        (IReadOnlyList<DocumentoExigido> documentos, IReadOnlyList<NoExigencia> nos) =
            exigencias?.Invoke(processoBase) ?? ([.. processoBase.DocumentosExigidos], []);
        GrafoConfiguracao grafo = new(
            [.. processoBase.Etapas], processoBase.OfertaAtendimento!, [.. processoBase.DistribuicaoVagas],
            processoBase.BonusRegional, [.. processoBase.CriteriosDesempate], processoBase.Classificacao!,
            [.. processoBase.CronogramaFases], documentos, nos, null,
            fatosColetados: [.. processoBase.FatosColetados],
            formularios: [.. processoBase.Formularios],
            termosExigidos: [.. processoBase.TermosExigidos],
            gruposColetados: [.. processoBase.GruposColetados],
            numeroDeOpcoesDeCurso: 1);

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
            ofertaB, null, new Dictionary<string, JsonElement>(), new Dictionary<string, IReadOnlyList<OcorrenciaDeGrupoInscricao>>(), [],
            Unifesspa.UniPlus.Selecao.Application.Services.ContextoDaVersaoFixadaResolver.Admitidas(grafo) with { NomeCivilDoLogin = PersonasDeTeste.NomeFernandaDrumond }, PrecondicaoIfMatch.DeTags([existente.ETag])).IsSuccess.Should().BeTrue();

        Result<ReciboDaInscricaoDto> daOfertaA = await SubmeterNaOfertaAsync(cpf, versao, grafo, termo, existente, ofertaA);

        daOfertaA.IsSuccess.Should().BeTrue(daOfertaA.Error?.Message);
        Emitido(existente).OfertaCursoId.Should().Be(ofertaA);
        Emitido(existente).Modalidades!.Select(static m => m.Codigo).Should().Equal(
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

        Inscricao inscricao = NovaInscricao(cpf, "sub-1", versao.Id);
        Result<ReciboDaInscricaoDto> daOfertaB = await SubmeterNaOfertaAsync(cpf, versao, grafo, termo, inscricao, ofertaB);

        daOfertaB.IsSuccess.Should().BeTrue(daOfertaB.Error?.Message);
        Emitido(inscricao).Modalidades!.Select(static m => m.Codigo).Should().Equal("AC", "AC_FEM");
    }

    [Fact(DisplayName = "O envio com 1ª e 2ª opção concorre às modalidades da distribuição de cada oferta, na versão que declara duas opções")]
    public async Task Handle_DuasOpcoes_ConcorreAsModalidadesDeCadaOferta()
    {
        Cpf cpf = PersonasDeTeste.FernandaDrumond;
        (VersaoConfiguracao versao, GrafoConfiguracao grafoBase, VersaoTermoEscolhida termo) = MontarVersaoComItemObrigatorioETermo();
        Guid direito = Guid.CreateVersion7();
        Guid computacao = Guid.CreateVersion7();
        GrafoConfiguracao grafo = OfertasDeTeste.ComOpcoesDeCurso(
            OfertasDeTeste.ComDistribuicao(grafoBase, OfertasDeTeste.Distribuicao(direito, "AC", "AC_FEM"), OfertasDeTeste.Distribuicao(computacao, "AC")),
            numeroDeOpcoesDeCurso: 2);

        Inscricao inscricao = NovaInscricao(cpf, "sub-1", versao.Id);
        Result<ReciboDaInscricaoDto> resultado = await SubmeterNaOfertaAsync(cpf, versao, grafo, termo, inscricao, direito, computacao);

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        ComprovanteInscricao comprovante = Emitido(inscricao);
        comprovante.OfertaCursoId.Should().Be(direito);
        comprovante.OfertaCursoSegundaOpcaoId.Should().Be(computacao);
        comprovante.Modalidades!.Select(static m => m.Codigo).Should().Equal("AC", "AC_FEM");
        comprovante.ModalidadesSegundaOpcao!.Select(static m => m.Codigo).Should().Equal("AC");
    }

    [Fact(DisplayName = "A 2ª opção é recusada quando a versão que a inscrição fixou declara uma opção só")]
    public async Task Handle_SegundaOpcaoEmVersaoFixadaDeUmaOpcao_Recusa()
    {
        Cpf cpf = PersonasDeTeste.FernandaDrumond;
        (VersaoConfiguracao versao, GrafoConfiguracao grafoBase, VersaoTermoEscolhida termo) = MontarVersaoComItemObrigatorioETermo();
        Guid direito = Guid.CreateVersion7();
        Guid computacao = Guid.CreateVersion7();
        GrafoConfiguracao grafo = OfertasDeTeste.ComDistribuicao(
            grafoBase, OfertasDeTeste.Distribuicao(direito, "AC"), OfertasDeTeste.Distribuicao(computacao, "AC"));

        Inscricao inscricao = NovaInscricao(cpf, "sub-1", versao.Id);
        Result<ReciboDaInscricaoDto> resultado = await SubmeterNaOfertaAsync(cpf, versao, grafo, termo, inscricao, direito, computacao);

        resultado.Errors.Should().ContainSingle().Which.Should().Match<FieldError>(
            e => e.Field == "ofertaCursoSegundaOpcaoId" && e.Error.Code == InscricaoErrorCodes.SegundaOpcaoNaoAdmitida);
    }

    private static async Task<Result<ReciboDaInscricaoDto>> SubmeterNaOfertaAsync(
        Cpf cpf, VersaoConfiguracao versao, GrafoConfiguracao grafo, VersaoTermoEscolhida termo, Inscricao inscricao, Guid ofertaEnviada,
        Guid? segundaOpcaoEnviada = null)
    {
        IInscricaoRepository inscricaoRepository = Substitute.For<IInscricaoRepository>();
        inscricaoRepository.ObterAtivaPorProcessoECpfAsync(ProcessoId, cpf, Arg.Any<CancellationToken>()).Returns(inscricao);
        IProcessoSeletivoRepository processoRepository = Substitute.For<IProcessoSeletivoRepository>();
        IRegistroCodecsEnvelope registroCodecs = Substitute.For<IRegistroCodecsEnvelope>();
        DivulgacaoDeTeste.ConfigurarVersao(processoRepository, registroCodecs, versao, grafo);

        // A candidata responde o que faz valer a modalidade de ação afirmativa da oferta B.
        InscricaoRequest request = RequestComApelidoETermoAceito(cpf, termo, grafo) with
        {
            OfertaCursoId = ofertaEnviada,
            OfertaCursoSegundaOpcaoId = segundaOpcaoEnviada,
        };
        Result<ReciboDaInscricaoDto> resultado = await SubmeterAsync(
            new SubmeterInscricaoCommand(ProcessoId, request, PrecondicaoIfMatch.DeTags([inscricao.ETag])),
            inscricaoRepository, processoRepository, registroCodecs, Substitute.For<IUnitOfWork>(), UsuarioComCpf(cpf));
        Guardar(inscricao, inscricaoRepository);
        return resultado;
    }

    [Fact(DisplayName = "O envio recusa o documento obrigatório sem arquivo; com o arquivo confirmado submete, fixa o que conta e agenda a limpeza do que não se aplica")]
    public async Task Handle_DocumentoObrigatorio_ContaOsArquivosEnviados()
    {
        Cpf cpf = PersonasDeTeste.FernandaDrumond;
        DocumentoExigido laudo = null!, naoSeAplica = null!;
        (VersaoConfiguracao versao, GrafoConfiguracao grafo, VersaoTermoEscolhida termo) = MontarVersaoComItemObrigatorioETermo(exigencias: processo =>
        {
            Guid fase = processo.CronogramaFases.First(static f => f.ColetaInscricao).Id;
            DocumentoExigido Doc(string nome, Aplicabilidade aplicabilidade, IReadOnlyList<CondicaoGatilho> condicoes) => DocumentoExigido.Criar(
                fase, Guid.CreateVersion7(), nome.ToUpperInvariant(), nome, "CAT", ProtecaoDoDocumentoDeTeste.Pessoal, aplicabilidade, obrigatorio: true, null, condicoes, [], null,
                FormatosPermitidos.Criar(true, null).Value!, null, finalidade: FinalidadeFormulario.Inscricao).Value!;
            laudo = Doc("Laudo", Aplicabilidade.Geral, []);
            naoSeAplica = Doc("Outro", Aplicabilidade.Condicional, [CondicaoGatilho.Criar(0, "APELIDO", Operador.Igual, JsonSerializer.SerializeToElement("Outro")).Value!]);
            return ([laudo, naoSeAplica], [NoExigencia.CriarFolha(laudo, 0).Value!, NoExigencia.CriarFolha(naoSeAplica, 1).Value!]);
        });

        async Task<(Result<ReciboDaInscricaoDto> Resultado, IDocumentoDaInscricaoRepository Repositorio, ArquivoDaApresentacao Fixado, ArquivoDaApresentacao Orfao, Inscricao Inscricao)> EnviarAsync(bool comLaudo, bool semApelido = false, bool laudoEmVerificacao = false)
        {
            Inscricao existente = NovaInscricao(cpf, "sub-1", versao.Id);
            IInscricaoRepository inscricaoRepository = Substitute.For<IInscricaoRepository>();
            inscricaoRepository.ObterAtivaPorProcessoECpfAsync(ProcessoId, cpf, Arg.Any<CancellationToken>()).Returns(existente);
            IProcessoSeletivoRepository processoRepository = Substitute.For<IProcessoSeletivoRepository>();
            IRegistroCodecsEnvelope registroCodecs = Substitute.For<IRegistroCodecsEnvelope>();
            DivulgacaoDeTeste.ConfigurarVersao(processoRepository, registroCodecs, versao, grafo);

            ApresentacaoDeDocumento doLaudo = ApresentacaoDeDocumento.Criar(existente.Id, laudo, null);
            ApresentacaoDeDocumento doOutro = ApresentacaoDeDocumento.Criar(existente.Id, naoSeAplica, null);
            ArquivoDaApresentacao Confirmado(ApresentacaoDeDocumento apresentacao, DocumentoExigido exigencia, bool verificado = true)
            {
                ArquivoDaApresentacao arquivo = ArquivoDaApresentacao.IniciarPendente(
                    apresentacao.Id, existente.Id, exigencia, "application/pdf", 1024, TimeProvider.System, TimeSpan.FromMinutes(15)).Value!;
                arquivo.Confirmar("\"etag\"", TimeProvider.System);
                if (verificado)
                {
                    arquivo.RegistrarVerificado(1024, new string('a', 64), FormatoPermitido.Pdf);
                }

                return arquivo;
            }

            ArquivoDaApresentacao fixado = Confirmado(doLaudo, laudo, verificado: comLaudo), orfao = Confirmado(doOutro, naoSeAplica);
            IDocumentoDaInscricaoRepository documentos = Substitute.For<IDocumentoDaInscricaoRepository>();
            documentos.ListarApresentacoesDaInscricaoAsync(existente.Id, Arg.Any<CancellationToken>()).Returns([doLaudo, doOutro]);
            documentos.ListarArquivosDaInscricaoAsync(existente.Id, Arg.Any<CancellationToken>()).Returns(comLaudo || laudoEmVerificacao ? [fixado, orfao] : [orfao]);
            documentos.ObterArquivoAsync(fixado.Id, Arg.Any<CancellationToken>()).Returns(fixado);
            documentos.ObterArquivoAsync(orfao.Id, Arg.Any<CancellationToken>()).Returns(orfao);

            InscricaoRequest request = RequestComApelidoETermoAceito(cpf, termo, grafo);
            if (semApelido)
            {
                request = request with { Respostas = request.Respostas!.Where(static r => r.Key != "APELIDO").ToDictionary(static r => r.Key, static r => r.Value) };
            }

            Result<ReciboDaInscricaoDto> resultado = await SubmeterAsync(
                new SubmeterInscricaoCommand(ProcessoId, request, PrecondicaoIfMatch.DeTags([existente.ETag])),
                inscricaoRepository, processoRepository, registroCodecs, Substitute.For<IUnitOfWork>(), UsuarioComCpf(cpf), documentoRepository: documentos);
            Guardar(existente, inscricaoRepository);
            return (resultado, documentos, fixado, orfao, existente);
        }

        (Result<ReciboDaInscricaoDto> semArquivo, IDocumentoDaInscricaoRepository repoSem, _, _, _) = await EnviarAsync(comLaudo: false);
        semArquivo.IsFailure.Should().BeTrue();
        semArquivo.Errors.Should().ContainSingle(e => e.Error.Code == InscricaoErrorCodes.DocumentoObrigatorioAusente);
        await repoSem.DidNotReceiveWithAnyArgs().RemoverArquivoAsync(default!, default);

        (Result<ReciboDaInscricaoDto> emVerificacao, _, _, _, _) = await EnviarAsync(comLaudo: false, laudoEmVerificacao: true);
        emVerificacao.Errors.Should().ContainSingle(e => e.Error.Code == InscricaoErrorCodes.DocumentoObrigatorioEmVerificacao)
            .Which.Error.Message.Should().Be("Aguarde a verificação completa dos documentos obrigatórios para enviar a inscrição.");

        (Result<ReciboDaInscricaoDto> semTudo, _, _, _, _) = await EnviarAsync(comLaudo: false, semApelido: true);
        semTudo.Errors.Select(static e => e.Error.Code).Should().Contain(InscricaoErrorCodes.DocumentoObrigatorioAusente)
            .And.Contain(c => c != InscricaoErrorCodes.DocumentoObrigatorioAusente, "a falta do documento sai junto das recusas do formulário");

        (Result<ReciboDaInscricaoDto> comArquivo, IDocumentoDaInscricaoRepository repo, ArquivoDaApresentacao fixado, ArquivoDaApresentacao orfao, Inscricao enviada) = await EnviarAsync(comLaudo: true);
        comArquivo.IsSuccess.Should().BeTrue(comArquivo.Error?.Message);
        fixado.ConstouDeEnvio.Should().BeTrue();
        DocumentoDoComprovante noComprovante = Emitido(enviada).Documentos.Should().ContainSingle("a exigência que não se aplica não consta do comprovante").Subject;
        noComprovante.Nome.Should().Be("Laudo");
        noComprovante.Situacao.Should().Be(SituacaoDoDocumentoCodigo.Satisfeita);
        noComprovante.Arquivos.Should().Equal(new ArquivoDoComprovante(new string('a', 64), 1024, "PDF"));
        await repo.Received(1).RemoverArquivoAsync(orfao, Arg.Any<CancellationToken>());
        await repo.Received(1).AgendarLimpezaAsync(orfao.ChaveDefinitivaPrevista, Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Item obrigatório respondido e termo obrigatório aceito na versão fixada: submete, gera número e grava o comprovante, com 'Prefiro não informar' em identidade de gênero e orientação sexual")]
    public async Task Handle_ItemRespondidoETermoAceito_Submete()
    {
        Cpf cpf = PersonasDeTeste.FernandaDrumond;
        (VersaoConfiguracao versao, GrafoConfiguracao grafo, VersaoTermoEscolhida termo) = MontarVersaoComItemObrigatorioETermo();
        Inscricao existente = NovaInscricao(cpf, "sub-1", versao.Id);

        IInscricaoRepository inscricaoRepository = Substitute.For<IInscricaoRepository>();
        inscricaoRepository.ObterAtivaPorProcessoECpfAsync(ProcessoId, cpf, Arg.Any<CancellationToken>()).Returns(existente);

        IProcessoSeletivoRepository processoRepository = Substitute.For<IProcessoSeletivoRepository>();

        IRegistroCodecsEnvelope registroCodecs = Substitute.For<IRegistroCodecsEnvelope>();
        DivulgacaoDeTeste.ConfigurarVersao(processoRepository, registroCodecs, versao, grafo);

        IUnitOfWork unitOfWork = Substitute.For<IUnitOfWork>();
        IGeradorDeNumeroInscricao gerador = GeradorQueDevolve("00000042");

        Result<ReciboDaInscricaoDto> resultado = await SubmeterAsync(
            new SubmeterInscricaoCommand(ProcessoId, RequestComApelidoETermoAceito(cpf, termo, grafo), PrecondicaoIfMatch.DeTags([existente.ETag])),
            inscricaoRepository, processoRepository, registroCodecs, unitOfWork, UsuarioComCpf(cpf), geradorDeNumeroInscricao: gerador);

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        resultado.Value!.NumeroInscricao.Should().Be("00000042");
        resultado.Value!.InscricaoId.Should().Be(existente.Id);
        resultado.Value!.NumeroDoComprovante.Should().Be(1);
        Guardar(existente, inscricaoRepository);
        ComprovanteInscricao emitido = Emitido(existente);
        resultado.Value!.Instante.Should().Be(emitido.Instante);
        emitido.TodasAsRespostas().Should().ContainKey("APELIDO").WhoseValue.GetString().Should().Be("Nanda");
        emitido.TodasAsRespostas()["IDENTIDADE_GENERO"].GetString().Should().Be("PREFIRO_NAO_INFORMAR", "'Prefiro não informar' é resposta válida e volta ao titular no comprovante");
        emitido.TodasAsRespostas()["ORIENTACAO_SEXUAL"].GetString().Should().Be("PREFIRO_NAO_INFORMAR");
        HashCanonicalComputer.IsValidHashShape(emitido.HashIntegridade).Should().BeTrue();
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
        inscricaoRepository.ObterAtivaPorProcessoECpfAsync(ProcessoId, cpf, Arg.Any<CancellationToken>()).Returns(existente);

        IProcessoSeletivoRepository processoRepository = Substitute.For<IProcessoSeletivoRepository>();

        IRegistroCodecsEnvelope registroCodecs = Substitute.For<IRegistroCodecsEnvelope>();
        DivulgacaoDeTeste.ConfigurarVersao(processoRepository, registroCodecs, versao, grafo);

        IUnitOfWork unitOfWork = Substitute.For<IUnitOfWork>();
        IGeradorDeNumeroInscricao gerador = Substitute.For<IGeradorDeNumeroInscricao>();

        // Todo o conjunto básico está respondido, exceto APELIDO — nem agora, nem numa chamada
        // anterior de Salvar.
        Dictionary<string, JsonElement> respostas = RespostasDoConjuntoBasico(cpf);
        InscricaoRequest request = new(
            OfertaDo(grafo), respostas, null, [new AceiteDeTermoInput("TERMO_VERACIDADE", termo.VersaoId, termo.Hash)]);

        Result<ReciboDaInscricaoDto> resultado = await SubmeterAsync(
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
        inscricaoRepository.ObterAtivaPorProcessoECpfAsync(ProcessoId, cpf, Arg.Any<CancellationToken>()).Returns(existente);

        IProcessoSeletivoRepository processoRepository = Substitute.For<IProcessoSeletivoRepository>();

        IRegistroCodecsEnvelope registroCodecs = Substitute.For<IRegistroCodecsEnvelope>();
        DivulgacaoDeTeste.ConfigurarVersao(processoRepository, registroCodecs, versao, grafo);

        IUnitOfWork unitOfWork = Substitute.For<IUnitOfWork>();

        Dictionary<string, JsonElement> respostas = RespostasDoConjuntoBasico(cpf);
        respostas["APELIDO"] = JsonSerializer.SerializeToElement("Nanda");
        InscricaoRequest request = new(null, respostas, null, null);

        Result<ReciboDaInscricaoDto> resultado = await SubmeterAsync(
            new SubmeterInscricaoCommand(ProcessoId, request, PrecondicaoIfMatch.DeTags([existente.ETag])),
            inscricaoRepository, processoRepository, registroCodecs, unitOfWork, UsuarioComCpf(cpf));

        resultado.IsFailure.Should().BeTrue();
        resultado.Errors.Should().ContainSingle(e => e.Field == "TERMO_VERACIDADE" && e.Error.Code == "Inscricao.TermoNaoAceito",
            "a recusa sai no código do aceite que o candidato envia");
        await unitOfWork.DidNotReceive().SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "CEP que o Geo não encontra recusa o envio no campo do endereço, sem gravar")]
    public async Task Handle_CepNaoEncontrado_RecusaNoCampoDoEndereco()
    {
        IConferenciaEnderecoGeo geo = ConferenciaQueAceitaTudo();
        geo.ResolverCepAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Result<CepResolvido>.Failure(new DomainError(ConferenciaEnderecoGeoErrorCodes.CepNaoResolve, "sem cobertura")));

        (Result<ReciboDaInscricaoDto> resultado, IUnitOfWork unitOfWork) = await SubmeterComEnderecoAsync(geo);

        resultado.Errors.Should().ContainSingle(e => e.Field == "ENDERECO_RESIDENCIAL" && e.Error.Code == InscricaoErrorCodes.EnderecoCepNaoEncontrado);
        await unitOfWork.DidNotReceive().SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "CEP geral oficial sem logradouro e bairro recusa o envio no campo do endereço")]
    public async Task Handle_CepGeralSemLogradouro_Recusa()
    {
        IConferenciaEnderecoGeo geo = ConferenciaQueAceitaTudo();
        geo.ResolverCepAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Result<CepResolvido>.Success(new CepResolvido("1501402", "Marabá", "PA", "cep-geral")));

        (Result<ReciboDaInscricaoDto> resultado, _) = await SubmeterComEnderecoAsync(geo);

        resultado.Errors.Select(e => e.Error.Code).Should().BeEquivalentTo(
            [InscricaoErrorCodes.EnderecoLogradouroObrigatorio, InscricaoErrorCodes.EnderecoBairroObrigatorio]);
    }

    [Fact(DisplayName = "Geo fora do ar recusa o envio como indisponível, e não como CEP não encontrado")]
    public async Task Handle_GeoIndisponivel_RecusaComoIndisponivel()
    {
        IConferenciaEnderecoGeo geo = ConferenciaQueAceitaTudo();
        geo.ResolverCepAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Result<CepResolvido>.Failure(new DomainError(ConferenciaEnderecoGeoErrorCodes.GeoIndisponivel, "fora do ar")));

        (Result<ReciboDaInscricaoDto> resultado, _) = await SubmeterComEnderecoAsync(geo);

        resultado.Error!.Code.Should().Be(ConferenciaEnderecoGeoErrorCodes.GeoIndisponivel);
    }

    [Fact(DisplayName = "Envio sem CEP e com UF divergente recebe a orientação do CEP, e não a divergência de município")]
    public async Task Handle_EnderecoSemCepComUfDivergente_RecusaPorFaltaDeCep()
    {
        IConferenciaEnderecoGeo geo = ConferenciaQueAceitaTudo();
        geo.ConferirMunicipioAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Result<string>.Failure(new DomainError(ConferenciaEnderecoGeoErrorCodes.MunicipioDivergente, "UF divergente")));

        (Result<ReciboDaInscricaoDto> resultado, _) = await SubmeterComEnderecoAsync(geo, cep: null);

        resultado.Errors.Should().ContainSingle(e => e.Field == "ENDERECO_RESIDENCIAL" && e.Error.Code == InscricaoErrorCodes.EnderecoSemCep);
        resultado.Errors.Should().NotContain(e => e.Field == "ENDERECO_RESIDENCIAL" && e.Error.Code == ConferenciaEnderecoGeoErrorCodes.MunicipioDivergente);
    }

    private static async Task<(Result<ReciboDaInscricaoDto> Resultado, IUnitOfWork UnitOfWork)> SubmeterComEnderecoAsync(IConferenciaEnderecoGeo geo, string? cep = "66010000")
    {
        Cpf cpf = PersonasDeTeste.FernandaDrumond;
        (VersaoConfiguracao versao, GrafoConfiguracao grafo, VersaoTermoEscolhida termo) = MontarVersaoComItemObrigatorioETermo();
        Inscricao existente = NovaInscricao(cpf, "sub-1", versao.Id);

        IInscricaoRepository inscricaoRepository = Substitute.For<IInscricaoRepository>();
        inscricaoRepository.ObterAtivaPorProcessoECpfAsync(ProcessoId, cpf, Arg.Any<CancellationToken>()).Returns(existente);

        IProcessoSeletivoRepository processoRepository = Substitute.For<IProcessoSeletivoRepository>();
        IRegistroCodecsEnvelope registroCodecs = Substitute.For<IRegistroCodecsEnvelope>();
        DivulgacaoDeTeste.ConfigurarVersao(processoRepository, registroCodecs, versao, grafo);
        IUnitOfWork unitOfWork = Substitute.For<IUnitOfWork>();

        Dictionary<string, JsonElement> respostas = RespostasDoConjuntoBasico(cpf);
        respostas["APELIDO"] = JsonSerializer.SerializeToElement("Nanda");
        respostas["ENDERECO_RESIDENCIAL"] = JsonSerializer.SerializeToElement(new { cep, cidade = new { codigoIbge = "1501402", uf = "PA" } });
        InscricaoRequest request = new(OfertaDo(grafo), respostas, null, [new AceiteDeTermoInput("TERMO_VERACIDADE", termo.VersaoId, termo.Hash)]);

        Result<ReciboDaInscricaoDto> resultado = await SubmeterAsync(
            new SubmeterInscricaoCommand(ProcessoId, request, PrecondicaoIfMatch.DeTags([existente.ETag])),
            inscricaoRepository, processoRepository, registroCodecs, unitOfWork, UsuarioComCpf(cpf), geo);
        return (resultado, unitOfWork);
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
        inscricaoRepository.ObterAtivaPorProcessoECpfAsync(ProcessoId, cpf, Arg.Any<CancellationToken>()).Returns(existente);

        IProcessoSeletivoRepository processoRepository = Substitute.For<IProcessoSeletivoRepository>();

        IRegistroCodecsEnvelope registroCodecs = Substitute.For<IRegistroCodecsEnvelope>();
        DivulgacaoDeTeste.ConfigurarVersao(processoRepository, registroCodecs, versao, grafo);

        IUnitOfWork unitOfWork = Substitute.For<IUnitOfWork>();

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
            OfertaDo(grafo), respostas, null, [new AceiteDeTermoInput("TERMO_VERACIDADE", termo.VersaoId, termo.Hash)]);

        Result<ReciboDaInscricaoDto> resultado = await SubmeterAsync(
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
            null, null, respostasDoRascunho,
            new Dictionary<string, IReadOnlyList<OcorrenciaDeGrupoInscricao>>(StringComparer.Ordinal), [],
            Unifesspa.UniPlus.Selecao.Application.Services.ContextoDaVersaoFixadaResolver.Admitidas(grafo) with { NomeCivilDoLogin = PersonasDeTeste.NomeFernandaDrumond }, PrecondicaoIfMatch.DeTags([existente.ETag]));
        salvo.IsSuccess.Should().BeTrue(salvo.Error?.Message);

        IInscricaoRepository inscricaoRepository = Substitute.For<IInscricaoRepository>();
        inscricaoRepository.ObterAtivaPorProcessoECpfAsync(ProcessoId, cpf, Arg.Any<CancellationToken>()).Returns(existente);

        IProcessoSeletivoRepository processoRepository = Substitute.For<IProcessoSeletivoRepository>();

        IRegistroCodecsEnvelope registroCodecs = Substitute.For<IRegistroCodecsEnvelope>();
        DivulgacaoDeTeste.ConfigurarVersao(processoRepository, registroCodecs, versao, grafo);

        IUnitOfWork unitOfWork = Substitute.For<IUnitOfWork>();

        // Só o aceite do termo vem nesta chamada — APELIDO já estava gravado pela chamada de Salvar.
        InscricaoRequest request = new(
            OfertaDo(grafo), new Dictionary<string, JsonElement>(StringComparer.Ordinal), null,
            [new AceiteDeTermoInput("TERMO_VERACIDADE", termo.VersaoId, termo.Hash)]);

        Result<ReciboDaInscricaoDto> resultado = await SubmeterAsync(
            new SubmeterInscricaoCommand(ProcessoId, request, PrecondicaoIfMatch.DeTags([existente.ETag])),
            inscricaoRepository, processoRepository, registroCodecs, unitOfWork, UsuarioComCpf(cpf));

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        Guardar(existente, inscricaoRepository);
        Emitido(existente).Respostas.Should().ContainKey("APELIDO").WhoseValue.GetString().Should().Be("Nanda");
    }

    /// <summary>Um booleano que, respondido "não", esconde o que depende dele.</summary>
    private static FatoColetado Pergunta(string fato, int ordem) => FatoColetado.Criar(
        fato, ordem, fato, TipoRenderizacao.Booleano, Obrigatoriedade.Sempre, null, classificacaoProtecao: "SENSIVEL", hipoteseLegal: ProtecaoDoFatoDeTeste.HipoteseLegal, finalidadeTratamento: ProtecaoDoFatoDeTeste.Finalidade, descricao: ProtecaoDoFatoDeTeste.Descricao).Value!;

    /// <summary>Um texto opcional que só aparece com <paramref name="pergunta"/> respondida "sim".</summary>
    private static FatoColetado SoComSim(string fato, int ordem, string pergunta) => FatoColetado.Criar(
        fato, ordem, fato, TipoRenderizacao.Texto, Obrigatoriedade.Nunca,
        [CondicaoPrecondicaoFato.Criar(0, pergunta, Operador.Igual, JsonSerializer.SerializeToElement(true)).Value!],
        classificacaoProtecao: "SENSIVEL", formato: "LIVRE", hipoteseLegal: ProtecaoDoFatoDeTeste.HipoteseLegal, finalidadeTratamento: ProtecaoDoFatoDeTeste.Finalidade, descricao: ProtecaoDoFatoDeTeste.Descricao).Value!;

    private static async Task<(Result<ReciboDaInscricaoDto> Resultado, Inscricao Inscricao)> SubmeterComAsync(
        IReadOnlyList<FatoColetado> itens,
        IReadOnlyList<GrupoColetado>? grupos,
        IReadOnlyDictionary<string, JsonElement> respostas,
        IReadOnlyDictionary<string, IReadOnlyList<OcorrenciaDeGrupoInscricaoInput>>? respostasDosGrupos = null,
        Action<ProcessoSeletivo>? ajustar = null,
        string? nomeSocialDaConta = null,
        IConferenciaEnderecoGeo? geo = null,
        IReadOnlyCollection<string>? remover = null)
    {
        Cpf cpf = PersonasDeTeste.FernandaDrumond;
        (VersaoConfiguracao versao, GrafoConfiguracao grafo, VersaoTermoEscolhida termo) = MontarVersaoComItemObrigatorioETermo(itens, grupos, ajustar);
        Inscricao existente = NovaInscricao(cpf, "sub-1", versao.Id);

        IInscricaoRepository inscricaoRepository = Substitute.For<IInscricaoRepository>();
        inscricaoRepository.ObterAtivaPorProcessoECpfAsync(ProcessoId, cpf, Arg.Any<CancellationToken>()).Returns(existente);
        IProcessoSeletivoRepository processoRepository = Substitute.For<IProcessoSeletivoRepository>();
        IRegistroCodecsEnvelope registroCodecs = Substitute.For<IRegistroCodecsEnvelope>();
        DivulgacaoDeTeste.ConfigurarVersao(processoRepository, registroCodecs, versao, grafo);

        InscricaoRequest comApelido = RequestComApelidoETermoAceito(cpf, termo, grafo);
        Dictionary<string, JsonElement> todas = new(comApelido.Respostas!, StringComparer.Ordinal);
        foreach (string fato in remover ?? [])
        {
            todas.Remove(fato);
        }

        foreach ((string fato, JsonElement valor) in respostas)
        {
            todas[fato] = valor;
        }

        Result<ReciboDaInscricaoDto> resultado = await SubmeterAsync(
            new SubmeterInscricaoCommand(ProcessoId, comApelido with { Respostas = todas, Grupos = respostasDosGrupos }, PrecondicaoIfMatch.DeTags([existente.ETag])),
            inscricaoRepository, processoRepository, registroCodecs, Substitute.For<IUnitOfWork>(), UsuarioComCpf(cpf, nomeSocial: nomeSocialDaConta), geo);
        Guardar(existente, inscricaoRepository);
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
        (Result<ReciboDaInscricaoDto> resultado, Inscricao inscricao) = await SubmeterComAsync(
            [Pergunta("CONCORRER_PCD", 1)], grupos: null,
            new Dictionary<string, JsonElement>(StringComparer.Ordinal) { ["CONCORRER_PCD"] = JsonSerializer.SerializeToElement(false) },
            ajustar: AmplaSoComDeficiencia);

        resultado.Errors.Should().ContainSingle().Which.Field.Should().Be("CONCORRER_PCD");
        resultado.Errors[0].Error.Code.Should().Be(InscricaoErrorCodes.NenhumaModalidade);
        inscricao.Situacao.Should().Be(Domain.Enums.SituacaoInscricao.Rascunho);
        inscricao.Comprovantes.Should().BeEmpty();
    }

    [Fact(DisplayName = "Com o Geo fora do ar, a modalidade decidida em que o candidato não concorre a nenhuma segue como recusa 422")]
    public async Task Handle_NenhumaModalidadeComGeoForaDoAr_Recusa422()
    {
        (Result<ReciboDaInscricaoDto> resultado, _) = await SubmeterComAsync(
            [Pergunta("CONCORRER_PCD", 1)], grupos: null,
            new Dictionary<string, JsonElement>(StringComparer.Ordinal) { ["CONCORRER_PCD"] = JsonSerializer.SerializeToElement(false) },
            ajustar: AmplaSoComDeficiencia, geo: GeoQueConhece().Indisponivel());

        resultado.Errors.Should().ContainSingle(e => e.Field == "CONCORRER_PCD" && e.Error.Code == InscricaoErrorCodes.NenhumaModalidade);
    }

    [Fact(DisplayName = "Com o Geo fora do ar, a modalidade que deriva da residência e fica sem decidir não é recusa: o envio devolve indisponível")]
    public async Task Handle_ModalidadeQueDependeDaResidenciaComGeoForaDoAr_Recusa503()
    {
        (Result<ReciboDaInscricaoDto> resultado, Inscricao inscricao) = await SubmeterComAsync(
            [], grupos: null, Texto(),
            ajustar: processo => processo.DefinirDistribuicaoVagas(
                [ProcessoSeletivoConformeBuilder.DistribuicaoAmplaConcorrencia(PredicadoDnf.CriarDeCondicoesAgrupadas(
                    [(0, CondicaoDnf.Criar("MUNICIPIO_RESIDENCIA", Operador.Igual, JsonSerializer.SerializeToElement(Belem)).Value!)]).Value!)],
                FatosDeModalidadeDeTeste.DoCatalogo, PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue(),
            geo: GeoQueConhece().Indisponivel());

        resultado.Error!.Code.Should().Be(ConferenciaEnderecoGeoErrorCodes.GeoIndisponivel);
        inscricao.EhRascunho.Should().BeTrue();
    }

    [Fact(DisplayName = "Com a modalidade aplicável, o envio a grava como derivada e o comprovante a traz com o nome congelado")]
    public async Task Handle_ComModalidade_GravaEComprova()
    {
        (Result<ReciboDaInscricaoDto> resultado, Inscricao inscricao) = await SubmeterComAsync(
            [Pergunta("CONCORRER_PCD", 1)], grupos: null,
            new Dictionary<string, JsonElement>(StringComparer.Ordinal) { ["CONCORRER_PCD"] = JsonSerializer.SerializeToElement(true) },
            ajustar: AmplaSoComDeficiencia);

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        Emitido(inscricao).Modalidades.Should().Equal(new ModalidadeDeConcorrencia("AC", "AC", 0));
        inscricao.Derivados["MODALIDADE"].EnumerateArray().Select(static v => v.GetString()).Should().Equal("AC");
        inscricao.Comprovantes.Should().ContainSingle().Which.IntegridadeConfere().Should().BeTrue();
    }

    [Fact(DisplayName = "A modalidade enviada pelo candidato é recusada e nunca gravada")]
    public async Task Handle_ModalidadeEnviadaPeloCandidato_Recusa()
    {
        (Result<ReciboDaInscricaoDto> resultado, Inscricao inscricao) = await SubmeterComAsync(
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

        (Result<ReciboDaInscricaoDto> resultado, _) = await SubmeterComAsync(
            [], grupos: null, new Dictionary<string, JsonElement>(StringComparer.Ordinal),
            ajustar: processo =>
            {
                FormularioDeTeste.DefinirItens(
                    processo,
                    [FatoColetado.Criar(
                        "RENDA_DECLARADA", 0, "Renda declarada", TipoRenderizacao.Texto, Obrigatoriedade.Sempre, null,
                        classificacaoProtecao: "PESSOAL", formato: "LIVRE", finalidade: FinalidadeFormulario.IsencaoTaxa, hipoteseLegal: ProtecaoDoFatoDeTeste.HipoteseLegal, finalidadeTratamento: ProtecaoDoFatoDeTeste.Finalidade, descricao: ProtecaoDoFatoDeTeste.Descricao).Value!],
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
        (Result<ReciboDaInscricaoDto> resultado, Inscricao inscricao) = await SubmeterComAsync(
            [Pergunta("PRECISA_ATENDIMENTO", 1), SoComSim("CONDICAO_DESCRITA", 2, "PRECISA_ATENDIMENTO")],
            grupos: null,
            new Dictionary<string, JsonElement>(StringComparer.Ordinal)
            {
                ["PRECISA_ATENDIMENTO"] = JsonSerializer.SerializeToElement(false),
                ["CONDICAO_DESCRITA"] = JsonSerializer.SerializeToElement("Lactante"),
            });

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        Emitido(inscricao).TodasAsRespostas().Should().ContainKey("PRECISA_ATENDIMENTO").And.NotContainKey("CONDICAO_DESCRITA");
        inscricao.Respostas.Should().NotContainKey("CONDICAO_DESCRITA", "a inscrição enviada guarda só o que se aplica");
    }

    [Fact(DisplayName = "Com sim e a conta sem nome social, o envio recusa o campo só com a orientação do Meu perfil, no lugar da obrigatoriedade genérica")]
    public async Task Handle_SimSemNomeSocialNaConta_RecusaComOrientacaoAntesDaObrigatoriedade()
    {
        (Result<ReciboDaInscricaoDto> resultado, Inscricao inscricao) = await SubmeterComAsync(
            [], grupos: null,
            new Dictionary<string, JsonElement>(StringComparer.Ordinal) { ["DESEJA_NOME_SOCIAL"] = JsonSerializer.SerializeToElement(true) });

        resultado.Errors.Where(e => e.Field == "NOME_SOCIAL").Select(static e => e.Error.Code)
            .Should().Equal(InscricaoErrorCodes.NomeSocialAusenteNaConta);
        inscricao.Comprovantes.Should().BeEmpty();
    }

    [Fact(DisplayName = "Com sim e o nome social da conta, o envio passa e grava o nome social")]
    public async Task Handle_SimComNomeSocialDaConta_Aceita()
    {
        (Result<ReciboDaInscricaoDto> resultado, Inscricao inscricao) = await SubmeterComAsync(
            [], grupos: null,
            new Dictionary<string, JsonElement>(StringComparer.Ordinal)
            {
                ["DESEJA_NOME_SOCIAL"] = JsonSerializer.SerializeToElement(true),
                ["NOME_SOCIAL"] = JsonSerializer.SerializeToElement(PersonasDeTeste.NomeSocialFernandaDrumond),
            },
            nomeSocialDaConta: PersonasDeTeste.NomeSocialFernandaDrumond);

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        inscricao.Respostas.Should().ContainKey("NOME_SOCIAL");
    }

    [Fact(DisplayName = "Com não, o envio passa e não grava o nome social, ainda que a conta tenha um")]
    public async Task Handle_NaoDesejaNomeSocial_NaoGravaNomeSocial()
    {
        (Result<ReciboDaInscricaoDto> resultado, Inscricao inscricao) = await SubmeterComAsync(
            [], grupos: null,
            new Dictionary<string, JsonElement>(StringComparer.Ordinal)
            {
                ["DESEJA_NOME_SOCIAL"] = JsonSerializer.SerializeToElement(false),
                ["NOME_SOCIAL"] = JsonSerializer.SerializeToElement(PersonasDeTeste.NomeSocialFernandaDrumond),
            },
            nomeSocialDaConta: PersonasDeTeste.NomeSocialFernandaDrumond);

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        inscricao.Respostas.Should().NotContainKey("NOME_SOCIAL");
        Emitido(inscricao).TodasAsRespostas().Should().NotContainKey("NOME_SOCIAL");
    }

    [Fact(DisplayName = "O grupo que outra resposta esconde é descartado inteiro no envio")]
    public async Task Handle_GrupoOculto_EDescartadoInteiro()
    {
        GrupoColetado dependentes = GrupoColetado.Criar(
            "DEPENDENTES", 2, FormularioDeTeste.Secao, "Dependentes", minimo: 0, maximo: null,
            PredicadoDnf.CriarDeCondicoesAgrupadas([(0, CondicaoDnf.Criar("TEM_DEPENDENTES", Operador.Igual, JsonSerializer.SerializeToElement(true)).Value!)]).Value!,
            Obrigatoriedade.Nunca,
            [FatoColetado.Criar("NOME_DEPENDENTE", 0, "Nome", TipoRenderizacao.Texto, Obrigatoriedade.Sempre, null, classificacaoProtecao: "PESSOAL", formato: "LIVRE", hipoteseLegal: ProtecaoDoFatoDeTeste.HipoteseLegal, finalidadeTratamento: ProtecaoDoFatoDeTeste.Finalidade, descricao: ProtecaoDoFatoDeTeste.Descricao).Value!],
            FinalidadeFormulario.Inscricao).Value!;

        (Result<ReciboDaInscricaoDto> resultado, Inscricao inscricao) = await SubmeterComAsync(
            [Pergunta("TEM_DEPENDENTES", 1)],
            [dependentes],
            new Dictionary<string, JsonElement>(StringComparer.Ordinal) { ["TEM_DEPENDENTES"] = JsonSerializer.SerializeToElement(false) },
            new Dictionary<string, IReadOnlyList<OcorrenciaDeGrupoInscricaoInput>>(StringComparer.Ordinal)
            {
                ["DEPENDENTES"] = [new("dependente-1", new Dictionary<string, JsonElement>(StringComparer.Ordinal) { ["NOME_DEPENDENTE"] = JsonSerializer.SerializeToElement(PersonasDeTeste.Candidatas[2].Nome) })],
            });

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        Emitido(inscricao).Grupos.Should().NotContainKey("DEPENDENTES");
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

        (Result<ReciboDaInscricaoDto> resultado, Inscricao inscricao) = await SubmeterComAsync(
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
        Emitido(inscricao).Grupos["COMPOSICAO_FAMILIAR"].Single().Respostas.Should().ContainKey("MEMBRO_TEM_RENDA").And.NotContainKey("RENDA_DO_MEMBRO");
        inscricao.Grupos["COMPOSICAO_FAMILIAR"].Single().Respostas.Should().NotContainKey("RENDA_DO_MEMBRO");
    }

    [Fact(DisplayName = "DbUpdateConcurrencyException (xmin) no commit descarta o rastreamento e devolve conflito, sem deixar a exceção propagar")]
    public async Task Handle_ConflitoDeConcorrenciaNoCommit_DescartaEDevolveConflito()
    {
        Cpf cpf = PersonasDeTeste.FernandaDrumond;
        (VersaoConfiguracao versao, GrafoConfiguracao grafo, VersaoTermoEscolhida termo) = MontarVersaoComItemObrigatorioETermo();
        Inscricao existente = NovaInscricao(cpf, "sub-1", versao.Id);

        IInscricaoRepository inscricaoRepository = Substitute.For<IInscricaoRepository>();
        inscricaoRepository.ObterAtivaPorProcessoECpfAsync(ProcessoId, cpf, Arg.Any<CancellationToken>()).Returns(existente);

        IProcessoSeletivoRepository processoRepository = Substitute.For<IProcessoSeletivoRepository>();

        IRegistroCodecsEnvelope registroCodecs = Substitute.For<IRegistroCodecsEnvelope>();
        DivulgacaoDeTeste.ConfigurarVersao(processoRepository, registroCodecs, versao, grafo);

        IUnitOfWork unitOfWork = Substitute.For<IUnitOfWork>();
        unitOfWork.SalvarAlteracoesAsync(Arg.Any<CancellationToken>())
            .ThrowsAsync(new DbUpdateConcurrencyException("conflito sintético de teste"));

        Result<ReciboDaInscricaoDto> resultado = await SubmeterAsync(
            new SubmeterInscricaoCommand(ProcessoId, RequestComApelidoETermoAceito(cpf, termo, grafo), PrecondicaoIfMatch.DeTags([existente.ETag])),
            inscricaoRepository, processoRepository, registroCodecs, unitOfWork, UsuarioComCpf(cpf));

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("Inscricao.ConflitoDeConcorrencia");
        unitOfWork.Received(1).DescartarAlteracoesNaoSalvas();
    }

    private static readonly FatoColetado DataAteOEnvio = FatoColetado.Criar(
        "DATA_DO_EVENTO", 1, "Data do evento", TipoRenderizacao.Data, Obrigatoriedade.Sempre, null, classificacaoProtecao: "PESSOAL",
        restricoes: [new FaixaDeData(minima: null, LimiteDeData.NoEnvio(anos: 0, dias: 0))], hipoteseLegal: ProtecaoDoFatoDeTeste.HipoteseLegal, finalidadeTratamento: ProtecaoDoFatoDeTeste.Finalidade, descricao: ProtecaoDoFatoDeTeste.Descricao).Value!;

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
        inscricaoRepository.ObterAtivaPorProcessoECpfAsync(ProcessoId, cpf, Arg.Any<CancellationToken>()).Returns(existente);
        IProcessoSeletivoRepository processoRepository = Substitute.For<IProcessoSeletivoRepository>();
        IRegistroCodecsEnvelope registroCodecs = Substitute.For<IRegistroCodecsEnvelope>();
        DivulgacaoDeTeste.ConfigurarVersao(processoRepository, registroCodecs, versao, grafo);
        IUnitOfWork unitOfWork = Substitute.For<IUnitOfWork>();

        // 23:59:59,9 de 15/01 em Belém; cada leitura seguinte do relógio já cai em 16/01.
        Result<ReciboDaInscricaoDto> resultado = await SubmeterAsync(
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
        inscricaoRepository.ObterAtivaPorProcessoECpfAsync(ProcessoId, cpf, Arg.Any<CancellationToken>()).Returns(existente);
        IProcessoSeletivoRepository processoRepository = Substitute.For<IProcessoSeletivoRepository>();
        IRegistroCodecsEnvelope registroCodecs = Substitute.For<IRegistroCodecsEnvelope>();
        DivulgacaoDeTeste.ConfigurarVersao(processoRepository, registroCodecs, versao, grafo);

        Result<ReciboDaInscricaoDto> resultado = await SubmeterAsync(
            new SubmeterInscricaoCommand(ProcessoId, RequestComDataDoEvento(cpf, termo, grafo, "2026-01-15"), PrecondicaoIfMatch.DeTags([existente.ETag])),
            inscricaoRepository, processoRepository, registroCodecs, Substitute.For<IUnitOfWork>(), UsuarioComCpf(cpf),
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
        inscricaoRepository.ObterAtivaPorProcessoECpfAsync(ProcessoId, cpf, Arg.Any<CancellationToken>()).Returns(existente);
        IProcessoSeletivoRepository processoRepository = Substitute.For<IProcessoSeletivoRepository>();
        IRegistroCodecsEnvelope registroCodecs = Substitute.For<IRegistroCodecsEnvelope>();
        DivulgacaoDeTeste.ConfigurarVersao(processoRepository, registroCodecs, versao, grafo);

        Result<ReciboDaInscricaoDto> envio = await SubmeterAsync(
            new SubmeterInscricaoCommand(ProcessoId, RequestComDataDoEvento(cpf, termo, grafo, "2026-01-15"), PrecondicaoIfMatch.DeTags([existente.ETag])),
            inscricaoRepository, processoRepository, registroCodecs, Substitute.For<IUnitOfWork>(), UsuarioComCpf(cpf),
            relogio: new RelogioFixo(new DateTimeOffset(2026, 1, 15, 12, 0, 0, TimeSpan.Zero)));
        envio.IsSuccess.Should().BeTrue(envio.Error?.Message);

        Result<ReciboDaInscricaoDto> correcao = await SubmeterAsync(
            new SubmeterInscricaoCommand(ProcessoId, RequestComDataDoEvento(cpf, termo, grafo, "2026-01-16"), PrecondicaoIfMatch.DeTags([existente.ETag])),
            inscricaoRepository, processoRepository, registroCodecs, Substitute.For<IUnitOfWork>(), UsuarioComCpf(cpf),
            relogio: new RelogioFixo(new DateTimeOffset(2026, 1, 16, 12, 0, 0, TimeSpan.Zero)));

        correcao.IsSuccess.Should().BeTrue(correcao.Error?.Message);
    }

    /// <summary>Um relógio cuja leitura seguinte é sempre mais tarde que a anterior, pelo passo.</summary>
    private sealed class RelogioQueAvanca(DateTimeOffset inicio, TimeSpan passo) : TimeProvider
    {
        private int _leituras;

        public override DateTimeOffset GetUtcNow() => inicio + (passo * _leituras++);
    }

    private const string Belem = "1501402";
    private const string Salvador = "2927408";

    /// <summary>O Geo que conhece Belém no Pará e o CEP de Belém usado nas respostas básicas.</summary>
    private static FakeConferenciaEnderecoGeo GeoQueConhece() =>
        new FakeConferenciaEnderecoGeo().ComMunicipio(Belem, "PA", "Belém").ComCep("66010000", Belem, "PA", cidade: "Belém");

    private static Dictionary<string, JsonElement> Texto(params (string Fato, string Valor)[] respostas) =>
        respostas.ToDictionary(static r => r.Fato, static r => JsonSerializer.SerializeToElement(r.Valor), StringComparer.Ordinal);

    [Fact(DisplayName = "O envio grava a naturalidade, o município de residência e a cidade do endereço com o nome do Geo, ignorando o nome do navegador")]
    public async Task Handle_MunicipiosConferidos_GravaONomeDoGeo()
    {
        Dictionary<string, JsonElement> respostas = Texto();
        respostas["ENDERECO_RESIDENCIAL"] = JsonSerializer.SerializeToElement(new { cep = "66010000", cidade = new { codigoIbge = Belem, nome = "BELEM-PA", uf = "PA" } });

        (Result<ReciboDaInscricaoDto> resultado, Inscricao inscricao) = await SubmeterComAsync([], null, respostas, geo: GeoQueConhece());

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        string esperado = ValorDeMunicipio.Gravado(Belem, "Belém").GetRawText();
        inscricao.Respostas["NATURALIDADE_MUNICIPIO"].GetRawText().Should().Be(esperado);
        inscricao.Derivados["MUNICIPIO_RESIDENCIA"].GetRawText().Should().Be(esperado);
        inscricao.Respostas["ENDERECO_RESIDENCIAL"].GetProperty("cidade").GetProperty("nome").GetString().Should().Be("Belém");
        Emitido(inscricao).TodasAsRespostas()["NATURALIDADE_MUNICIPIO"].GetRawText().Should().Be(esperado);
    }

    [Fact(DisplayName = "O município subitem de grupo também fica gravado com o nome do Geo")]
    public async Task Handle_MunicipioEmGrupo_GravaONomeDoGeo()
    {
        GrupoColetado locais = GrupoMunicipal();

        (Result<ReciboDaInscricaoDto> resultado, Inscricao inscricao) = await SubmeterComAsync(
            [], [locais], Texto(), OcorrenciaMunicipal(Belem), geo: GeoQueConhece());

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        inscricao.Grupos["LOCAIS"].Single().Respostas["LOCAL_MUNICIPIO"].GetRawText().Should().Be(ValorDeMunicipio.Gravado(Belem, "Belém").GetRawText());
    }

    [Fact(DisplayName = "O município de outra UF dentro de grupo é recusado no caminho do subitem, com uma recusa só")]
    public async Task Handle_MunicipioEmGrupoDeOutraUf_RecusaNoSubitem()
    {
        (Result<ReciboDaInscricaoDto> resultado, _) = await SubmeterComAsync(
            [], [GrupoMunicipal()], Texto(), OcorrenciaMunicipal(Salvador), geo: GeoQueConhece().ComMunicipio(Salvador, "BA", "Salvador"));

        resultado.Errors.Where(e => e.Field == "LOCAIS.local-1.LOCAL_MUNICIPIO").Should().ContainSingle()
            .Which.Error.Code.Should().Be(ConferenciaEnderecoGeoErrorCodes.MunicipioDivergente);
    }

    private static GrupoColetado GrupoMunicipal() => GrupoColetado.Criar(
        "LOCAIS", 1, FormularioDeTeste.Secao, "Locais", minimo: 0, maximo: null, exibicao: null, Obrigatoriedade.Nunca,
        [FatoColetado.Criar(
            "LOCAL_MUNICIPIO", 0, "Município", TipoRenderizacao.Municipio, Obrigatoriedade.Sempre, null, classificacaoProtecao: "PESSOAL",
            hipoteseLegal: ProtecaoDoFatoDeTeste.HipoteseLegal, finalidadeTratamento: ProtecaoDoFatoDeTeste.Finalidade, descricao: ProtecaoDoFatoDeTeste.Descricao,
            restricoes: [new MunicipiosDaUf("NATURALIDADE_UF")]).Value!],
        FinalidadeFormulario.Inscricao).Value!;

    private static Dictionary<string, IReadOnlyList<OcorrenciaDeGrupoInscricaoInput>> OcorrenciaMunicipal(string codigo) =>
        new(StringComparer.Ordinal) { ["LOCAIS"] = [new("local-1", Texto(("LOCAL_MUNICIPIO", codigo)))] };

    [Fact(DisplayName = "O município que o Geo não conhece é recusado no campo com município não encontrado")]
    public async Task Handle_MunicipioInexistente_RecusaNoCampo()
    {
        (Result<ReciboDaInscricaoDto> resultado, _) = await SubmeterComAsync(
            [], null, Texto(("NATURALIDADE_MUNICIPIO", "1599999")), geo: GeoQueConhece());

        resultado.Errors.Should().ContainSingle(e => e.Field == "NATURALIDADE_MUNICIPIO")
            .Which.Error.Code.Should().Be(ConferenciaEnderecoGeoErrorCodes.MunicipioNaoEncontrado);
    }

    [Fact(DisplayName = "O município de outra UF é recusado no campo com município divergente, no lugar da recusa genérica da restrição")]
    public async Task Handle_MunicipioDeOutraUf_RecusaSoComADoGeo()
    {
        (Result<ReciboDaInscricaoDto> resultado, _) = await SubmeterComAsync(
            [], null, Texto(("NATURALIDADE_MUNICIPIO", Salvador)), geo: GeoQueConhece().ComMunicipio(Salvador, "BA", "Salvador"));

        resultado.Errors.Where(e => e.Field == "NATURALIDADE_MUNICIPIO").Should().ContainSingle()
            .Which.Error.Code.Should().Be(ConferenciaEnderecoGeoErrorCodes.MunicipioDivergente);
    }

    [Fact(DisplayName = "As recusas do endereço, do município e do veredito saem juntas numa resposta só")]
    public async Task Handle_RecusasDeVariosPassos_SaemJuntas()
    {
        Dictionary<string, JsonElement> respostas = Texto(("NATURALIDADE_MUNICIPIO", Salvador), ("APELIDO", ""));
        respostas["ENDERECO_RESIDENCIAL"] = JsonSerializer.SerializeToElement(new { cep = (string?)null, cidade = new { codigoIbge = Belem, uf = "PA" } });
        respostas.Remove("NOME_MAE");

        (Result<ReciboDaInscricaoDto> resultado, _) = await SubmeterComAsync(
            [], null, respostas, geo: GeoQueConhece().ComMunicipio(Salvador, "BA", "Salvador"));

        resultado.Errors.Select(e => (e.Field, e.Error.Code)).Should().Contain(
        [
            ("ENDERECO_RESIDENCIAL", InscricaoErrorCodes.EnderecoSemCep),
            ("NATURALIDADE_MUNICIPIO", ConferenciaEnderecoGeoErrorCodes.MunicipioDivergente),
            ("APELIDO", InscricaoErrorCodes.ItemObrigatorioAusente),
        ]);
    }

    [Fact(DisplayName = "Com o Geo fora do ar, a recusa que não depende dele sai como 422 e não como indisponibilidade")]
    public async Task Handle_GeoForaDoArComRecusaIndependente_Recusa422()
    {
        Dictionary<string, JsonElement> respostas = Texto();
        respostas["ENDERECO_RESIDENCIAL"] = JsonSerializer.SerializeToElement(new { cep = (string?)null, cidade = new { codigoIbge = Belem, uf = "PA" } });

        (Result<ReciboDaInscricaoDto> resultado, _) = await SubmeterComAsync([], null, respostas, geo: GeoQueConhece().Indisponivel());

        resultado.Errors.Should().ContainSingle(e => e.Field == "ENDERECO_RESIDENCIAL" && e.Error.Code == InscricaoErrorCodes.EnderecoSemCep);
        resultado.Error?.Code.Should().NotBe(ConferenciaEnderecoGeoErrorCodes.GeoIndisponivel);
    }

    [Fact(DisplayName = "Com o Geo fora do ar e nenhuma outra recusa, o envio inteiro é recusado como indisponível e nada é gravado")]
    public async Task Handle_GeoForaDoArSemOutraRecusa_Recusa503()
    {
        (Result<ReciboDaInscricaoDto> resultado, Inscricao inscricao) = await SubmeterComAsync([], null, Texto(), geo: GeoQueConhece().Indisponivel());

        resultado.Error!.Code.Should().Be(ConferenciaEnderecoGeoErrorCodes.GeoIndisponivel);
        inscricao.EhRascunho.Should().BeTrue();
    }

    [Fact(DisplayName = "O município de um campo que a avaliação esconde não é conferido no Geo nem recusado, e não fica gravado")]
    public async Task Handle_MunicipioDeCampoOculto_NaoEhConferido()
    {
        Dictionary<string, JsonElement> respostas = Texto(
            ("NACIONALIDADE", "ESTRANGEIRO"), ("PASSAPORTE_NUMERO", "FX1234567"), ("RNM_NUMERO", "V1234567"), ("NATURALIDADE_MUNICIPIO", "1599999"));
        IConferenciaEnderecoGeo geo = Substitute.For<IConferenciaEnderecoGeo>();
        geo.ConferirMunicipioAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(Result<string>.Success("Belém"));
        geo.ConferirCepAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(Result<string>.Success("Belém"));
        geo.ResolverCepAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(Result<CepResolvido>.Success(new CepResolvido(Belem, "Belém", "PA", "logradouro")));

        (Result<ReciboDaInscricaoDto> resultado, Inscricao inscricao) = await SubmeterComAsync(
            [], null, respostas, geo: geo, remover: ["RG_NUMERO", "RG_ORGAO_EMISSOR", "RG_UF", "RG_DATA_EMISSAO"]);

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        inscricao.Respostas.Should().NotContainKey("NATURALIDADE_MUNICIPIO");
        await geo.DidNotReceive().ConferirMunicipioAsync("1599999", Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "O endereço sem código de cidade, ou com o nome do navegador, fica com a cidade que o Geo resolveu pelo CEP")]
    public async Task Handle_EnderecosConferidos_GravamACidadeDoGeo()
    {
        FatoColetado comercial = FatoColetado.Criar(
            "ENDERECO_COMERCIAL", 6, "Endereço comercial", TipoRenderizacao.Endereco, Obrigatoriedade.Nunca, null, classificacaoProtecao: "PESSOAL",
            hipoteseLegal: ProtecaoDoFatoDeTeste.HipoteseLegal, finalidadeTratamento: ProtecaoDoFatoDeTeste.Finalidade, descricao: ProtecaoDoFatoDeTeste.Descricao).Value!;
        Dictionary<string, JsonElement> respostas = Texto();
        respostas["ENDERECO_RESIDENCIAL"] = JsonSerializer.SerializeToElement(new { cep = "66010000", cidade = new { nome = "SSA" } });
        respostas["ENDERECO_COMERCIAL"] = JsonSerializer.SerializeToElement(new { cep = "66010000", cidade = new { codigoIbge = Belem, nome = "BEL", uf = "PA" } });

        (Result<ReciboDaInscricaoDto> resultado, Inscricao inscricao) = await SubmeterComAsync([comercial], null, respostas, geo: GeoQueConhece());

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        foreach (string endereco in new[] { "ENDERECO_RESIDENCIAL", "ENDERECO_COMERCIAL" })
        {
            JsonElement cidade = inscricao.Respostas[endereco].GetProperty("cidade");
            (cidade.GetProperty("codigoIbge").GetString(), cidade.GetProperty("nome").GetString(), cidade.GetProperty("uf").GetString())
                .Should().Be((Belem, "Belém", "PA"), endereco);
        }

        inscricao.Derivados["MUNICIPIO_RESIDENCIA"].GetRawText().Should().Be(ValorDeMunicipio.Gravado(Belem, "Belém").GetRawText());
    }

    [Fact(DisplayName = "Com o Geo fora do ar, o CPF de outra pessoa é recusado como 422 e não vira indisponibilidade")]
    public async Task Handle_GeoForaDoArComCpfDeOutraPessoa_Recusa422()
    {
        (Result<ReciboDaInscricaoDto> resultado, _) = await SubmeterComAsync(
            [], null, Texto(("CPF", PersonasDeTeste.KevinCarvalho.Valor)), geo: GeoQueConhece().Indisponivel());

        resultado.Errors.Should().Contain(e => e.Field == "CPF" && e.Error.Code == InscricaoErrorCodes.CpfDiferenteDoDono);
    }

    [Fact(DisplayName = "As recusas do agregado, como o CPF de outra pessoa, saem junto das demais na resposta única")]
    public async Task Handle_CpfDeOutraPessoaComOutraRecusa_SaemJuntas()
    {
        (Result<ReciboDaInscricaoDto> resultado, _) = await SubmeterComAsync(
            [], null, Texto(("CPF", PersonasDeTeste.KevinCarvalho.Valor), ("NATURALIDADE_MUNICIPIO", Salvador)),
            geo: GeoQueConhece().ComMunicipio(Salvador, "BA", "Salvador"));

        resultado.Errors.Select(e => (e.Field, e.Error.Code)).Should().Contain(
        [
            ("CPF", InscricaoErrorCodes.CpfDiferenteDoDono),
            ("NATURALIDADE_MUNICIPIO", ConferenciaEnderecoGeoErrorCodes.MunicipioDivergente),
        ]);
    }

    [Fact(DisplayName = "Grupo com ocorrências de id repetido é recusado na forma, sem chegar à avaliação nem à conferência dos municípios")]
    public async Task Handle_GrupoComIdRepetido_RecusaSemExcecao()
    {
        (Result<ReciboDaInscricaoDto> resultado, _) = await SubmeterComAsync(
            [], [GrupoMunicipal()], Texto(),
            new Dictionary<string, IReadOnlyList<OcorrenciaDeGrupoInscricaoInput>>(StringComparer.Ordinal)
            {
                ["LOCAIS"] = [new("local-1", Texto(("LOCAL_MUNICIPIO", Belem))), new("local-1", Texto(("LOCAL_MUNICIPIO", Belem)))],
            },
            geo: GeoQueConhece());

        resultado.Errors.Should().Contain(e => e.Error.Code == InscricaoErrorCodes.GrupoIdDuplicado);
    }

    private static FatoColetado ObrigatorioSoParaQuemMoraEm(string municipio) => FatoColetado.Criar(
        "OUTRA_REGIAO", 7, "Outra região", TipoRenderizacao.Texto,
        Obrigatoriedade.Quando(PredicadoDnf.CriarDeCondicoesAgrupadas(
            [(0, CondicaoDnf.Criar("MUNICIPIO_RESIDENCIA", Operador.Igual, JsonSerializer.SerializeToElement(municipio)).Value!)]).Value!),
        null, classificacaoProtecao: "PESSOAL", formato: "LIVRE", hipoteseLegal: ProtecaoDoFatoDeTeste.HipoteseLegal,
        finalidadeTratamento: ProtecaoDoFatoDeTeste.Finalidade, descricao: ProtecaoDoFatoDeTeste.Descricao).Value!;

    [Fact(DisplayName = "Obrigatoriedade que depende da residência com o Geo fora do ar: o envio é recusado como indisponível, e não como campo obrigatório")]
    public async Task Handle_ObrigatoriedadeDependenteDaResidenciaComGeoForaDoAr_Recusa503()
    {
        (Result<ReciboDaInscricaoDto> resultado, Inscricao inscricao) = await SubmeterComAsync(
            [ObrigatorioSoParaQuemMoraEm("9999999")], null, Texto(), geo: GeoQueConhece().Indisponivel());

        resultado.Error!.Code.Should().Be(ConferenciaEnderecoGeoErrorCodes.GeoIndisponivel);
        inscricao.EhRascunho.Should().BeTrue();
    }

    [Fact(DisplayName = "Campo obrigatório em definitivo e vazio, com o Geo fora do ar, é recusa que conta: 422 com a recusa do campo")]
    public async Task Handle_ObrigatorioEmDefinitivoVazioComGeoForaDoAr_Recusa422()
    {
        (Result<ReciboDaInscricaoDto> resultado, _) = await SubmeterComAsync(
            [ObrigatorioSoParaQuemMoraEm("9999999")], null, Texto(), geo: GeoQueConhece().Indisponivel(), remover: ["NOME_MAE"]);

        resultado.Errors.Should().ContainSingle(e => e.Field == "NOME_MAE" && e.Error.Code == InscricaoErrorCodes.ItemObrigatorioAusente);
        resultado.Errors.Should().NotContain(e => e.Field == "OUTRA_REGIAO");
    }

    [Fact(DisplayName = "Campo obrigatório em definitivo e vazio conta como recusa mesmo quando uma restrição dele cita a residência que o Geo deixou indeterminada")]
    public async Task Handle_ObrigatorioVazioComRestricaoSobreAResidencia_ContaComoRecusa()
    {
        FatoColetado doEstado = FatoColetado.Criar(
            "LOCAL_NO_ESTADO", 8, "Local no estado", TipoRenderizacao.Municipio, Obrigatoriedade.Sempre, null, classificacaoProtecao: "PESSOAL",
            hipoteseLegal: ProtecaoDoFatoDeTeste.HipoteseLegal, finalidadeTratamento: ProtecaoDoFatoDeTeste.Finalidade, descricao: ProtecaoDoFatoDeTeste.Descricao,
            restricoes: [new MunicipiosDaUf("UF_RESIDENCIA")]).Value!;

        (Result<ReciboDaInscricaoDto> resultado, _) = await SubmeterComAsync([doEstado], null, Texto(), geo: GeoQueConhece().Indisponivel());

        resultado.Errors.Should().ContainSingle(e => e.Field == "LOCAL_NO_ESTADO" && e.Error.Code == InscricaoErrorCodes.ItemObrigatorioAusente);
    }

    [Fact(DisplayName = "Regra sobre o subitem de município de um grupo tem o mesmo veredito no envio e na consulta depois do envio, com os grupos vindos do gravado")]
    public async Task Consulta_RegraSobreMunicipioDeGrupo_TemOMesmoVeredito()
    {
        FatoColetado soEmBelem = FatoColetado.Criar(
            "OBSERVACAO_DE_BELEM", 1, "Observação", TipoRenderizacao.Texto, Obrigatoriedade.Nunca,
            [CondicaoPrecondicaoFato.Criar(0, "LOCAL_MUNICIPIO", Operador.Igual, JsonSerializer.SerializeToElement(Belem)).Value!],
            classificacaoProtecao: "PESSOAL", formato: "LIVRE", hipoteseLegal: ProtecaoDoFatoDeTeste.HipoteseLegal,
            finalidadeTratamento: ProtecaoDoFatoDeTeste.Finalidade, descricao: ProtecaoDoFatoDeTeste.Descricao).Value!;
        GrupoColetado locais = GrupoColetado.Criar(
            "LOCAIS", 1, FormularioDeTeste.Secao, "Locais", minimo: 0, maximo: null, exibicao: null, Obrigatoriedade.Nunca,
            [GrupoMunicipal().Subitens.First(), soEmBelem], FinalidadeFormulario.Inscricao).Value!;
        Cpf cpf = PersonasDeTeste.FernandaDrumond;
        (VersaoConfiguracao versao, GrafoConfiguracao grafo, VersaoTermoEscolhida termo) = MontarVersaoComItemObrigatorioETermo(grupos: [locais]);
        Inscricao existente = NovaInscricao(cpf, "sub-1", versao.Id);
        IInscricaoRepository inscricaoRepository = Substitute.For<IInscricaoRepository>();
        inscricaoRepository.ObterAtivaPorProcessoECpfAsync(ProcessoId, cpf, Arg.Any<CancellationToken>()).Returns(existente);
        IProcessoSeletivoRepository processoRepository = Substitute.For<IProcessoSeletivoRepository>();
        IRegistroCodecsEnvelope registroCodecs = Substitute.For<IRegistroCodecsEnvelope>();
        DivulgacaoDeTeste.ConfigurarVersao(processoRepository, registroCodecs, versao, grafo);
        InscricaoRequest pedido = RequestComApelidoETermoAceito(cpf, termo, grafo) with
        {
            Grupos = new Dictionary<string, IReadOnlyList<OcorrenciaDeGrupoInscricaoInput>>(StringComparer.Ordinal)
            {
                ["LOCAIS"] = [new("local-1", Texto(("LOCAL_MUNICIPIO", Belem), ("OBSERVACAO_DE_BELEM", "Perto do porto")))],
            },
        };

        Result<ReciboDaInscricaoDto> enviada = await SubmeterAsync(
            new SubmeterInscricaoCommand(ProcessoId, pedido, PrecondicaoIfMatch.DeTags([existente.ETag])),
            inscricaoRepository, processoRepository, registroCodecs, Substitute.For<IUnitOfWork>(), UsuarioComCpf(cpf), GeoQueConhece());

        enviada.IsSuccess.Should().BeTrue(enviada.Error?.Message);
        existente.Grupos["LOCAIS"].Single().Respostas.Should().ContainKey("OBSERVACAO_DE_BELEM", "no envio a regra viu o município pelo código");
        Result<Unifesspa.UniPlus.Selecao.Application.Services.ContextoDaVersaoFixada> contexto = await Unifesspa.UniPlus.Selecao.Application.Services.ContextoDaVersaoFixadaResolver
            .ResolverAsync(existente.VersaoFormularioId, ProcessoId, processoRepository, registroCodecs, existente.OfertaCursoId, existente.OfertaCursoSegundaOpcaoId, CancellationToken.None);
        Result<ResultadoDaAvaliacaoDaInscricao> consulta = await Unifesspa.UniPlus.Selecao.Application.Services.PreparacaoDaAvaliacaoDaInscricao.AvaliarAsync(
            existente, contexto.Value!, new Dictionary<string, JsonElement>(StringComparer.Ordinal),
            new Dictionary<string, IReadOnlyList<OcorrenciaRespondida>>(StringComparer.Ordinal),
            new EnvioDasRespostas(DivulgacaoDeTeste.Relogio.GetUtcNow(), TimeZoneInfo.Utc), GeoQueConhece().Indisponivel(),
            await Unifesspa.UniPlus.Selecao.Application.Services.ArquivosDaInscricao.CarregarAsync(Substitute.For<IDocumentoDaInscricaoRepository>(), existente.Id, CancellationToken.None),
            CancellationToken.None);

        consulta.IsSuccess.Should().BeTrue(consulta.Error?.Message);
        consulta.Value!.Formulario.Grupos.Single().Ocorrencias.Single().Itens.Single(static i => i.FatoCodigo == "OBSERVACAO_DE_BELEM").Visivel
            .Should().Be(Ternario.Verdadeiro, "na consulta o grupo vem do gravado, com o município como objeto, e a regra o vê pelo código");
    }

    [Fact(DisplayName = "Depois do envio, a consulta usa a residência gravada no envio: a exigência regional tem o mesmo veredito com o Geo fora do ar")]
    public async Task Consulta_DepoisDoEnvio_UsaAResidenciaGravadaSemOGeo()
    {
        Cpf cpf = PersonasDeTeste.FernandaDrumond;
        DocumentoExigido regional = null!;
        (VersaoConfiguracao versao, GrafoConfiguracao grafo, VersaoTermoEscolhida termo) = MontarVersaoComItemObrigatorioETermo(exigencias: processo =>
        {
            Guid fase = processo.CronogramaFases.First(static f => f.ColetaInscricao).Id;
            regional = DocumentoExigido.Criar(
                fase, Guid.CreateVersion7(), "REGIONAL", "Regional", "CAT", ProtecaoDoDocumentoDeTeste.Pessoal, Aplicabilidade.Condicional, obrigatorio: false, null,
                [CondicaoGatilho.Criar(0, "MUNICIPIO_RESIDENCIA", Operador.Igual, JsonSerializer.SerializeToElement(Belem)).Value!], [], null,
                FormatosPermitidos.Criar(true, null).Value!, null, finalidade: FinalidadeFormulario.Inscricao).Value!;
            return ([regional], [NoExigencia.CriarFolha(regional, 0).Value!]);
        });
        Inscricao existente = NovaInscricao(cpf, "sub-1", versao.Id);
        IInscricaoRepository inscricaoRepository = Substitute.For<IInscricaoRepository>();
        inscricaoRepository.ObterAtivaPorProcessoECpfAsync(ProcessoId, cpf, Arg.Any<CancellationToken>()).Returns(existente);
        IProcessoSeletivoRepository processoRepository = Substitute.For<IProcessoSeletivoRepository>();
        IRegistroCodecsEnvelope registroCodecs = Substitute.For<IRegistroCodecsEnvelope>();
        DivulgacaoDeTeste.ConfigurarVersao(processoRepository, registroCodecs, versao, grafo);

        Result<ReciboDaInscricaoDto> enviada = await SubmeterAsync(
            new SubmeterInscricaoCommand(ProcessoId, RequestComApelidoETermoAceito(cpf, termo, grafo), PrecondicaoIfMatch.DeTags([existente.ETag])),
            inscricaoRepository, processoRepository, registroCodecs, Substitute.For<IUnitOfWork>(), UsuarioComCpf(cpf), GeoQueConhece());
        enviada.IsSuccess.Should().BeTrue(enviada.Error?.Message);

        Result<Unifesspa.UniPlus.Selecao.Application.Services.ContextoDaVersaoFixada> contexto = await Unifesspa.UniPlus.Selecao.Application.Services.ContextoDaVersaoFixadaResolver
            .ResolverAsync(existente.VersaoFormularioId, ProcessoId, processoRepository, registroCodecs, existente.OfertaCursoId, existente.OfertaCursoSegundaOpcaoId, CancellationToken.None);
        Result<ResultadoDaAvaliacaoDaInscricao> consulta = await Unifesspa.UniPlus.Selecao.Application.Services.PreparacaoDaAvaliacaoDaInscricao.AvaliarAsync(
            existente, contexto.Value!, new Dictionary<string, JsonElement>(StringComparer.Ordinal),
            new Dictionary<string, IReadOnlyList<OcorrenciaRespondida>>(StringComparer.Ordinal),
            new EnvioDasRespostas(DivulgacaoDeTeste.Relogio.GetUtcNow(), TimeZoneInfo.Utc),
            GeoQueConhece().Indisponivel(),
            await Unifesspa.UniPlus.Selecao.Application.Services.ArquivosDaInscricao.CarregarAsync(Substitute.For<IDocumentoDaInscricaoRepository>(), existente.Id, CancellationToken.None),
            CancellationToken.None);

        consulta.IsSuccess.Should().BeTrue(consulta.Error?.Message);
        existente.Derivados["MUNICIPIO_RESIDENCIA"].GetRawText().Should().Be(ValorDeMunicipio.Gravado(Belem, "Belém").GetRawText());
        consulta.Value!.Documentos.Itens.Single().Situacao.Should().Be(SituacaoDoDocumento.Pendente);
    }

    [Fact(DisplayName = "Data de nascimento mudada na consulta: a faixa etária sai da data nova e a residência continua a gravada no envio, sem Geo")]
    public async Task Consulta_DataDeNascimentoMudada_RecalculaAFaixaEtariaESegueComAResidenciaGravada()
    {
        Cpf cpf = PersonasDeTeste.FernandaDrumond;
        DocumentoExigido aos26 = null!, aos16 = null!, deBelem = null!;
        (VersaoConfiguracao versao, GrafoConfiguracao grafo, VersaoTermoEscolhida termo) = MontarVersaoComItemObrigatorioETermo(exigencias: processo =>
        {
            Guid fase = processo.CronogramaFases.First(static f => f.ColetaInscricao).Id;
            DocumentoExigido Doc(string nome, string fato, JsonElement valor) => DocumentoExigido.Criar(
                fase, Guid.CreateVersion7(), nome.ToUpperInvariant(), nome, "CAT", ProtecaoDoDocumentoDeTeste.Pessoal, Aplicabilidade.Condicional, obrigatorio: false, null,
                [CondicaoGatilho.Criar(0, fato, Operador.Igual, valor).Value!], [], null,
                FormatosPermitidos.Criar(true, null).Value!, null, finalidade: FinalidadeFormulario.Inscricao).Value!;
            aos26 = Doc("Aos26", "FAIXA_ETARIA", JsonSerializer.SerializeToElement(26));
            aos16 = Doc("Aos16", "FAIXA_ETARIA", JsonSerializer.SerializeToElement(16));
            deBelem = Doc("DeBelem", "MUNICIPIO_RESIDENCIA", JsonSerializer.SerializeToElement(Belem));
            return ([aos26, aos16, deBelem], [NoExigencia.CriarFolha(aos26, 0).Value!, NoExigencia.CriarFolha(aos16, 1).Value!, NoExigencia.CriarFolha(deBelem, 2).Value!]);
        });
        Inscricao existente = NovaInscricao(cpf, "sub-1", versao.Id);
        IInscricaoRepository inscricaoRepository = Substitute.For<IInscricaoRepository>();
        inscricaoRepository.ObterAtivaPorProcessoECpfAsync(ProcessoId, cpf, Arg.Any<CancellationToken>()).Returns(existente);
        IProcessoSeletivoRepository processoRepository = Substitute.For<IProcessoSeletivoRepository>();
        IRegistroCodecsEnvelope registroCodecs = Substitute.For<IRegistroCodecsEnvelope>();
        DivulgacaoDeTeste.ConfigurarVersao(processoRepository, registroCodecs, versao, grafo);
        (await SubmeterAsync(
            new SubmeterInscricaoCommand(ProcessoId, RequestComApelidoETermoAceito(cpf, termo, grafo), PrecondicaoIfMatch.DeTags([existente.ETag])),
            inscricaoRepository, processoRepository, registroCodecs, Substitute.For<IUnitOfWork>(), UsuarioComCpf(cpf), GeoQueConhece()))
            .IsSuccess.Should().BeTrue();

        Result<Unifesspa.UniPlus.Selecao.Application.Services.ContextoDaVersaoFixada> contexto = await Unifesspa.UniPlus.Selecao.Application.Services.ContextoDaVersaoFixadaResolver
            .ResolverAsync(existente.VersaoFormularioId, ProcessoId, processoRepository, registroCodecs, existente.OfertaCursoId, existente.OfertaCursoSegundaOpcaoId, CancellationToken.None);
        Result<ResultadoDaAvaliacaoDaInscricao> consulta = await Unifesspa.UniPlus.Selecao.Application.Services.PreparacaoDaAvaliacaoDaInscricao.AvaliarAsync(
            existente, contexto.Value!, Texto(("DATA_NASCIMENTO", "2010-01-01")),
            new Dictionary<string, IReadOnlyList<OcorrenciaRespondida>>(StringComparer.Ordinal),
            new EnvioDasRespostas(DivulgacaoDeTeste.Relogio.GetUtcNow(), TimeZoneInfo.Utc), GeoQueConhece().Indisponivel(),
            await Unifesspa.UniPlus.Selecao.Application.Services.ArquivosDaInscricao.CarregarAsync(Substitute.For<IDocumentoDaInscricaoRepository>(), existente.Id, CancellationToken.None),
            CancellationToken.None);

        consulta.IsSuccess.Should().BeTrue(consulta.Error?.Message);
        Dictionary<Guid, SituacaoDoDocumento> situacoes = consulta.Value!.Documentos.Itens.ToDictionary(static d => d.Documento.Id, static d => d.Situacao);
        situacoes[aos16.Id].Should().Be(SituacaoDoDocumento.Pendente, "a faixa etária vem da data de nascimento do corpo");
        situacoes[aos26.Id].Should().NotBe(SituacaoDoDocumento.Pendente);
        situacoes[deBelem.Id].Should().Be(SituacaoDoDocumento.Pendente, "a residência vem da gravada no envio");
    }

    [Fact(DisplayName = "Corrigir a inscrição sem reenviar a naturalidade confere o município gravado de novo e regrava o nome")]
    public async Task Handle_SegundoEnvioSemANaturalidade_RegravaONome()
    {
        Cpf cpf = PersonasDeTeste.FernandaDrumond;
        (VersaoConfiguracao versao, GrafoConfiguracao grafo, VersaoTermoEscolhida termo) = MontarVersaoComItemObrigatorioETermo();
        Inscricao existente = NovaInscricao(cpf, "sub-1", versao.Id);
        IInscricaoRepository inscricaoRepository = Substitute.For<IInscricaoRepository>();
        inscricaoRepository.ObterAtivaPorProcessoECpfAsync(ProcessoId, cpf, Arg.Any<CancellationToken>()).Returns(existente);
        IProcessoSeletivoRepository processoRepository = Substitute.For<IProcessoSeletivoRepository>();
        IRegistroCodecsEnvelope registroCodecs = Substitute.For<IRegistroCodecsEnvelope>();
        DivulgacaoDeTeste.ConfigurarVersao(processoRepository, registroCodecs, versao, grafo);
        (await SubmeterAsync(
            new SubmeterInscricaoCommand(ProcessoId, RequestComApelidoETermoAceito(cpf, termo, grafo), PrecondicaoIfMatch.DeTags([existente.ETag])),
            inscricaoRepository, processoRepository, registroCodecs, Substitute.For<IUnitOfWork>(), UsuarioComCpf(cpf), GeoQueConhece()))
            .IsSuccess.Should().BeTrue();

        InscricaoRequest comNovoApelido = RequestComApelidoETermoAceito(cpf, termo, grafo, apelido: "Nandinha");
        InscricaoRequest correcao = comNovoApelido with
        {
            Respostas = comNovoApelido.Respostas!.Where(static r => r.Key != "NATURALIDADE_MUNICIPIO").ToDictionary(static r => r.Key, static r => r.Value, StringComparer.Ordinal),
        };
        FakeConferenciaEnderecoGeo geo = GeoQueConhece();

        Result<ReciboDaInscricaoDto> corrigida = await SubmeterAsync(
            new SubmeterInscricaoCommand(ProcessoId, correcao, PrecondicaoIfMatch.DeTags([existente.ETag])),
            inscricaoRepository, processoRepository, registroCodecs, Substitute.For<IUnitOfWork>(), UsuarioComCpf(cpf), geo);

        corrigida.IsSuccess.Should().BeTrue(corrigida.Error?.Message);
        existente.Respostas["NATURALIDADE_MUNICIPIO"].GetRawText().Should().Be(ValorDeMunicipio.Gravado(Belem, "Belém").GetRawText());
    }
}
