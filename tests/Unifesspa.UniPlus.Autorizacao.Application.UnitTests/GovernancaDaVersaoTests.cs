namespace Unifesspa.UniPlus.Autorizacao.Application.UnitTests;

using AwesomeAssertions;

using Microsoft.Extensions.DependencyInjection;

using Unifesspa.UniPlus.Authorization.Abstractions;
using Unifesspa.UniPlus.Autorizacao.Application.Commands.Janelas;
using Unifesspa.UniPlus.Autorizacao.Application.Commands.Perfis;
using Unifesspa.UniPlus.Autorizacao.Application.DTOs;
using Unifesspa.UniPlus.Autorizacao.Application.Queries.Perfis;
using Unifesspa.UniPlus.Autorizacao.Domain.Entities;
using Unifesspa.UniPlus.Autorizacao.Domain.Enums;
using Unifesspa.UniPlus.Autorizacao.Domain.Errors;
using Unifesspa.UniPlus.Kernel.Results;

using static Unifesspa.UniPlus.Autorizacao.Application.UnitTests.CenarioDaGovernancaDaVersao;
using static Unifesspa.UniPlus.Autorizacao.Application.UnitTests.DoublesDePerfis;

public sealed class GovernancaDaVersaoTests
{
    private const string A = "selecao:processos:consultar";
    private const string B = "selecao:processos:operar";
    private const string Pessoal = "selecao:dados-pessoais:consultar";
    private const string EtapaGestao = "gestao_de_acessos";
    private const string EtapaProtecao = "protecao_de_dados";

    private static readonly TitularDaPermissao Rebeca = new("https://idp.test/realms/unifesspa", "rebeca.lima", "rebeca-lima12@uniplus-test.local");

    private static DecisaoSolicitada Aprovar(Perfil perfil, string? etapa = EtapaGestao) =>
        new(perfil.Id, perfil.VersaoPendente!.Id, etapa, ResultadoDaDecisao.Aprovado, null);

    [Fact(DisplayName = "A gestão com a permissão da etapa aprova e a versão sem acréscimo protegido entra em vigor")]
    public async Task Aprovar_Gestao_EntraEmVigor()
    {
        Perfil perfil = PendenteVerificada([A]);
        CenarioDaGovernancaDaVersao cenario = new(perfil, PerfisDeTeste.Gestora, [Gestao]);

        GravacaoDaOperacao<PerfilDto> gravacao = await cenario.Aprovacoes.DecidirAsync(Aprovar(perfil), cenario.Verificador, CancellationToken.None);

        gravacao.Resultado.IsSuccess.Should().BeTrue();
        perfil.VersaoVigente.Should().NotBeNull();
        cenario.Unidade.Gravacoes.Should().Be(1);
        cenario.Verificador.Gravados.Should().ContainSingle();
    }

    [Fact(DisplayName = "Sem nenhuma das permissões da aprovação a decisão é recusada e nada é gravado")]
    public async Task Aprovar_SemPermissao_Recusa()
    {
        Perfil perfil = PendenteVerificada([A]);
        CenarioDaGovernancaDaVersao cenario = new(perfil, PerfisDeTeste.Gestora, []);

        GravacaoDaOperacao<PerfilDto> gravacao = await cenario.Aprovacoes.DecidirAsync(Aprovar(perfil), cenario.Verificador, CancellationToken.None);

        gravacao.Resultado.Errors.Single().Error.Code.Should().Be(PerfilErrorCodes.SemPermissaoNaEtapa);
        cenario.Unidade.Gravacoes.Should().Be(0);
        cenario.Verificador.Gravados.Should().ContainSingle();
        cenario.Concessoes.ConsultasDeConcessaoAtiva.Should().Be(0, "a recusa de acesso vem antes de qualquer leitura de concessão");
    }

