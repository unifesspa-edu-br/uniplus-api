namespace Unifesspa.UniPlus.Autorizacao.Domain.UnitTests;

using AwesomeAssertions;

using Unifesspa.UniPlus.Autorizacao.Apoio;
using Unifesspa.UniPlus.Autorizacao.Domain.Entities;
using Unifesspa.UniPlus.Autorizacao.Domain.Enums;
using Unifesspa.UniPlus.Autorizacao.Domain.Errors;
using Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Domain.Enums;
using Unifesspa.UniPlus.Kernel.Results;

public sealed class PerfilContencaoTests
{
    private const string Consultar = "selecao:processos:consultar";
    private const string Descricao = "Descrição válida.";
    private const string Motivo = PerfisDeTeste.MotivoDaContencao;

    private static readonly DateTimeOffset Agora = PerfisDeTeste.Instante;

    private static readonly Identidade Leandro = new("https://idp.test/realms/unifesspa", "leandro.viana", "Leandro Viana");

    private readonly CatalogoMutavelDeTeste _catalogo = new();

    [Fact(DisplayName = "Suspender abre a suspensão e o evento, com o papel e o motivo normalizado")]
    public void Suspender_AbreSuspensaoEEvento()
    {
        Perfil perfil = Comum();

        Result resultado = Suspender(perfil, "  " + Motivo + "  ");

        resultado.IsSuccess.Should().BeTrue();
        perfil.SuspensaoAberta!.Motivo.Should().Be(Motivo);
        perfil.SuspensaoAberta.Papel.Should().Be(EtapaDaVersao.GestaoDeAcessos);
        perfil.SuspensaoAberta.SuspensaPor.Should().Be(PerfisDeTeste.Gestor);
        EventoDaContencao evento = perfil.Eventos.Single();
        evento.Tipo.Should().Be(TipoDoEventoDaContencao.Suspensao);
        evento.VersaoVigenteId.Should().Be(perfil.VersaoVigente!.Id);
        evento.PerfilProtegido.Should().BeFalse();
    }

    [Fact(DisplayName = "Suspender registra no evento que o perfil era protegido")]
    public void Suspender_PerfilProtegido_RegistraNoEvento()
    {
        Perfil perfil = Protegido();

        Suspender(perfil, Motivo);

        perfil.Eventos.Single().PerfilProtegido.Should().BeTrue();
    }

    [Fact(DisplayName = "Suspender recusa, nesta ordem: governança, motivo e perfil já suspenso")]
    public void Suspender_Recusas()
    {
        Perfil governanca = PerfisDeTeste.DeGovernanca("Gestor de acessos", Descricao, [Consultar], _catalogo);
        Perfil suspenso = Comum();
        Suspender(suspenso, Motivo);

        Codigo(Suspender(governanca, Motivo)).Should().Be(PerfilErrorCodes.GovernancaImutavel);
        Codigo(Suspender(Comum(), "   ")).Should().Be(PerfilErrorCodes.MotivoInvalido);
        Codigo(Suspender(suspenso, Motivo)).Should().Be(PerfilErrorCodes.JaSuspenso);
        suspenso.Suspensoes.Should().HaveCount(1);
    }

    [Fact(DisplayName = "Desativar inativa o perfil, abre a desativação e o evento, e repetir não muda nada")]
    public void Desativar_InativaERepeticaoNaoMuda()
    {
        Perfil perfil = Comum();

        PerfisDeTeste.Desativar(perfil, _catalogo).IsSuccess.Should().BeTrue();
        PerfisDeTeste.Desativar(perfil, _catalogo).IsSuccess.Should().BeTrue();

        perfil.Ativo.Should().BeFalse();
        perfil.Desativacoes.Should().HaveCount(1);
        perfil.DesativacaoAberta!.Motivo.Should().Be(Motivo);
        perfil.Eventos.Should().ContainSingle(e => e.Tipo == TipoDoEventoDaContencao.Desativacao);
    }

    [Fact(DisplayName = "Desativar recusa perfil de governança e motivo vazio")]
    public void Desativar_Recusas()
    {
        Perfil governanca = PerfisDeTeste.DeGovernanca("Gestor de acessos", Descricao, [Consultar], _catalogo);
        Perfil comum = Comum();

        Codigo(PerfisDeTeste.Desativar(governanca, _catalogo)).Should().Be(PerfilErrorCodes.GovernancaImutavel);
        Codigo(comum.Desativar(PerfisDeTeste.Gestor, null, Agora, _catalogo)).Should().Be(PerfilErrorCodes.MotivoInvalido);
        comum.Ativo.Should().BeTrue();
    }

