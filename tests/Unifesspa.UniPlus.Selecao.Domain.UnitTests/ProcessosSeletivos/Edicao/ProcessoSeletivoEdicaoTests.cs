namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.Edicao;

using System.Text;
using System.Text.Json.Nodes;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.UnitTests.Entities;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

/// <summary>
/// O protocolo comum das 21 operações de escrita da configuração (ADR-0110 D4/D5, ADR-0125 §5):
/// a verificação de argumento precede a guarda, a guarda precede a regra, a recusa não muda a
/// sessão e o sucesso avança a revisão dela.
/// </summary>
public sealed class ProcessoSeletivoEdicaoTests
{
    private static readonly DateTimeOffset Agora = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);
    private static readonly string HashFixo = new('a', 64);

    public static TheoryData<string> Operacoes => [.. OperacaoDeEdicao.Todas.Select(static o => o.Nome)];

    public static TheoryData<string> OperacoesQueVerificamArgumento =>
        [.. OperacaoDeEdicao.Todas.Where(static o => o.ComArgumentoInvalido is not null).Select(static o => o.Nome)];

    [Fact(DisplayName = "O catálogo cobre as 21 operações do protocolo comum")]
    public void Catalogo_TemAs21Operacoes() => OperacaoDeEdicao.Todas.Should().HaveCount(21);

    [Theory(DisplayName = "Argumento nulo lança antes da guarda, mesmo no processo publicado sem sessão")]
    [MemberData(nameof(OperacoesQueVerificamArgumento))]
    public void ArgumentoInvalido_LancaAntesDaGuarda(string nome)
    {
        ProcessoSeletivo processo = ProcessoPublicadoSemSessao(out _);
        OperacaoDeEdicao operacao = OperacaoDeEdicao.Por(nome);

        Action chamada = () => operacao.ComArgumentoInvalido!(processo, PrecondicaoIfMatch.Ausente);

        chamada.Should().Throw<ArgumentException>();
    }

    [Fact(DisplayName = "Treze operações verificam argumento antes da guarda")]
    public void TrezeOperacoes_VerificamArgumento() =>
        OperacaoDeEdicao.Todas.Count(static o => o.ComArgumentoInvalido is not null).Should().Be(13);

    [Theory(DisplayName = "Processo publicado sem sessão recusa a operação pela guarda")]
    [MemberData(nameof(Operacoes))]
    public void PublicadoSemSessao_RecusaPelaGuarda(string nome)
    {
        ProcessoSeletivo processo = ProcessoPublicadoSemSessao(out _);

        Result resultado = OperacaoDeEdicao.Por(nome).Reaplicar(processo, PrecondicaoIfMatch.Ausente);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("ProcessoSeletivo.MutacaoPosPublicacaoBloqueada");
    }

    [Theory(DisplayName = "Precondição velha recusa a operação sem avançar a revisão da sessão")]
    [MemberData(nameof(Operacoes))]
    public void PrecondicaoVelha_RecusaSemAvancarARevisao(string nome)
    {
        ProcessoSeletivo processo = ProcessoEmSessao();
        PrecondicaoIfMatch velha = PrecondicaoIfMatch.DeTags(["\"outra-sessao:1\""]);

        Result resultado = OperacaoDeEdicao.Por(nome).Reaplicar(processo, velha);

        resultado.IsFailure.Should().BeTrue();
        processo.Rascunho!.Revisao.Should().Be(1);
    }

    [Theory(DisplayName = "Operação bem-sucedida sob sessão avança a revisão em um")]
    [MemberData(nameof(Operacoes))]
    public void SucessoSobSessao_AvancaARevisao(string nome)
    {
        ProcessoSeletivo processo = ProcessoEmSessao();
        PrecondicaoIfMatch atual = PrecondicaoIfMatch.DeTags([processo.ETagDaSessaoEditorial!]);

        Result resultado = OperacaoDeEdicao.Por(nome).Reaplicar(processo, atual);

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        processo.Rascunho!.Revisao.Should().Be(2);
    }

    [Fact(DisplayName = "Em rascunho a operação não exige precondição")]
    public void EmRascunho_NaoExigePrecondicao()
    {
        ProcessoSeletivo processo = ProcessoConformeFactory.Criar();

        Result resultado = processo.DefinirCascataRemanejamento(null, PrecondicaoIfMatch.Ausente);

        resultado.IsSuccess.Should().BeTrue();
    }

    [Fact(DisplayName = "Bônus regional nulo é válido quando o processo declara que não o aplica")]
    public void BonusNulo_EValido()
    {
        ProcessoSeletivo processo = ProcessoConformeFactory.Criar();

        processo.DefinirBonusRegional(aplica: false, bonus: null, PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();
    }

    [Fact(DisplayName = "Fatos coletados sem grupos informados são válidos")]
    public void GruposNulos_SaoValidos()
    {
        ProcessoSeletivo processo = ProcessoConformeFactory.Criar();

        processo.DefinirFatosColetados(Unifesspa.UniPlus.Regras.Formularios.FinalidadeFormulario.Inscricao, [], PrecondicaoIfMatch.Ausente, grupos: null)
            .IsSuccess.Should().BeTrue();
    }

    private static ProcessoSeletivo ProcessoPublicadoSemSessao(out VersaoConfiguracao versao)
    {
        ProcessoSeletivo processo = ProcessoConformeFactory.Criar();
        Result<VersaoConfiguracao> publicacao = processo.Publicar(
            NovosDados(), Encoding.UTF8.GetBytes(new JsonObject { ["status"] = "ok" }.ToJsonString()), "1.1",
            "canonical-json/sha256@v1", HashFixo, "user-sub-123", new RelogioFixo(Agora),
            ContextoDeContagemDePrazos.SemCalendario, FatosDeModalidadeDeTeste.DoCatalogo, TiposDeAtoUnicosDeTeste.Nenhum);
        publicacao.IsSuccess.Should().BeTrue(publicacao.Error?.Message);
        processo.DequeueDomainEvents();
        versao = publicacao.Value!;
        return processo;
    }

    private static ProcessoSeletivo ProcessoEmSessao()
    {
        ProcessoSeletivo processo = ProcessoPublicadoSemSessao(out VersaoConfiguracao versao);
        processo.AbrirRetificacao(
            "Correção do prazo", versao, identificadorDaVersaoBase: null,
            VersoesPublicadasDeTeste.SoAVigente(processo), "user-sub-1", Agora).IsSuccess.Should().BeTrue();
        return processo;
    }

    private static DadosEdital NovosDados() => DadosEdital.Criar(
        numero: "001/2026",
        periodoInscricaoInicio: new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.FromHours(-3)),
        periodoInscricaoFim: new DateTimeOffset(2026, 1, 31, 23, 59, 59, TimeSpan.FromHours(-3)),
        documentoEditalId: Guid.CreateVersion7()).Value!;
}
