namespace Unifesspa.UniPlus.Selecao.IntegrationTests.Inscricoes;

using System.Text.Json;

using AwesomeAssertions;

using Unifesspa.UniPlus.Infrastructure.Core.Persistence;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Services;
using Unifesspa.UniPlus.Selecao.Application.DTOs.Comprovantes;
using Unifesspa.UniPlus.Selecao.Application.Services.Comprovantes;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Errors;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Repositories;
using Unifesspa.UniPlus.Selecao.IntegrationTests.TestSupport;
using Unifesspa.UniPlus.Testes.Compartilhado;

using static Unifesspa.UniPlus.Selecao.IntegrationTests.Inscricoes.CenarioDeInscricaoPublicada;
using static Unifesspa.UniPlus.Selecao.IntegrationTests.Inscricoes.ProcessoComMatrizDaLeiDeTeste;
using static Unifesspa.UniPlus.Selecao.IntegrationTests.Inscricoes.ProcessosDeInscricaoDeTeste;
using static Unifesspa.UniPlus.Selecao.IntegrationTests.Inscricoes.PublicacaoDoProcessoDeTeste;

/// <summary>O envio da inscrição sobre a versão publicada, as modalidades gravadas e o comprovante emitido, relido e montado para exibir, inclusive o da correção.</summary>
[Trait("Category", "Integration")]
[Collection(InscricaoSobreProcessoPublicadoColecao.Nome)]
public sealed class EnvioEComprovanteDaInscricaoTests
{
    private readonly CenarioDeInscricaoPublicada _cenario;

    public EnvioEComprovanteDaInscricaoTests(InscricaoDbFixture fixture) => _cenario = new CenarioDeInscricaoPublicada(fixture);

    [Fact(DisplayName = "O envio sobre a versão publicada grava as modalidades em que o candidato concorre e o comprovante relido as traz com o nome congelado")]
    public async Task Envio_SobreAVersaoPublicada_GravaAsModalidadesEComprova()
    {
        await using UniPlusDbContext db = _cenario.CriarDbContext();
        ProcessoSeletivo processo = ProcessoComAMatrizNormativa(nameof(Envio_SobreAVersaoPublicada_GravaAsModalidadesEComprova));
        (Guid processoId, _) = await PublicarEDivulgarAsync(db, processo, OpcoesDaOrigemEscolar());
        string etag = await _cenario.IniciarComoFernandaAsync(db, processoId);

        Result<ReciboDaInscricaoDto> envio = await _cenario.EnviarAsync(
            db, processoId, PedidoDeEnvio(processo, RespostasDaOrigemEDasCotas(OrigemEscolar.SomentePublica, ep: true, ppi: true, renda: true)),
            etag, "00000101");

        envio.IsSuccess.Should().BeTrue(envio.Error?.Message ?? string.Join("; ", envio.Errors.Select(static e => $"{e.Field}: {e.Error.Message}")));
        (Inscricao enviada, IReadOnlyList<ComprovanteInscricao> comprovantes) = await _cenario.RelerAsync(processoId);
        await ConferirModalidadesGravadasAsync(enviada.Id);
        ComprovanteInscricao comprovante = comprovantes.Should().ContainSingle().Subject;
        ConferirModalidadesDoComprovante(comprovante);
    }

    [Fact(DisplayName = "O envio sobre a versão publicada sem nenhuma modalidade aplicável é recusado, sem comprovante, e a inscrição continua em rascunho")]
    public async Task Envio_SemModalidade_RecusaEMantemORascunho()
    {
        await using UniPlusDbContext db = _cenario.CriarDbContext();
        ProcessoSeletivo processo = ProcessoQueDerivaAModalidade(nameof(Envio_SemModalidade_RecusaEMantemORascunho));
        (Guid processoId, _) = await PublicarEDivulgarAsync(db, processo);
        string etag = await _cenario.IniciarComoFernandaAsync(db, processoId);

        Result<ReciboDaInscricaoDto> envio = await EnviarSemModalidadeAsync(db, processo, processoId, etag);

        envio.Errors.Should().ContainSingle().Which.Field.Should().Be("CONCORRER_PCD");
        envio.Errors[0].Error.Code.Should().Be(InscricaoErrorCodes.NenhumaModalidade);
        (Inscricao inscricao, IReadOnlyList<ComprovanteInscricao> comprovantes) = await _cenario.RelerAsync(processoId);
        inscricao.Estado.Situacao.Should().Be(SituacaoInscricao.Rascunho);
        comprovantes.Should().BeEmpty();
    }

