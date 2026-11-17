namespace Unifesspa.UniPlus.Autorizacao.Application.UnitTests;

using AwesomeAssertions;

using Unifesspa.UniPlus.Authorization.Catalogo;
using Unifesspa.UniPlus.Autorizacao.Application.Commands.Perfis;
using Unifesspa.UniPlus.Autorizacao.Domain.Entities;
using Unifesspa.UniPlus.Autorizacao.Domain.Interfaces;
using Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Domain.Enums;
using Unifesspa.UniPlus.Kernel.Domain.ValueObjects;

using static Unifesspa.UniPlus.Autorizacao.Application.UnitTests.DoublesDePerfis;

/// <summary>O registro da reclassificação das permissões da versão que entra em vigor, quando o catálogo gravado as suspende.</summary>
public sealed class ConferenciaDoCatalogoNaVersaoTests
{
    private const string Consultar = "selecao:processos:consultar";

    private static readonly CatalogoDeTeste Catalogo = new();

    [Fact(DisplayName = "A permissão que o catálogo gravado classifica acima da versão registra a reclassificação do perfil")]
    public async Task RegistrarNaEntrada_Sobe_Registra()
    {
        (Perfil perfil, ReclassificacoesNaMemoria reclassificacoes) = Cenario(deUnidadeNoCatalogo: false, subir: true);

        await Conferencia(reclassificacoes).RegistrarNaEntradaAsync(perfil, perfil.VersaoPendente!, CancellationToken.None);

        ReclassificacaoDaPermissao registrada = reclassificacoes.Reclassificacoes.Should().ContainSingle().Subject;
        (registrada.PerfilId, registrada.Codigo.Valor).Should().Be((perfil.Id, Consultar));
    }

    [Fact(DisplayName = "A permissão que o catálogo gravado muda de escopo também registra a reclassificação")]
    public async Task RegistrarNaEntrada_MudaDeEscopo_Registra()
    {
        (Perfil perfil, ReclassificacoesNaMemoria reclassificacoes) = Cenario(deUnidadeNoCatalogo: true, subir: false);

        await Conferencia(reclassificacoes).RegistrarNaEntradaAsync(perfil, perfil.VersaoPendente!, CancellationToken.None);

        reclassificacoes.Reclassificacoes.Should().ContainSingle();
    }

    [Fact(DisplayName = "A permissão que o catálogo gravado não suspende não registra nada")]
    public async Task RegistrarNaEntrada_NaoSuspende_NaoRegistra()
    {
        (Perfil perfil, ReclassificacoesNaMemoria reclassificacoes) = Cenario(deUnidadeNoCatalogo: false, subir: false);

        await Conferencia(reclassificacoes).RegistrarNaEntradaAsync(perfil, perfil.VersaoPendente!, CancellationToken.None);

        reclassificacoes.Reclassificacoes.Should().BeEmpty();
    }

    [Fact(DisplayName = "A permissão já reclassificada para o perfil na implantação não se registra de novo")]
    public async Task RegistrarNaEntrada_JaReclassificada_NaoRepete()
    {
        (Perfil perfil, ReclassificacoesNaMemoria reclassificacoes) = Cenario(deUnidadeNoCatalogo: false, subir: true);
        ConferenciaDoCatalogoNaVersao conferencia = Conferencia(reclassificacoes);
        await conferencia.RegistrarNaEntradaAsync(perfil, perfil.VersaoPendente!, CancellationToken.None);

        await conferencia.RegistrarNaEntradaAsync(perfil, perfil.VersaoPendente!, CancellationToken.None);

        reclassificacoes.Reclassificacoes.Should().ContainSingle();
    }

    [Fact(DisplayName = "Sem implantação do catálogo no banco nada se registra")]
    public async Task RegistrarNaEntrada_SemImplantacao_NaoRegistra()
    {
        (Perfil perfil, ReclassificacoesNaMemoria reclassificacoes) = Cenario(deUnidadeNoCatalogo: false, subir: true);
        reclassificacoes.ImplantacaoAtual = null;

        await Conferencia(reclassificacoes).RegistrarNaEntradaAsync(perfil, perfil.VersaoPendente!, CancellationToken.None);

        reclassificacoes.Reclassificacoes.Should().BeEmpty();
    }

    private static ConferenciaDoCatalogoNaVersao Conferencia(ReclassificacoesNaMemoria reclassificacoes) =>
        new(reclassificacoes, new CatalogoDaAplicacao([]), new CenarioDaGovernancaDaVersao.RelogioFixo(PerfisDeTeste.Instante.AddHours(1)));

    // A linha do catálogo gravado difere da versão no eixo pedido: sobe a classificação ou troca o escopo.
    private static (Perfil Perfil, ReclassificacoesNaMemoria Reclassificacoes) Cenario(bool deUnidadeNoCatalogo, bool subir)
    {
        Perfil perfil = PerfisDeTeste.Pendente("Analista", "Descrição válida.", [Consultar], Catalogo);
        PermissaoDaVersao permissao = perfil.VersaoPendente!.Permissoes.Single();
        ClassificacaoDeProtecao classificacao = subir ? (ClassificacaoDeProtecao)((int)permissao.Classificacao + 1) : permissao.Classificacao;
        CodigoDePermissao codigo = CodigoDePermissao.De(Consultar);
        PermissaoDoCatalogo linha = new(codigo, classificacao, deUnidadeNoCatalogo, false);

        return (perfil, new ReclassificacoesNaMemoria
        {
            ImplantacaoAtual = new ImplantacaoNoBanco(Guid.NewGuid(), ResumoSha256.DoConteudo("impressao"u8), new Dictionary<CodigoDePermissao, PermissaoDoCatalogo> { [codigo] = linha }),
        });
    }
}
