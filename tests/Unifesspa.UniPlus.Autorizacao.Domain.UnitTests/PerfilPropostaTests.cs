namespace Unifesspa.UniPlus.Autorizacao.Domain.UnitTests;

using AwesomeAssertions;

using Unifesspa.UniPlus.Autorizacao.Apoio;
using Unifesspa.UniPlus.Autorizacao.Domain.Entities;
using Unifesspa.UniPlus.Autorizacao.Domain.Enums;
using Unifesspa.UniPlus.Autorizacao.Domain.Errors;
using Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;

public sealed class PerfilPropostaTests
{
    private const string Consultar = "selecao:processos:consultar";
    private const string Publicar = "selecao:processos:publicar";
    private const string Dados = "selecao:inscricoes:ler-dados-pessoais";
    private const string Descricao = "Descrição válida.";
    private const string Motivo = "Permissão concedida por engano.";

    private static readonly DateTimeOffset Agora = PerfisDeTeste.Instante;
    private static readonly ContextoDaVersao ComJanelaAberta = new(NomeEmUsoPorOutroPerfil: false, JanelaDeMigracaoAberta: true);
    private static readonly ContextoDaVersao NomeEmUso = new(NomeEmUsoPorOutroPerfil: true, JanelaDeMigracaoAberta: false);

    private readonly CatalogoMutavelDeTeste _catalogo = new();

    [Fact(DisplayName = "A proposta que remove e acrescenta aplica a remoção na hora")]
    public void Propor_RemoveEAcrescenta_RemocaoNaHoraEPendenteSoComOAcrescimo()
    {
        Perfil perfil = PerfisDeTeste.Vigente("Analista", Descricao, [Consultar, Dados], _catalogo);
        VersaoDoPerfil anterior = perfil.VersaoVigente!;

        DesfechoDaVersao desfecho = Propor(perfil, "Analista", [Consultar, Publicar]);

        desfecho.Concluida.Should().BeTrue();
        desfecho.Troca!.Removidas.Should().Equal(Dados);
        anterior.Situacao.Should().Be(SituacaoDaVersao.Encerrada);
        VersaoDoPerfil remocao = perfil.VersaoVigente!;
        remocao.Origem.Should().Be(OrigemDaVersao.Remocao);
        remocao.Numero.Should().Be(2);
        remocao.MotivoDaRemocao.Should().Be(Motivo);
        remocao.Codigos().Should().Equal(Consultar);
        perfil.ComposicaoEmVigor.Should().Equal(Consultar);
    }

    [Fact(DisplayName = "A proposta que remove e acrescenta deixa pendente uma versão só com o acréscimo, à espera do documento")]
    public void Propor_RemoveEAcrescenta_PendenteComOAcrescimo()
    {
        Perfil perfil = PerfisDeTeste.Vigente("Analista", Descricao, [Consultar, Dados], _catalogo);

        DesfechoDaVersao desfecho = Propor(perfil, "Analista", [Consultar, Publicar]);

        VersaoDoPerfil pendente = perfil.VersaoPendente!;
        desfecho.Criada.Should().BeSameAs(pendente);
        pendente.Numero.Should().Be(3);
        pendente.Origem.Should().Be(OrigemDaVersao.Proposta);
        pendente.Codigos().Should().Equal(Consultar, Publicar);
        pendente.Documento.Should().NotBeNull();
    }

    [Fact(DisplayName = "A proposta só com remoção vale na hora, como a remoção avulsa, e não cria versão pendente")]
    public void Propor_SoRemocao_NaoCriaPendente()
    {
        Perfil perfil = PerfisDeTeste.Vigente("Analista", Descricao, [Consultar, Publicar], _catalogo);

        DesfechoDaVersao desfecho = Propor(perfil, "Analista", [Consultar]);

        desfecho.Concluida.Should().BeTrue();
        desfecho.Criada.Should().BeNull();
        desfecho.Troca!.Removidas.Should().Equal(Publicar);
        perfil.VersaoPendente.Should().BeNull();
        perfil.VersaoVigente!.Origem.Should().Be(OrigemDaVersao.Remocao);
        perfil.ComposicaoEmVigor.Should().Equal(Consultar);
    }

    [Fact(DisplayName = "A proposta que remove e muda o nome deixa a mudança de nome pendente sobre a versão reduzida")]
    public void Propor_RemoveEMudaONome_PendenteComONovoNome()
    {
        Perfil perfil = PerfisDeTeste.Vigente("Analista", Descricao, [Consultar, Publicar], _catalogo);

        DesfechoDaVersao desfecho = Propor(perfil, "Analista de processos", [Consultar]);

        desfecho.Criada.Should().BeSameAs(perfil.VersaoPendente);
        perfil.Nome.Should().Be("Analista");
        perfil.VersaoPendente!.Nome.Should().Be("Analista de processos");
        perfil.VersaoPendente.Codigos().Should().Equal(Consultar);
        perfil.ComposicaoEmVigor.Should().Equal(Consultar);
    }