    [Theory(DisplayName = "A etapa ausente ou que a pessoa não decide é recusada como etapa inválida")]
    [InlineData(null)]
    [InlineData("outra")]
    [InlineData(EtapaProtecao)]
    public async Task Aprovar_EtapaInvalida_Recusa(string? etapa)
    {
        Perfil perfil = PendenteVerificada([A]);
        CenarioDaGovernancaDaVersao cenario = new(perfil, PerfisDeTeste.Gestora, [Gestao]);

        GravacaoDaOperacao<PerfilDto> gravacao = await cenario.Aprovacoes.DecidirAsync(Aprovar(perfil, etapa), cenario.Verificador, CancellationToken.None);

        gravacao.Resultado.Errors.Single().Error.Code.Should().Be(PerfilErrorCodes.EtapaInvalida);
    }

    [Fact(DisplayName = "Quem criou a versão não a aprova, mesmo com a permissão")]
    public async Task Aprovar_PeloCriador_Recusa()
    {
        Perfil perfil = PendenteVerificada([A]);
        CenarioDaGovernancaDaVersao cenario = new(perfil, PerfisDeTeste.Autor, [Gestao]);

        GravacaoDaOperacao<PerfilDto> gravacao = await cenario.Aprovacoes.DecidirAsync(Aprovar(perfil), cenario.Verificador, CancellationToken.None);

        gravacao.Resultado.Errors.Single().Error.Code.Should().Be(PerfilErrorCodes.DecisorEAutor);
        perfil.VersaoVigente.Should().BeNull();
    }

    [Fact(DisplayName = "Sem identidade no token a decisão não é registrada")]
    public async Task Aprovar_SemAutor_Recusa()
    {
        Perfil perfil = PendenteVerificada([A]);
        CenarioDaGovernancaDaVersao cenario = new(perfil, null, [Gestao]);

        GravacaoDaOperacao<PerfilDto> gravacao = await cenario.Aprovacoes.DecidirAsync(Aprovar(perfil), cenario.Verificador, CancellationToken.None);

        gravacao.Resultado.Errors.Single().Error.Code.Should().Be(PerfilErrorCodes.AutorNaoIdentificado);
    }

    [Fact(DisplayName = "A versão que não existe no perfil é recusada")]
    public async Task Aprovar_VersaoInexistente_Recusa()
    {
        Perfil perfil = PendenteVerificada([A]);
        CenarioDaGovernancaDaVersao cenario = new(perfil, PerfisDeTeste.Gestora, [Gestao]);

        GravacaoDaOperacao<PerfilDto> gravacao = await cenario.Aprovacoes.DecidirAsync(
            new DecisaoSolicitada(perfil.Id, Guid.NewGuid(), EtapaGestao, ResultadoDaDecisao.Aprovado, null), cenario.Verificador, CancellationToken.None);

        gravacao.Resultado.Errors.Single().Error.Code.Should().Be(PerfilErrorCodes.VersaoNaoEncontrada);
    }

    [Fact(DisplayName = "A recusa com motivo encerra a versão pendente")]
    public async Task Recusar_ComMotivo_EncerraAPendente()
    {
        Perfil perfil = PendenteVerificada([A]);
        CenarioDaGovernancaDaVersao cenario = new(perfil, PerfisDeTeste.Gestora, [Gestao]);

        GravacaoDaOperacao<PerfilDto> gravacao = await cenario.Aprovacoes.DecidirAsync(
            new DecisaoSolicitada(perfil.Id, perfil.VersaoPendente!.Id, EtapaGestao, ResultadoDaDecisao.Recusado, "Documento sem o protocolo."),
            cenario.Verificador,
            CancellationToken.None);

        gravacao.Resultado.IsSuccess.Should().BeTrue();
        perfil.VersaoPendente.Should().BeNull();
    }