    [Fact(DisplayName = "O comprovante lido do banco sai montado com a versão fixada, o CPF mascarado, a posição e o prazo da correção aberto")]
    public async Task ComprovanteGravado_SaiMontadoParaExibir()
    {
        await using UniPlusDbContext db = _cenario.CriarDbContext();
        ProcessoSeletivo processo = ProcessoComAMatrizNormativa(nameof(ComprovanteGravado_SaiMontadoParaExibir));
        (Guid processoId, _) = await PublicarEDivulgarAsync(db, processo, OpcoesDaOrigemEscolar());
        string etag = await _cenario.IniciarComoFernandaAsync(db, processoId);
        (Result<ReciboDaInscricaoDto> primeiro, _) = await EnviarECorrigirAsync(db, processo, processoId, etag, "00000104");

        await using UniPlusDbContext leitura = _cenario.CriarDbContext();
        (ComprovanteDaInscricaoDto antigo, ComprovanteDaInscricaoDto atual) = await MontarComprovantesAsync(leitura, primeiro.Value!.InscricaoId);

        ConferirComprovantesMontados(antigo, atual, processoId);
    }

    [Fact(DisplayName = "A correção que troca as modalidades emite um comprovante novo, com hash próprio, e o anterior fica como estava")]
    public async Task Correcao_QueTrocaAsModalidades_EmiteComprovanteNovo()
    {
        await using UniPlusDbContext db = _cenario.CriarDbContext();
        ProcessoSeletivo processo = ProcessoComAMatrizNormativa(nameof(Correcao_QueTrocaAsModalidades_EmiteComprovanteNovo));
        (Guid processoId, _) = await PublicarEDivulgarAsync(db, processo, OpcoesDaOrigemEscolar());
        string etag = await _cenario.IniciarComoFernandaAsync(db, processoId);
        (Result<ReciboDaInscricaoDto> primeiro, Result<ReciboDaInscricaoDto> correcao) = await EnviarECorrigirAsync(db, processo, processoId, etag, "00000103");
        (Inscricao corrigida, IReadOnlyList<ComprovanteInscricao> comprovantes) = await _cenario.RelerAsync(processoId);
        comprovantes.Should().HaveCount(2);
        (primeiro.Value!.NumeroDoComprovante, correcao.Value!.NumeroDoComprovante).Should().Be((1, 2), "o recibo diz a posição do comprovante emitido");
        ConferirCorrecaoDosComprovantes(comprovantes);
        (await _cenario.LinhasDaModalidadeAsync(corrigida.Id)).Select(static l => l.Codigo).Should().Equal("AC");
    }
    private async Task ConferirModalidadesGravadasAsync(Guid inscricaoId)
    {
        (await _cenario.LinhasDaModalidadeAsync(inscricaoId)).Should().Equal(
            (0, (int)OrigemDaResposta.Derivada, "AC"), (1, (int)OrigemDaResposta.Derivada, "LB_EP"), (2, (int)OrigemDaResposta.Derivada, "LB_PPI"),
            (3, (int)OrigemDaResposta.Derivada, "LI_EP"), (4, (int)OrigemDaResposta.Derivada, "LI_PPI"));
    }

    private static void ConferirModalidadesDoComprovante(ComprovanteInscricao comprovante)
    {
        comprovante.Modalidades!.Select(static m => (m.Codigo, m.Nome)).Should().Equal(
            ("AC", "Ampla concorrência"), ("LB_EP", "Modalidade LB_EP"), ("LB_PPI", "Modalidade LB_PPI"),
            ("LI_EP", "Modalidade LI_EP"), ("LI_PPI", "Modalidade LI_PPI"));
        comprovante.IntegridadeConfere().Should().BeTrue();
    }

    private Task<Result<ReciboDaInscricaoDto>> EnviarSemModalidadeAsync(UniPlusDbContext db, ProcessoSeletivo processo, Guid processoId, string etag)
    {
        return _cenario.EnviarAsync(
            db, processoId,
            PedidoDeEnvio(processo, new Dictionary<string, JsonElement>(StringComparer.Ordinal) { ["CONCORRER_PCD"] = JsonSerializer.SerializeToElement(false) }),
            etag, "00000102");
    }

