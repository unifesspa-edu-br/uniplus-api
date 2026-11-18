namespace Unifesspa.UniPlus.Selecao.IntegrationTests.Inscricoes;

using System.Text.Json;

using AwesomeAssertions;

using Unifesspa.UniPlus.Infrastructure.Core.Persistence;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Selecao.Application.DTOs;
using Unifesspa.UniPlus.Selecao.Application.DTOs.Comprovantes;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

using static Unifesspa.UniPlus.Selecao.IntegrationTests.Inscricoes.CenarioDeInscricaoPublicada;
using static Unifesspa.UniPlus.Selecao.IntegrationTests.Inscricoes.ProcessosDeInscricaoDeTeste;
using static Unifesspa.UniPlus.Selecao.IntegrationTests.Inscricoes.PublicacaoDoProcessoDeTeste;

/// <summary>O rascunho que adota a versão divulgada ao salvar e ao enviar, sem gravar na leitura, e o primeiro envio que congela a versão, mesmo com retificação divulgada depois.</summary>
[Trait("Category", "Integration")]
[Collection(InscricaoSobreProcessoPublicadoColecao.Nome)]
public sealed class AdocaoDaVersaoDivulgadaPeloRascunhoTests
{
    private readonly CenarioDeInscricaoPublicada _cenario;

    public AdocaoDaVersaoDivulgadaPeloRascunhoTests(InscricaoDbFixture fixture) => _cenario = new CenarioDeInscricaoPublicada(fixture);

    [Fact(DisplayName = "O rascunho adota a versão divulgada ao salvar e ao enviar, sem gravar na leitura; o primeiro envio congela a versão")]
    public async Task Rascunho_AdotaAVersaoDivulgada_EOPrimeiroEnvioACongela()
    {
        string nome = nameof(Rascunho_AdotaAVersaoDivulgada_EOPrimeiroEnvioACongela);
        ProcessoSeletivo v1 = ProcessoComUmCampo(nome, "ANTIGA", Obrigatoriedade.Nunca);
        ProcessoSeletivo v2 = ProcessoQueDerivaAModalidadeComUmCampo(nome, "NOVA");
        ProcessoSeletivo v3 = ProcessoComUmCampo(nome, "OUTRA", Obrigatoriedade.Sempre);
        (Guid processoId, DadosEdital dados) = await PublicarComRespostaAsync(v1);
        VersaoConfiguracao retificacao = await PrepararRetificacaoAsync(processoId, v2, dados);
        await ConferirVersaoAnteriorAsync(processoId);
        await DivulgarPreparadaAsync(processoId, retificacao);
        InscricaoDto lida = await ConferirLeituraDaNovaVersaoAsync(processoId);
        await ConferirFormularioEAvaliacaoAsync(processoId);
        await ConferirRecusaAsync(processoId, v2, lida.ETag);
        InscricaoDto salva = await SalvarAdocaoAsync(processoId, lida);
        await ConferirEnvioECorrecaoAsync(processoId, v2, v3, dados, lida, salva);
    }

    private async Task<(Guid ProcessoId, DadosEdital Dados)> PublicarComRespostaAsync(ProcessoSeletivo processo)
    {
        await using UniPlusDbContext db = _cenario.CriarDbContext();
        (Guid processoId, DadosEdital dados) = await PublicarEDivulgarAsync(db, processo);
        string etag = await _cenario.IniciarComoFernandaAsync(db, processoId);
        InscricaoRequest pedido = new(null, new Dictionary<string, JsonElement>(StringComparer.Ordinal)
        { ["ANTIGA"] = JsonSerializer.SerializeToElement("resposta") }, null, null);
        Result<InscricaoDto> resultado = await _cenario.SalvarAsync(db, processoId, pedido, etag);
        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        return (processoId, dados);
    }

    private async Task<VersaoConfiguracao> PrepararRetificacaoAsync(Guid processoId, ProcessoSeletivo processo, DadosEdital dados)
    {
        await using UniPlusDbContext db = _cenario.CriarDbContext();
        return await GravarRetificacaoAsync(db, processoId, processo, dados);
    }

    private async Task DivulgarPreparadaAsync(Guid processoId, VersaoConfiguracao retificacao)
    {
        await using UniPlusDbContext db = _cenario.CriarDbContext();
        await DivulgarAsync(db, processoId, retificacao);
    }

    private async Task ConferirVersaoAnteriorAsync(Guid processoId)
    {
        Guid versao = (await _cenario.RelerAsync(processoId)).Inscricao.Formulario.VersaoFormularioId;
        await using UniPlusDbContext db = _cenario.CriarDbContext();
        Result<InscricaoDto> resultado = await _cenario.ObterAsync(db, processoId, UsuarioComCpf(PersonasDeTeste.FernandaDrumond));
        resultado.Value!.Formulario.VersaoFormularioId.Should().Be(versao);
        resultado.Value.Formulario.Respostas.Should().ContainKey("ANTIGA");
    }

