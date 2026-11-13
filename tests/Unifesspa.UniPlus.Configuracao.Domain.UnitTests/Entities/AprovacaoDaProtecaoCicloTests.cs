namespace Unifesspa.UniPlus.Configuracao.Domain.UnitTests.Entities;

using AwesomeAssertions;

using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Enums;
using Unifesspa.UniPlus.Configuracao.Domain.UnitTests.TestSupport;
using Unifesspa.UniPlus.Configuracao.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Domain.Enums;

using static Unifesspa.UniPlus.Configuracao.Domain.UnitTests.TestSupport.ManifestacoesDeTeste;

/// <summary>
/// O ciclo de aprovação no circuito do item (ADR-0163, UNI-REQ-0373): o reinício de item já solicitado abre um ciclo
/// novo e encerra as manifestações do anterior; a edição que não reinicia mantém o ciclo e as manifestações.
/// </summary>
public sealed class AprovacaoDaProtecaoCicloTests
{
    private static readonly DateTimeOffset Instante = TipoDocumentoNaAprovacaoBuilder.Segunda.AddHours(3);

    [Fact(DisplayName = "O item nasce no primeiro ciclo, sem manifestação")]
    public void Novo_NoPrimeiroCiclo()
    {
        AprovacaoDaProtecao circuito = new TipoDocumentoNaAprovacaoBuilder().Build().Aprovacao;

        circuito.Ciclo.Should().Be(CicloDaAprovacao.Primeiro);
        circuito.ManifestadaEm.Should().BeNull();
        circuito.ManifestacaoDesfavoravelVigente.Should().BeFalse();
    }

    [Fact(DisplayName = "Mudar a proteção do item ainda pendente não abre ciclo: nada foi solicitado nele")]
    public void Pendente_MudarAProtecao_MantemOCiclo()
    {
        TipoDocumento tipo = new TipoDocumentoNaAprovacaoBuilder().Build();

        TipoDocumentoNaAprovacaoBuilder.EditarAProtecao(tipo);

        tipo.Aprovacao.Ciclo.Should().Be(CicloDaAprovacao.Primeiro);
    }

    [Fact(DisplayName = "Mudar a proteção do item solicitado abre o ciclo seguinte e encerra as manifestações dele")]
    public void Solicitado_MudarAProtecao_AbreOCicloSeguinte()
    {
        TipoDocumento tipo = new TipoDocumentoNaAprovacaoBuilder().Solicitado().Build();
        Do(tipo).Registrar(tipo.Aprovacao, tipo.Aprovacao.Ciclo, Inadequada(), Kevin, Instante);

        TipoDocumentoNaAprovacaoBuilder.EditarAProtecao(tipo);

        tipo.Aprovacao.Should().BeEquivalentTo(new
        {
            Situacao = SituacaoDaAprovacao.Pendente,
            Ciclo = new CicloDaAprovacao(2),
            ManifestadaEm = (DateTimeOffset?)null,
            ManifestacaoDesfavoravelVigente = false,
        });
    }

    [Theory(DisplayName = "Editar qualquer campo do item reprovado ou com ajuste recomendado abre o ciclo seguinte")]
    [InlineData(SituacaoDaAprovacao.Reprovada)]
    [InlineData(SituacaoDaAprovacao.AjusteRecomendado)]
    public void DecididoSemAprovar_EditarONome_AbreOCicloSeguinte(SituacaoDaAprovacao situacao)
    {
        TipoDocumento tipo = Construir(situacao);

        TipoDocumentoNaAprovacaoBuilder.EditarONome(tipo);

        (tipo.Aprovacao.Situacao, tipo.Aprovacao.Ciclo).Should().Be((SituacaoDaAprovacao.Pendente, new CicloDaAprovacao(2)));
    }

    [Fact(DisplayName = "Mudar a proteção do item aprovado abre o ciclo seguinte; mudar só o nome não")]
    public void Aprovado_SoAProtecaoAbreCiclo()
    {
        TipoDocumento soONome = new TipoDocumentoNaAprovacaoBuilder().Aprovado().Build();
        TipoDocumento aProtecao = new TipoDocumentoNaAprovacaoBuilder().Aprovado().Build();

        TipoDocumentoNaAprovacaoBuilder.EditarONome(soONome);
        TipoDocumentoNaAprovacaoBuilder.EditarAProtecao(aProtecao);

        soONome.Aprovacao.Ciclo.Should().Be(CicloDaAprovacao.Primeiro);
        aProtecao.Aprovacao.Ciclo.Should().Be(new CicloDaAprovacao(2));
    }

