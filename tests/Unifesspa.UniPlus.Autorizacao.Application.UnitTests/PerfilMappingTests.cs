namespace Unifesspa.UniPlus.Autorizacao.Application.UnitTests;

using AwesomeAssertions;

using Unifesspa.UniPlus.Application.Abstractions.Respostas;
using Unifesspa.UniPlus.Authorization.Catalogo;
using Unifesspa.UniPlus.Authorization.Enums;
using Unifesspa.UniPlus.Autorizacao.Application.DTOs;
using Unifesspa.UniPlus.Autorizacao.Application.Mappings;
using Unifesspa.UniPlus.Autorizacao.Domain.Entities;
using Unifesspa.UniPlus.Autorizacao.Domain.Enums;
using Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Domain.Enums;

using static Unifesspa.UniPlus.Autorizacao.Application.UnitTests.CatalogoDoMapeamento;

public sealed class PerfilMappingTests
{
    [Fact(DisplayName = "A sensibilidade do perfil é a mais alta entre as permissões")]
    public void ToDto_PerfilComVariasPermissoes_ExpoeAMaiorSensibilidade()
    {
        PerfilDto dto = PerfilMapping.ToDto(
            CriarPerfil("teste:itens:ler", "teste:itens:manter", "teste:laudos:consultar"),
            Catalogo);

        dto.Sensibilidade.Should().Be(ValoresCanonicos.De(ClassificacaoDeProtecao.Sensivel));
    }

    [Fact(DisplayName = "A permissão retirada do catálogo conta como sensível")]
    public void ToDto_PermissaoForaDoCatalogo_ContaComoSensivel()
    {
        Perfil perfil = CriarPerfil("teste:itens:ler", "teste:itens:retirada");

        PerfilDto dto = PerfilMapping.ToDto(perfil, new CatalogoDaAplicacao([]));

        dto.Sensibilidade.Should().Be(ValoresCanonicos.De(ClassificacaoDeProtecao.Sensivel));
    }

    [Fact(DisplayName = "O perfil de governança expõe o tipo de governança e as permissões próprias como composição")]
    public void ToDto_PerfilDeGovernanca_ExpoeOTipoEAComposicao()
    {
        Perfil perfil = PerfisDeTeste.DeGovernanca("Gestão de acessos", "Governa os acessos.", ["teste:itens:ler"], new DoublesDePerfis.CatalogoDeTeste());

        PerfilDto dto = PerfilMapping.ToDto(perfil, Catalogo);

        dto.Governanca.Should().Be(TipoDeGovernanca.GestorDeAcessos.ToCodigo());
        dto.ComposicaoEmVigor!.VersaoId.Should().BeNull();
        dto.ComposicaoEmVigor.Permissoes.Select(static p => p.Codigo).Should().Equal("teste:itens:ler");
    }

    [Fact(DisplayName = "O perfil sem governança não traz tipo de governança")]
    public void ToDto_PerfilComum_NaoTrazGovernanca() =>
        PerfilMapping.ToDto(CriarPerfil("teste:itens:ler"), Catalogo).Governanca.Should().BeNull();

    [Fact(DisplayName = "A versão em vigor expõe número, protocolo, instante de entrada e verificação do documento")]
    public void ToDto_PerfilComVersaoVigente_ExpoeAComposicaoEmVigor()
    {
        Perfil perfil = CriarPerfil("teste:itens:manter", "teste:itens:ler");

        PerfilDto dto = PerfilMapping.ToDto(perfil, Catalogo);

        dto.ComposicaoEmVigor.Should().BeEquivalentTo(new
        {
            VersaoId = perfil.VersaoVigente!.Id,
            Numero = 1,
            Protocolo = PerfisDeTeste.Protocolo,
            DocumentoVerificadoEm = PerfisDeTeste.Instante,
            Desde = PerfisDeTeste.Instante,
        });
        dto.ComposicaoEmVigor!.Permissoes.Select(static p => p.Codigo).Should().Equal("teste:itens:ler", "teste:itens:manter");
        dto.VersaoPendente.Should().BeNull();
    }

