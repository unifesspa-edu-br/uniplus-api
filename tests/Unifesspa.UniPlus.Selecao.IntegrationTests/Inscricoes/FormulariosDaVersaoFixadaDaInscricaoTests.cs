namespace Unifesspa.UniPlus.Selecao.IntegrationTests.Inscricoes;

using System.Text.Json;

using AwesomeAssertions;

using Unifesspa.UniPlus.Infrastructure.Core.Persistence;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Application.DTOs;
using Unifesspa.UniPlus.Selecao.Application.DTOs.Comprovantes;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

using static Unifesspa.UniPlus.Selecao.IntegrationTests.Inscricoes.CenarioDeInscricaoPublicada;
using static Unifesspa.UniPlus.Selecao.IntegrationTests.Inscricoes.ProcessosDeInscricaoDeTeste;
using static Unifesspa.UniPlus.Selecao.IntegrationTests.Inscricoes.PublicacaoDoProcessoDeTeste;

/// <summary>
/// A inscrição que responde mais de um formulário obrigatório: a leitura devolve todos os da versão fixada, e o envio
/// exige as respostas de todos eles.
/// </summary>
[Trait("Category", "Integration")]
[Collection(InscricaoSobreProcessoPublicadoColecao.Nome)]
public sealed class FormulariosDaVersaoFixadaDaInscricaoTests
{
    private const string Complemento = "COMPLEMENTO_DA_INSCRICAO";
    private const string CampoDoComplemento = "CAMPO_DO_COMPLEMENTO";

    private readonly CenarioDeInscricaoPublicada _cenario;

    public FormulariosDaVersaoFixadaDaInscricaoTests(InscricaoDbFixture fixture) => _cenario = new CenarioDeInscricaoPublicada(fixture);

    [Fact(DisplayName = "Depois do envio, a leitura devolve os dois formulários da versão fixada, sem o que a retificação divulgada traz")]
    public async Task InscricaoEnviada_LeOsFormulariosDaVersaoFixada_ApesarDaRetificacaoDivulgada()
    {
        string nome = nameof(InscricaoEnviada_LeOsFormulariosDaVersaoFixada_ApesarDaRetificacaoDivulgada);
        ProcessoSeletivo fixada = ProcessoComFormulariosObrigatorios(nome, (Complemento, CampoDoComplemento));
        (Guid processoId, DadosEdital dados) = await EnviarComOsDoisFormulariosAsync(fixada, "00000301");
        await DivulgarRetificacaoComTerceiroFormularioAsync(nome, processoId, dados);

        IReadOnlyList<FormularioApresentadoDoProcessoDto> formularios = await LerFormulariosAsync(processoId);

        formularios.Select(static f => f.Codigo).Should().Equal(Complemento, FormularioDeTeste.CodigoDaInscricao);
        formularios.Single(static f => f.Codigo == Complemento).Formulario.Coleta.Itens.Select(static c => c.FatoCodigo)
            .Should().Contain(CampoDoComplemento).And.NotContain("CAMPO_NOVO_DO_COMPLEMENTO");
    }

    [Fact(DisplayName = "Enviar só as respostas do formulário de inscrição é recusado citando o campo do outro formulário obrigatório")]
    public async Task Envio_SemAsRespostasDoOutroFormulario_RecusaCitandoOCampoDele()
    {
        ProcessoSeletivo processo = ProcessoComFormulariosObrigatorios(
            nameof(Envio_SemAsRespostasDoOutroFormulario_RecusaCitandoOCampoDele), (Complemento, CampoDoComplemento));
        await using UniPlusDbContext db = _cenario.CriarDbContext();
        (Guid processoId, _) = await PublicarEDivulgarAsync(db, processo);
        string etag = await _cenario.IniciarComoFernandaAsync(db, processoId);

        Result<ReciboDaInscricaoDto> recusado = await _cenario.EnviarAsync(db, processoId, PedidoDeEnvio(processo, new Dictionary<string, JsonElement>(StringComparer.Ordinal)), etag, "00000302");

        recusado.IsFailure.Should().BeTrue();
        recusado.Errors.Should().Contain(static e => e.Field == CampoDoComplemento);
    }

    private async Task DivulgarRetificacaoComTerceiroFormularioAsync(string nome, Guid processoId, DadosEdital dados)
    {
        ProcessoSeletivo retificada = ProcessoComFormulariosObrigatorios(nome, (Complemento, "CAMPO_NOVO_DO_COMPLEMENTO"), ("TERCEIRO_FORMULARIO", "CAMPO_DO_TERCEIRO"));
        await using UniPlusDbContext db = _cenario.CriarDbContext();
        await DivulgarRetificacaoAsync(db, processoId, retificada, dados);
    }

    private async Task<(Guid ProcessoId, DadosEdital Dados)> EnviarComOsDoisFormulariosAsync(ProcessoSeletivo processo, string numero)
    {
        await using UniPlusDbContext db = _cenario.CriarDbContext();
        (Guid processoId, DadosEdital dados) = await PublicarEDivulgarAsync(db, processo);
        string etag = await _cenario.IniciarComoFernandaAsync(db, processoId);
        InscricaoRequest pedido = PedidoDeEnvio(processo, new Dictionary<string, JsonElement>(StringComparer.Ordinal)
        { [CampoDoComplemento] = JsonSerializer.SerializeToElement("resposta") });
        Result<ReciboDaInscricaoDto> enviado = await _cenario.EnviarAsync(db, processoId, pedido, etag, numero);
        enviado.IsSuccess.Should().BeTrue(enviado.Error?.Message ?? string.Join("; ", enviado.Errors.Select(static e => $"{e.Field}: {e.Error.Message}")));
        return (processoId, dados);
    }

    private async Task<IReadOnlyList<FormularioApresentadoDoProcessoDto>> LerFormulariosAsync(Guid processoId)
    {
        await using UniPlusDbContext db = _cenario.CriarDbContext();
        Result<IReadOnlyList<FormularioApresentadoDoProcessoDto>> formularios =
            await _cenario.ObterFormulariosAsync(db, processoId, UsuarioComCpf(PersonasDeTeste.FernandaDrumond));
        formularios.IsSuccess.Should().BeTrue(formularios.Error?.Message);
        return formularios.Value!;
    }
}
