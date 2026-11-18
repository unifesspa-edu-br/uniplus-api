namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.Entities;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;

using static Unifesspa.UniPlus.Selecao.Domain.UnitTests.Entities.ApoioDaPublicacaoDoProcessoSeletivo;
using static Unifesspa.UniPlus.Selecao.Domain.UnitTests.Entities.ApoioDaRetificacaoDoProcessoSeletivo;

/// <summary>
/// Cobertura de <see cref="ProcessoSeletivo.Retificar"/> (congelamento dos parâmetros na publicação, UNI-REQ-0019, ADR-0101/0103/0104):
/// a retificação é uma <b>relação entre atos</b>, não um tipo — o novo ato emenda o
/// ato criador da versão corrente, sucede-a na cadeia linear e exige motivo; a versão
/// anterior fica intocada e o status permanece Publicado.
/// </summary>
/// <remarks>
/// Não há mais entidade <c>Edital</c> a inspecionar: o documento é o ato publicado, e
/// vive em <c>Publicacoes</c>. O que estes testes verificam é a única coisa que a
/// Seleção decide sobre ele — o seu identificador, e o ato que ele emenda.
/// </remarks>
public sealed class ProcessoSeletivoRetificarTests
{
    [Fact(DisplayName = "Retificar processo publicado emite um ato que emenda o ato criador da versão corrente, com motivo")]
    public void Retificar_ProcessoPublicado_EmiteAtoQueEmendaOVigente()
    {
        RelogioManual clock = Relogio();
        ProcessoSeletivo processo = NovoProcessoPublicadoHaUmMinuto(clock, out VersaoConfiguracao versaoAbertura);

        VersaoConfiguracao versao = RetificarComSucesso(processo, versaoAbertura, "Correção do prazo de inscrição", clock);

        processo.Status.Should().Be(StatusProcesso.Publicado, "retificar não altera o status Publicado");
        versao.NumeroVersao.Should().Be(2);
        versao.AtoCriadorId.Should().NotBe(versaoAbertura.AtoCriadorId, "a retificação é um ato NOVO — o passado não se muta");
        versao.AtoCriadorRetificaId.Should().Be(
            versaoAbertura.AtoCriadorId,
            "o alvo é o ato criador da versão corrente, e o servidor o infere — o cliente nunca o informa (ADR-0101)");
    }

    [Fact(DisplayName = "A retificação de um ato NÃO muda o tipo do ato: o que a marca é a relação, e nada além dela")]
    public void Retificar_NaoCarregaMarcaDeTipo_SoARelacao()
    {
        RelogioManual clock = Relogio();
        ProcessoSeletivo processo = NovoProcessoPublicadoHaUmMinuto(clock, out VersaoConfiguracao versaoAbertura);

        VersaoConfiguracao retificada = RetificarComSucesso(processo, versaoAbertura, "Correção do anexo II", clock);

        versaoAbertura.AtoCriadorRetificaId.Should().BeNull("a raiz da cadeia não emenda ninguém");
        retificada.AtoCriadorRetificaId.Should().NotBeNull("a retificação emenda — e é só isso que a distingue");
        AfirmarQueAVersaoNaoCarregaMarcaDeTipo();
    }

    [Fact(DisplayName = "Lifecycle — retificar processo ainda em rascunho é recusado (CA-09)")]
    public void Retificar_ProcessoRascunho_RecusaTransicaoInvalida()
    {
        ProcessoSeletivo processo = NovoProcessoConforme();

        Result<VersaoConfiguracao> resultado = RetificarComDadosPadrao(processo, VersaoQualquer(), "qualquer", Relogio());

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("ProcessoSeletivo.TransicaoInvalida");
        processo.DequeueDomainEvents().Should().BeEmpty();
    }

    [Fact(DisplayName = "Retificação sem motivo é recusada — a relação é o par (ato emendado, motivo), completo ou nenhum")]
    public void Retificar_SemMotivo_Recusa()
    {
        RelogioManual clock = Relogio();
        ProcessoSeletivo processo = NovoProcessoPublicadoHaUmMinuto(clock, out VersaoConfiguracao versaoAbertura);

        Result<VersaoConfiguracao> resultado = RetificarComDadosPadrao(processo, versaoAbertura, "   ", clock);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("ProcessoSeletivo.MotivoRetificacaoObrigatorio");
        processo.DequeueDomainEvents().Should().BeEmpty("nenhuma versão é criada quando a relação está incompleta");
    }

    [Fact(DisplayName = "Segunda retificação empilha na CABEÇA da cadeia (abertura→R1→R2), não na raiz (CA-06)")]
    public void Retificar_SegundaRetificacao_EmpilhaNaCabeca()
    {
        RelogioManual clock = Relogio();
        ProcessoSeletivo processo = NovoProcessoPublicadoHaUmMinuto(clock, out VersaoConfiguracao versaoAbertura);
        VersaoConfiguracao primeira = RetificarComSucesso(processo, versaoAbertura, "primeira retificação", clock);
        clock.Avancar(TimeSpan.FromMinutes(1));

        // O aviso que prorroga um prazo já prorrogado cita o aviso anterior, não o edital original (ADR-0103).
        VersaoConfiguracao segunda = RetificarComSucesso(processo, primeira, "segunda retificação", clock);

        primeira.AtoCriadorRetificaId.Should().Be(versaoAbertura.AtoCriadorId, "R1 emenda a abertura");
        segunda.AtoCriadorRetificaId.Should().Be(
            primeira.AtoCriadorId,
            "R2 emenda R1 — cada ato é emendado no máximo uma vez, e a cadeia é linear");
        segunda.NumeroVersao.Should().Be(3, "o topo da cadeia de configuração avança com a última retificação");
    }