    [Fact(DisplayName = "Reativar por outra pessoa ativa o perfil e fecha a desativação com o motivo e o evento")]
    public void Reativar_PorOutraPessoa_Ativa()
    {
        Perfil perfil = Comum();
        PerfisDeTeste.Desativar(perfil, _catalogo);

        Result resultado = PerfisDeTeste.Reativar(perfil, _catalogo);

        resultado.IsSuccess.Should().BeTrue();
        perfil.Ativo.Should().BeTrue();
        perfil.DesativacaoAberta.Should().BeNull();
        perfil.Desativacoes.Single().MotivoDaReativacao.Should().Be(Motivo);
        perfil.Eventos.Select(e => e.Tipo).Should().Equal(TipoDoEventoDaContencao.Desativacao, TipoDoEventoDaContencao.Reativacao);
    }

    [Fact(DisplayName = "Reativar por quem desativou é recusado, e reativar o perfil ativo não muda nada")]
    public void Reativar_Recusas()
    {
        Perfil perfil = Comum();
        PerfisDeTeste.Desativar(perfil, _catalogo);
        Perfil ativo = Comum();

        Codigo(perfil.Reativar(PerfisDeTeste.Gestor, Motivo, Agora, _catalogo)).Should().Be(PerfilErrorCodes.ReativacaoPorQuemDesativou);
        perfil.Ativo.Should().BeFalse();
        ativo.Reativar(PerfisDeTeste.Gestor, Motivo, Agora, _catalogo).IsSuccess.Should().BeTrue();
        ativo.Eventos.Should().BeEmpty();
    }

    [Fact(DisplayName = "Reativar sem motivo é recusado")]
    public void Reativar_SemMotivo_Recusa()
    {
        Perfil perfil = Comum();
        PerfisDeTeste.Desativar(perfil, _catalogo);

        Codigo(perfil.Reativar(PerfisDeTeste.ProtecaoDeDados, " ", Agora, _catalogo)).Should().Be(PerfilErrorCodes.MotivoInvalido);
        perfil.Ativo.Should().BeFalse();
    }

    [Fact(DisplayName = "No perfil sem dados protegidos, a decisão da gestão fecha a suspensão")]
    public void Retomar_NaoProtegido_GestaoFecha()
    {
        Perfil perfil = Comum();
        Suspender(perfil, Motivo);

        Decidir(perfil, PerfisDeTeste.ProtecaoDeDados, EtapaDaVersao.GestaoDeAcessos).IsSuccess.Should().BeTrue();

        perfil.SuspensaoAberta.Should().BeNull();
        EventoDaContencao retomada = perfil.Eventos[^1];
        retomada.Tipo.Should().Be(TipoDoEventoDaContencao.Retomada);
        retomada.Origem.Should().Be(OrigemDaRetomada.DecisaoDaEtapa);
        retomada.DecisaoId.Should().Be(perfil.Suspensoes.Single().Decisoes.Single().Id);
    }

    [Theory(DisplayName = "No perfil protegido, as duas etapas fecham a suspensão em qualquer ordem")]
    [InlineData(true)]
    [InlineData(false)]
    public void Retomar_Protegido_DuasEtapasEmQualquerOrdem(bool gestaoPrimeiro)
    {
        Perfil perfil = Protegido();
        Suspender(perfil, Motivo);
        ((Identidade, EtapaDaVersao) primeira, (Identidade, EtapaDaVersao) segunda) = OrdemDasEtapas(gestaoPrimeiro);

        Decidir(perfil, primeira.Item1, primeira.Item2).IsSuccess.Should().BeTrue();
        perfil.SuspensaoAberta.Should().NotBeNull();
        perfil.EtapasPendentesDaRetomada(_catalogo).Should().Equal(segunda.Item2);
        Decidir(perfil, segunda.Item1, segunda.Item2).IsSuccess.Should().BeTrue();

        perfil.SuspensaoAberta.Should().BeNull();
        perfil.Suspensoes.Single().Decisoes.Should().HaveCount(2).And.OnlyContain(d => d.PerfilProtegidoNaDecisao);
    }

    [Fact(DisplayName = "Quem suspendeu não decide a retomada")]
    public void Retomar_PorQuemSuspendeu_Recusa()
    {
        Perfil perfil = Comum();
        Suspender(perfil, Motivo);

        Codigo(Decidir(perfil, PerfisDeTeste.Gestor, EtapaDaVersao.GestaoDeAcessos)).Should().Be(PerfilErrorCodes.RetomadaPorQuemSuspendeu);
        perfil.SuspensaoAberta.Should().NotBeNull();
    }

