namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Queries;

using System.Text;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Application.Abstractions.Authentication;
using Unifesspa.UniPlus.Kernel.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Application.DTOs;
using Unifesspa.UniPlus.Selecao.Application.Queries.Inscricoes;
using Unifesspa.UniPlus.Selecao.Application.Services;
using Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Publicacao;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

/// <summary>
/// O formulário renderizável da versão que a inscrição fixou ao iniciar (issue #1859): resolve
/// pelo <c>VersaoFormularioId</c> gravado, nunca pela divulgação atual — a mesma projeção de
/// <see cref="ProcessosSeletivos.ObterFormularioRenderizavelQueryHandlerTests"/>, só a resolução
/// da versão é diferente.
/// </summary>
public sealed class ObterFormularioDaInscricaoQueryHandlerTests
{
    private static readonly Guid ProcessoId = Guid.CreateVersion7();
    private const string SchemaVersion = "0.0.22";

    private static IUserContext UsuarioComCpf(Cpf cpf, string contaId = "conta-1")
    {
        IUserContext contexto = Substitute.For<IUserContext>();
        contexto.Cpf.Returns(cpf.Valor);
        contexto.UserId.Returns(contaId);
        return contexto;
    }

    /// <param name="faseDoBloco">A fase do formulário, que ganha o bloco de comprovação documental; sem ela, o formulário só tem a revisão.</param>
    /// <param name="exigencias">As exigências congeladas, em JSON.</param>
    private static (VersaoConfiguracao Versao, EnvelopeReidratado Envelope) MontarVersaoComFormularioDeInscricao(
        Guid? faseDoBloco = null, string exigencias = "", FinalidadeFormulario finalidade = FinalidadeFormulario.Inscricao)
    {
        ProcessoSeletivo processoBase = ProcessoSeletivoConformeBuilder.Criar("PS 2026 — Formulário da inscrição");
        EnvelopeReidratado envelope = new(
            GrafoComFormulario(processoBase, FormularioDeInscricao(faseDoBloco, finalidade)),
            DadosEdital.Criar(
                "001/2026", new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero), new DateTimeOffset(2026, 1, 31, 0, 0, 0, TimeSpan.Zero), Guid.CreateVersion7()).Value!,
            new string('a', 64), "America/Belem", retificacao: null, conformidade: null);

