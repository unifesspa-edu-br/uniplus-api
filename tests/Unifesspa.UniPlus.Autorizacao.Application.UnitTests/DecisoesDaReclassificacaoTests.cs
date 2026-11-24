namespace Unifesspa.UniPlus.Autorizacao.Application.UnitTests;

using AwesomeAssertions;

using Unifesspa.UniPlus.Authorization.Contracts;
using Unifesspa.UniPlus.Autorizacao.Application.Commands.Perfis;
using Unifesspa.UniPlus.Autorizacao.Application.Commands.Reclassificacoes;
using Unifesspa.UniPlus.Autorizacao.Application.DTOs;
using Unifesspa.UniPlus.Autorizacao.Domain.Entities;
using Unifesspa.UniPlus.Autorizacao.Domain.Errors;
using Unifesspa.UniPlus.Autorizacao.Domain.Interfaces;
using Unifesspa.UniPlus.Autorizacao.Domain.UnitTests;
using Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Domain.Enums;
using Unifesspa.UniPlus.Kernel.Domain.ValueObjects;

using static Unifesspa.UniPlus.Autorizacao.Application.UnitTests.CenarioDaGovernancaDaVersao;
using static Unifesspa.UniPlus.Autorizacao.Application.UnitTests.DoublesDePerfis;

/// <summary>
/// A decisão da liberação da permissão reclassificada, na ordem das recusas: o pedido lido, a permissão da etapa, a
/// reclassificação, o autor e a regra do agregado; e o que se grava quando a decisão passa.
/// </summary>
public sealed class DecisoesDaReclassificacaoTests
{
    private const string Codigo = "selecao:processos:consultar";

    private static readonly Identidade Paulo = new("https://idp.test/realms/unifesspa", "paulo.ramos", "Paulo Ramos");

    [Fact(DisplayName = "A etapa inválida é recusada antes de avaliar o acesso")]
    public async Task Decidir_EtapaInvalida_RecusaSemAvaliarOAcesso()
    {
        Cenario cenario = new(PerfisDeTeste.ProtecaoDeDados, [Protecao]);

        GravacaoDaOperacao<DecisaoDaReclassificacaoDto> gravacao = await cenario.DecidirAsync(Cenario.Pedido(etapa: "outra"));

        gravacao.Resultado.Error!.Code.Should().Be(ReclassificacaoErrorCodes.EtapaInvalida);
        cenario.Verificador.Gravados.Should().BeEmpty();
    }

    [Fact(DisplayName = "O resultado inválido e o alvo inválido são recusados antes de avaliar o acesso, nessa ordem")]
    public async Task Decidir_ResultadoOuAlvoInvalido_RecusaSemAvaliarOAcesso()
    {
        Cenario cenario = new(PerfisDeTeste.ProtecaoDeDados, [Protecao]);

        GravacaoDaOperacao<DecisaoDaReclassificacaoDto> semResultado = await cenario.DecidirAsync(Cenario.Pedido(resultado: "talvez", todas: true, concessoes: [Guid.NewGuid()]));
        GravacaoDaOperacao<DecisaoDaReclassificacaoDto> semAlvo = await cenario.DecidirAsync(Cenario.Pedido(todas: true, concessoes: [Guid.NewGuid()]));

        semResultado.Resultado.Error!.Code.Should().Be(ReclassificacaoErrorCodes.ResultadoInvalido);
        semAlvo.Resultado.Error!.Code.Should().Be(ReclassificacaoErrorCodes.AlvoInvalido);
        cenario.Verificador.Gravados.Should().BeEmpty();
    }

    [Fact(DisplayName = "Sem a permissão da etapa a recusa é de permissão, mesmo para a reclassificação que não existe, e a trilha registra a negativa")]
    public async Task Decidir_SemPermissaoDaEtapa_RecusaAntesDeLerAReclassificacao()
    {
        Cenario cenario = new(PerfisDeTeste.ProtecaoDeDados, [Gestao]);

        GravacaoDaOperacao<DecisaoDaReclassificacaoDto> gravacao = await cenario.DecidirAsync(Cenario.Pedido(), Guid.NewGuid());

        gravacao.Resultado.Error!.Code.Should().Be(ReclassificacaoErrorCodes.SemPermissaoNaEtapa);
        TrilhaDeAcesso.Do(cenario.Verificador).Should().ContainSingle().Which.Resultado.Should().Be(ResultadoDoAcesso.Negado);
    }

    [Fact(DisplayName = "A reclassificação que não existe é recusada com a permissão já avaliada")]
    public async Task Decidir_ReclassificacaoInexistente_NaoEncontrada()
    {
        Cenario cenario = new(PerfisDeTeste.ProtecaoDeDados, [Protecao]);

        GravacaoDaOperacao<DecisaoDaReclassificacaoDto> gravacao = await cenario.DecidirAsync(Cenario.Pedido(), Guid.NewGuid());

        gravacao.Resultado.Error!.Code.Should().Be(ReclassificacaoErrorCodes.NaoEncontrada);
        TrilhaDeAcesso.Do(cenario.Verificador).Should().ContainSingle().Which.Resultado.Should().Be(ResultadoDoAcesso.Permitido);
    }