    [Fact(DisplayName = "Retificar — versão corrente de OUTRO processo lança sem mutar o processo")]
    public void Retificar_VersaoDeOutroProcesso_Lanca()
    {
        RelogioManual clock = Relogio();
        ProcessoSeletivo processo = NovoProcessoPublicadoHaUmMinuto(clock, out _);

        Func<Result<VersaoConfiguracao>> retificar = () => RetificarComDadosPadrao(processo, VersaoQualquer(), "Correção de datas", clock);

        retificar.Should().Throw<ArgumentException>().WithParameterName("versaoAtual");
        processo.DequeueDomainEvents().Should().BeEmpty("nada é mutado quando a versão informada não é deste certame");
    }

    [Fact(DisplayName = "Relógio regredido: o id do ato da retificação NÃO ordena antes do ato que ele emenda (Guid v7, ADR-0032)")]
    public void Retificar_RelogioRegride_IdDoAtoNaoRegride()
    {
        RelogioManual clock = Relogio();
        ProcessoSeletivo processo = NovoProcessoPublicado(clock, out VersaoConfiguracao versaoAbertura);

        // Ajuste NTP em degrau: o id do ato tem de nascer do mesmo instante ancorado que a vigência.
        clock.Avancar(TimeSpan.FromMinutes(-10));
        VersaoConfiguracao retificacao = RetificarComSucesso(processo, versaoAbertura, "retificação sob relógio regredido", clock);

        AfirmarQueOIdDoAtoNaoOrdenaAntesDoEmendado(versaoAbertura, retificacao);
        retificacao.VigenteAPartirDe.Should().Be(
            versaoAbertura.VigenteAPartirDe,
            "id e vigência descrevem o MESMO instante: uma leitura do relógio, uma âncora só");
    }

    // Ordenar o alvo pelo tempo tornaria uma cadeia perfeitamente linear irretificável.
    [Fact(DisplayName = "Retificar — relógio que regride não move o alvo: quem ordena a cadeia é a versão, não o tempo")]
    public void Retificar_RelogioRegride_AlvoVemDaCadeiaDeVersoes()
    {
        RelogioManual clock = Relogio();
        ProcessoSeletivo processo = NovoProcessoPublicado(clock, out VersaoConfiguracao versaoAbertura);

        clock.Avancar(TimeSpan.FromMinutes(-10));
        VersaoConfiguracao primeira = RetificarComSucesso(processo, versaoAbertura, "primeira retificação", clock);
        primeira.VigenteAPartirDe.Should().Be(versaoAbertura.VigenteAPartirDe, "a vigência da sucessora é ancorada na anterior quando o relógio regride — nunca precede o passado");
        VersaoConfiguracao segunda = RetificarComSucesso(processo, primeira, "segunda retificação", clock);

        segunda.AtoCriadorRetificaId.Should().Be(primeira.AtoCriadorId, "o alvo é o ato criador da versão corrente — a cadeia manda, não o relógio");
        segunda.NumeroVersao.Should().Be(3);
    }

    // Se um dia voltar a existir aqui um atributo que diga "isto é uma retificação", este
    // teste é o que o denuncia: a única diferença entre as versões é a presença do ato emendado.
    private static void AfirmarQueAVersaoNaoCarregaMarcaDeTipo() =>
        typeof(VersaoConfiguracao).GetProperties()
            .Select(p => p.Name)
            .Should().NotContain(
                nome => nome.Contains("Natureza", StringComparison.OrdinalIgnoreCase)
                    || nome.Contains("TipoAto", StringComparison.OrdinalIgnoreCase),
                "a configuração congelada não carrega o tipo do ato: saber o que um ato É pertence a Publicações");

    // O Guid v7 carrega o timestamp nos 48 bits mais significativos (RFC 9562 §5.7), e é por eles
    // que o Postgres ordena `uuid` (ADR-0032); comparar `Guid` em C# não serviria, o layout é misto.
    private static void AfirmarQueOIdDoAtoNaoOrdenaAntesDoEmendado(VersaoConfiguracao emendada, VersaoConfiguracao retificacao)
    {
        DateTimeOffset instanteDaAbertura = InstanteDoGuidV7(emendada.AtoCriadorId);
        DateTimeOffset instanteDaRetificacao = InstanteDoGuidV7(retificacao.AtoCriadorId);

        instanteDaRetificacao.Should().BeOnOrAfter(
            instanteDaAbertura,
            "o ato que emenda nunca ordena antes do ato emendado — o instante do id é ancorado, como o da vigência");
    }

    private static DateTimeOffset InstanteDoGuidV7(Guid id)
    {
        Span<byte> bytes = stackalloc byte[16];
        id.TryWriteBytes(bytes, bigEndian: true, out _);

        long milissegundos = 0;
        for (int i = 0; i < 6; i++)
        {
            milissegundos = (milissegundos << 8) | bytes[i];
        }

        return DateTimeOffset.FromUnixTimeMilliseconds(milissegundos);
    }
}
