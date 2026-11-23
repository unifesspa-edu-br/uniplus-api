namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.ConferenciaDaConfiguracao;

using System.Reflection;
using System.Text;
using System.Text.Json.Nodes;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Errors;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.ConferenciaDaConfiguracao;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Cronograma;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Edicao;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Publicacao;
using Unifesspa.UniPlus.Selecao.Domain.UnitTests.Entities;
using Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.Edicao;
using Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.FasesEEtapas;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

/// <summary>
/// Quem corrige lê primeiro a causa que orienta: a identificação do processo vem antes da conformidade por item, e o que o
/// processo diz de si vem antes do conjunto que as declarações formam. A conferência do estado da transição, que é conflito, vem
/// antes de toda a conferência da configuração.
/// </summary>
public sealed class OrdemDasRecusasDaConferenciaTests
{
    private static readonly DateTimeOffset Agora = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);

    private const string CodigoDaJanelaInvertida = "FaseCronograma.JanelaInvertida";

    private static FaseCronograma FaseComJanelaInvertida() => FaseCronograma.Reidratar(
        Guid.CreateVersion7(),
        new OrigemCanonicaDaFase(Guid.CreateVersion7(), "FASE_DA_JANELA", OrigemDataFase.NoEdital, false, false, false, false),
        new DecisoesDeCronogramaDaFase(99, Agora, Agora.AddDays(-1), UnidadesDeTeste.Ceps),
        new PublicacoesDaFase([], false, null),
        []);

    private static void LimparIdentificador(ProcessoSeletivo processo) =>
        typeof(ProcessoSeletivo).GetProperty(nameof(ProcessoSeletivo.IdentificadorLegivel), BindingFlags.Instance | BindingFlags.Public)!
            .SetValue(processo, null);

    private static string[] Codigos(ProcessoSeletivo processo) =>
        [.. ConfiguracaoAptaACongelar.Conferir(processo.Id, processo.Declaracao(), CadastrosDeTeste.Para(processo)).Errors.Select(static causa => causa.Error.Code)];

    [Fact(DisplayName = "A identificação do processo vem antes da conformidade por item")]
    public void Identificacao_VemAntesDaConformidadePorItem()
    {
        ProcessoSeletivo semEtapas = ProcessoSeletivo.Criar(
            "Processo incompleto", TiposDeProcessoDeTeste.SiSU, OrigemCandidatos.InscricaoPropria, Guid.CreateVersion7(),
            UnidadeAdministradoraSnapshot.Criar("CEPS", "CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!,
            LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!, identificadorLegivel: IdentificadoresDeTeste.Novo());
        LimparIdentificador(semEtapas);

        string[] codigos = Codigos(semEtapas);

        codigos.Should().StartWith(ProcessoSeletivoErrorCodes.IdentificadorLegivelAusente);
        codigos.Should().Contain("ProcessoSeletivo.ConformidadeInsuficiente");
        Array.IndexOf(codigos, "ProcessoSeletivo.ConformidadeInsuficiente").Should().BeGreaterThan(0);
    }

    [Fact(DisplayName = "A recusa de uma fase que o processo declarou vem antes da ordem repetida do conjunto")]
    public void DeclaracaoDaFase_VemAntesDoConjunto()
    {
        ProcessoSeletivo processo = ProcessoConformeFactory.Criar();
        EtapaProcesso existente = processo.Etapas.First();
        EtapaProcesso repetida = ApoioDeEtapaReidratada.DeProvaObjetiva(
            Guid.CreateVersion7(), "Outra prova", existente.Ordem, existente.FaseCodigo!, 1m);
        ProcessoCarregadoDoBanco.Etapa(processo, repetida);
        ProcessoCarregadoDoBanco.Fase(processo, FaseComJanelaInvertida());

        string[] codigos = Codigos(processo);

        codigos.Should().Contain([CodigoDaJanelaInvertida, "ProcessoSeletivo.OrdemEtapaDuplicada"]);
        Array.IndexOf(codigos, CodigoDaJanelaInvertida).Should().BeLessThan(Array.IndexOf(codigos, "ProcessoSeletivo.OrdemEtapaDuplicada"));
    }

    [Fact(DisplayName = "Publicar um processo já publicado devolve a recusa do status, mesmo com a configuração inválida")]
    public void Publicar_JaPublicadoComConfiguracaoInvalida_RecusaPeloStatus()
    {
        ProcessoSeletivo publicado = ProcessoEmEdicaoDeTeste.Publicado(out _);
        LimparIdentificador(publicado);

        Result<VersaoConfiguracao> resultado = publicado.Publicar(
            new CenarioDeCongelamento { Dados = DadosDoEdital(), Documento = new DocumentoCongelado(Bytes(), "1.1", "canonical-json/sha256@v1", new string('a', 64)), Relogio = new RelogioFixo(Agora) });

        resultado.Errors.Select(static causa => causa.Error.Code).Should().Equal("ProcessoSeletivo.TransicaoInvalida");
    }

    private static byte[] Bytes() => Encoding.UTF8.GetBytes(new JsonObject { ["status"] = "ok" }.ToJsonString());

    private static DadosEdital DadosDoEdital() => DadosEdital.Criar(
        "001/2026",
        new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.FromHours(-3)),
        new DateTimeOffset(2026, 1, 31, 23, 59, 59, TimeSpan.FromHours(-3)),
        Guid.CreateVersion7()).Value!;
}