    [Fact(DisplayName = "A etapa da proteção de dados não cabe no perfil sem dados protegidos")]
    public void Retomar_EtapaDeProtecaoEmPerfilComum_Recusa()
    {
        Perfil perfil = Comum();
        Suspender(perfil, Motivo);

        Codigo(Decidir(perfil, Leandro, EtapaDaVersao.ProtecaoDeDados)).Should().Be(PerfilErrorCodes.EtapaInvalida);
    }

    [Fact(DisplayName = "A mesma etapa não se decide duas vezes, e a mesma pessoa não decide as duas etapas")]
    public void Retomar_EtapaRepetidaEMesmaPessoa_Recusam()
    {
        Perfil perfil = Protegido();
        Suspender(perfil, Motivo);
        Decidir(perfil, PerfisDeTeste.ProtecaoDeDados, EtapaDaVersao.GestaoDeAcessos);

        Codigo(Decidir(perfil, Leandro, EtapaDaVersao.GestaoDeAcessos)).Should().Be(PerfilErrorCodes.EtapaDaRetomadaJaDecidida);
        Codigo(Decidir(perfil, PerfisDeTeste.ProtecaoDeDados, EtapaDaVersao.ProtecaoDeDados)).Should().Be(PerfilErrorCodes.MesmoDecisorNasDuasEtapas);
        perfil.Suspensoes.Single().Decisoes.Should().HaveCount(1);
    }

    [Fact(DisplayName = "Decidir a retomada sem suspensão ou sem motivo é recusado")]
    public void Retomar_SemSuspensaoOuSemMotivo_Recusa()
    {
        Perfil perfil = Comum();
        Suspender(Protegido(), Motivo);

        Codigo(Decidir(perfil, PerfisDeTeste.ProtecaoDeDados, EtapaDaVersao.GestaoDeAcessos)).Should().Be(PerfilErrorCodes.NaoSuspenso);
        Suspender(perfil, Motivo);
        Codigo(Decidir(perfil, PerfisDeTeste.ProtecaoDeDados, EtapaDaVersao.GestaoDeAcessos, motivo: " ")).Should().Be(PerfilErrorCodes.MotivoInvalido);
    }

    [Fact(DisplayName = "Reavaliar sem suspensão aberta não faz nada")]
    public void Reavaliar_SemSuspensao_NaoFaz()
    {
        Perfil perfil = Comum();

        bool fechou = perfil.ReavaliarRetomada(new OrigemDaReavaliacao.PelaReclassificacao(Consultar), _catalogo, Agora);

        fechou.Should().BeFalse();
        perfil.Eventos.Should().BeEmpty();
    }

    [Fact(DisplayName = "Reavaliar fecha a suspensão quando a gestão decidiu e o perfil deixou de ser protegido, com a origem")]
    public void Reavaliar_PerfilDeixaDeSerProtegido_Fecha()
    {
        Perfil perfil = Protegido();
        Suspender(perfil, Motivo);
        Decidir(perfil, PerfisDeTeste.ProtecaoDeDados, EtapaDaVersao.GestaoDeAcessos);
        _catalogo.SensibilidadeDosDados = ClassificacaoDeProtecao.Interno;

        bool fechou = perfil.ReavaliarRetomada(new OrigemDaReavaliacao.PelaReclassificacao(CatalogoMutavelDeTeste.Dados), _catalogo, Agora);

        fechou.Should().BeTrue();
        EventoDaContencao retomada = perfil.Eventos[^1];
        retomada.Origem.Should().Be(OrigemDaRetomada.Reclassificacao);
        retomada.PermissaoCodigo.Should().Be(CatalogoMutavelDeTeste.Dados);
        retomada.Ator.Should().Be(AtorDoEventoDaContencao.Sistema);
    }

    [Fact(DisplayName = "Reavaliar não fecha enquanto falta uma etapa exigida")]
    public void Reavaliar_FaltandoEtapa_NaoFecha()
    {
        Perfil perfil = Protegido();
        Suspender(perfil, Motivo);
        Decidir(perfil, PerfisDeTeste.ProtecaoDeDados, EtapaDaVersao.GestaoDeAcessos);

        perfil.ReavaliarRetomada(new OrigemDaReavaliacao.PelaReclassificacao(CatalogoMutavelDeTeste.Dados), _catalogo, Agora)
            .Should().BeFalse();
        perfil.SuspensaoAberta.Should().NotBeNull();
    }

