namespace Unifesspa.UniPlus.Autorizacao.Domain.UnitTests;

using AwesomeAssertions;

using Unifesspa.UniPlus.Autorizacao.Apoio;
using Unifesspa.UniPlus.Autorizacao.Domain.Entities;
using Unifesspa.UniPlus.Autorizacao.Domain.Enums;
using Unifesspa.UniPlus.Autorizacao.Domain.Errors;
using Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Domain.Enums;

public sealed class PerfilAprovacaoTests
{
    private const string Consultar = "selecao:processos:consultar";
    private const string Publicar = "selecao:processos:publicar";
    private const string Dados = CatalogoMutavelDeTeste.Dados;
    private const string Descricao = "Descrição válida.";

    private static readonly DateTimeOffset Agora = PerfisDeTeste.Instante;

    private readonly CatalogoMutavelDeTeste _catalogo = new();

    [Fact(DisplayName = "Acréscimo interno: a aprovação da gestão basta e a versão entra em vigor")]
    public void Aprovar_AcrescimoInterno_UmaEtapa()
    {
        Perfil perfil = PendenteVerificada([Consultar]);

        DesfechoDaVersao desfecho = Decidir(perfil, EtapaDaVersao.GestaoDeAcessos, PerfisDeTeste.AutoriaDaGestora);

        desfecho.Concluida.Should().BeTrue();
        desfecho.Troca.Should().NotBeNull();
        perfil.VersaoVigente!.Decisoes.Should().ContainSingle().Which.Etapa.Should().Be(EtapaDaVersao.GestaoDeAcessos);
    }

    [Fact(DisplayName = "Acréscimo pessoal: a gestão aprova e a versão aguarda a proteção de dados")]
    public void Aprovar_AcrescimoPessoal_PrecisaDasDuasEtapas()
    {
        Perfil perfil = PendenteVerificada([Consultar, Dados]);

        DesfechoDaVersao gestao = Decidir(perfil, EtapaDaVersao.GestaoDeAcessos, PerfisDeTeste.AutoriaDaGestora);
        gestao.Troca.Should().BeNull();
        perfil.VersaoPendente!.AprovadaPelaGestao.Should().BeTrue();

        DesfechoDaVersao protecao = Decidir(perfil, EtapaDaVersao.ProtecaoDeDados, PerfisDeTeste.AutoriaDaProtecaoDeDados);

        protecao.Troca.Should().NotBeNull();
        perfil.VersaoPendente.Should().BeNull();
        perfil.VersaoVigente!.DecisoesDoCiclo().Select(d => d.Etapa)
            .Should().Equal(EtapaDaVersao.GestaoDeAcessos, EtapaDaVersao.ProtecaoDeDados);
    }

    [Fact(DisplayName = "A proteção de dados não decide antes da gestão")]
    public void Aprovar_ProtecaoAntesDaGestao_EtapaInvalida()
    {
        Perfil perfil = PendenteVerificada([Consultar, Dados]);

        Decidir(perfil, EtapaDaVersao.ProtecaoDeDados, PerfisDeTeste.AutoriaDaProtecaoDeDados)
            .Erro!.Code.Should().Be(PerfilErrorCodes.EtapaInvalida);
    }

    [Fact(DisplayName = "Quem criou a versão não a decide em nenhuma etapa")]
    public void Aprovar_Criador_DecisorEAutor()
    {
        Perfil perfil = PendenteVerificada([Consultar]);

        Decidir(perfil, EtapaDaVersao.GestaoDeAcessos, PerfisDeTeste.AutoriaDoAutor)
            .Erro!.Code.Should().Be(PerfilErrorCodes.DecisorEAutor);
    }