    [Fact(DisplayName = "A versão pendente expõe a proposta, o documento e a etapa em que está")]
    public void ToDto_PerfilComVersaoPendente_ExpoeAProposta()
    {
        Perfil perfil = CenarioDaGovernancaDaVersao.PendenteVerificada(["teste:itens:ler"]);

        VersaoPendenteDto? pendente = PerfilMapping.ToDto(perfil, Catalogo).VersaoPendente;

        pendente.Should().BeEquivalentTo(new
        {
            VersaoId = perfil.VersaoPendente!.Id,
            Numero = 1,
            Protocolo = PerfisDeTeste.Protocolo,
            EtapaAtual = EtapaDaVersao.GestaoDeAcessos.ToCodigo(),
            ExigeProtecaoDeDados = false,
        });
    }

    [Fact(DisplayName = "Sem leitor, a versão pendente não é de quem lê")]
    public void ToDto_SemLeitor_VersaoPendenteNaoEDeQuemLe()
    {
        Perfil perfil = CenarioDaGovernancaDaVersao.PendenteVerificada(["teste:itens:ler"]);

        PerfilMapping.ToDto(perfil, Catalogo).VersaoPendente!.PropostaPorQuemLe.Should().BeFalse();
    }

    [Fact(DisplayName = "Com o autor da proposta como leitor, a versão pendente é de quem lê")]
    public void ToDto_LeitorAutorDaProposta_VersaoPendenteEDeQuemLe()
    {
        Perfil perfil = CenarioDaGovernancaDaVersao.PendenteVerificada(["teste:itens:ler"]);

        PerfilMapping.ToDto(perfil, Catalogo, PerfisDeTeste.Autor).VersaoPendente!.PropostaPorQuemLe.Should().BeTrue();
    }

    [Fact(DisplayName = "Com outra pessoa como leitor, a versão pendente não é de quem lê")]
    public void ToDto_LeitorDiferenteDoAutor_VersaoPendenteNaoEDeQuemLe()
    {
        Perfil perfil = CenarioDaGovernancaDaVersao.PendenteVerificada(["teste:itens:ler"]);

        PerfilMapping.ToDto(perfil, Catalogo, PerfisDeTeste.Gestor).VersaoPendente!.PropostaPorQuemLe.Should().BeFalse();
    }

    [Fact(DisplayName = "O perfil desativado expõe o instante da desativação")]
    public void ToDto_PerfilDesativado_ExpoeODesativadoDesde()
    {
        Perfil perfil = CriarPerfil("teste:itens:ler");
        PerfisDeTeste.Desativar(perfil, new DoublesDePerfis.CatalogoDeTeste());

        PerfilMapping.ToDto(perfil, Catalogo).DesativadoDesde.Should().Be(PerfisDeTeste.Instante);
    }

    [Fact(DisplayName = "O perfil não suspenso não traz suspensão")]
    public void ToDto_PerfilNaoSuspenso_NaoTrazSuspensao() =>
        PerfilMapping.ToDto(CriarPerfil("teste:itens:ler"), Catalogo).Suspensao.Should().BeNull();

    [Fact(DisplayName = "A suspensão expõe o papel, as etapas exigidas pela composição em vigor e as decisões")]
    public void ToDto_PerfilSuspensoProtegido_ExpoeAsEtapasEAsDecisoes()
    {
        Perfil perfil = CriarPerfil("teste:dados-pessoais:consultar");
        SuspenderPelaGestao(perfil);
        DecidirPelaProtecaoDeDados(perfil);

        SuspensaoDoPerfilDto? suspensao = PerfilMapping.ToDto(perfil, Catalogo).Suspensao;

        suspensao!.PapelDeQuemSuspendeu.Should().Be(EtapaDaVersao.GestaoDeAcessos.ToCodigo());
        suspensao.EtapasExigidasAgora.Should().Equal(EtapaDaVersaoCodigo.GestaoDeAcessos, EtapaDaVersaoCodigo.ProtecaoDeDados);
        suspensao.EtapasDecididas.Should().Equal(EtapaDaVersaoCodigo.ProtecaoDeDados);
    }

    [Fact(DisplayName = "Sem composição em vigor e com pendente protegida, a suspensão mostra a sensibilidade da pendente e as etapas da composição vazia")]
    public void ToDto_SuspensoSemVigenteComPendenteProtegida_DistingueAsBasesDeSensibilidade()
    {
        Perfil perfil = CenarioDaGovernancaDaVersao.PendenteVerificada(["teste:laudos:consultar"]);
        SuspenderPelaGestao(perfil);

        PerfilDto dto = PerfilMapping.ToDto(perfil, Catalogo);

        dto.Sensibilidade.Should().Be(ValoresCanonicos.De(ClassificacaoDeProtecao.Sensivel));
        dto.Suspensao!.EtapasExigidasAgora.Should().Equal(EtapaDaVersaoCodigo.GestaoDeAcessos);
    }