    private async Task<InscricaoDto> ConferirLeituraDaNovaVersaoAsync(Guid processoId)
    {
        await using UniPlusDbContext db = _cenario.CriarDbContext();
        Result<InscricaoDto> resultado = await _cenario.ObterAsync(db, processoId, UsuarioComCpf(PersonasDeTeste.FernandaDrumond));
        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        InscricaoDto lida = resultado.Value!;
        (Inscricao naoGravada, _) = await _cenario.RelerAsync(processoId);
        lida.Formulario.VersaoFormularioId.Should().NotBe(naoGravada.Formulario.VersaoFormularioId);
        lida.Formulario.Respostas.Should().NotContainKey("ANTIGA");
        naoGravada.Formulario.Preenchimento.Respostas.Should().ContainKey("ANTIGA");
        naoGravada.Revisao.Should().BeLessThan(int.Parse(lida.ETag.Trim('"').Split(':')[1], System.Globalization.CultureInfo.InvariantCulture));
        return lida;
    }

    private async Task ConferirFormularioEAvaliacaoAsync(Guid processoId)
    {
        await using UniPlusDbContext db = _cenario.CriarDbContext();
        Result<FormularioRenderizavelDto> formulario = await _cenario.ObterFormularioAsync(db, processoId, UsuarioComCpf(PersonasDeTeste.FernandaDrumond));
        formulario.IsSuccess.Should().BeTrue(formulario.Error?.Message);
        formulario.Value!.FatosColetados.Select(static c => c.FatoCodigo).Should().Contain("NOVA").And.NotContain("ANTIGA");
        Result<AvaliacaoDaInscricaoDto> avaliacao = await _cenario.AvaliarAsync(db, processoId, UsuarioComCpf(PersonasDeTeste.FernandaDrumond), null);
        avaliacao.Value!.Modalidades.Situacao.Should().Be("FALTA_RESPONDER", "só a versão nova deriva a modalidade da resposta a CONCORRER_PCD");
    }

    private async Task ConferirRecusaAsync(Guid processoId, ProcessoSeletivo processo, string etag)
    {
        Guid versaoAnterior = (await _cenario.RelerAsync(processoId)).Inscricao.Formulario.VersaoFormularioId;
        InscricaoRequest pedido = PedidoDeEnvio(processo, new Dictionary<string, JsonElement>(StringComparer.Ordinal)
        { ["CONCORRER_PCD"] = JsonSerializer.SerializeToElement(true) });
        await using UniPlusDbContext db = _cenario.CriarDbContext();
        Result<ReciboDaInscricaoDto> recusado = await _cenario.EnviarAsync(db, processoId, pedido, etag, "00000201");
        recusado.Errors.Should().ContainSingle().Which.Field.Should().Be("NOVA");
        (await _cenario.RelerAsync(processoId)).Inscricao.Formulario.VersaoFormularioId.Should().Be(versaoAnterior);
    }

    private async Task<InscricaoDto> SalvarAdocaoAsync(Guid processoId, InscricaoDto lida)
    {
        await using UniPlusDbContext db = _cenario.CriarDbContext();
        Result<InscricaoDto> resultado = await _cenario.SalvarAsync(db, processoId, new InscricaoRequest(null, null, null, null), lida.ETag);
        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        (Inscricao adotada, _) = await _cenario.RelerAsync(processoId);
        adotada.Formulario.VersaoFormularioId.Should().Be(lida.Formulario.VersaoFormularioId);
        adotada.Formulario.Preenchimento.Respostas.Should().NotContainKey("ANTIGA");
        adotada.Estado.Situacao.Should().Be(SituacaoInscricao.Rascunho);
        return resultado.Value!;
    }

    private async Task ConferirEnvioECorrecaoAsync(Guid processoId, ProcessoSeletivo v2, ProcessoSeletivo v3, DadosEdital dados, InscricaoDto lida, InscricaoDto salva)
    {
        InscricaoRequest pedido = PedidoDeEnvio(v2, new Dictionary<string, JsonElement>(StringComparer.Ordinal)
        {
            ["NOVA"] = JsonSerializer.SerializeToElement("resposta"),
            ["CONCORRER_PCD"] = JsonSerializer.SerializeToElement(true),
        });
        await EnviarEDivulgarAsync(processoId, v3, dados, salva.ETag, pedido);
        (Inscricao enviada, _) = await _cenario.RelerAsync(processoId);
        await using UniPlusDbContext db = _cenario.CriarDbContext();
        Result<ReciboDaInscricaoDto> correcao = await _cenario.EnviarAsync(db, processoId, pedido, enviada.ETag, "00000203");
        correcao.IsSuccess.Should().BeTrue(correcao.Error?.Message ?? string.Join("; ", correcao.Errors.Select(static e => $"{e.Field}: {e.Error.Message}")));
        (Inscricao corrigida, IReadOnlyList<ComprovanteInscricao> comprovantes) = await _cenario.RelerAsync(processoId);
        corrigida.Formulario.VersaoFormularioId.Should().Be(lida.Formulario.VersaoFormularioId);
        comprovantes.Should().HaveCount(2);
    }

    private async Task EnviarEDivulgarAsync(Guid processoId, ProcessoSeletivo v3, DadosEdital dados, string etag, InscricaoRequest pedido)
    {
        await using (UniPlusDbContext db = _cenario.CriarDbContext())
        {
            (await _cenario.EnviarAsync(db, processoId, pedido, etag, "00000202")).IsSuccess.Should().BeTrue();
        }

        await using UniPlusDbContext outroDb = _cenario.CriarDbContext();
        await DivulgarRetificacaoAsync(outroDb, processoId, v3, dados);
    }
}