    [Fact(DisplayName = "O criador cancela a própria pendente sem a permissão de manter perfis")]
    public async Task Cancelar_PeloCriador_Cancela()
    {
        Perfil perfil = PendenteVerificada([A]);
        CenarioDaGovernancaDaVersao cenario = new(perfil, PerfisDeTeste.Autor, []);

        GravacaoDaOperacao<PerfilDto> gravacao = await cenario.Cancelamento.CancelarAsync(
            perfil.Id, perfil.VersaoPendente!.Id, "Escolhi as permissões erradas.", cenario.Verificador, CancellationToken.None);

        gravacao.Resultado.IsSuccess.Should().BeTrue();
        perfil.VersaoPendente.Should().BeNull();
        gravacao.Resultado.Value!.UltimaVersaoEncerradaSemVigor.Should().NotBeNull();
        cenario.Concessoes.ConsultasDeConcessaoAtiva.Should().Be(0, "o cancelamento só precisa de quem é a pessoa");
    }

    [Fact(DisplayName = "Quem mantém perfis cancela a pendente de outra pessoa")]
    public async Task Cancelar_PorQuemMantem_Cancela()
    {
        Perfil perfil = PendenteVerificada([A]);
        CenarioDaGovernancaDaVersao cenario = new(perfil, PerfisDeTeste.Gestora, [Manter]);

        GravacaoDaOperacao<PerfilDto> gravacao = await cenario.Cancelamento.CancelarAsync(
            perfil.Id, perfil.VersaoPendente!.Id, "A mudança foi substituída.", cenario.Verificador, CancellationToken.None);

        gravacao.Resultado.IsSuccess.Should().BeTrue();
        cenario.Verificador.Gravados.Should().ContainSingle();
    }

    [Theory(DisplayName = "Sem a permissão, cancelar a pendente de outra pessoa ou uma versão inexistente é negado")]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Cancelar_SemPermissao_Nega(bool versaoExistente)
    {
        Perfil perfil = PendenteVerificada([A]);
        CenarioDaGovernancaDaVersao cenario = new(perfil, PerfisDeTeste.Gestora, []);
        Guid versaoId = versaoExistente ? perfil.VersaoPendente!.Id : Guid.NewGuid();

        GravacaoDaOperacao<PerfilDto> gravacao = await cenario.Cancelamento.CancelarAsync(
            perfil.Id, versaoId, "Motivo qualquer.", cenario.Verificador, CancellationToken.None);

        gravacao.Resultado.Errors.Single().Error.Code.Should().Be(PerfilErrorCodes.SemPermissaoNaEtapa);
        perfil.VersaoPendente.Should().NotBeNull();
    }

    [Fact(DisplayName = "Cancelar sem identidade no token não registra nada")]
    public async Task Cancelar_SemAutor_Recusa()
    {
        Perfil perfil = PendenteVerificada([A]);
        CenarioDaGovernancaDaVersao cenario = new(perfil, null, [Manter]);

        GravacaoDaOperacao<PerfilDto> gravacao = await cenario.Cancelamento.CancelarAsync(
            perfil.Id, perfil.VersaoPendente!.Id, "Motivo qualquer.", cenario.Verificador, CancellationToken.None);

        gravacao.Resultado.Errors.Single().Error.Code.Should().Be(PerfilErrorCodes.AutorNaoIdentificado);
    }

    [Fact(DisplayName = "A remoção vale na hora e avisa quem mantém perfis, menos quem removeu")]
    public async Task Remover_Permissao_AvisaOsOutrosGestores()
    {
        Perfil perfil = PerfisDeTeste.Vigente("Análise de atendimento", "Analisa os pedidos.", [A, B], new CatalogoDeTeste());
        CenarioDaGovernancaDaVersao cenario = new(perfil, PerfisDeTeste.Gestora, [Manter], [Rebeca, Titular(PerfisDeTeste.Gestora)]);

        GravacaoDaOperacao<PerfilDto> gravacao = await cenario.Remocao.RemoverAsync(
            perfil.Id, new RemoverPermissoesRequest([B, B], "Permissão concedida por engano.", "erro_de_composicao"), CancellationToken.None);

        gravacao.Resultado.IsSuccess.Should().BeTrue();
        perfil.ComposicaoEmVigor.Should().BeEquivalentTo([A]);
        gravacao.Avisos.Should().NotBeEmpty().And.OnlyContain(a => a.Destinatario.StartsWith("rebeca", StringComparison.Ordinal));
    }

