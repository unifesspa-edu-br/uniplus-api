namespace Unifesspa.UniPlus.Autorizacao.Domain.UnitTests;

using AwesomeAssertions;

using Unifesspa.UniPlus.Autorizacao.Apoio;
using Unifesspa.UniPlus.Autorizacao.Domain.Entities;
using Unifesspa.UniPlus.Autorizacao.Domain.Enums;
using Unifesspa.UniPlus.Autorizacao.Domain.Errors;
using Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;

public sealed class PerfilRemocaoTests
{
    private const string Consultar = "selecao:processos:consultar";
    private const string Publicar = "selecao:processos:publicar";
    private const string Exportar = "selecao:processos:exportar";
    private const string Descricao = "Descrição válida.";
    private const string Motivo = "Permissão concedida por engano.";

    private static readonly DateTimeOffset Agora = PerfisDeTeste.Instante;

    private readonly CatalogoMutavelDeTeste _catalogo = new();

    [Fact(DisplayName = "A remoção vale na hora: a vigente se encerra e a versão de remoção abre sem aprovação")]
    public void Remover_ValeNaHora()
    {
        Perfil perfil = PerfisDeTeste.Vigente("Analista", Descricao, [Consultar, Publicar], _catalogo);
        VersaoDoPerfil anterior = perfil.VersaoVigente!;

        DesfechoDaVersao desfecho = perfil.Remover(Pedido([Publicar]), _catalogo);

        desfecho.Concluida.Should().BeTrue();
        desfecho.Troca!.Removidas.Should().Equal(Publicar);
        anterior.Situacao.Should().Be(SituacaoDaVersao.Encerrada);
        VersaoDoPerfil remocao = perfil.VersaoVigente!;
        remocao.Origem.Should().Be(OrigemDaVersao.Remocao);
        remocao.Numero.Should().Be(2);
        remocao.MotivoDaRemocao.Should().Be(Motivo);
        remocao.CategoriaDaRemocao.Should().Be(CategoriaDaRemocao.Incidente);
        remocao.Codigos().Should().Equal(Consultar);
        remocao.Alcance.Should().Be(anterior.Alcance);
        remocao.Protocolo.Should().Be(anterior.Protocolo);
        perfil.ComposicaoEmVigor.Should().Equal(Consultar);
        perfil.Vigencias.Count(v => v.Aberta).Should().Be(1);
    }

    [Fact(DisplayName = "A remoção total é admitida e preserva o alcance de unidade e o institucional")]
    public void Remover_Tudo_PreservaOAlcance()
    {
        Perfil institucional = PerfisDeTeste.Vigente("Analista", Descricao, [Consultar], _catalogo);
        Perfil deUnidade = PerfisDeTeste.Vigente("Operador", Descricao, ["unidade:inscricoes:operar"], _catalogo);

        institucional.Remover(Pedido([Consultar]), _catalogo).Concluida.Should().BeTrue();
        deUnidade.Remover(Pedido(["unidade:inscricoes:operar"]), _catalogo).Concluida.Should().BeTrue();

        institucional.ComposicaoEmVigor.Should().BeEmpty();
        institucional.VersaoVigente!.Alcance.Should().Be(TipoDeEscopo.Instituicao);
        deUnidade.VersaoVigente!.Alcance.Should().Be(TipoDeEscopo.Unidade);
    }

    [Fact(DisplayName = "O documento da remoção aponta direto para o da versão que o instituiu, depois de duas remoções")]
    public void Remover_DuasVezes_ApontaParaOMesmoDocumento()
    {
        Perfil perfil = PerfisDeTeste.Vigente("Analista", Descricao, [Consultar, Publicar, Exportar], _catalogo);
        VersaoDoPerfil instituidora = perfil.VersaoVigente!;

        perfil.Remover(Pedido([Exportar]), _catalogo).Concluida.Should().BeTrue();
        perfil.Remover(Pedido([Publicar]), _catalogo).Concluida.Should().BeTrue();

        VersaoDoPerfil segunda = perfil.VersaoVigente!;
        segunda.Numero.Should().Be(3);
        segunda.VersaoDoDocumento.Should().Be(instituidora.Id);
        perfil.DocumentoDe(segunda).Should().BeSameAs(instituidora.Documento);
    }