    [Fact(DisplayName = "A mesma pessoa não decide a gestão e a proteção de dados")]
    public void Aprovar_MesmaPessoaNasDuasEtapas_Recusa()
    {
        Perfil perfil = PendenteVerificada([Consultar, Dados]);
        Decidir(perfil, EtapaDaVersao.GestaoDeAcessos, PerfisDeTeste.AutoriaDaGestora);

        Decidir(perfil, EtapaDaVersao.ProtecaoDeDados, PerfisDeTeste.AutoriaDaGestora)
            .Erro!.Code.Should().Be(PerfilErrorCodes.MesmoDecisorNasDuasEtapas);
    }

    [Fact(DisplayName = "Ter concessão do perfil não impede decidir e a trilha registra o fato")]
    public void Aprovar_DecisorComJanelaAberta_RegistraNaTrilha()
    {
        Perfil perfil = PendenteVerificada([Consultar]);

        Decidir(perfil, EtapaDaVersao.GestaoDeAcessos, PerfisDeTeste.Autoria(PerfisDeTeste.Gestora, temConcessao: true))
            .Concluida.Should().BeTrue();

        DecisaoDaVersao decisao = perfil.VersaoVigente!.Decisoes.Single();
        decisao.DecisorTemConcessao.Should().BeTrue();
        decisao.DecisorEmail.Should().Be("betina.neves@unifesspa.edu.br");
        decisao.DecisorNome.Should().Be("Betina Neves");
    }

    [Fact(DisplayName = "A mesma etapa não se decide duas vezes no ciclo")]
    public void Aprovar_EtapaJaDecidida_Recusa()
    {
        Perfil perfil = PendenteVerificada([Consultar, Dados]);
        Decidir(perfil, EtapaDaVersao.GestaoDeAcessos, PerfisDeTeste.AutoriaDaGestora);

        Decidir(perfil, EtapaDaVersao.GestaoDeAcessos, PerfisDeTeste.Autoria(PerfisDeTeste.ProtecaoDeDados))
            .Erro!.Code.Should().Be(PerfilErrorCodes.EtapaJaDecidida);
    }

    [Fact(DisplayName = "Aprovar antes de o documento ser verificado é recusado")]
    public void Aprovar_SemDocumentoVerificado_Recusa()
    {
        Perfil perfil = PerfisDeTeste.Pendente("Analista", Descricao, [Consultar], _catalogo);

        Decidir(perfil, EtapaDaVersao.GestaoDeAcessos, PerfisDeTeste.AutoriaDaGestora)
            .Erro!.Code.Should().Be(PerfilErrorCodes.DocumentoNaoVerificado);
    }

    [Fact(DisplayName = "A recusa exige motivo, encerra a versão e nunca a aprova")]
    public void Recusar_EncerraAVersao()
    {
        Perfil perfil = PendenteVerificada([Consultar]);

        Decidir(perfil, EtapaDaVersao.GestaoDeAcessos, PerfisDeTeste.AutoriaDaGestora, ResultadoDaDecisao.Recusado, null)
            .Erro!.Code.Should().Be(PerfilErrorCodes.MotivoInvalido);
        Decidir(perfil, EtapaDaVersao.GestaoDeAcessos, PerfisDeTeste.AutoriaDaGestora, ResultadoDaDecisao.Recusado, "Fora do escopo.")
            .Concluida.Should().BeTrue();

        VersaoDoPerfil recusada = perfil.UltimaVersaoEncerradaSemVigor!;
        recusada.Situacao.Should().Be(SituacaoDaVersao.Recusada);
        recusada.Decisoes.Single().Motivo.Should().Be("Fora do escopo.");
        perfil.TemComposicaoEmVigor.Should().BeFalse();
    }