    [Fact(DisplayName = "A edição sem reinício do item solicitado mantém o ciclo e a manifestação desfavorável vigente")]
    public void Solicitado_EditarONome_MantemAsManifestacoes()
    {
        TipoDocumento tipo = new TipoDocumentoNaAprovacaoBuilder().Solicitado().Build();
        Do(tipo).Registrar(tipo.Aprovacao, tipo.Aprovacao.Ciclo, Inadequada(), Kevin, Instante);

        TipoDocumentoNaAprovacaoBuilder.EditarONome(tipo);

        tipo.Aprovacao.Should().BeEquivalentTo(new
        {
            Situacao = SituacaoDaAprovacao.Solicitada,
            Ciclo = CicloDaAprovacao.Primeiro,
            ManifestadaEm = (DateTimeOffset?)Instante,
            ManifestacaoDesfavoravelVigente = true,
        });
    }

    [Fact(DisplayName = "O aceite da recomendação aprova no mesmo ciclo, sem exigir justificativa")]
    public void AjusteRecomendado_Aceite_MantemOCiclo()
    {
        TipoDocumento tipo = new TipoDocumentoNaAprovacaoBuilder().ComAjusteRecomendado().Build();

        tipo.AceitarRecomendacaoDaProtecao(TipoDocumentoNaAprovacaoBuilder.Isabella, Instante).IsSuccess.Should().BeTrue();

        (tipo.Aprovacao.Situacao, tipo.Aprovacao.Ciclo, tipo.ClassificacaoProtecao)
            .Should().Be((SituacaoDaAprovacao.Aprovada, CicloDaAprovacao.Primeiro, ClassificacaoDeProtecao.Sensivel));
    }

    [Fact(DisplayName = "Cada reinício de item já solicitado avança um ciclo")]
    public void ReiniciosSucessivos_AvancamUmCicloCada()
    {
        TipoDocumento tipo = new TipoDocumentoNaAprovacaoBuilder().Reprovado().Build();

        TipoDocumentoNaAprovacaoBuilder.EditarONome(tipo);
        tipo.SolicitarAprovacaoDaProtecao(TipoDocumentoNaAprovacaoBuilder.Isabella, Instante);
        tipo.ReprovarProtecao("Ainda desnecessário.", TipoDocumentoNaAprovacaoBuilder.Helena, Instante.AddHours(1));
        TipoDocumentoNaAprovacaoBuilder.EditarONome(tipo);

        tipo.Aprovacao.Ciclo.Should().Be(new CicloDaAprovacao(3));
    }

    [Fact(DisplayName = "No fato, editar o descritivo do item reprovado abre o ciclo seguinte e encerra a manifestação; no solicitado, não")]
    public void Fato_EditarODescritivo()
    {
        FatoCandidato reprovado = FatoSolicitado();
        reprovado.ReprovarProtecao("Dado desnecessário.", TipoDocumentoNaAprovacaoBuilder.Helena, Instante);
        FatoCandidato solicitado = FatoSolicitado();
        ManifestacoesDoCiclo.De(new ItemDaProtecao(CadastroDoCatalogo.Fato, solicitado.Id), CicloDaAprovacao.Primeiro, [])
            .Registrar(solicitado.Aprovacao, CicloDaAprovacao.Primeiro, RecomendaAjuste(), Kevin, Instante);

        reprovado.AlterarDescritivo("Renda familiar", "Renda bruta declarada.", null).IsSuccess.Should().BeTrue();
        solicitado.AlterarDescritivo("Renda familiar", "Renda bruta declarada.", null).IsSuccess.Should().BeTrue();

        reprovado.Aprovacao.Ciclo.Should().Be(new CicloDaAprovacao(2));
        (solicitado.Aprovacao.Ciclo, solicitado.Aprovacao.ManifestacaoDesfavoravelVigente).Should().Be((CicloDaAprovacao.Primeiro, true));
    }

    private static FatoCandidato FatoSolicitado()
    {
        FatoCandidato fato = FatoCandidato.CriarDoAdministrador(
            "TEM_RENDA", "Tem renda", "Se o candidato tem renda.", DominioFato.Booleano, CardinalidadeFato.Escalar, null, null, "INSCRICAO",
            EscopoFato.Candidato, ClassificacaoDeProtecao.Pessoal, TipoDocumentoNaAprovacaoBuilder.Finalidade, HipoteseLegalTratamento.CumprimentoObrigacaoLegal).Value!;
        fato.SolicitarAprovacaoDaProtecao(TipoDocumentoNaAprovacaoBuilder.Isabella, TipoDocumentoNaAprovacaoBuilder.Segunda);
        return fato;
    }

    private static TipoDocumento Construir(SituacaoDaAprovacao situacao) => situacao == SituacaoDaAprovacao.Reprovada
        ? new TipoDocumentoNaAprovacaoBuilder().Reprovado().Build()
        : new TipoDocumentoNaAprovacaoBuilder().ComAjusteRecomendado().Build();
}
