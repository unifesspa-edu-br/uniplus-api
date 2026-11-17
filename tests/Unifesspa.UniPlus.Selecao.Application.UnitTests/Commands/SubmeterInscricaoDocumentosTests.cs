namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands;

using System.Text.Json;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Application.Abstractions.Authentication;
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
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Errors;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

using static FormularioDoEnvioDeTeste;
using static SubmeterDeTeste;

/// <summary>
/// O envio conta os arquivos dos documentos obrigatórios.
/// </summary>
public sealed class SubmeterInscricaoDocumentosTests
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
}