    [Fact(DisplayName = "A pendente vencida expira ao ser decidida e não entra em vigor")]
    public void Aprovar_AposOPrazo_Expira()
    {
        Perfil perfil = PendenteVerificada([Consultar]);
        VersaoDoPerfil versao = perfil.VersaoPendente!;
        DateTimeOffset noPrazoExato = Agora.AddDays(VersaoDoPerfil.PrazoDaAprovacaoEmDias);

        DesfechoDaVersao desfecho = Decidir(perfil, EtapaDaVersao.GestaoDeAcessos, PerfisDeTeste.AutoriaDaGestora, instante: noPrazoExato);

        desfecho.Erro!.Code.Should().Be(PerfilErrorCodes.VersaoExpirada);
        desfecho.Gravar.Should().BeTrue();
        versao.Situacao.Should().Be(SituacaoDaVersao.Expirada);
        versao.ExpiradaEm.Should().Be(noPrazoExato);
        perfil.UltimaVersaoEncerradaSemVigor.Should().BeSameAs(versao);
    }

    [Fact(DisplayName = "Toda operação sobre a versão já expirada responde versão expirada, em qualquer tentativa")]
    public void VersaoExpirada_TodaOperacaoRecusaPeloPrazo()
    {
        Perfil perfil = PendenteVerificada([Consultar]);
        Guid versaoId = perfil.VersaoPendente!.Id;
        perfil.MaterializarExpiracao(Agora.AddDays(VersaoDoPerfil.PrazoDaAprovacaoEmDias)).Should().BeTrue();

        for (int tentativa = 0; tentativa < 2; tentativa++)
        {
            Decidir(perfil, EtapaDaVersao.GestaoDeAcessos, PerfisDeTeste.AutoriaDaGestora, versaoId: versaoId)
                .Erro!.Code.Should().Be(PerfilErrorCodes.VersaoExpirada);
            Decidir(perfil, EtapaDaVersao.GestaoDeAcessos, PerfisDeTeste.AutoriaDaGestora, ResultadoDaDecisao.Recusado, "Fora do escopo.", versaoId: versaoId)
                .Erro!.Code.Should().Be(PerfilErrorCodes.VersaoExpirada);
            perfil.CancelarVersao(versaoId, PerfisDeTeste.Autor, "Motivo.", Agora).Erro!.Code.Should().Be(PerfilErrorCodes.VersaoExpirada);
            perfil.NovoDocumento(versaoId, 1024).Errors.Single().Error.Code.Should().Be(PerfilErrorCodes.VersaoExpirada);
            perfil.DocumentoParaReemitirEnvio(versaoId).Errors.Single().Error.Code.Should().Be(PerfilErrorCodes.VersaoExpirada);
            perfil.ConfirmarDocumento(versaoId, "etag", Agora).Errors.Single().Error.Code.Should().Be(PerfilErrorCodes.VersaoExpirada);
        }
    }

    [Fact(DisplayName = "No perfil desativado, a versão expirada responde versão expirada ao novo documento e à reemissão")]
    public void VersaoExpirada_PerfilDesativado_RecusaPeloPrazo()
    {
        Perfil perfil = PendenteVerificada([Consultar]);
        Guid versaoId = perfil.VersaoPendente!.Id;
        PerfisDeTeste.Desativar(perfil, _catalogo);
        perfil.MaterializarExpiracao(Agora.AddDays(VersaoDoPerfil.PrazoDaAprovacaoEmDias)).Should().BeTrue();

        for (int tentativa = 0; tentativa < 2; tentativa++)
        {
            perfil.NovoDocumento(versaoId, 1024).Errors.Single().Error.Code.Should().Be(PerfilErrorCodes.VersaoExpirada);
            perfil.DocumentoParaReemitirEnvio(versaoId).Errors.Single().Error.Code.Should().Be(PerfilErrorCodes.VersaoExpirada);
        }
    }

    [Fact(DisplayName = "O prazo conta da criação e não reinicia na troca de etapa")]
    public void Aprovar_TrocaDeEtapa_NaoReiniciaOPrazo()
    {
        Perfil perfil = PendenteVerificada([Consultar, Dados]);
        VersaoDoPerfil versao = perfil.VersaoPendente!;
        DateTimeOffset expiraEm = versao.ExpiraEm!.Value;
        Decidir(perfil, EtapaDaVersao.GestaoDeAcessos, PerfisDeTeste.AutoriaDaGestora, instante: Agora.AddDays(14));

        versao.ExpiraEm.Should().Be(expiraEm);
        Decidir(perfil, EtapaDaVersao.ProtecaoDeDados, PerfisDeTeste.AutoriaDaProtecaoDeDados, instante: expiraEm)
            .Erro!.Code.Should().Be(PerfilErrorCodes.VersaoExpirada);
    }

