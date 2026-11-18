namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands;

using System.Text.Json;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;

using NSubstitute;
using NSubstitute.ExceptionExtensions;

using Unifesspa.UniPlus.Application.Abstractions.Interfaces;
using Unifesspa.UniPlus.Kernel.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Application.Commands.Inscricoes;
using Unifesspa.UniPlus.Selecao.Application.DTOs;
using Unifesspa.UniPlus.Selecao.Application.DTOs.Comprovantes;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Errors;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

using static FormularioDoEnvioDeTeste;
using static SubmeterDeTeste;

/// <summary>
/// O veredito do envio decide por obrigatórios, termos e documentos antes de qualquer gravação, e o que é gravado depende dele.
/// </summary>
public sealed class SubmeterInscricaoVereditoEGravacaoTests
{
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
            inscricaoRepository, processoRepository, registroCodecs, unitOfWork, UsuarioComCpf(cpf), portas: new PortasDoEnvioDeTeste(Gerador: gerador));

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
            inscricaoRepository, processoRepository, registroCodecs, unitOfWork, UsuarioComCpf(cpf), portas: new PortasDoEnvioDeTeste(Gerador: gerador));

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

    [Fact(DisplayName = "O item e o termo obrigatórios de outra finalidade, como a isenção, não recusam o envio da inscrição")]
    public async Task Handle_ObrigatoriosDaIsencao_NaoRecusamOEnvio()
    {
        VersaoTermoEscolhida versaoDoTermo = new(
            Guid.CreateVersion7(), Guid.CreateVersion7(), "TERMO_DE_TESTE", 1, "Termo da isenção", "Declaro a renda informada.",
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

    [Fact(DisplayName = "DbUpdateConcurrencyException (xmin) no commit descarta o rastreamento e devolve conflito, sem deixar a exceção propagar")]
    public async Task Handle_ConflitoDeConcorrenciaNoCommit_DescartaEDevolveConflito()
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
        unitOfWork.SalvarAlteracoesAsync(Arg.Any<CancellationToken>())
            .ThrowsAsync(new DbUpdateConcurrencyException("conflito sintético de teste"));

        Result<ReciboDaInscricaoDto> resultado = await SubmeterAsync(
            new SubmeterInscricaoCommand(ProcessoId, RequestComApelidoETermoAceito(cpf, termo, grafo), PrecondicaoIfMatch.DeTags([existente.ETag])),
            inscricaoRepository, processoRepository, registroCodecs, unitOfWork, UsuarioComCpf(cpf));

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("Inscricao.ConflitoDeConcorrencia");
        unitOfWork.Received(1).DescartarAlteracoesNaoSalvas();
    }
}
