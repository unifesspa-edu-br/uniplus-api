namespace Unifesspa.UniPlus.Autorizacao.Application.UnitTests;

using AwesomeAssertions;

using Unifesspa.UniPlus.Authorization.Contracts;
using Unifesspa.UniPlus.Autorizacao.Application.Commands.Perfis;
using Unifesspa.UniPlus.Autorizacao.Application.Commands.Reclassificacoes;
using Unifesspa.UniPlus.Autorizacao.Application.DTOs;
using Unifesspa.UniPlus.Autorizacao.Application.Queries.Reclassificacoes;
using Unifesspa.UniPlus.Autorizacao.Domain.Entities;
using Unifesspa.UniPlus.Autorizacao.Domain.Enums;
using Unifesspa.UniPlus.Autorizacao.Domain.Errors;
using Unifesspa.UniPlus.Autorizacao.Domain.Interfaces;
using Unifesspa.UniPlus.Autorizacao.Domain.UnitTests;
using Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Domain.Enums;
using Unifesspa.UniPlus.Kernel.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Results;

using static Unifesspa.UniPlus.Autorizacao.Application.UnitTests.CenarioDaGovernancaDaVersao;
using static Unifesspa.UniPlus.Autorizacao.Application.UnitTests.DoublesDePerfis;

/// <summary>
/// A fila de uma etapa e o detalhe da reclassificação: quem entra na fila e em que ordem, e a ordem das recusas do detalhe
/// (a permissão, depois a reclassificação que não existe).
/// </summary>
public sealed class FilaEDetalheDaReclassificacaoTests
{
    private const string Codigo = "selecao:processos:consultar";

    private static readonly RelogioFixo Tempo = new(PerfisDeTeste.Instante.AddHours(1));

    [Fact(DisplayName = "A fila lista, na ordem das candidatas, só quem tem concessão suspensa à espera da etapa, com o nome do perfil e a contagem")]
    public async Task Fila_ListaNaOrdemSoQuemAguardaAEtapa()
    {
        Cenario cenario = new();
        ReclassificacaoDaPermissao primeira = cenario.Reclassificar(ClassificacaoDeProtecao.Interno, ClassificacaoDeProtecao.Pessoal);
        ReclassificacaoDaPermissao descida = cenario.Reclassificar(ClassificacaoDeProtecao.Pessoal, ClassificacaoDeProtecao.Interno);
        ReclassificacaoDaPermissao segunda = cenario.Reclassificar(ClassificacaoDeProtecao.Interno, ClassificacaoDeProtecao.Pessoal);

        IReadOnlyList<ItemDaFilaDeReclassificacoesDto> fila = await cenario.Fila.MontarAsync(EtapaDaVersao.ProtecaoDeDados, CancellationToken.None);

        fila.Select(i => i.Id).Should().Equal(primeira.Id, segunda.Id);
        fila.Should().OnlyContain(i => i.PerfilNome == "Análise de atendimento" && i.ConcessoesAguardando == 1 && i.Codigo == Codigo);
        fila.Select(i => i.Id).Should().NotContain(descida.Id);
    }

    [Fact(DisplayName = "A fila da etapa que nenhuma concessão aguarda fica vazia")]
    public async Task Fila_EtapaSemConcessaoAguardando_Vazia()
    {
        Cenario cenario = new();
        cenario.Reclassificar(ClassificacaoDeProtecao.Interno, ClassificacaoDeProtecao.Pessoal);

        IReadOnlyList<ItemDaFilaDeReclassificacoesDto> fila = await cenario.Fila.MontarAsync(EtapaDaVersao.GestaoDeAcessos, CancellationToken.None);

        fila.Should().BeEmpty();
    }

    [Fact(DisplayName = "O detalhe recusa quem não tem a permissão antes de ler a reclassificação, que exista ou não")]
    public async Task Detalhe_SemPermissao_RecusaAntesDeLer()
    {
        Cenario cenario = new(permitidas: []);
        ReclassificacaoDaPermissao existente = cenario.Reclassificar(ClassificacaoDeProtecao.Interno, ClassificacaoDeProtecao.Pessoal);

        Result<ReclassificacaoDto> daExistente = await cenario.ObterAsync(existente.Id);
        Result<ReclassificacaoDto> daInexistente = await cenario.ObterAsync(Guid.NewGuid());

        daExistente.Error!.Code.Should().Be(ReclassificacaoErrorCodes.SemPermissaoNaEtapa);
        daInexistente.Error!.Code.Should().Be(ReclassificacaoErrorCodes.SemPermissaoNaEtapa);
        cenario.Verificador.Gravados.Should().HaveCount(2, "cada tentativa recusada deixa a decisão na trilha");
    }

    [Fact(DisplayName = "O detalhe da reclassificação que não existe é recusado depois da permissão")]
    public async Task Detalhe_Inexistente_NaoEncontrada()
    {
        Cenario cenario = new(permitidas: [Protecao]);

        Result<ReclassificacaoDto> detalhe = await cenario.ObterAsync(Guid.NewGuid());

        detalhe.Error!.Code.Should().Be(ReclassificacaoErrorCodes.NaoEncontrada);
        TrilhaDeAcesso.Do(cenario.Verificador).Should().ContainSingle().Which.Resultado.Should().Be(ResultadoDoAcesso.Permitido);
    }

