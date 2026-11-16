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
using Unifesspa.UniPlus.Selecao.Application.Services;
using Unifesspa.UniPlus.Selecao.Application.UnitTests.TestSupport;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Errors;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

using static Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands.SubmeterDeTeste;

/// <summary>
/// O envio e os itens, termos e documentos obrigatórios da versão fixada, e os documentos do estrangeiro.
/// </summary>
public sealed class SubmeterInscricaoDocumentosETermosTests
{
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
            Inscricao existente = NovaInscricao(cpf, "conta-1", versao.Id);
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
                inscricaoRepository, processoRepository, registroCodecs, Substitute.For<IUnitOfWork>(), UsuarioComCpf(cpf), new PortasDoEnvioDeTeste(Documentos: documentos));
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
        Inscricao existente = NovaInscricao(cpf, "conta-1", versao.Id);

        IInscricaoRepository inscricaoRepository = Substitute.For<IInscricaoRepository>();
        inscricaoRepository.ObterAtivaPorProcessoECpfAsync(ProcessoId, cpf, Arg.Any<CancellationToken>()).Returns(existente);

        IProcessoSeletivoRepository processoRepository = Substitute.For<IProcessoSeletivoRepository>();

        IRegistroCodecsEnvelope registroCodecs = Substitute.For<IRegistroCodecsEnvelope>();
        DivulgacaoDeTeste.ConfigurarVersao(processoRepository, registroCodecs, versao, grafo);

        IUnitOfWork unitOfWork = Substitute.For<IUnitOfWork>();
        IGeradorDeNumeroInscricao gerador = GeradorQueDevolve("00000042");

        Result<ReciboDaInscricaoDto> resultado = await SubmeterAsync(
            new SubmeterInscricaoCommand(ProcessoId, RequestComApelidoETermoAceito(cpf, termo, grafo), PrecondicaoIfMatch.DeTags([existente.ETag])),
            inscricaoRepository, processoRepository, registroCodecs, unitOfWork, UsuarioComCpf(cpf), new PortasDoEnvioDeTeste(Gerador: gerador));

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
        Inscricao existente = NovaInscricao(cpf, "conta-1", versao.Id);

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
            inscricaoRepository, processoRepository, registroCodecs, unitOfWork, UsuarioComCpf(cpf), new PortasDoEnvioDeTeste(Gerador: gerador));

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
        Inscricao existente = NovaInscricao(cpf, "conta-1", versao.Id);

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

    [Theory(DisplayName = "Estrangeiro que informa só um dos documentos, passaporte ou RNM, é recusado pela falta do outro")]
    [InlineData("PASSAPORTE_NUMERO", "RNM_NUMERO")]
    [InlineData("RNM_NUMERO", "PASSAPORTE_NUMERO")]
    public async Task Handle_EstrangeiroComUmSoDosDocumentos_Recusa(string informado, string faltante)
    {
        Cpf cpf = PersonasDeTeste.KevinCarvalho;
        (VersaoConfiguracao versao, GrafoConfiguracao grafo, VersaoTermoEscolhida termo) = MontarVersaoComItemObrigatorioETermo();
        Inscricao existente = NovaInscricao(cpf, "conta-1", versao.Id);

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
        Inscricao existente = NovaInscricao(cpf, "conta-1", versao.Id);
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
}