    [Fact(DisplayName = "A troca de vigência reavalia a retomada com a versão que entrou")]
    public void TrocarVigencia_ReavaliaARetomada()
    {
        Perfil perfil = PerfisDeTeste.Vigente("Analista", Descricao, [Consultar, CatalogoMutavelDeTeste.Dados], _catalogo);
        Suspender(perfil, Motivo);
        Decidir(perfil, PerfisDeTeste.ProtecaoDeDados, EtapaDaVersao.GestaoDeAcessos);

        PerfisDeTeste.Trocar(perfil, [Consultar], _catalogo);

        perfil.SuspensaoAberta.Should().BeNull();
        EventoDaContencao retomada = perfil.Eventos[^1];
        retomada.Origem.Should().Be(OrigemDaRetomada.TrocaDeVigencia);
        retomada.VersaoQueEntrouId.Should().Be(perfil.VersaoVigente!.Id);
    }

    [Fact(DisplayName = "A trilha segue a ordem dos fatos: cada registro tem posição local crescente")]
    public void Trilha_PosicoesLocaisCrescem()
    {
        Perfil perfil = Protegido();
        Suspender(perfil, Motivo);
        Decidir(perfil, PerfisDeTeste.ProtecaoDeDados, EtapaDaVersao.GestaoDeAcessos);
        Decidir(perfil, Leandro, EtapaDaVersao.ProtecaoDeDados);
        PerfisDeTeste.Desativar(perfil, _catalogo);

        PosicoesDaTrilha(perfil).Should().BeInAscendingOrder().And.OnlyHaveUniqueItems();
    }

    [Fact(DisplayName = "As etapas exigidas: a gestão sempre, a proteção de dados só no perfil protegido")]
    public void EtapasExigidas_PorProtecao()
    {
        SuspensaoDoPerfil.EtapasExigidas(false).Should().Equal(EtapaDaVersao.GestaoDeAcessos);
        SuspensaoDoPerfil.EtapasExigidas(true).Should().Equal(EtapaDaVersao.GestaoDeAcessos, EtapaDaVersao.ProtecaoDeDados);
    }

    [Fact(DisplayName = "O perfil é protegido quando a composição em vigor tem permissão pessoal ou acima")]
    public void Protegido_PelaSensibilidadeDaComposicao()
    {
        Comum().Protegido(_catalogo).Should().BeFalse();
        Protegido().Protegido(_catalogo).Should().BeTrue();
    }

    private Perfil Comum() => PerfisDeTeste.Vigente("Analista", Descricao, [Consultar], _catalogo);

    private Perfil Protegido() => PerfisDeTeste.Vigente("Analista de dados", Descricao, [Consultar, CatalogoMutavelDeTeste.Dados], _catalogo);

    private Result Suspender(Perfil perfil, string? motivo) =>
        perfil.Suspender(PerfisDeTeste.Gestor, EtapaDaVersao.GestaoDeAcessos, motivo, Agora, _catalogo);

    private Result Decidir(Perfil perfil, Identidade decisor, EtapaDaVersao etapa, string? motivo = Motivo) =>
        perfil.DecidirRetomada(new ContextoDaRetomada(decisor, DecisorTemConcessao: true, etapa, motivo, _catalogo, Agora));

    private static string Codigo(Result resultado) => resultado.Errors.Single().Error.Code;

    // A gestão de acessos e a proteção de dados, na ordem pedida.
    private static ((Identidade, EtapaDaVersao) Primeira, (Identidade, EtapaDaVersao) Segunda) OrdemDasEtapas(bool gestaoPrimeiro)
    {
        (Identidade, EtapaDaVersao) gestao = (PerfisDeTeste.ProtecaoDeDados, EtapaDaVersao.GestaoDeAcessos);
        (Identidade, EtapaDaVersao) protecao = (Leandro, EtapaDaVersao.ProtecaoDeDados);

        return gestaoPrimeiro ? (gestao, protecao) : (protecao, gestao);
    }

    // As posições locais da trilha, na ordem em que os fatos aconteceram.
    private static int[] PosicoesDaTrilha(Perfil perfil)
    {
        SuspensaoDoPerfil suspensao = perfil.Suspensoes.Single();

        return
        [
            suspensao.OrdinalLocal, perfil.Eventos[0].OrdinalLocal, suspensao.Decisoes[0].OrdinalLocal, suspensao.Decisoes[1].OrdinalLocal,
            perfil.Eventos[1].OrdinalLocal, perfil.Desativacoes.Single().OrdinalLocal, perfil.Eventos[2].OrdinalLocal,
        ];
    }
}
