namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.ConferenciaDaConfiguracao;

using System.Text;
using System.Text.Json.Nodes;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Events;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.ConferenciaDaConfiguracao;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Publicacao;
using Unifesspa.UniPlus.Selecao.Domain.UnitTests.Entities;
using Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.Edicao;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

/// <summary>
/// A configuração apta a congelar só existe para o processo que foi conferido sem impedimento: quem confere um processo
/// não congela outro, e a falha da conferência entrega todas as causas em vez de uma configuração apta.
/// </summary>
public sealed class ConfiguracaoAptaACongelarTests
{
    private static readonly DateTimeOffset Agora = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);
    private static readonly byte[] Bytes = Encoding.UTF8.GetBytes(new JsonObject { ["status"] = "ok" }.ToJsonString());

    public static TheoryData<string> Transicoes => ["Publicar", "Retificar"];

    [Fact(DisplayName = "O processo conforme tem configuração apta, que carrega só o processo")]
    public void ProcessoConforme_TemConfiguracaoApta()
    {
        ProcessoSeletivo processo = ProcessoConformeFactory.Criar();

        Result<ConfiguracaoAptaACongelar> conferida = ConfiguracaoAptaACongelar.Conferir(processo.Id, processo.Declaracao(), CadastrosDeTeste.Para(processo));

        conferida.IsSuccess.Should().BeTrue(conferida.Error?.Message);
        conferida.Value!.ProcessoId.Should().Be(processo.Id);
    }

    [Fact(DisplayName = "O processo com impedimento não tem configuração apta e a falha traz as causas, cada uma no seu campo")]
    public void ProcessoComImpedimento_FalhaComAsCausas()
    {
        ProcessoSeletivo semEtapas = ProcessoSeletivo.Criar(
            "Processo incompleto", TiposDeProcessoDeTeste.Sisu, OrigemCandidatos.InscricaoPropria, Guid.CreateVersion7(),
            UnidadeAdministradoraSnapshot.Criar("CEPS", "CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!,
            LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!, identificadorLegivel: IdentificadoresDeTeste.Novo());

        Result<ConfiguracaoAptaACongelar> conferida = ConfiguracaoAptaACongelar.Conferir(semEtapas.Id, semEtapas.Declaracao(), CadastrosDeTeste.Para(semEtapas));

        conferida.IsFailure.Should().BeTrue();
        conferida.Value.Should().BeNull();
        conferida.Error!.Code.Should().Be("ProcessoSeletivo.ConformidadeInsuficiente");
        conferida.Errors.Should().OnlyContain(static causa => !string.IsNullOrEmpty(causa.Field));
    }

    [Fact(DisplayName = "Cada item de conformidade pendente é uma causa em frase, com o rótulo do item e o código dele na origem")]
    public void ItemPendente_ViraCausaEmFrase()
    {
        ProcessoSeletivo semEtapas = ProcessoSeletivo.Criar(
            "Processo incompleto", TiposDeProcessoDeTeste.Sisu, OrigemCandidatos.InscricaoPropria, Guid.CreateVersion7(),
            UnidadeAdministradoraSnapshot.Criar("CEPS", "CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!,
            LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!, identificadorLegivel: IdentificadoresDeTeste.Novo());

        IReadOnlyList<FieldError> causas = ConfiguracaoAptaACongelar.Conferir(semEtapas.Id, semEtapas.Declaracao(), CadastrosDeTeste.Para(semEtapas)).Errors
            .Where(static causa => causa.Error.Code == "ProcessoSeletivo.ConformidadeInsuficiente").ToList();

        causas.Should().HaveCountGreaterThan(1);
        causas.Should().OnlyContain(static causa => causa.Error.Message.StartsWith("Item de conformidade pendente: ", StringComparison.Ordinal)
            && causa.Error.Message.EndsWith('.') && !causa.Error.Message.EndsWith("..", StringComparison.Ordinal) && causa.Origem != null);
    }

    [Fact(DisplayName = "Conferir sem a configuração ou sem cadastros lança")]
    public void Conferir_ArgumentoNulo_Lanca()
    {
        ProcessoSeletivo processo = ProcessoConformeFactory.Criar();

        Action semConfiguracao = () => ConfiguracaoAptaACongelar.Conferir(processo.Id, null!, CadastrosDeTeste.Padrao);
        Action semCadastros = () => ConfiguracaoAptaACongelar.Conferir(processo.Id, processo.Declaracao(), null!);

        semConfiguracao.Should().Throw<ArgumentNullException>().WithParameterName("configuracao");
        semCadastros.Should().Throw<ArgumentNullException>().WithParameterName("cadastros");
    }

    [Theory(DisplayName = "A configuração apta de outro processo lança em vez de congelar")]
    [MemberData(nameof(Transicoes))]
    public void ConfiguracaoAptaDeOutroProcesso_Lanca(string transicao)
    {
        ProcessoSeletivo outro = ProcessoConformeFactory.Criar();
        ConfiguracaoAptaACongelar deOutroProcesso = ConfiguracaoAptaACongelar.Conferir(outro.Id, outro.Declaracao(), CadastrosDeTeste.Para(outro)).Value!;

        Action congelar = AcaoQueCongela(transicao, deOutroProcesso);

        congelar.Should().Throw<InvalidOperationException>();
    }

    [Fact(DisplayName = "O instante do pedido é o da vigência da versão e o do evento: o agregado não lê o relógio")]
    public void InstanteDoPedido_ValeParaAVersaoEParaOEvento()
    {
        ProcessoSeletivo processo = ProcessoConformeFactory.Criar();
        ConfiguracaoAptaACongelar apta = ConfiguracaoAptaACongelar.Conferir(processo.Id, processo.Declaracao(), CadastrosDeTeste.Para(processo)).Value!;
        DateTimeOffset instante = new(2026, 3, 10, 8, 30, 15, 123, TimeSpan.Zero);

        Result<VersaoConfiguracao> resultado = processo.Publicar(Pedido(apta, instante));

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        resultado.Value!.VigenteAPartirDe.Should().Be(instante);
        processo.DomainEvents.OfType<ProcessoPublicadoEvent>().Should().ContainSingle()
            .Which.OccurredOn.Should().Be(instante);
    }

    private static Action AcaoQueCongela(string transicao, ConfiguracaoAptaACongelar apta)
    {
        PedidoDeCongelamento pedido = Pedido(apta, Agora);

        return transicao switch
        {
            "Publicar" => () => ProcessoConformeFactory.Criar().Publicar(pedido),
            _ => () => ProcessoEmEdicaoDeTeste.Publicado(out VersaoConfiguracao versao).Retificar(pedido, versao, "Correção do prazo"),
        };
    }

    private static PedidoDeCongelamento Pedido(ConfiguracaoAptaACongelar apta, DateTimeOffset instante) => new()
    {
        Dados = DadosEdital.Criar(
            "001/2026",
            new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.FromHours(-3)),
            new DateTimeOffset(2026, 1, 31, 23, 59, 59, TimeSpan.FromHours(-3)),
            Guid.CreateVersion7()).Value!,
        Documento = new DocumentoCongelado(Bytes, "1.1", "canonical-json/sha256@v1", new string('a', 64)),
        AtorUsuarioContaId = "user-conta-123",
        Instante = instante,
        ConfiguracaoApta = apta,
    };
}