    [Fact(DisplayName = "A remoção cancela por contenção a pendente que ainda vale")]
    public void Remover_ComPendente_CancelaPorContencao()
    {
        Perfil perfil = PerfisDeTeste.Vigente("Analista", Descricao, [Consultar, Publicar], _catalogo);
        VersaoDoPerfil pendente = PerfisDeTeste.Garantir(PerfisDeTeste.CriarVersao(perfil,
            PerfisDeTeste.Conteudo("Analista", Descricao, [Consultar, Publicar, Exportar], _catalogo),
            PerfisDeTeste.SemContexto,
            PerfisDeTeste.AutoriaDoAutor,
            Agora,
            _catalogo));

        DesfechoDaVersao desfecho = perfil.Remover(Pedido([Publicar], Agora.AddDays(3)), _catalogo);

        desfecho.Cancelada.Should().BeSameAs(pendente);
        pendente.Situacao.Should().Be(SituacaoDaVersao.Cancelada);
        pendente.FormaDoCancelamento.Should().Be(FormaDoCancelamento.Contencao);
        pendente.MotivoDoCancelamento.Should().BeNull();
        perfil.VersaoPendente.Should().BeNull();
    }

    [Fact(DisplayName = "No prazo exato a pendente expira em vez de ser cancelada e não há aviso de contenção")]
    public void Remover_PendenteNoPrazoExato_Expira()
    {
        Perfil perfil = PerfisDeTeste.Vigente("Analista", Descricao, [Consultar, Publicar], _catalogo);
        VersaoDoPerfil pendente = PerfisDeTeste.Garantir(PerfisDeTeste.CriarVersao(perfil,
            PerfisDeTeste.Conteudo("Analista", Descricao, [Consultar, Publicar, Exportar], _catalogo),
            PerfisDeTeste.SemContexto,
            PerfisDeTeste.AutoriaDoAutor,
            Agora,
            _catalogo));

        DesfechoDaVersao desfecho = perfil.Remover(Pedido([Publicar], Agora.AddDays(VersaoDoPerfil.PrazoDaAprovacaoEmDias)), _catalogo);

        desfecho.Cancelada.Should().BeNull();
        pendente.Situacao.Should().Be(SituacaoDaVersao.Expirada);
    }

    [Fact(DisplayName = "As recusas da remoção seguem a ordem: governança, perfil inativo, sem vigente, permissões e motivo")]
    public void Remover_Recusas()
    {
        Perfil governanca = PerfisDeTeste.DeGovernanca("Gestor de acessos", Descricao, [Consultar], _catalogo);
        Perfil inativo = PerfisDeTeste.Vigente("Inativo", Descricao, [Consultar], _catalogo);
        PerfisDeTeste.Desativar(inativo, _catalogo);
        Perfil semVigente = PerfisDeTeste.Pendente("Pendente", Descricao, [Consultar], _catalogo);
        Perfil vigente = PerfisDeTeste.Vigente("Analista", Descricao, [Consultar], _catalogo);

        governanca.Remover(Pedido([Consultar]), _catalogo).Erro!.Code.Should().Be(PerfilErrorCodes.GovernancaImutavel);
        inativo.Remover(Pedido([Consultar]), _catalogo).Erro!.Code.Should().Be(PerfilErrorCodes.Inativo);
        semVigente.Remover(Pedido([Consultar]), _catalogo).Erro!.Code.Should().Be(PerfilErrorCodes.SemVersaoEmVigor);
        vigente.Remover(Pedido([]), _catalogo).Erro!.Code.Should().Be(PerfilErrorCodes.RemocaoInvalida);
        vigente.Remover(Pedido([Exportar]), _catalogo).Erro!.Code.Should().Be(PerfilErrorCodes.RemocaoInvalida);
        vigente.Remover(Pedido([Consultar], motivo: null), _catalogo).Erro!.Code.Should().Be(PerfilErrorCodes.MotivoInvalido);
        vigente.VersaoVigente!.Numero.Should().Be(1);
    }

    private static PedidoDeRemocao Pedido(string[] codigos, DateTimeOffset? instante = null, string? motivo = Motivo) =>
        new(codigos, motivo, CategoriaDaRemocao.Incidente, PerfisDeTeste.AutoriaDaGestora, instante ?? Agora);
}