    [Fact(DisplayName = "A remoção com a pendente em andamento cancela a pendente e avisa quem a criou")]
    public async Task Remover_ComPendente_CancelaEAvisaOCriador()
    {
        Perfil perfil = PerfisDeTeste.Vigente("Análise de atendimento", "Analisa os pedidos.", [A, B], new CatalogoDeTeste());
        PerfisDeTeste.Garantir(PerfisDeTeste.CriarVersao(perfil,
            PerfisDeTeste.Conteudo(perfil.Nome, perfil.Descricao, [A, B, Pessoal], new CatalogoDeTeste()),
            PerfisDeTeste.SemContexto,
            PerfisDeTeste.AutoriaDoAutor,
            PerfisDeTeste.Instante,
            new CatalogoDeTeste()));
        CenarioDaGovernancaDaVersao cenario = new(perfil, PerfisDeTeste.Gestora, [Manter], [Rebeca]);

        GravacaoDaOperacao<PerfilDto> gravacao = await cenario.Remocao.RemoverAsync(
            perfil.Id, new RemoverPermissoesRequest([B], "Permissão concedida por engano.", null), CancellationToken.None);

        gravacao.Resultado.IsSuccess.Should().BeTrue();
        perfil.VersaoPendente.Should().BeNull();
        gravacao.Avisos.Should().HaveCountGreaterThanOrEqualTo(2);
    }

    [Theory(DisplayName = "A remoção com categoria desconhecida ou sem identidade é recusada")]
    [InlineData("inventada", true)]
    [InlineData(null, false)]
    public async Task Remover_Invalida_Recusa(string? categoria, bool comAutor)
    {
        Perfil perfil = PerfisDeTeste.Vigente("Análise de atendimento", "Analisa os pedidos.", [A, B], new CatalogoDeTeste());
        CenarioDaGovernancaDaVersao cenario = new(perfil, comAutor ? PerfisDeTeste.Gestora : null, [Manter]);

        GravacaoDaOperacao<PerfilDto> gravacao = await cenario.Remocao.RemoverAsync(
            perfil.Id, new RemoverPermissoesRequest([B], "Permissão concedida por engano.", categoria), CancellationToken.None);

        gravacao.Resultado.IsFailure.Should().BeTrue();
        cenario.Unidade.Gravacoes.Should().Be(0);
    }

    [Fact(DisplayName = "A rotina expira a pendente vencida uma só vez")]
    public async Task Expirar_PendenteVencida_ExpiraUmaVez()
    {
        Perfil perfil = PendenteVerificada([A]);
        CenarioDaGovernancaDaVersao cenario = new(
            perfil, PerfisDeTeste.Gestora, [], relogio: new RelogioFixo(PerfisDeTeste.Instante.AddDays(16)));

        int primeira = await cenario.Expiracao.ExecutarAsync(CancellationToken.None);
        int segunda = await cenario.Expiracao.ExecutarAsync(CancellationToken.None);

        primeira.Should().Be(1);
        segunda.Should().Be(0);
        perfil.VersaoPendente.Should().BeNull();
    }

    [Fact(DisplayName = "A fila da gestão lista a versão pendente com o documento verificado")]
    public async Task Fila_Gestao_ListaAPendente()
    {
        Perfil perfil = PendenteVerificada([A]);
        CenarioDaGovernancaDaVersao cenario = new(perfil, PerfisDeTeste.Gestora, [Gestao]);

        Result<IReadOnlyList<ItemDaFilaDeVersoesDto>> fila = await Fila(cenario, EtapaGestao);
        Result<IReadOnlyList<ItemDaFilaDeVersoesDto>> daProtecao = await Fila(cenario, EtapaProtecao);

        fila.Value!.Should().ContainSingle().Which.PerfilId.Should().Be(perfil.Id);
        daProtecao.Errors.Single().Error.Code.Should().Be(PerfilErrorCodes.SemPermissaoNaEtapa);
    }