        return (VersaoComExigencias(exigencias), envelope);
    }

    private static FormularioProcesso FormularioDeInscricao(Guid? faseDoBloco, FinalidadeFormulario finalidade) => FormularioProcesso.Criar(
        finalidade, faseDoBloco, null,
        [
            .. faseDoBloco is null
                ? Array.Empty<EtapaFormulario>()
                : [EtapaFormulario.Criar("DOCUMENTOS", 0, TipoEtapaFormulario.Bloco, BlocoSistema.ComprovacaoDocumental, "Documentos", null, null).Value!],
            EtapaFormulario.Criar("REVISAO", 1, TipoEtapaFormulario.Bloco, BlocoSistema.RevisaoEAceite, "Revisão e aceite", null, null).Value!,
        ]).Value!;

    private static GrafoConfiguracao GrafoComFormulario(ProcessoSeletivo processoBase, FormularioProcesso formulario) => new(
        [.. processoBase.Etapas], processoBase.AtendimentoEspecializado!, [.. processoBase.DistribuicaoVagas],
        processoBase.BonusRegional, [.. processoBase.CriteriosDesempate], processoBase.Classificacao!,
        [.. processoBase.CronogramaFases], [.. processoBase.DocumentosExigidos], [], null, formularios: [formulario],
        numeroDeOpcoesDeCurso: 1);

    private static VersaoConfiguracao VersaoComExigencias(string exigencias) => VersaoConfiguracao.Abrir(
        ProcessoId,
        new DocumentoCongelado(Encoding.UTF8.GetBytes($$$"""{"documentosExigidos": {"dataReferenciaFatos": null, "exigencias": [{{{exigencias}}}]}}"""), SchemaVersion, "canonical-json/sha256@v1", new string('a', 64)),
        Guid.CreateVersion7(), "user-conta-1", new DateTimeOffset(2026, 10, 10, 12, 0, 0, TimeSpan.Zero));

    private static IRegistroCodecsEnvelope RegistroReidratando(EnvelopeReidratado envelope)
    {
        IRegistroCodecsEnvelope registro = Substitute.For<IRegistroCodecsEnvelope>();
        registro.Capacidades.Returns([new CapacidadeCodec(SchemaVersion, TemEncoder: true, TemDecoder: true, MotivoDaRecusa: null)]);
        registro.Reidratar(Arg.Any<VersaoConfiguracao>()).Returns(Result<EnvelopeReidratado>.Success(envelope));
        return registro;
    }

    private static readonly Guid FaseDoBloco = Guid.Parse("0199a000-0000-7000-8000-0000000000f1");

    private static Task<Result<FormularioRenderizavelDto>> Obter(PortasDaInscricaoDeTeste portas, IUserContext usuario) =>
        ObterFormularioDaInscricaoQueryHandler.Handle(
            new ObterFormularioDaInscricaoQuery(ProcessoId), portas.InscricaoAtiva(), portas.Formulario(), usuario, CancellationToken.None);

    private static Inscricao Rascunho(Cpf cpf, VersaoConfiguracao versao) =>
        Inscricao.Iniciar(ProcessoId, cpf, "conta-1", "https://issuer", versao.Id).Value!;

    private static PortasDaInscricaoDeTeste PortasNaVersao(Cpf cpf, Inscricao inscricao, VersaoConfiguracao versao, EnvelopeReidratado envelope)
    {
        PortasDaInscricaoDeTeste portas = new(ProcessoId) { Codecs = RegistroReidratando(envelope) };
        portas.ComInscricao(cpf, inscricao);
        portas.Processos.ObterVersoesAsync(ProcessoId, Arg.Any<CancellationToken>()).Returns([versao]);
        portas.Processos.ObterVersoesPorAtoCriadorAsync(Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>()).Returns([versao]);
        return portas;
    }

    private static string ExigenciaCondicionalDeComprovante() =>
        $$"""
        {"tipoDocumentoNome": "Comprovante de residência", "aplicabilidade": "Condicional", "obrigatorio": true, "exigidoNaFaseId": "{{FaseDoBloco}}",
         "finalidade": "INSCRICAO", "formatosPermitidos": {"lista": null, "qualquer": true}, "modelo": null}
        """;

    [Fact(DisplayName = "Sem o claim cpf no token, recusa sem consultar repositório nenhum")]
    public async Task Handle_SemCpfNoToken_Recusa()
    {
        PortasDaInscricaoDeTeste portas = new(ProcessoId);
        IUserContext userContext = Substitute.For<IUserContext>();
        userContext.Cpf.Returns((string?)null);

        Result<FormularioRenderizavelDto> resultado = await Obter(portas, userContext);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("Inscricao.CpfAusenteNoToken");
        await portas.Inscricoes.DidNotReceive().ObterAtivaPorProcessoECpfAsync(Arg.Any<Guid>(), Arg.Any<Cpf>(), Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Sem inscrição para o CPF e o processo, recusa como não encontrada")]
    public async Task Handle_SemInscricao_RecusaComoNaoEncontrada()
    {
        Cpf cpf = PersonasDeTeste.FernandaDrumond;
        PortasDaInscricaoDeTeste portas = new(ProcessoId);
        portas.ComInscricao(cpf, null);

        Result<FormularioRenderizavelDto> resultado = await Obter(portas, UsuarioComCpf(cpf));

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("Inscricao.NaoEncontrada");
    }

    [Fact(DisplayName = "Devolve o formulário da versão divulgada ao rascunho")]
    public async Task Handle_RascunhoDevolveFormularioDaVersaoDivulgada()
    {
        Cpf cpf = PersonasDeTeste.KevinCarvalho;
        (VersaoConfiguracao versao, EnvelopeReidratado envelope) = MontarVersaoComFormularioDeInscricao();
        PortasDaInscricaoDeTeste portas = PortasNaVersao(cpf, Rascunho(cpf, versao), versao, envelope);

        Result<FormularioRenderizavelDto> resultado = await Obter(portas, UsuarioComCpf(cpf));

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        resultado.Value!.Finalidade.Should().Be(EstruturaFormulario.ParaToken(FinalidadeFormulario.Inscricao));
    }

    [Fact(DisplayName = "O formulário da inscrição do candidato não traz a lista de comprovação: os documentos dele vêm da avaliação")]
    public async Task Handle_FormularioComBlocoDeComprovacao_NaoTrazALista()
    {
        Cpf cpf = PersonasDeTeste.KevinCarvalho;
        (VersaoConfiguracao versao, EnvelopeReidratado envelope) = MontarVersaoComFormularioDeInscricao(FaseDoBloco, ExigenciaCondicionalDeComprovante());
        PortasDaInscricaoDeTeste portas = PortasNaVersao(cpf, Rascunho(cpf, versao), versao, envelope);

        Result<FormularioRenderizavelDto> resultado = await Obter(portas, UsuarioComCpf(cpf));

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        resultado.Value!.ComprovacaoDocumental.Should().BeNull("a lista do certame não diz o que se aplica ao candidato");
    }

    [Fact(DisplayName = "Inscrição enviada na versão fixada sem formulário de inscrição é defeito, não ausência: a versão já foi provada ao iniciar")]
    public async Task Handle_VersaoFixadaSemFormularioDeInscricao_LancaDefeito()
    {
        Cpf cpf = PersonasDeTeste.KevinCarvalho;
        (VersaoConfiguracao versao, EnvelopeReidratado envelope) = MontarVersaoComFormularioDeInscricao(finalidade: FinalidadeFormulario.Habilitacao);
        PortasDaInscricaoDeTeste portas = PortasNaVersao(cpf, InscricaoEnviadaDeTeste.Enviar(Rascunho(cpf, versao)), versao, envelope);

        Func<Task> leitura = () => Obter(portas, UsuarioComCpf(cpf));

        await leitura.Should().ThrowAsync<InvalidOperationException>().WithMessage("*não tem o formulário de inscrição*");
    }

    [Fact(DisplayName = "A projeção da versão sem formulário da finalidade é sucesso sem formulário, não falha")]
    public void Projetar_VersaoSemFormularioDaFinalidade_SucessoSemFormulario()
    {
        (VersaoConfiguracao versao, EnvelopeReidratado envelope) = MontarVersaoComFormularioDeInscricao(finalidade: FinalidadeFormulario.Habilitacao);

        Result<FormularioRenderizavelDto?> projetado = ProjecaoDoFormularioDaVersao.Projetar(
            versao, FinalidadeFormulario.Inscricao, RegistroReidratando(envelope), acervoDaComprovacao: null);

        projetado.IsSuccess.Should().BeTrue(projetado.Error?.Message);
        projetado.Value.Should().BeNull();
    }

    [Fact(DisplayName = "O formulário da inscrição diz a fase em que é respondido, que separa os documentos dela na avaliação")]
    public async Task Handle_FormularioComFase_PublicaAFase()
    {
        Cpf cpf = PersonasDeTeste.KevinCarvalho;
        (VersaoConfiguracao versao, EnvelopeReidratado envelope) = MontarVersaoComFormularioDeInscricao(FaseDoBloco);
        PortasDaInscricaoDeTeste portas = PortasNaVersao(cpf, Rascunho(cpf, versao), versao, envelope);

        Result<FormularioRenderizavelDto> resultado = await Obter(portas, UsuarioComCpf(cpf));

        resultado.Value!.FaseId.Should().Be(FaseDoBloco);
    }
}