    [Fact(DisplayName = "O detalhe da reclassificação que existe é devolvido a quem decide a etapa")]
    public async Task Detalhe_Existente_Devolve()
    {
        Cenario cenario = new(permitidas: [Protecao]);
        ReclassificacaoDaPermissao existente = cenario.Reclassificar(ClassificacaoDeProtecao.Interno, ClassificacaoDeProtecao.Pessoal);

        Result<ReclassificacaoDto> detalhe = await cenario.ObterAsync(existente.Id);

        detalhe.IsSuccess.Should().BeTrue();
        detalhe.Value!.Id.Should().Be(existente.Id);
        detalhe.Value.PerfilId.Should().Be(existente.PerfilId);
    }

    [Fact(DisplayName = "O detalhe traz a mudança de classificação, a versão em uso e a concessão suspensa com o código e o rótulo de cada valor")]
    public async Task Detalhe_Existente_TrazOsValoresComCodigoERotulo()
    {
        Cenario cenario = new(permitidas: [Protecao]);
        ReclassificacaoDaPermissao existente = cenario.Reclassificar(ClassificacaoDeProtecao.Interno, ClassificacaoDeProtecao.Pessoal);

        ReclassificacaoDto detalhe = (await cenario.ObterAsync(existente.Id)).Value!;

        detalhe.Mudanca.ClassificacaoAnterior.EmPar().Should().Be(("INTERNO", "Interno"));
        detalhe.Mudanca.ClassificacaoNova.EmPar().Should().Be(("PESSOAL", "Pessoal"));
        detalhe.Versoes.Single().Situacao.EmPar().Should().Be(("VIGENTE", "Vigente"));
        detalhe.Versoes.Single().Permissoes.Single().Classificacao.EmPar().Should().Be(("INTERNO", "Interno"));
        detalhe.Concessoes.Single().EscopoTipo.EmPar().Should().Be(("INSTITUICAO", "Instituição"));
        detalhe.Concessoes.Single().EtapasExigidas.Select(static e => (e.Codigo, e.Rotulo)).Should().Equal(("PROTECAO_DE_DADOS", "Proteção de dados"));
        detalhe.Concessoes.Single().EtapasDecididas.Should().BeEmpty();
    }

    private sealed class Cenario
    {
        private readonly Perfil _perfil = PerfisDeTeste.Vigente("Análise de atendimento", "Analisa os pedidos.", [Codigo], new CatalogoDeTeste());
        private readonly ReclassificacoesNaMemoria _reclassificacoes = new();
        private readonly ConcessoesDaReclassificacao _concessoes;
        private readonly AutorizacaoDaReclassificacao _autorizacao;
        private readonly DetalheDaReclassificacao _detalhe;

        public Cenario(string[]? permitidas = null)
        {
            _concessoes = new ConcessoesDaReclassificacao(_reclassificacoes);
            _reclassificacoes.NomesDosPerfis[_perfil.Id] = _perfil.Nome;
            _reclassificacoes.VersoesEmUso.Add(_perfil.VersaoVigente!.Id);
            _reclassificacoes.ConcessoesComAPermissao.Add(new ConcessaoComAPermissao(
                ConcessaoBuilder.DoPerfil(_perfil, new CatalogoDeTeste()).Para(new Identidade("https://idp.test/realms/unifesspa", "paulo.ramos", "Paulo Ramos")).Construir(),
                new BaseDaConcessao(ClassificacaoDeProtecao.Interno, false)));
            Verificador = new VerificadorDeTeste(PerfisDeTeste.ProtecaoDeDados.ContaId, permitidas ?? [Protecao]);
            _autorizacao = new AutorizacaoDaReclassificacao(new AutoriaDaOperacao(new Usuario(PerfisDeTeste.ProtecaoDeDados), Tempo, new ConcessoesNaMemoria()));
            _detalhe = new DetalheDaReclassificacao(_reclassificacoes, new PerfisNaMemoria([_perfil]), _concessoes, Tempo);
            Fila = new FilaDeReclassificacoes(_reclassificacoes, _concessoes, Tempo);
        }

        public VerificadorDeTeste Verificador { get; }

        public FilaDeReclassificacoes Fila { get; }

        public ReclassificacaoDaPermissao Reclassificar(ClassificacaoDeProtecao anterior, ClassificacaoDeProtecao nova)
        {
            ReclassificacaoDaPermissao reclassificacao = ReclassificacaoDaPermissao.Registrar(
                Guid.NewGuid(), _perfil.Id, CodigoDePermissao.De(Codigo), new MudancaDaPermissao(anterior, nova, false, false), Tempo.GetUtcNow(), _reclassificacoes.Reclassificacoes.Count);
            _reclassificacoes.Reclassificacoes.Add(reclassificacao);

            return reclassificacao;
        }

        public Task<Result<ReclassificacaoDto>> ObterAsync(Guid id) =>
            ObterReclassificacaoQueryHandler.Handle(new ObterReclassificacaoQuery(id), Verificador, _reclassificacoes, _autorizacao, _detalhe, CancellationToken.None);
    }
}