    private async Task<(Result<ReciboDaInscricaoDto> Primeiro, Result<ReciboDaInscricaoDto> Correcao)> EnviarECorrigirAsync(UniPlusDbContext db, ProcessoSeletivo processo, Guid processoId, string etag, string numero)
    {
        Result<ReciboDaInscricaoDto> primeiro = await _cenario.EnviarAsync(
            db, processoId, PedidoDeEnvio(processo, RespostasDaOrigemEDasCotas(OrigemEscolar.SomentePublica, ep: true, ppi: false, renda: false)),
            etag, numero);
        primeiro.IsSuccess.Should().BeTrue(primeiro.Error?.Message);
        (Inscricao enviada, _) = await _cenario.RelerAsync(processoId);
        Result<ReciboDaInscricaoDto> correcao = await _cenario.EnviarAsync(
            db, processoId, PedidoDeEnvio(processo, RespostasDaOrigemEDasCotas(OrigemEscolar.SomentePublica, ep: false, ppi: false, renda: false)),
            enviada.ETag, numero);
        correcao.IsSuccess.Should().BeTrue(correcao.Error?.Message);
        return (primeiro, correcao);
    }

    private static MontadorDoComprovante Montador(UniPlusDbContext leitura)
    {
        ProcessoSeletivoRepository processos = ProcessoSeletivoRepositoryDeTeste.Criar(leitura);
        return new(
            processos, RegistroCodecs, new PrazoDaCorrecaoDoComprovante(new CertameDivulgadoRepository(leitura), processos, RegistroCodecs, Fuso(), DentroDoPeriodo),
            new CancelamentoDoComprovante(Fuso()));
    }

    private static void ConferirComprovantesMontados(ComprovanteDaInscricaoDto antigo, ComprovanteDaInscricaoDto atual, Guid processoId)
    {
        (antigo.Inscricao.Numero, antigo.Inscricao.Total, antigo.Inscricao.Vale, antigo.Inscricao.NumeroSeguinte).Should().Be((1, 2, false, (int?)2));
        (atual.Inscricao.Numero, atual.Inscricao.Vale, atual.Inscricao.NumeroAnterior, atual.Inscricao.NumeroSeguinte).Should().Be((2, true, (int?)1, (int?)null));
        antigo.Inscricao.PrazoDaCorrecao.Should().BeNull("o comprovante substituído não mostra prazo");
        atual.Inscricao.PrazoDaCorrecao!.Aberto.Should().BeTrue("a inscrição enviada, dentro do período, pode ser corrigida");
        atual.Candidato.CpfMascarado.Should().Be(PersonasDeTeste.FernandaDrumond.Mascarado);
        atual.Processo.ProcessoSeletivoId.Should().Be(processoId);
        atual.Processo.NomeDoProcesso.Should().NotBeNullOrWhiteSpace();
        atual.Inscricao.Envio.NumeroInscricao.Should().Be("00000104");
        System.Text.Json.JsonSerializer.Serialize(atual).Should().NotContain(PersonasDeTeste.FernandaDrumond.Valor, "o CPF só sai mascarado");
    }

    private static void ConferirCorrecaoDosComprovantes(IReadOnlyList<ComprovanteInscricao> comprovantes)
    {
        ComprovanteInscricao anterior = comprovantes[0];
        ComprovanteInscricao novo = comprovantes[1];
        anterior.Modalidades!.Select(static m => m.Codigo).Should().Equal("AC", "LI_EP");
        anterior.IntegridadeConfere().Should().BeTrue();
        novo.Modalidades!.Select(static m => m.Codigo).Should().Equal("AC");
        novo.HashIntegridade.Should().NotBe(anterior.HashIntegridade);
        novo.IntegridadeConfere().Should().BeTrue();
    }

    private async Task<(ComprovanteDaInscricaoDto Antigo, ComprovanteDaInscricaoDto Atual)> MontarComprovantesAsync(UniPlusDbContext leitura, Guid inscricaoId)
    {
        InscricaoRepository repositorio = _cenario.NovoRepositorioDeInscricao(leitura);
        MontadorDoComprovante montador = Montador(leitura);
        Inscricao inscricao = (await repositorio.ObterPorIdAsync(inscricaoId))!;

        ComprovanteDaInscricaoDto antigo = (await montador.MontarAsync(inscricao, (await _cenario.NovoRepositorioDeComprovantes(leitura).ObterComprovanteAsync(inscricao.Id, 1))!, CancellationToken.None)).Value!;
        ComprovanteDaInscricaoDto atual = (await montador.MontarAsync(inscricao, (await _cenario.NovoRepositorioDeComprovantes(leitura).ObterComprovanteAsync(inscricao.Id, 2))!, CancellationToken.None)).Value!;

        return (antigo, atual);
    }
}