    [Fact(DisplayName = "A materialização do prazo expira só a pendente vencida")]
    public void MaterializarExpiracao_SoVencida()
    {
        Perfil perfil = PendenteVerificada([Consultar]);

        perfil.MaterializarExpiracao(Agora.AddDays(14)).Should().BeFalse();
        perfil.MaterializarExpiracao(Agora.AddDays(15)).Should().BeTrue();
        perfil.MaterializarExpiracao(Agora.AddDays(16)).Should().BeFalse();
    }

    [Fact(DisplayName = "Recálculo para menos: a classe deixa de exigir a proteção de dados e as aprovações caem")]
    public void Decidir_ClasseDeixaDeExigir_DescartaAsAprovacoes()
    {
        Perfil perfil = PendenteVerificada([Consultar, Dados]);
        Decidir(perfil, EtapaDaVersao.GestaoDeAcessos, PerfisDeTeste.AutoriaDaGestora);
        _catalogo.SensibilidadeDosDados = ClassificacaoDeProtecao.Interno;

        DesfechoDaVersao desfecho = Decidir(perfil, EtapaDaVersao.ProtecaoDeDados, PerfisDeTeste.AutoriaDaProtecaoDeDados);

        desfecho.Erro!.Code.Should().Be(PerfilErrorCodes.AprovacoesDescartadas);
        desfecho.Gravar.Should().BeTrue();
        desfecho.Descartadas.Should().ContainSingle();
        desfecho.Recalculo.Should().Match<RecalculoDaAprovacao>(r => r.ExigiaAntes && !r.ExigeAgora && r.CicloAnterior == 1);
        VersaoDoPerfil versao = perfil.VersaoPendente!;
        versao.ExigeProtecaoDeDados.Should().BeFalse();
        versao.CicloDeAprovacao.Should().Be(2);
        versao.DecisoesDoCiclo().Should().BeEmpty();
        versao.Recalculos.Should().ContainSingle();
    }

    [Fact(DisplayName = "Recálculo para mais: a classe passa a exigir a proteção de dados e recomeça pela gestão")]
    public void Decidir_ClassePassaAExigir_DescartaAsAprovacoes()
    {
        _catalogo.SensibilidadeDosDados = ClassificacaoDeProtecao.Interno;
        Perfil perfil = PendenteVerificada([Consultar, Dados]);
        _catalogo.SensibilidadeDosDados = ClassificacaoDeProtecao.Pessoal;

        DesfechoDaVersao desfecho = Decidir(perfil, EtapaDaVersao.GestaoDeAcessos, PerfisDeTeste.AutoriaDaGestora);

        desfecho.Erro!.Code.Should().Be(PerfilErrorCodes.AprovacoesDescartadas);
        desfecho.Descartadas.Should().BeEmpty();
        desfecho.Recalculo.Should().Match<RecalculoDaAprovacao>(r => !r.ExigiaAntes && r.ExigeAgora);
        Decidir(perfil, EtapaDaVersao.GestaoDeAcessos, PerfisDeTeste.AutoriaDaGestora).Troca.Should().BeNull();
        perfil.VersaoPendente!.ExigeProtecaoDeDados.Should().BeTrue();
    }

    [Fact(DisplayName = "O alcance que mudou no catálogo entre a criação e a aprovação recusa a aprovação")]
    public void Aprovar_AlcanceMudouNoCatalogo_Recusa()
    {
        Perfil perfil = PendenteVerificada([Consultar]);
        _catalogo.DeUnidadeExtras.Add(Consultar);

        Decidir(perfil, EtapaDaVersao.GestaoDeAcessos, PerfisDeTeste.AutoriaDaGestora)
            .Erro!.Code.Should().Be(PerfilErrorCodes.AlcanceAlterado);
    }