    [Fact(DisplayName = "A remoção da proposta cancela por contenção a pendente que ainda valia")]
    public void Propor_ComPendente_CancelaAAnteriorECriaANova()
    {
        Perfil perfil = PerfisDeTeste.Vigente("Analista", Descricao, [Consultar, Dados], _catalogo);
        VersaoDoPerfil anterior = Pendente(perfil, [Consultar, Dados, Publicar]);

        DesfechoDaVersao desfecho = Propor(perfil, "Analista", [Consultar, Publicar]);

        desfecho.Cancelada.Should().BeSameAs(anterior);
        anterior.FormaDoCancelamento.Should().Be(FormaDoCancelamento.Contencao);
        perfil.VersaoPendente.Should().BeSameAs(desfecho.Criada);
        perfil.VersaoPendente!.Numero.Should().Be(4);
    }

    [Fact(DisplayName = "A proposta sem remoção cria a pendente e não aplica remoção")]
    public void Propor_SemRemocao_CriaAPendente()
    {
        Perfil perfil = PerfisDeTeste.Vigente("Analista", Descricao, [Consultar], _catalogo);

        DesfechoDaVersao desfecho = Propor(perfil, "Analista", [Consultar, Publicar], motivo: null);

        desfecho.Troca.Should().BeNull();
        desfecho.Criada.Should().BeSameAs(perfil.VersaoPendente);
        perfil.VersaoVigente!.Origem.Should().Be(OrigemDaVersao.Proposta);
    }

    [Fact(DisplayName = "A proposta que remove sem motivo é recusada com o erro de motivo e nada muda")]
    public void Propor_RemoveSemMotivo_Recusa()
    {
        Perfil perfil = PerfisDeTeste.Vigente("Analista", Descricao, [Consultar, Dados], _catalogo);

        DesfechoDaVersao desfecho = Propor(perfil, "Analista", [Consultar, Publicar], motivo: null);

        desfecho.Erro!.Code.Should().Be(PerfilErrorCodes.MotivoInvalido);
        NadaMudou(perfil);
    }

    [Fact(DisplayName = "A pendente recusada desfaz a remoção: a janela de migração aberta recusa a proposta e a vigente fica como estava")]
    public void Propor_PendenteRecusada_NaoAplicaARemocao()
    {
        Perfil perfil = PerfisDeTeste.Vigente("Analista", Descricao, [Consultar, Dados], _catalogo);

        DesfechoDaVersao desfecho = Propor(perfil, "Analista", [Consultar, Publicar], contexto: ComJanelaAberta);

        desfecho.Erro!.Code.Should().Be(PerfilErrorCodes.JanelaDeMigracaoAberta);
        NadaMudou(perfil);
    }

    [Fact(DisplayName = "Nome em uso por outro perfil recusa a proposta que remove, sem remover")]
    public void Propor_NomeEmUso_NaoAplicaARemocao()
    {
        Perfil perfil = PerfisDeTeste.Vigente("Analista", Descricao, [Consultar, Dados], _catalogo);

        DesfechoDaVersao desfecho = Propor(perfil, "Outro nome", [Consultar], contexto: NomeEmUso);

        desfecho.Erro!.Code.Should().Be(PerfilErrorCodes.NomeDuplicado);
        NadaMudou(perfil);
    }

    [Fact(DisplayName = "As recusas do estado do perfil valem para a proposta que remove")]
    public void Propor_Remove_PerfilInativo_Recusa()
    {
        Perfil perfil = PerfisDeTeste.Vigente("Analista", Descricao, [Consultar, Dados], _catalogo);
        PerfisDeTeste.Desativar(perfil, _catalogo);

        Propor(perfil, "Analista", [Consultar, Publicar]).Erro!.Code.Should().Be(PerfilErrorCodes.Inativo);
        NadaMudou(perfil);
    }

    private static void NadaMudou(Perfil perfil)
    {
        perfil.Versoes.Should().HaveCount(1);
        perfil.VersaoVigente!.Numero.Should().Be(1);
        perfil.VersaoPendente.Should().BeNull();
        perfil.Vigencias.Should().ContainSingle();
    }

    private DesfechoDaVersao Propor(
        Perfil perfil, string nome, string[] permissoes, string? motivo = Motivo, ContextoDaVersao? contexto = null) =>
        perfil.Propor(
            new PropostaDeVersao(
                PerfisDeTeste.Conteudo(nome, Descricao, permissoes, _catalogo),
                motivo,
                CategoriaDaRemocao.Incidente,
                PerfisDeTeste.AutoriaDoGestor,
                Agora),
            contexto ?? PerfisDeTeste.SemContexto,
            _catalogo);

    private VersaoDoPerfil Pendente(Perfil perfil, string[] codigos) =>
        PerfisDeTeste.Garantir(PerfisDeTeste.CriarVersao(
            perfil, PerfisDeTeste.Conteudo("Analista", Descricao, codigos, _catalogo), PerfisDeTeste.SemContexto, PerfisDeTeste.AutoriaDoAutor, Agora, _catalogo));
}