    [Theory(DisplayName = "A fila de etapa desconhecida é recusada")]
    [InlineData(null)]
    [InlineData("outra")]
    public async Task Fila_EtapaInvalida_Recusa(string? etapa)
    {
        Perfil perfil = PendenteVerificada([A]);
        CenarioDaGovernancaDaVersao cenario = new(perfil, PerfisDeTeste.Gestora, [Gestao]);

        Result<IReadOnlyList<ItemDaFilaDeVersoesDto>> fila = await Fila(cenario, etapa);

        fila.Errors.Single().Error.Code.Should().Be(PerfilErrorCodes.EtapaInvalida);
    }

    [Fact(DisplayName = "Os handlers devolvem a resposta e os avisos para a cascata do Wolverine")]
    public async Task Handlers_DevolvemRespostaEAvisos()
    {
        Perfil perfil = PendenteVerificada([A]);
        CenarioDaGovernancaDaVersao cenario = new(perfil, PerfisDeTeste.Gestora, [Gestao, Manter]);
        Guid versaoId = perfil.VersaoPendente!.Id;

        (Result<PerfilDto> Resposta, IEnumerable<object> Mensagens) recusa = await GovernarVersaoDoPerfilCommandHandler.Handle(
            new RecusarVersaoDoPerfilCommand(perfil.Id, versaoId, EtapaGestao, "Sem o protocolo."), cenario.Aprovacoes, cenario.Verificador, CancellationToken.None);
        (Result<PerfilDto> Resposta, IEnumerable<object> Mensagens) cancelamento = await GovernarVersaoDoPerfilCommandHandler.Handle(
            new CancelarVersaoDoPerfilCommand(perfil.Id, versaoId, "Motivo."), cenario.Cancelamento, cenario.Verificador, CancellationToken.None);
        (Result<PerfilDto> Resposta, IEnumerable<object> Mensagens) aprovacao = await GovernarVersaoDoPerfilCommandHandler.Handle(
            new AprovarVersaoDoPerfilCommand(perfil.Id, versaoId, EtapaGestao), cenario.Aprovacoes, cenario.Verificador, CancellationToken.None);
        (Result<PerfilDto> Resposta, IEnumerable<object> Mensagens) remocao = await GovernarVersaoDoPerfilCommandHandler.Handle(
            new RemoverPermissoesDoPerfilCommand(perfil.Id, new RemoverPermissoesRequest([A], "Motivo.", null)), cenario.Remocao, SemJanelaPorMaterializar(), CancellationToken.None);

        recusa.Resposta.IsSuccess.Should().BeTrue();
        cancelamento.Resposta.IsFailure.Should().BeTrue();
        aprovacao.Resposta.IsFailure.Should().BeTrue();
        remocao.Resposta.IsFailure.Should().BeTrue();
        recusa.Mensagens.Should().BeEmpty();
    }

    private static Task<Result<IReadOnlyList<ItemDaFilaDeVersoesDto>>> Fila(CenarioDaGovernancaDaVersao cenario, string? etapa) =>
        ListarFilaDeVersoesQueryHandler.Handle(
            new ListarFilaDeVersoesQuery(etapa),
            cenario.Verificador,
            new FilaDeVersoesDaEtapa(
                cenario.Perfis,
                new Unifesspa.UniPlus.Authorization.Catalogo.CatalogoDaAplicacao([]),
                new RelogioFixo(PerfisDeTeste.Instante.AddHours(1))),
            CancellationToken.None);

    private static TitularDaPermissao Titular(Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects.Identidade pessoa) =>
        new(pessoa.Emissor, pessoa.Sub, $"{pessoa.Sub}@uniplus-test.local");

    private static MaterializacaoPrevia SemJanelaPorMaterializar()
    {
        using ServiceProvider provedor = new ServiceCollection().BuildServiceProvider();

        return new MaterializacaoPrevia(new JanelasNaMemoria(), provedor.GetRequiredService<IServiceScopeFactory>(), TimeProvider.System);
    }
}