    [Fact(DisplayName = "O perfil com versão em vigor e pendente é projetado por inteiro, com os blocos das versões vindos do mapeamento de versões")]
    public void ToDto_PerfilComVigenteEPendente_ProjetaOPerfilPorInteiro()
    {
        Perfil perfil = PerfilComVigenteEPendente();

        PerfilDto dto = PerfilMapping.ToDto(perfil, Catalogo, PerfisDeTeste.Autor);

        dto.Should().BeEquivalentTo(EsperadoDe(perfil, "interno", SituacaoDoAcessoDoPerfil.EmVigor, PerfisDeTeste.Autor));
    }

    [Fact(DisplayName = "O perfil sem vigente cuja única versão foi cancelada expõe a última versão encerrada sem vigor")]
    public void ToDto_PerfilComVersaoCancelada_ExpoeAUltimaVersaoEncerradaSemVigor()
    {
        Perfil perfil = CenarioDaGovernancaDaVersao.PendenteVerificada(["teste:itens:ler"]);
        perfil.CancelarVersao(perfil.VersaoPendente!.Id, PerfisDeTeste.Autor, "Proposta cancelada.", PerfisDeTeste.Instante);

        PerfilDto dto = PerfilMapping.ToDto(perfil, Catalogo);

        dto.UltimaVersaoEncerradaSemVigor.Should().NotBeNull();
        dto.Should().BeEquivalentTo(EsperadoDe(perfil, "publico", SituacaoDoAcessoDoPerfil.SemVersaoEmVigor, null));
    }

    internal static Perfil PerfilComVigenteEPendente()
    {
        Perfil perfil = CriarPerfil("teste:itens:ler");
        DoublesDePerfis.CatalogoDeTeste catalogo = new();
        PerfisDeTeste.Garantir(PerfisDeTeste.CriarVersao(
            perfil, PerfisDeTeste.Conteudo(perfil.Nome, perfil.Descricao, ["teste:itens:ler", "teste:itens:manter"], catalogo),
            PerfisDeTeste.SemContexto, PerfisDeTeste.AutoriaDoAutor, PerfisDeTeste.Instante, catalogo));
        DocumentoDaVersao documento = PerfisDeTeste.Garantir(
            DocumentosDoPerfil.Confirmar(perfil, perfil.VersaoPendente!.Id, "etag-de-teste", PerfisDeTeste.Instante));
        DocumentosDoPerfil.RegistrarVerificado(perfil, documento.Id, new string('a', 64), PerfisDeTeste.Instante);

        return perfil;
    }

    private static PerfilDto EsperadoDe(Perfil perfil, string sensibilidade, SituacaoDoAcessoDoPerfil situacaoDoAcesso, Identidade? leitor) =>
        new(
            perfil.Id, perfil.Nome, perfil.Descricao, true, null, sensibilidade, CodigoComRotulo<SituacaoDoAcessoDoPerfil>.De(situacaoDoAcesso),
            perfil.VersaoVigente is { } vigente ? VersaoMapping.VigenteDe(perfil, vigente, Catalogo) : null,
            VersaoMapping.PendenteDe(perfil, Catalogo, leitor),
            VersaoMapping.UltimaEncerradaSemVigorDe(perfil),
            null,
            null);

    internal static Perfil CriarPerfil(params string[] codigos) =>
        PerfisDeTeste.Vigente("Operação de inscrições", "Opera as inscrições do processo.", codigos, new DoublesDePerfis.CatalogoDeTeste());

    internal static void SuspenderPelaGestao(Perfil perfil) =>
        perfil.Suspender(PerfisDeTeste.Gestor, EtapaDaVersao.GestaoDeAcessos, "Suspenso para o teste.", PerfisDeTeste.Instante, new DoublesDePerfis.CatalogoDeTeste())
            .IsSuccess.Should().BeTrue();

    private static void DecidirPelaProtecaoDeDados(Perfil perfil) =>
        perfil.DecidirRetomada(new(
            PerfisDeTeste.ProtecaoDeDados, true, EtapaDaVersao.ProtecaoDeDados, MotivoInformado.Normalizar("Retomada avaliada."), new DoublesDePerfis.CatalogoDeTeste(), PerfisDeTeste.Instante))
            .IsSuccess.Should().BeTrue();
}
