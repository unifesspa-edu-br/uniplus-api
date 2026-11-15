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
        Guid processoId;
        DadosEdital dados;
        string etag;
        await using (UniPlusDbContext db = _cenario.CriarDbContext())
        {
            (processoId, dados) = await PublicarEDivulgarAsync(db, v1);
            etag = await _cenario.IniciarComoFernandaAsync(db, processoId);
            Result<InscricaoDto> comAntiga = await _cenario.SalvarAsync(
                db, processoId,
                new InscricaoRequest(null, new Dictionary<string, JsonElement>(StringComparer.Ordinal) { ["ANTIGA"] = JsonSerializer.SerializeToElement("resposta") }, null, null),
                etag);
            comAntiga.IsSuccess.Should().BeTrue(comAntiga.Error?.Message);
            etag = comAntiga.Value!.ETag;
        }

        // A retificação fechada e ainda sem publicidade não é adotada: o rascunho continua na versão divulgada.
        VersaoConfiguracao semPublicidade;
        await using (UniPlusDbContext db = _cenario.CriarDbContext())
        {
            semPublicidade = await GravarRetificacaoAsync(db, processoId, v2, dados);
        }

        Guid versaoUm = (await _cenario.RelerAsync(processoId)).Inscricao.VersaoFormularioId;
        await using (UniPlusDbContext db = _cenario.CriarDbContext())
        {
            Result<InscricaoDto> ainda = await _cenario.ObterAsync(db, processoId, UsuarioComCpf(PersonasDeTeste.FernandaDrumond));
            ainda.Value!.VersaoFormularioId.Should().Be(versaoUm);
            ainda.Value!.Respostas.Should().ContainKey("ANTIGA");
        }

        await using (UniPlusDbContext db = _cenario.CriarDbContext())
        {
            await DivulgarAsync(db, processoId, semPublicidade);
        }

        // A leitura mostra a versão nova e não grava: a linha continua na versão em que o rascunho nasceu.
        InscricaoDto lida;
        await using (UniPlusDbContext db = _cenario.CriarDbContext())
        {
            Result<InscricaoDto> ler = await _cenario.ObterAsync(db, processoId, UsuarioComCpf(PersonasDeTeste.FernandaDrumond));
            ler.IsSuccess.Should().BeTrue(ler.Error?.Message);
            lida = ler.Value!;
        }

        (Inscricao naoGravada, _) = await _cenario.RelerAsync(processoId);
        lida.VersaoFormularioId.Should().NotBe(naoGravada.VersaoFormularioId);
        lida.Respostas.Should().NotContainKey("ANTIGA");
        naoGravada.Respostas.Should().ContainKey("ANTIGA");
        naoGravada.Revisao.Should().BeLessThan(int.Parse(lida.ETag.Trim('"').Split(':')[1], System.Globalization.CultureInfo.InvariantCulture));

        // O formulário e a avaliação avulsa do rascunho também são os da versão nova.
        await using (UniPlusDbContext db = _cenario.CriarDbContext())
        {
            Result<FormularioRenderizavelDto> formulario = await _cenario.ObterFormularioAsync(db, processoId, UsuarioComCpf(PersonasDeTeste.FernandaDrumond));
            formulario.IsSuccess.Should().BeTrue(formulario.Error?.Message);
            formulario.Value!.FatosColetados.Select(static c => c.FatoCodigo).Should().Contain("NOVA").And.NotContain("ANTIGA");
            Result<AvaliacaoDaInscricaoDto> avaliacao = await _cenario.AvaliarAsync(db, processoId, UsuarioComCpf(PersonasDeTeste.FernandaDrumond), null);
            avaliacao.Value!.Modalidades.Situacao.Should().Be("FALTA_RESPONDER", "só a versão nova deriva a modalidade da resposta a CONCORRER_PCD");
        }

        // O envio sem a pergunta nova obrigatória é recusado nela, e nada é gravado.
        InscricaoRequest semNova = PedidoDeEnvio(v2, new Dictionary<string, JsonElement>(StringComparer.Ordinal) { ["CONCORRER_PCD"] = JsonSerializer.SerializeToElement(true) });
        await using (UniPlusDbContext db = _cenario.CriarDbContext())
        {
            Result<ReciboDaInscricaoDto> recusado = await _cenario.EnviarAsync(db, processoId, semNova, lida.ETag, "00000201");
            recusado.Errors.Should().ContainSingle().Which.Field.Should().Be("NOVA");
        }

        (Inscricao aindaRascunho, _) = await _cenario.RelerAsync(processoId);
        aindaRascunho.VersaoFormularioId.Should().Be(naoGravada.VersaoFormularioId);

        // Salvar grava a adoção: a versão nova, sem a resposta à pergunta retirada.
        InscricaoDto salvo;
        await using (UniPlusDbContext db = _cenario.CriarDbContext())
        {
            Result<InscricaoDto> salvar = await _cenario.SalvarAsync(db, processoId, new InscricaoRequest(null, null, null, null), lida.ETag);
            salvar.IsSuccess.Should().BeTrue(salvar.Error?.Message);
            salvo = salvar.Value!;
        }

        (Inscricao adotada, _) = await _cenario.RelerAsync(processoId);
        adotada.VersaoFormularioId.Should().Be(lida.VersaoFormularioId);
        adotada.Respostas.Should().NotContainKey("ANTIGA");
        adotada.Situacao.Should().Be(SituacaoInscricao.Rascunho);

        // O primeiro envio, agora com a pergunta nova respondida, congela a versão.
        InscricaoRequest comNova = PedidoDeEnvio(v2, new Dictionary<string, JsonElement>(StringComparer.Ordinal)
        {
            ["NOVA"] = JsonSerializer.SerializeToElement("resposta"),
            ["CONCORRER_PCD"] = JsonSerializer.SerializeToElement(true),
        });
        await using (UniPlusDbContext db = _cenario.CriarDbContext())
        {
            (await _cenario.EnviarAsync(db, processoId, comNova, salvo.ETag, "00000202")).IsSuccess.Should().BeTrue();
        }

        // Uma retificação divulgada depois do envio não muda a versão da inscrição: a correção continua nela.
        await using (UniPlusDbContext db = _cenario.CriarDbContext())
        {
            await DivulgarRetificacaoAsync(db, processoId, v3, dados);
        }

        (Inscricao enviada, _) = await _cenario.RelerAsync(processoId);
        await using (UniPlusDbContext db = _cenario.CriarDbContext())
        {
            Result<ReciboDaInscricaoDto> correcao = await _cenario.EnviarAsync(db, processoId, comNova, enviada.ETag, "00000203");
            correcao.IsSuccess.Should().BeTrue(correcao.Error?.Message ?? string.Join("; ", correcao.Errors.Select(static e => $"{e.Field}: {e.Error.Message}")));
        }

        (Inscricao corrigida, IReadOnlyList<ComprovanteInscricao> comprovantes) = await _cenario.RelerAsync(processoId);
        corrigida.VersaoFormularioId.Should().Be(lida.VersaoFormularioId);
        comprovantes.Should().HaveCount(2);
    }
}