    [Fact(DisplayName = "Permissão que saiu do catálogo ou virou reservada recusa a aprovação")]
    public void Aprovar_ConteudoForaDoCatalogo_Recusa()
    {
        Perfil perfil = PendenteVerificada([Consultar]);
        _catalogo.Reservadas.Add(Consultar);
        Decidir(perfil, EtapaDaVersao.GestaoDeAcessos, PerfisDeTeste.AutoriaDaGestora)
            .Erro!.Code.Should().Be(PerfilErrorCodes.PermissaoReservada);

        _catalogo.Reservadas.Clear();
        _catalogo.Inexistentes.Add(Consultar);
        Decidir(perfil, EtapaDaVersao.GestaoDeAcessos, PerfisDeTeste.AutoriaDaGestora)
            .Erro!.Code.Should().Be(PerfilErrorCodes.PermissaoInexistente);
    }

    [Fact(DisplayName = "Nome que outro perfil tomou recusa a aprovação")]
    public void Aprovar_NomeTomado_Recusa()
    {
        Perfil perfil = PendenteVerificada([Consultar]);

        perfil.Decidir(Pedido(perfil, EtapaDaVersao.GestaoDeAcessos, PerfisDeTeste.AutoriaDaGestora), NomeEmUso, _catalogo)
            .Erro!.Code.Should().Be(PerfilErrorCodes.NomeDuplicado);
    }

    [Fact(DisplayName = "Acréscimo a perfil com concessão entra em vigor na aprovação, protegido ou não")]
    public void Aprovar_AcrescimoComConcessao_EntraEmVigor()
    {
        Perfil interno = PerfisDeTeste.Vigente("Analista", Descricao, [Consultar], _catalogo);
        Verificada(interno, [Consultar, Publicar]);

        Decidir(interno, EtapaDaVersao.GestaoDeAcessos, PerfisDeTeste.AutoriaDaGestora).Troca.Should().NotBeNull();
    }

    [Fact(DisplayName = "A versão que só muda nome ou descrição exige apenas a gestão")]
    public void Aprovar_SemAcrescimo_UmaEtapa()
    {
        Perfil perfil = PerfisDeTeste.Vigente("Analista", Descricao, [Consultar, Dados], _catalogo);
        PerfisDeTeste.Garantir(PerfisDeTeste.CriarVersao(perfil,
            PerfisDeTeste.Conteudo("Analista sênior", Descricao, [Consultar, Dados], _catalogo),
            PerfisDeTeste.SemContexto,
            PerfisDeTeste.AutoriaDoAutor,
            Agora,
            _catalogo));
        VersaoDoPerfil nova = perfil.VersaoPendente!;
        nova.ExigeProtecaoDeDados.Should().BeFalse();
        Verificar(perfil);

        Decidir(perfil, EtapaDaVersao.GestaoDeAcessos, PerfisDeTeste.AutoriaDaGestora).Troca.Should().NotBeNull();
        perfil.Nome.Should().Be("Analista sênior");
    }

    [Fact(DisplayName = "A primeira versão classifica pelo conjunto inteiro")]
    public void Criar_PrimeiraVersao_ClassificaPeloConjunto()
    {
        PerfisDeTeste.Pendente("Analista", Descricao, [Consultar], _catalogo).VersaoPendente!.ExigeProtecaoDeDados.Should().BeFalse();
        PerfisDeTeste.Pendente("Analista", Descricao, [Consultar, Dados], _catalogo).VersaoPendente!.ExigeProtecaoDeDados.Should().BeTrue();
    }