    [Fact(DisplayName = "O token sem autor identificado é recusado depois da reclassificação, e a reclassificação inexistente vem antes")]
    public async Task Decidir_SemAutorIdentificado_RecusaDepoisDaReclassificacao()
    {
        Cenario cenario = new(null, [Protecao]);

        GravacaoDaOperacao<DecisaoDaReclassificacaoDto> existente = await cenario.DecidirAsync(Cenario.Pedido());
        GravacaoDaOperacao<DecisaoDaReclassificacaoDto> inexistente = await cenario.DecidirAsync(Cenario.Pedido(), Guid.NewGuid());

        existente.Resultado.Error!.Code.Should().Be(PerfilErrorCodes.AutorNaoIdentificado);
        inexistente.Resultado.Error!.Code.Should().Be(ReclassificacaoErrorCodes.NaoEncontrada);
        cenario.Unidade.Gravacoes.Should().Be(0);
    }

    [Fact(DisplayName = "A decisão que passa grava as liberações e as bases novas numa gravação só")]
    public async Task Decidir_Liberada_GravaLiberacoesEBases()
    {
        Cenario cenario = new(PerfisDeTeste.ProtecaoDeDados, [Protecao]);

        GravacaoDaOperacao<DecisaoDaReclassificacaoDto> gravacao = await cenario.DecidirAsync(Cenario.Pedido());

        gravacao.Resultado.IsSuccess.Should().BeTrue();
        gravacao.Resultado.Value!.Decididas.Should().Equal(cenario.Concessao.Id);
        gravacao.Resultado.Value.Liberadas.Should().Equal(cenario.Concessao.Id);
        cenario.Reclassificacoes.Liberacoes.Should().ContainSingle().Which.ConcessaoId.Should().Be(cenario.Concessao.Id);
        cenario.Reclassificacoes.Bases.Should().ContainSingle().Which.ConcessaoId.Should().Be(cenario.Concessao.Id);
        cenario.Reclassificacoes.Adicionadas.Select(a => a.GetType()).Should().Equal(typeof(LiberacaoDaReclassificacao), typeof(BaseDaPermissaoNaConcessao));
        cenario.Unidade.Gravacoes.Should().Be(1);
    }

    [Fact(DisplayName = "A decisão recusada pelo agregado não grava liberação nem base")]
    public async Task Decidir_RecusadaPeloAgregado_NaoGrava()
    {
        Cenario cenario = new(PerfisDeTeste.ProtecaoDeDados, [Protecao]);

        GravacaoDaOperacao<DecisaoDaReclassificacaoDto> gravacao = await cenario.DecidirAsync(Cenario.Pedido(resultado: "MANTIDA"));

        gravacao.Resultado.Error!.Code.Should().Be(ReclassificacaoErrorCodes.MotivoInvalido);
        cenario.Reclassificacoes.Liberacoes.Should().BeEmpty();
        cenario.Reclassificacoes.Bases.Should().BeEmpty();
        cenario.Unidade.Gravacoes.Should().Be(0);
    }

    private sealed class Cenario
    {
        private readonly DecisoesDaReclassificacao _decisoes;

        public Cenario(Identidade? ator, string[] permitidas)
        {
            Perfil perfil = PerfisDeTeste.Vigente("Análise de atendimento", "Analisa os pedidos.", [Codigo], new CatalogoDeTeste());
            RelogioFixo tempo = new(PerfisDeTeste.Instante.AddHours(1));
            Concessao = ConcessaoBuilder.DoPerfil(perfil, new CatalogoDeTeste()).Para(Paulo).Construir();
            Reclassificacao = ReclassificacaoDaPermissao.Registrar(
                Guid.NewGuid(), perfil.Id, CodigoDePermissao.De(Codigo), new MudancaDaPermissao(ClassificacaoDeProtecao.Interno, ClassificacaoDeProtecao.Pessoal, false, false), tempo.GetUtcNow(), 0);
            Reclassificacoes.Reclassificacoes.Add(Reclassificacao);
            Reclassificacoes.ConcessoesComAPermissao.Add(new ConcessaoComAPermissao(Concessao, new BaseDaConcessao(ClassificacaoDeProtecao.Interno, false)));
            Reclassificacoes.VersoesComDocumentoLido.Add(Concessao.VersaoId!.Value);
            Verificador = new VerificadorDeTeste(ator?.ContaId ?? "sem-autor", permitidas);
            _decisoes = new DecisoesDaReclassificacao(
                new OperacaoSobreOPerfil(new PerfisNaMemoria([perfil]), Unidade, tempo),
                Reclassificacoes,
                new ConcessoesDaReclassificacao(Reclassificacoes),
                new AutoriaDaOperacao(new Usuario(ator), tempo, new ConcessoesNaMemoria()));
        }

        public Concessao Concessao { get; }

        public ReclassificacaoDaPermissao Reclassificacao { get; }

        public ReclassificacoesNaMemoria Reclassificacoes { get; } = new();

        public UnidadeDeTrabalho Unidade { get; } = new();

        public VerificadorDeTeste Verificador { get; }

        public static DecidirReclassificacaoRequest Pedido(string? etapa = "PROTECAO_DE_DADOS", string? resultado = "LIBERADA", bool todas = true, IReadOnlyList<Guid>? concessoes = null) =>
            new(etapa, resultado, concessoes, todas, null);

        public Task<GravacaoDaOperacao<DecisaoDaReclassificacaoDto>> DecidirAsync(DecidirReclassificacaoRequest pedido, Guid? reclassificacaoId = null) =>
            _decisoes.DecidirAsync(reclassificacaoId ?? Reclassificacao.Id, pedido, Verificador, CancellationToken.None);
    }
}
