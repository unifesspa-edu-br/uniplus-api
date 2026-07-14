namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands;

using System.Text;
using System.Text.Json.Nodes;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Application.Abstractions.Authentication;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;
using Unifesspa.UniPlus.Selecao.Application.DTOs;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// Abertura da sessão editorial (Story #860). O que só existe <b>aqui</b>, e não no
/// domínio: a recusa de uma base que o sistema não sabe <b>reidratar</b> (CA-09) — o
/// registro de codecs é abstração da Application, e o agregado não o conhece (ADR-0042).
/// </summary>
public sealed class AbrirRetificacaoCommandHandlerTests
{
    private static readonly string HashFixo = string.Concat(Enumerable.Repeat("ab01234567", 7))[..64];
    private static readonly byte[] Bytes = Encoding.UTF8.GetBytes(new JsonObject { ["status"] = "ok" }.ToJsonString());
    private static readonly DateTimeOffset Agora = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);

    // ══════════════════════════════════════════════════════════════════════════════
    // CA-09 — a recusa vem na PORTA, não no descarte
    //
    // Uma sessão aberta sobre uma versão que o sistema não sabe reconstruir é uma sessão
    // IMPOSSÍVEL DE DESCARTAR: o descarte repõe a configuração congelada, e para isso
    // precisa decodificar o envelope. O administrador só descobriria ao tentar desistir —
    // e a única saída seria fechar uma retificação que ele não queria fazer.
    // ══════════════════════════════════════════════════════════════════════════════

    [Fact(DisplayName = "CA-09: abrir sobre uma base 1.0 (conhecida, mas sem decoder) RECUSA — a sessão nasceria impossível de descartar")]
    public async Task Abrir_BaseNaoReidratavel_Recusa()
    {
        Cenario cenario = Cenario.ComVersaoBase(
            schemaVersion: "1.0",
            capacidades: [
                new CapacidadeCodec("1.0", TemEncoder: false, TemDecoder: false, MotivoDaRecusa: "pode congelar blocos nao_construido"),
                new CapacidadeCodec("1.1", TemEncoder: true, TemDecoder: true, MotivoDaRecusa: null),
            ]);

        Result<RetificacaoEmCursoDto> resultado = await cenario.ExecutarAsync();

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("EnvelopeCodec.VersaoNaoReidratavel");
        cenario.Processo.Rascunho.Should().BeNull("uma abertura recusada não deixa sessão nenhuma para trás");
    }

    [Fact(DisplayName = "CA-09: abrir sobre uma versão FORA do registro de codecs recusa, com motivo próprio")]
    public async Task Abrir_VersaoDesconhecida_Recusa()
    {
        Cenario cenario = Cenario.ComVersaoBase(
            schemaVersion: "9.9",
            capacidades: [new CapacidadeCodec("1.1", TemEncoder: true, TemDecoder: true, MotivoDaRecusa: null)]);

        Result<RetificacaoEmCursoDto> resultado = await cenario.ExecutarAsync();

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(
            "EnvelopeCodec.VersaoDesconhecida",
            "a recusa é NOMEADA: quem abre precisa distinguir uma versão que o sistema não conhece de uma que ele conhece e não reidrata");
    }

    [Fact(DisplayName = "Abrir sobre uma base reidratável (1.1) devolve a sessão com o ETag")]
    public async Task Abrir_BaseReidratavel_Aceita()
    {
        Cenario cenario = Cenario.ComVersaoBase(
            schemaVersion: "1.1",
            capacidades: [new CapacidadeCodec("1.1", TemEncoder: true, TemDecoder: true, MotivoDaRecusa: null)]);

        Result<RetificacaoEmCursoDto> resultado = await cenario.ExecutarAsync();

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        RetificacaoEmCursoDto dto = resultado.Value!;
        dto.Revisao.Should().Be(1);
        dto.Motivo.Should().Be("Correção do prazo");
        dto.ETag.Should().Be($"\"{dto.Id}:1\"");
        cenario.Processo.Status.Should().Be(StatusProcesso.Publicado, "CA-10: abrir não muda o status");
    }

    [Fact(DisplayName = "Abrir num processo inexistente devolve 404 — e não toca no registro de codecs")]
    public async Task Abrir_ProcessoInexistente_NaoEncontrado()
    {
        IProcessoSeletivoRepository repositorio = Substitute.For<IProcessoSeletivoRepository>();
        repositorio.ObterParaMutacaoAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns((ProcessoSeletivo?)null);
        IRegistroCodecsEnvelope registro = Substitute.For<IRegistroCodecsEnvelope>();

        Result<RetificacaoEmCursoDto> resultado = await AbrirRetificacaoCommandHandler.Handle(
            new AbrirRetificacaoCommand(Guid.CreateVersion7(), "Correção"),
            repositorio,
            registro,
            Substitute.For<ISelecaoUnitOfWork>(),
            UsuarioAutenticado(),
            new RelogioFixo(Agora),
            CancellationToken.None);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("ProcessoSeletivo.NaoEncontrado");
        _ = registro.DidNotReceive().Capacidades;
    }

    [Fact(DisplayName = "Abrir num processo nunca publicado (sem versão corrente) recusa a transição")]
    public async Task Abrir_SemVersaoCorrente_Recusa()
    {
        ProcessoSeletivo processo = NovoProcessoConforme();
        IProcessoSeletivoRepository repositorio = Substitute.For<IProcessoSeletivoRepository>();
        repositorio.ObterParaMutacaoAsync(processo.Id, Arg.Any<CancellationToken>()).Returns(processo);
        repositorio.ObterVersaoAtualAsync(processo.Id, Arg.Any<CancellationToken>())
            .Returns((VersaoConfiguracao?)null);

        Result<RetificacaoEmCursoDto> resultado = await AbrirRetificacaoCommandHandler.Handle(
            new AbrirRetificacaoCommand(processo.Id, "Correção"),
            repositorio,
            Substitute.For<IRegistroCodecsEnvelope>(),
            Substitute.For<ISelecaoUnitOfWork>(),
            UsuarioAutenticado(),
            new RelogioFixo(Agora),
            CancellationToken.None);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("ProcessoSeletivo.TransicaoInvalida");
    }

    // ══════════════════════════════════════════════════════════════════════════════

    private sealed class Cenario
    {
        public required ProcessoSeletivo Processo { get; init; }

        public required IProcessoSeletivoRepository Repositorio { get; init; }

        public required IRegistroCodecsEnvelope Registro { get; init; }

        public static Cenario ComVersaoBase(string schemaVersion, IReadOnlyList<CapacidadeCodec> capacidades)
        {
            ProcessoSeletivo processo = NovoProcessoConforme();
            VersaoConfiguracao versao = processo.Publicar(
                NovosDados(), Bytes, schemaVersion, "canonical-json/sha256@v1", HashFixo, "user-sub-1",
                new RelogioFixo(Agora)).Value!;
            processo.DequeueDomainEvents();

            IProcessoSeletivoRepository repositorio = Substitute.For<IProcessoSeletivoRepository>();
            repositorio.ObterParaMutacaoAsync(processo.Id, Arg.Any<CancellationToken>()).Returns(processo);
            repositorio.ObterVersaoAtualAsync(processo.Id, Arg.Any<CancellationToken>()).Returns(versao);

            IRegistroCodecsEnvelope registro = Substitute.For<IRegistroCodecsEnvelope>();
            registro.Capacidades.Returns(capacidades);

            return new Cenario { Processo = processo, Repositorio = repositorio, Registro = registro };
        }

        public Task<Result<RetificacaoEmCursoDto>> ExecutarAsync() => AbrirRetificacaoCommandHandler.Handle(
            new AbrirRetificacaoCommand(Processo.Id, "Correção do prazo"),
            Repositorio,
            Registro,
            Substitute.For<ISelecaoUnitOfWork>(),
            UsuarioAutenticado(),
            new RelogioFixo(Agora),
            CancellationToken.None);
    }

    private static IUserContext UsuarioAutenticado()
    {
        IUserContext contexto = Substitute.For<IUserContext>();
        contexto.UserId.Returns("user-sub-1");
        return contexto;
    }

    private static DadosEdital NovosDados() => DadosEdital.Criar(
        "001/2026", new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 31), Guid.CreateVersion7()).Value!;

    private static ProcessoSeletivo NovoProcessoConforme()
    {
        ProcessoSeletivo processo = ProcessoSeletivo.Criar("PS 2026 — SiSU", TipoProcesso.SiSU);

        processo.DefinirEtapas(
            [EtapaProcesso.Criar("Prova Objetiva", CaraterEtapa.Classificatoria, peso: 1m, ordem: 1)],
            PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        processo.DefinirOfertaAtendimento(
            OfertaAtendimentoEspecializado.Criar([], [], []).Value!, PrecondicaoIfMatch.Ausente)
            .IsSuccess.Should().BeTrue();

        ModalidadeSelecionada modalidade = ModalidadeSelecionada.Criar(
            modalidadeOrigemId: Guid.CreateVersion7(),
            codigo: "AC",
            descricao: null,
            naturezaLegal: NaturezaLegalModalidade.Ampla,
            composicaoVagas: ComposicaoVagasModalidade.ResidualDoVo,
            composicaoOrigemCodigo: null,
            regraRemanejamento: RegraRemanejamentoModalidade.Nenhuma,
            remanejamentoDestino: null,
            remanejamentoPar: null,
            remanejamentoFallback: null,
            criteriosCumulativos: [],
            acaoQuandoIndeferido: null,
            baseLegal: "Res. Unifesspa 532/2021").Value!;

        processo.DefinirDistribuicaoVagas(
            [ConfiguracaoDistribuicaoVagas.Criar(
                ofertaCursoOrigemId: Guid.CreateVersion7(),
                voBase: 40,
                pr: 1m,
                regraDistribuicao: ReferenciaRegra.Criar(RegraDistribuicaoVagasCodigo.Institucional, "v1", HashFixo).Value!,
                referenciaDemografica: null,
                modalidades: [modalidade]).Value!],
            PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        processo.DefinirClassificacao(
            ConfiguracaoClassificacao.Criar(
                regraCalculo: ReferenciaRegra.Criar(RegraCalculoCodigo.ClassificacaoImportada, "v1", HashFixo).Value!,
                regraArredondamento: null,
                casasArredondamento: null,
                regraOrdemAlocacao: ReferenciaRegra.Criar(RegraOrdemAlocacaoCodigo.AlocacaoOpcoesRn04, "v1", HashFixo).Value!,
                nOpcoesAlocacao: 1,
                regrasEliminacao: []).Value!,
            PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        return processo;
    }

    private sealed class RelogioFixo(DateTimeOffset instante) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => instante;
    }
}