    [Fact(DisplayName = "O perfil de governança não decide nem cancela versão")]
    public void Governanca_Imutavel()
    {
        Perfil perfil = PerfisDeTeste.DeGovernanca("Gestor de acessos", Descricao, [Consultar], _catalogo);

        Decidir(perfil, EtapaDaVersao.GestaoDeAcessos, PerfisDeTeste.AutoriaDaGestora, versaoId: Guid.NewGuid())
            .Erro!.Code.Should().Be(PerfilErrorCodes.GovernancaImutavel);
        perfil.CancelarVersao(Guid.NewGuid(), PerfisDeTeste.Gestora, "Motivo.", Agora)
            .Erro!.Code.Should().Be(PerfilErrorCodes.GovernancaImutavel);
    }

    [Fact(DisplayName = "Cancelar exige motivo e a pendente vencida expira em vez de cancelar")]
    public void Cancelar_ExigeMotivoEMaterializaOPrazo()
    {
        Perfil perfil = PendenteVerificada([Consultar]);
        Guid versaoId = perfil.VersaoPendente!.Id;

        perfil.CancelarVersao(versaoId, PerfisDeTeste.Autor, "  ", Agora).Erro!.Code.Should().Be(PerfilErrorCodes.MotivoInvalido);
        perfil.CancelarVersao(versaoId, PerfisDeTeste.Autor, new string('x', 501), Agora).Erro!.Code.Should().Be(PerfilErrorCodes.MotivoInvalido);
        perfil.CancelarVersao(versaoId, PerfisDeTeste.Autor, "Motivo.", Agora.AddDays(15))
            .Erro!.Code.Should().Be(PerfilErrorCodes.VersaoExpirada);
        perfil.UltimaVersaoEncerradaSemVigor!.Situacao.Should().Be(SituacaoDaVersao.Expirada);
    }

    private static readonly ContextoDaVersao NomeEmUso = new(NomeEmUsoPorOutroPerfil: true, JanelaDeMigracaoAberta: false);

    private Perfil PendenteVerificada(string[] permissoes)
    {
        Perfil perfil = PerfisDeTeste.Pendente("Analista", Descricao, permissoes, _catalogo);
        Verificar(perfil);

        return perfil;
    }

    private void Verificada(Perfil perfil, string[] permissoes)
    {
        PerfisDeTeste.Garantir(PerfisDeTeste.CriarVersao(perfil,
            PerfisDeTeste.Conteudo("Analista", Descricao, permissoes, _catalogo),
            PerfisDeTeste.SemContexto,
            PerfisDeTeste.AutoriaDoAutor,
            Agora,
            _catalogo));
        Verificar(perfil);
    }

    private static void Verificar(Perfil perfil)
    {
        DocumentoDaVersao documento = PerfisDeTeste.Garantir(
            perfil.ConfirmarDocumento(perfil.VersaoPendente!.Id, "etag-de-teste", Agora));
        perfil.RegistrarDocumentoVerificado(documento.Id, new string('a', 64), Agora);
    }

    private static PedidoDeDecisaoDaVersao Pedido(
        Perfil perfil,
        EtapaDaVersao etapa,
        AutoriaDaVersao autoria,
        ResultadoDaDecisao resultado = ResultadoDaDecisao.Aprovado,
        string? motivo = null,
        DateTimeOffset? instante = null,
        Guid? versaoId = null) =>
        new(versaoId ?? perfil.VersaoPendente!.Id, etapa, resultado, motivo, autoria, instante ?? Agora);

    private DesfechoDaVersao Decidir(
        Perfil perfil,
        EtapaDaVersao etapa,
        AutoriaDaVersao autoria,
        ResultadoDaDecisao resultado = ResultadoDaDecisao.Aprovado,
        string? motivo = null,
        DateTimeOffset? instante = null,
        ContextoDaVersao? contexto = null,
        Guid? versaoId = null) =>
        perfil.Decidir(Pedido(perfil, etapa, autoria, resultado, motivo, instante, versaoId), contexto ?? PerfisDeTeste.SemContexto, _catalogo);
}
